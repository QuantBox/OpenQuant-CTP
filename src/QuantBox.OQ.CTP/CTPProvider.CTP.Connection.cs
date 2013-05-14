using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using QuantBox.CSharp2CTP;
using QuantBox.Helper.CTP;
using SmartQuant;
using SmartQuant.Data;
using SmartQuant.Execution;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;


namespace QuantBox.OQ.CTP
{
    partial class CTPProvider
    {
        #region 清除数据
        private void Clear()
        {
            _OrderSysID2OrderRef.Clear();
            _OrderRef2Order.Clear();
            _Orders4Cancel.Clear();
            _dbInMemInvestorPosition.Clear();
            _dictDepthMarketData.Clear();
            _dictInstruments.Clear();
            _dictCommissionRate.Clear();
            _dictMarginRate.Clear();
            _dictAltSymbol2Instrument.Clear();

            _yyyy = 0;
            _MM = 0;
            _dd = 0;
        }

        private void ChangeTradingDay(string tradingDay)
        {
            // 不用每次都比
            DateTime dt = DateTime.Now;
            int nTime = dt.Hour * 100 + dt.Minute;
            // 考虑到时间误差，给10分钟的切换时间
            if (2355 <= nTime || nTime <= 5)
            {
                // 行情时间切换
                // 在这个时间段内都使用传回来的时间，
                // 因为有可能不同交易所时间有误差，有的到第二天了，有些还没到
                try
                {
                    int _yyyyMMdd = int.Parse(tradingDay);
                    _yyyy = _yyyyMMdd / 10000;
                    _MM = (_yyyyMMdd % 10000) / 100;
                    _dd = _yyyyMMdd % 100;
                }
                catch (Exception)
                {
                    _yyyy = dt.Year;
                    _MM = dt.Month;
                    _dd = dt.Day;
                }
            }
        }

        private void ChangeActionDay()
        {
            // 换交易日，假设换交易日前肯定会登录一次，所以在登录的时候清理即可
            //_dictInstruments.Clear();
            //_dictCommissionRate.Clear();
            //_dictMarginRate.Clear();
        }
        #endregion

        #region 定时器
        private readonly System.Timers.Timer timerConnect = new System.Timers.Timer(1 * 60 * 1000);
        private readonly System.Timers.Timer timerDisconnect = new System.Timers.Timer(20 * 1000);
        private readonly System.Timers.Timer timerAccount = new System.Timers.Timer(3 * 60 * 1000);
        private readonly System.Timers.Timer timerPonstion = new System.Timers.Timer(5 * 60 * 1000);

        void timerConnect_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //网络问题从来没有连上，超时直接跳出
            if (!isConnected)
                return;

            // 换交易日了，更新部分数据
            ChangeActionDay();

            DateTime dt = DateTime.Now;
            int nTime = dt.Hour * 100 + dt.Minute;
            // 9点到15点15是交易时间
            // 夜盘晚上9点到第二天的2点30是交易时间
            bool bTrading = false;
            if (845<=nTime&&nTime<=1530)
            {
                bTrading = true;
            }
            if(2045<=nTime||nTime<=300)
            {
                bTrading = true;
            }

            if (!bTrading)
                return;

            // 交易时间断线，由C#层来销毁，然后重连
            if (_bWantMdConnect && !_bMdConnected)
            {
                mdlog.Info("断开->重连");
                Disconnect_MD();
                Connect_MD();
            }
            if (_bWantTdConnect && !_bTdConnected)
            {
                tdlog.Info("断开->重连");
                Disconnect_TD();
                Connect_TD();
            }
        }

        void timerDisconnect_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //如果从来没有连接上，在用户不知情的情况下又会自动连接上，所以要求定时断开连接
            if (!isConnected)
            {
                tdlog.Warn("从未连接成功，停止尝试！");
                _Disconnect();
            }
        }

        void timerPonstion_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_bTdConnected)
            {
                TraderApi.TD_ReqQryInvestorPosition(m_pTdApi, "");
            }
        }

        void timerAccount_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_bTdConnected)
            {
                TraderApi.TD_ReqQryTradingAccount(m_pTdApi);
            }
        }

        #endregion

        #region 连接
        private string _newTempPath;
        private void _Connect()
        {
            CTPAPI.GetInstance().__RegInstrumentDictionary(_dictInstruments);
            CTPAPI.GetInstance().__RegInstrumentCommissionRateDictionary(_dictCommissionRate);
            CTPAPI.GetInstance().__RegInstrumentMarginRateDictionary(_dictMarginRate);
            CTPAPI.GetInstance().__RegDepthMarketDataDictionary(_dictDepthMarketData);

            server = null;
            account = null;

            bool bCheckOk = false;

            do
            {
                if (0 == serversList.Count)
                {
                    MessageBox.Show("您还没有设置 服务器 信息，目前只选择第一条进行连接");
                    break;
                }
                if (0 == accountsList.Count)
                {
                    MessageBox.Show("您还没有设置 账号 信息，目前只选择第一条进行连接");
                    break;
                }

                server = serversList[0];
                account = accountsList[0];

                if (string.IsNullOrEmpty(server.BrokerID))
                {
                    MessageBox.Show("BrokerID不能为空");
                    break;
                }

                if (_bWantTdConnect && 0 == server.Trading.Count())
                {
                    MessageBox.Show("交易服务器地址不全");
                    break;
                }

                if (_bWantMdConnect && 0 == server.MarketData.Count())
                {
                    MessageBox.Show("行情服务器信息不全");
                    break;
                }

                if (string.IsNullOrEmpty(account.InvestorId)
                || string.IsNullOrEmpty(account.Password))
                {
                    MessageBox.Show("账号信息不全");
                    break;
                }

                bCheckOk = true;

            } while (false);

            if (false == bCheckOk)
            {
                ChangeStatus(ProviderStatus.Disconnected);
                isConnected = false;
                return;
            }

            //新建目录
            _newTempPath = ApiTempPath + Path.DirectorySeparatorChar + server.BrokerID + Path.DirectorySeparatorChar + account.InvestorId;
            Directory.CreateDirectory(_newTempPath);

            ChangeStatus(ProviderStatus.Connecting);
            //如果前面一次连接一直连不上，新改地址后也会没响应，所以先删除
            Disconnect_MD();
            Disconnect_TD();

            if (_bWantMdConnect || _bWantTdConnect)
            {
                timerDisconnect.Enabled = true;
                Connect_MsgQueue();
            }
            if (_bWantMdConnect)
            {
                Connect_MD();
            }
            if (_bWantTdConnect)
            {
                Connect_TD();
            }
        }


        private void Connect_MsgQueue()
        {
            //建立消息队列，只建一个，行情和交易复用一个
            lock (_lockMsgQueue)
            {
                if (null == m_pMsgQueue || IntPtr.Zero == m_pMsgQueue)
                {
                    m_pMsgQueue = CommApi.CTP_CreateMsgQueue();

                    CommApi.CTP_RegOnConnect(m_pMsgQueue, _fnOnConnect_Holder);
                    CommApi.CTP_RegOnDisconnect(m_pMsgQueue, _fnOnDisconnect_Holder);
                    CommApi.CTP_RegOnRspError(m_pMsgQueue, _fnOnRspError_Holder);

                    CommApi.CTP_StartMsgQueue(m_pMsgQueue);
                }
            }
        }

        //建立行情
        private void Connect_MD()
        {
            lock (_lockMd)
            {
                if (_bWantMdConnect
                   && (null == m_pMdApi || IntPtr.Zero == m_pMdApi))
                {
                    m_pMdApi = MdApi.MD_CreateMdApi();
                    MdApi.CTP_RegOnRtnDepthMarketData(m_pMsgQueue, _fnOnRtnDepthMarketData_Holder);
                    MdApi.MD_RegMsgQueue2MdApi(m_pMdApi, m_pMsgQueue);
                    MdApi.MD_Connect(m_pMdApi, _newTempPath, string.Join(";", server.MarketData.ToArray()), server.BrokerID, account.InvestorId, account.Password);

                    //向单例对象中注入操作用句柄
                    CTPAPI.GetInstance().__RegMdApi(m_pMdApi);
                }
            }
        }

        //建立交易
        private void Connect_TD()
        {
            lock (_lockTd)
            {
                if (_bWantTdConnect
                && (null == m_pTdApi || IntPtr.Zero == m_pTdApi))
                {
                    m_pTdApi = TraderApi.TD_CreateTdApi();
                    TraderApi.CTP_RegOnErrRtnOrderAction(m_pMsgQueue, _fnOnErrRtnOrderAction_Holder);
                    TraderApi.CTP_RegOnErrRtnOrderInsert(m_pMsgQueue, _fnOnErrRtnOrderInsert_Holder);
                    TraderApi.CTP_RegOnRspOrderAction(m_pMsgQueue, _fnOnRspOrderAction_Holder);
                    TraderApi.CTP_RegOnRspOrderInsert(m_pMsgQueue, _fnOnRspOrderInsert_Holder);
                    TraderApi.CTP_RegOnRspQryDepthMarketData(m_pMsgQueue, _fnOnRspQryDepthMarketData_Holder);
                    TraderApi.CTP_RegOnRspQryInstrument(m_pMsgQueue, _fnOnRspQryInstrument_Holder);
                    TraderApi.CTP_RegOnRspQryInstrumentCommissionRate(m_pMsgQueue, _fnOnRspQryInstrumentCommissionRate_Holder);
                    TraderApi.CTP_RegOnRspQryInstrumentMarginRate(m_pMsgQueue, _fnOnRspQryInstrumentMarginRate_Holder);
                    TraderApi.CTP_RegOnRspQryInvestorPosition(m_pMsgQueue, _fnOnRspQryInvestorPosition_Holder);
                    TraderApi.CTP_RegOnRspQryTradingAccount(m_pMsgQueue, _fnOnRspQryTradingAccount_Holder);
                    TraderApi.CTP_RegOnRtnInstrumentStatus(m_pMsgQueue, _fnOnRtnInstrumentStatus_Holder);
                    TraderApi.CTP_RegOnRtnOrder(m_pMsgQueue, _fnOnRtnOrder_Holder);
                    TraderApi.CTP_RegOnRtnTrade(m_pMsgQueue, _fnOnRtnTrade_Holder);
                    TraderApi.TD_RegMsgQueue2TdApi(m_pTdApi, m_pMsgQueue);
                    TraderApi.TD_Connect(m_pTdApi, _newTempPath, string.Join(";", server.Trading.ToArray()),
                        server.BrokerID, account.InvestorId, account.Password,
                        ResumeType,
                        server.UserProductInfo, server.AuthCode);

                    //向单例对象中注入操作用句柄
                    CTPAPI.GetInstance().__RegTdApi(m_pTdApi);
                }
            }
        }
        #endregion

        #region 断开连接
        private void _Disconnect()
        {
            timerConnect.Enabled = false;
            timerDisconnect.Enabled = false;
            timerAccount.Enabled = false;
            timerPonstion.Enabled = false;

            CTPAPI.GetInstance().__RegInstrumentDictionary(null);
            CTPAPI.GetInstance().__RegInstrumentCommissionRateDictionary(null);
            CTPAPI.GetInstance().__RegInstrumentMarginRateDictionary(null);
            CTPAPI.GetInstance().__RegDepthMarketDataDictionary(null);

            Disconnect_MD();
            Disconnect_TD();
            Disconnect_MsgQueue();

            Clear();
            ChangeStatus(ProviderStatus.Disconnected);
            isConnected = false;
            EmitDisconnectedEvent();
        }

        private void Disconnect_MsgQueue()
        {
            lock (_lockMsgQueue)
            {
                if (null != m_pMsgQueue && IntPtr.Zero != m_pMsgQueue)
                {
                    CommApi.CTP_StopMsgQueue(m_pMsgQueue);

                    CommApi.CTP_ReleaseMsgQueue(m_pMsgQueue);
                    m_pMsgQueue = IntPtr.Zero;
                }
            }
        }

        private void Disconnect_MD()
        {
            lock (_lockMd)
            {
                if (null != m_pMdApi && IntPtr.Zero != m_pMdApi)
                {
                    MdApi.MD_RegMsgQueue2MdApi(m_pMdApi, IntPtr.Zero);
                    MdApi.MD_ReleaseMdApi(m_pMdApi);
                    m_pMdApi = IntPtr.Zero;

                    CTPAPI.GetInstance().__RegTdApi(m_pMdApi);
                }
                _bMdConnected = false;
            }
        }

        private void Disconnect_TD()
        {
            lock (_lockTd)
            {
                if (null != m_pTdApi && IntPtr.Zero != m_pTdApi)
                {
                    TraderApi.TD_RegMsgQueue2TdApi(m_pTdApi, IntPtr.Zero);
                    TraderApi.TD_ReleaseTdApi(m_pTdApi);
                    m_pTdApi = IntPtr.Zero;

                    CTPAPI.GetInstance().__RegTdApi(m_pTdApi);
                }
                _bTdConnected = false;
            }
        }
        #endregion

        #region 更新时间
        private void UpdateLocalTime(SetTimeMode _SetLocalTimeMode, CThostFtdcRspUserLoginField pRspUserLogin)
        {
            string strNewTime;
            switch (_SetLocalTimeMode)
            {
                case SetTimeMode.None:
                    return;
                case SetTimeMode.LoginTime:
                    strNewTime = pRspUserLogin.LoginTime;
                    break;
                case SetTimeMode.SHFETime:
                    strNewTime = pRspUserLogin.SHFETime;
                    break;
                case SetTimeMode.DCETime:
                    strNewTime = pRspUserLogin.DCETime;
                    break;
                case SetTimeMode.CZCETime:
                    strNewTime = pRspUserLogin.CZCETime;
                    break;
                case SetTimeMode.FFEXTime:
                    strNewTime = pRspUserLogin.FFEXTime;
                    break;
                default:
                    return;
            }

            try
            {
                int HH = int.Parse(strNewTime.Substring(0, 2));
                int mm = int.Parse(strNewTime.Substring(3, 2));
                int ss = int.Parse(strNewTime.Substring(6, 2));

                DateTime _dateTime = new DateTime(_yyyy, _MM, _dd, HH, mm, ss);
                DateTime _newDateTime = _dateTime.AddMilliseconds(AddMilliseconds);
                tdlog.Info("SetLocalTime:Return:{0},{1}",
                    WinAPI.SetLocalTime(_newDateTime),
                    _newDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            }
            catch (Exception)
            {
                tdlog.Warn("{0}不能解析成时间", strNewTime);
            }
        }
        #endregion

        #region 连接状态回调
        private void OnConnect(IntPtr pApi, ref CThostFtdcRspUserLoginField pRspUserLogin, ConnectionStatus result)
        {
            //用于行情记算时简化时间解码
            try
            {
                int _yyyyMMdd = int.Parse(pRspUserLogin.TradingDay);
                _yyyy = _yyyyMMdd / 10000;
                _MM = (_yyyyMMdd % 10000) / 100;
                _dd = _yyyyMMdd % 100;
            }
            catch (Exception)
            {
                _yyyy = DateTime.Now.Year;
                _MM = DateTime.Now.Month;
                _dd = DateTime.Now.Day;
            }

            if (m_pMdApi == pApi)//行情
            {
                _bMdConnected = false;
                if (ConnectionStatus.E_logined == result)
                {
                    _bMdConnected = true;

                    mdlog.Info("TradingDay:{0},LoginTime:{1},SHFETime:{2},DCETime:{3},CZCETime:{4},FFEXTime:{5},FrontID:{6},SessionID:{7}",
                        pRspUserLogin.TradingDay, pRspUserLogin.LoginTime, pRspUserLogin.SHFETime,
                        pRspUserLogin.DCETime, pRspUserLogin.CZCETime, pRspUserLogin.FFEXTime,
                        pRspUserLogin.FrontID,pRspUserLogin.SessionID);

                    // 如果断线重连是使用的重新新建对象的方式，则要重新订阅
                    if (_dictAltSymbol2Instrument.Count > 0)
                    {
                        mdlog.Info("行情列表数{0},全部重新订阅", _dictAltSymbol2Instrument.Count);
                        foreach (string symbol in _dictAltSymbol2Instrument.Keys)
                        {
                            MdApi.MD_Subscribe(m_pMdApi, symbol);
                        }
                    }
                }
                //这也有个时间，但取出的时间无效
                mdlog.Info("{0},{1}", result, pRspUserLogin.LoginTime);
            }
            else if (m_pTdApi == pApi)//交易
            {
                _bTdConnected = false;
                if (ConnectionStatus.E_logined == result)
                {
                    _RspUserLogin = pRspUserLogin;

                    tdlog.Info("TradingDay:{0},LoginTime:{1},SHFETime:{2},DCETime:{3},CZCETime:{4},FFEXTime:{5},FrontID:{6},SessionID:{7}",
                        pRspUserLogin.TradingDay, pRspUserLogin.LoginTime, pRspUserLogin.SHFETime,
                        pRspUserLogin.DCETime, pRspUserLogin.CZCETime, pRspUserLogin.FFEXTime,
                        pRspUserLogin.FrontID, pRspUserLogin.SessionID);

                    UpdateLocalTime(SetLocalTimeMode, pRspUserLogin);
                }
                else if (ConnectionStatus.E_confirmed == result)
                {
                    _bTdConnected = true;
                    //请求查询资金
                    TraderApi.TD_ReqQryTradingAccount(m_pTdApi);

                    //请求查询全部持仓
                    TraderApi.TD_ReqQryInvestorPosition(m_pTdApi, null);

                    //请求查询合约
                    _dictInstruments.Clear();
                    _dictCommissionRate.Clear();
                    _dictMarginRate.Clear();
                    TraderApi.TD_ReqQryInstrument(m_pTdApi, null);

                    timerAccount.Enabled = true;
                    timerPonstion.Enabled = true;
                }

                tdlog.Info("{0},{1}", result, pRspUserLogin.LoginTime);
            }

            if (
                (_bMdConnected && _bTdConnected)//都连上
                || (!_bWantMdConnect && _bTdConnected)//只用分析交易连上
                || (!_bWantTdConnect && _bMdConnected)//只用分析行情连上
                )
            {
                timerConnect.Enabled = true;
                timerDisconnect.Enabled = false;//都连接上了，用不着定时断
                ChangeStatus(ProviderStatus.LoggedIn);
                isConnected = true;
                EmitConnectedEvent();
            }
        }

        private void OnDisconnect(IntPtr pApi, ref CThostFtdcRspInfoField pRspInfo, ConnectionStatus step)
        {
            if (m_pMdApi == pApi)//行情
            {
                _bMdConnected = false;
                if (isConnected)
                {
                    mdlog.Error("Step:{0},ErrorID:{1},ErrorMsg:{2},等待定时重试连接", step, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                }
                else
                {
                    mdlog.Info("Step:{0},ErrorID:{1},ErrorMsg:{2}", step, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                }
            }
            else if (m_pTdApi == pApi)//交易
            {
                _bTdConnected = false;
                if (isConnected)//如果以前连成功，表示密码没有错，只是初始化失败，可以重试
                {
                    tdlog.Error("Step:{0},ErrorID:{1},ErrorMsg:{2},等待定时重试连接", step, pRspInfo.ErrorID, pRspInfo.ErrorMsg);

                    if (7 == pRspInfo.ErrorID//综合交易平台：还没有初始化
                        || 8 == pRspInfo.ErrorID)//综合交易平台：前置不活跃
                    {
                        //这个地方登录重试太快了，等定时器来处理吧！
                        //Disconnect_TD();
                        //Connect_TD();
                    }
                }
                else
                {
                    tdlog.Info("Step:{0},ErrorID:{1},ErrorMsg:{2}", step, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                }
            }
            if (!isConnected)//从来没有连接成功过，可能是密码错误，直接退出
            {
                //不能在线程中停止线程，这样会导致软件关闭进程不退出
                //_Disconnect();
            }
            else
            {
                //以前连接过，现在断了次线，要等重连
                ChangeStatus(ProviderStatus.Connecting);
                EmitDisconnectedEvent();
            }
        }
        #endregion
    }
}
