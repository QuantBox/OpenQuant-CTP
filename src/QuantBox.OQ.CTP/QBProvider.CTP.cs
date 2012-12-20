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
    partial class QBProvider
    {
        private fnOnConnect                         _fnOnConnect_Holder;
        private fnOnDisconnect                      _fnOnDisconnect_Holder;
        private fnOnErrRtnOrderAction               _fnOnErrRtnOrderAction_Holder;
        private fnOnErrRtnOrderInsert               _fnOnErrRtnOrderInsert_Holder;
        private fnOnRspError                        _fnOnRspError_Holder;
        private fnOnRspOrderAction                  _fnOnRspOrderAction_Holder;
        private fnOnRspOrderInsert                  _fnOnRspOrderInsert_Holder;
        private fnOnRspQryDepthMarketData           _fnOnRspQryDepthMarketData_Holder;
        private fnOnRspQryInstrument                _fnOnRspQryInstrument_Holder;
        private fnOnRspQryInstrumentCommissionRate  _fnOnRspQryInstrumentCommissionRate_Holder;
        private fnOnRspQryInstrumentMarginRate      _fnOnRspQryInstrumentMarginRate_Holder;
        private fnOnRspQryInvestorPosition          _fnOnRspQryInvestorPosition_Holder;
        private fnOnRspQryTradingAccount            _fnOnRspQryTradingAccount_Holder;
        private fnOnRtnDepthMarketData              _fnOnRtnDepthMarketData_Holder;
        private fnOnRtnOrder                        _fnOnRtnOrder_Holder;
        private fnOnRtnTrade                        _fnOnRtnTrade_Holder;

        private void InitCallbacks()
        {
            //由于回调函数可能被GC回收，所以用成员变量将回调函数保存下来
            _fnOnConnect_Holder                         = OnConnect;
            _fnOnDisconnect_Holder                      = OnDisconnect;
            _fnOnErrRtnOrderAction_Holder               = OnErrRtnOrderAction;
            _fnOnErrRtnOrderInsert_Holder               = OnErrRtnOrderInsert;
            _fnOnRspError_Holder                        = OnRspError;
            _fnOnRspOrderAction_Holder                  = OnRspOrderAction;
            _fnOnRspOrderInsert_Holder                  = OnRspOrderInsert;
            _fnOnRspQryDepthMarketData_Holder           = OnRspQryDepthMarketData;
            _fnOnRspQryInstrument_Holder                = OnRspQryInstrument;
            _fnOnRspQryInstrumentCommissionRate_Holder  = OnRspQryInstrumentCommissionRate;
            _fnOnRspQryInstrumentMarginRate_Holder      = OnRspQryInstrumentMarginRate;
            _fnOnRspQryInvestorPosition_Holder          = OnRspQryInvestorPosition;
            _fnOnRspQryTradingAccount_Holder            = OnRspQryTradingAccount;
            _fnOnRtnDepthMarketData_Holder              = OnRtnDepthMarketData;
            _fnOnRtnOrder_Holder                        = OnRtnOrder;
            _fnOnRtnTrade_Holder                        = OnRtnTrade;
        }

        private IntPtr m_pMsgQueue = IntPtr.Zero;   //消息队列指针
        private IntPtr m_pMdApi = IntPtr.Zero;      //行情对象指针
        private IntPtr m_pTdApi = IntPtr.Zero;      //交易对象指针

        //行情有效状态，约定连接上并通过认证为有效
        private volatile bool _bMdConnected;
        //交易有效状态，约定连接上，通过认证并进行结算单确认为有效
        private volatile bool _bTdConnected;

        //表示用户操作，也许有需求是用户有多个行情，只连接第一个等
        private bool _bWantMdConnect;
        private bool _bWantTdConnect;

        private readonly object _lockMd = new object();
        private readonly object _lockTd = new object();
        private readonly object _lockMsgQueue = new object();

        //记录交易登录成功后的SessionID、FrontID等信息
        private CThostFtdcRspUserLoginField _RspUserLogin;

        //记录界面生成的报单，用于定位收到回报消息时所确定的报单,可以多个Ref对应一个Order
        private readonly Dictionary<string, SingleOrder> _OrderRef2Order = new Dictionary<string, SingleOrder>();
        //一个Order可能分拆成多个报单，如可能由平今与平昨，或开新单组合而成
        private readonly Dictionary<SingleOrder, Dictionary<string, CThostFtdcOrderField>> _Orders4Cancel = new Dictionary<SingleOrder, Dictionary<string, CThostFtdcOrderField>>();

        //记录账号的实际持仓，保证以最低成本选择开平
        private readonly DbInMemInvestorPosition _dbInMemInvestorPosition = new DbInMemInvestorPosition();
        //记录合约实际行情，用于向界面通知行情用，这里应当记录AltSymbol
        private readonly Dictionary<string, CThostFtdcDepthMarketDataField> _dictDepthMarketData = new Dictionary<string, CThostFtdcDepthMarketDataField>();
        //记录合约列表,从实盘合约名到对象的映射
        private readonly Dictionary<string, CThostFtdcInstrumentField> _dictInstruments = new Dictionary<string, CThostFtdcInstrumentField>();
        //记录手续费率,从实盘合约名到对象的映射
        private readonly Dictionary<string, CThostFtdcInstrumentCommissionRateField> _dictCommissionRate = new Dictionary<string, CThostFtdcInstrumentCommissionRateField>();
        //记录保证金率,从实盘合约名到对象的映射
        private readonly Dictionary<string, CThostFtdcInstrumentMarginRateField> _dictMarginRate = new Dictionary<string, CThostFtdcInstrumentMarginRateField>();
        //记录
        private readonly Dictionary<string, Instrument> _dictAltSymbol2Instrument = new Dictionary<string, Instrument>();

        //用于行情的时间，只在登录时改动，所以要求开盘时能得到更新
        private int _yyyy;
        private int _MM;
        private int _dd;

        private ServerItem server;
        private AccountItem account;

        #region 清除数据
        private void Clear()
        {
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

        private void ChangeDay()
        {
            //只在每天的1点以内更新一次
            if (_dd != DateTime.Now.Day
                &&DateTime.Now.Hour<1)
            {
                //测试平台晚上会出现交易日为明天的情况，如果现在清空会导致有行情过来，但不显示在界面上
                //所以修改行情接收部分总是更新
                //_dictDepthMarketData.Clear();
                _dictInstruments.Clear();
                _dictCommissionRate.Clear();
                _dictMarginRate.Clear();

                _yyyy = DateTime.Now.Year;
                _MM = DateTime.Now.Month;
                _dd = DateTime.Now.Day;
            }
        }
        #endregion

        #region 定时器
        private readonly System.Timers.Timer timerDisconnect = new System.Timers.Timer(20 * 1000);
        private readonly System.Timers.Timer timerAccount = new System.Timers.Timer(3 * 60 * 1000);
        private readonly System.Timers.Timer timerPonstion = new System.Timers.Timer(5 * 60 * 1000);

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
            ChangeDay();
            if (_bTdConnected)
            {
                TraderApi.TD_ReqQryInvestorPosition(m_pTdApi, "");
            }
        }

        void timerAccount_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ChangeDay();
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
                if (0 == serversList.Count)
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

                if (_bWantTdConnect &&0 == server.Trading.Count())
                {
                    MessageBox.Show("交易服务器地址不全");
                    break;
                }

                if (_bWantMdConnect &&0 == server.MarketData.Count())
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
                timerAccount.Enabled = true;
                timerPonstion.Enabled = true;
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

        private void UpdateLocalTime(SetTimeMode _SetLocalTimeMode,CThostFtdcRspUserLoginField pRspUserLogin)
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
            catch (Exception ex)
            {
                tdlog.Warn("{0}不能解析成时间", strNewTime);
            }
        }

        #region 连接状态回调
        private void OnConnect(IntPtr pApi, ref CThostFtdcRspUserLoginField pRspUserLogin, ConnectionStatus result)
        {
            if (m_pMdApi == pApi)//行情
            {
                _bMdConnected = false;
                if (ConnectionStatus.E_logined == result)
                {
                    _bMdConnected = true;

                    //只登录行情时得得更新行情时间，但行情却可以隔夜不断，所以要定时更新
                    if (!_bWantTdConnect)
                    {
                        _yyyy = DateTime.Now.Year;
                        _MM = DateTime.Now.Month;
                        _dd = DateTime.Now.Day;
                    }

                    mdlog.Info("TradingDay:{0},LoginTime:{1},SHFETime:{2},DCETime:{3},CZCETime:{4},FFEXTime:{5}",
                        pRspUserLogin.TradingDay, pRspUserLogin.LoginTime, pRspUserLogin.SHFETime,
                        pRspUserLogin.DCETime, pRspUserLogin.CZCETime, pRspUserLogin.FFEXTime);
                }
                //这也有个时间，但取出的时间无效
                mdlog.Info("{0},{1}",result, pRspUserLogin.LoginTime);
            }
            else if (m_pTdApi == pApi)//交易
            {
                _bTdConnected = false;

                if (ConnectionStatus.E_logined == result)
                {
                    _RspUserLogin = pRspUserLogin;

                    //用于行情记算时简化时间解码
                    int _yyyyMMdd = int.Parse(pRspUserLogin.TradingDay);
                    _yyyy = _yyyyMMdd / 10000;
                    _MM = (_yyyyMMdd % 10000) / 100;
                    _dd = _yyyyMMdd % 100;

                    tdlog.Info("TradingDay:{0},LoginTime:{1},SHFETime:{2},DCETime:{3},CZCETime:{4},FFEXTime:{5}",
                        pRspUserLogin.TradingDay, pRspUserLogin.LoginTime, pRspUserLogin.SHFETime,
                        pRspUserLogin.DCETime, pRspUserLogin.CZCETime, pRspUserLogin.FFEXTime);

                    UpdateLocalTime(SetLocalTimeMode,pRspUserLogin);
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
                    TraderApi.TD_ReqQryInstrument(m_pTdApi, null);
                }

                tdlog.Info("{0},{1}",result, pRspUserLogin.LoginTime);
            }

            if (
                (_bMdConnected && _bTdConnected)//都连上
                || (!_bWantMdConnect && _bTdConnected)//只用分析交易连上
                || (!_bWantTdConnect && _bMdConnected)//只用分析行情连上
                )
            {
                timerDisconnect.Enabled = false;//都连接上了，用不着定时断开了
                ChangeStatus(ProviderStatus.LoggedIn);
                isConnected = true;
                EmitConnectedEvent();
            }
        }

        private void OnDisconnect(IntPtr pApi, ref CThostFtdcRspInfoField pRspInfo, ConnectionStatus step)
        {
            if (m_pMdApi == pApi)//行情
            {
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
                if (isConnected)//如果以前连成功，表示密码没有错，只是初始化失败，可以重试
                {
                    tdlog.Error("Step:{0},ErrorID:{1},ErrorMsg:{2},等待定时重试连接", step, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
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

        #region 深度行情回调
        private DateTime _dateTime = DateTime.Now;
        private void OnRtnDepthMarketData(IntPtr pApi, ref CThostFtdcDepthMarketDataField pDepthMarketData)
        {
            CThostFtdcDepthMarketDataField DepthMarket;
            _dictDepthMarketData.TryGetValue(pDepthMarketData.InstrumentID, out DepthMarket);
            //将更新字典的功能提前，因为如果一开始就OnTrade中下单，涨跌停没有更新
            _dictDepthMarketData[pDepthMarketData.InstrumentID] = pDepthMarketData;

            if (TimeMode.LocalTime == _TimeMode)
            {
                //为了生成正确的Bar,使用本地时间
                _dateTime = Clock.Now;
            }
            else
            {
                //直接按HH:mm:ss来解析，测试过这种方法目前是效率比较高的方法
                int HH = int.Parse(pDepthMarketData.UpdateTime.Substring(0, 2));
                int mm = int.Parse(pDepthMarketData.UpdateTime.Substring(3, 2));
                int ss = int.Parse(pDepthMarketData.UpdateTime.Substring(6, 2));

                _dateTime = new DateTime(_yyyy, _MM, _dd, HH, mm, ss, pDepthMarketData.UpdateMillisec);
            }

            Instrument instrument = _dictAltSymbol2Instrument[pDepthMarketData.InstrumentID];

            //通过测试，发现IB的Trade与Quote在行情过来时数量是不同的，在这也做到不同
            if (DepthMarket.LastPrice == pDepthMarketData.LastPrice
                && DepthMarket.Volume == pDepthMarketData.Volume)
            { }
            else
            {
                //行情过来时是今天累计成交量，得转换成每个tick中成交量之差
                int volume = pDepthMarketData.Volume - DepthMarket.Volume;
                if (0 == DepthMarket.Volume)
                {
                    //没有接收到最开始的一条，所以这计算每个Bar的数据时肯定超大，强行设置为0
                    volume = 0;
                }
                else if (volume < 0)
                {
                    //如果隔夜运行，会出现今早成交量0-昨收盘成交量，出现负数，所以当发现为负时要修改
                    volume = pDepthMarketData.Volume;
                }

                Trade trade = new Trade(_dateTime,
                    pDepthMarketData.LastPrice == double.MaxValue ? 0 : pDepthMarketData.LastPrice,
                    volume);

                if (null != MarketDataFilter)
                {
                    Trade t = MarketDataFilter.FilterTrade(trade, instrument.Symbol);
                    if (null != t)
                    {
                        EmitNewTradeEvent(instrument, t);
                    }
                }
                else
                {
                    EmitNewTradeEvent(instrument, trade);
                }
            }

            if (
                DepthMarket.BidVolume1 == pDepthMarketData.BidVolume1
                && DepthMarket.AskVolume1 == pDepthMarketData.AskVolume1
                && DepthMarket.BidPrice1 == pDepthMarketData.BidPrice1
                && DepthMarket.AskPrice1 == pDepthMarketData.AskPrice1
                )
            { }
            else
            {
                Quote quote = new Quote(_dateTime,
                    pDepthMarketData.BidPrice1 == double.MaxValue ? 0 : pDepthMarketData.BidPrice1,
                    pDepthMarketData.BidVolume1,
                    pDepthMarketData.AskPrice1 == double.MaxValue ? 0 : pDepthMarketData.AskPrice1,
                    pDepthMarketData.AskVolume1
                );

                if (null != MarketDataFilter)
                {
                    Quote q = MarketDataFilter.FilterQuote(quote, instrument.Symbol);
                    if (null != q)
                    {
                        EmitNewQuoteEvent(instrument, q);
                    }
                }
                else
                {
                    EmitNewQuoteEvent(instrument, quote);
                }
            }
        }

        public void OnRspQryDepthMarketData(IntPtr pTraderApi, ref CThostFtdcDepthMarketDataField pDepthMarketData, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            if (0 == pRspInfo.ErrorID)
            {
                CThostFtdcDepthMarketDataField DepthMarket;
                if (!_dictDepthMarketData.TryGetValue(pDepthMarketData.InstrumentID, out DepthMarket))
                {
                    //没找到此元素，保存一下
                    _dictDepthMarketData[pDepthMarketData.InstrumentID] = pDepthMarketData;
                }

                tdlog.Info("已经接收查询深度行情 {0}", pDepthMarketData.InstrumentID);
                //通知单例
                CTPAPI.GetInstance().FireOnRspQryDepthMarketData(pDepthMarketData);
            }
            else
            {
                tdlog.Error("nRequestID:{0},ErrorID:{1},OnRspQryDepthMarketData:{2}", nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitError(nRequestID, pRspInfo.ErrorID, "OnRspQryDepthMarketData:" + pRspInfo.ErrorMsg);
            }
        }
    
        #endregion

        #region 撤单
        private void Cancel(SingleOrder order)
        {
            if (!_bTdConnected)
            {
                EmitError(-1,-1,"交易服务器没有连接，无法撤单");
                tdlog.Error("交易服务器没有连接，无法撤单");
                return;
            }

            Dictionary<string, CThostFtdcOrderField> _Ref2Action;
            if (_Orders4Cancel.TryGetValue(order, out _Ref2Action))
            {
                lock (_Ref2Action)
                {
                    CThostFtdcOrderField __Order;
                    foreach (CThostFtdcOrderField _Order in _Ref2Action.Values)
                    {
                        __Order = _Order;
                        //这地方要是过滤下就好了
                        TraderApi.TD_CancelOrder(m_pTdApi, ref __Order);
                    }
                }
            }
        }
        #endregion

        #region 下单与订单分割
        private struct SOrderSplitItem
        {
            public int qty;
            public string szCombOffsetFlag;
        };

        private void Send(NewOrderSingle order)
        {            
            if (!_bTdConnected)
            {
                EmitError(-1,-1,"交易服务器没有连接，无法报单");
                tdlog.Error("交易服务器没有连接，无法报单");
                return;
            }

            Instrument inst = InstrumentManager.Instruments[order.Symbol];
            string altSymbol = inst.GetSymbol(Name);
            string altExchange = inst.GetSecurityExchange(Name);
            double tickSize = inst.TickSize;

            CThostFtdcInstrumentField _Instrument;
            if (_dictInstruments.TryGetValue(altSymbol, out _Instrument))
            {
                //从合约列表中取交易所名与tickSize，不再依赖用户手工设置的参数了
                tickSize = _Instrument.PriceTick;
                altExchange = _Instrument.ExchangeID;
            }
            
            //最小变动价格修正
            double price = order.Price;

            //市价修正，如果不连接行情，此修正不执行，得策略层处理
            CThostFtdcDepthMarketDataField DepthMarket;
            //如果取出来了，并且为有效的，涨跌停价将不为0
            _dictDepthMarketData.TryGetValue(altSymbol, out DepthMarket);

            //市价单模拟
            if (OrdType.Market == order.OrdType)
            {
                //按买卖调整价格
                if (order.Side == Side.Buy)
                {
                    price = DepthMarket.LastPrice + LastPricePlusNTicks * tickSize;
                }
                else
                {
                    price = DepthMarket.LastPrice - LastPricePlusNTicks * tickSize;
                }
            }

            //没有设置就直接用
            if (tickSize > 0)
            {
                decimal remainder = ((decimal)price % (decimal)tickSize);
                if (remainder != 0)
                {
                    if (order.Side == Side.Buy)
                    {
                        price = Math.Ceiling(price / tickSize) * tickSize;
                    }
                    else
                    {
                        price = Math.Floor(price / tickSize) * tickSize;
                    }
                }
                else
                {
                    //正好能整除，不操作
                }            
            }

            if (0 == DepthMarket.UpperLimitPrice
                && 0 == DepthMarket.LowerLimitPrice)
            {
                //涨跌停无效
            }
            else
            {
                //防止价格超过涨跌停
                if (price >= DepthMarket.UpperLimitPrice)
                    price = DepthMarket.UpperLimitPrice;
                else if (price <= DepthMarket.LowerLimitPrice)
                    price = DepthMarket.LowerLimitPrice;
            }

            int YdPosition = 0;
            int TodayPosition = 0;

            string szCombOffsetFlag;
            if (order.Side == Side.Buy)
            {
                //买，先看有没有空单，有就平空单,没有空单，直接买开多单
                _dbInMemInvestorPosition.GetPositions(altSymbol,
                    TThostFtdcPosiDirectionType.Short, HedgeFlagType, out YdPosition, out TodayPosition);//TThostFtdcHedgeFlagType.Speculation
            }
            else//是否要区分Side.Sell与Side.SellShort呢？
            {
                //卖，先看有没有多单，有就平多单,没有多单，直接买开空单
                _dbInMemInvestorPosition.GetPositions(altSymbol,
                    TThostFtdcPosiDirectionType.Long, HedgeFlagType, out YdPosition, out TodayPosition);
            }

            tdlog.Info("Side:{0},Price:{1},LastPrice:{2},Qty:{3},Text:{4},YdPosition:{5},TodayPosition:{6}",
                    order.Side, order.Price, DepthMarket.LastPrice, order.OrderQty, order.Text, YdPosition, TodayPosition);

            List<SOrderSplitItem> OrderSplitList = new List<SOrderSplitItem>();
            SOrderSplitItem orderSplitItem;

            //根据 梦翔 与 马不停蹄 的提示，新加在Text域中指定开平标志的功能
            int nOpenCloseFlag = 0;
            if (order.Text.StartsWith(OpenPrefix))
            {
                nOpenCloseFlag = 1;
            }
            else if (order.Text.StartsWith(ClosePrefix))
            {
                nOpenCloseFlag = -1;
            }
            else if (order.Text.StartsWith(CloseTodayPrefix))
            {
                nOpenCloseFlag = -2;
            }
            else if (order.Text.StartsWith(CloseYesterdayPrefix))
            {
                nOpenCloseFlag = -3;
            }

            int leave = (int)order.OrderQty;

            //是否上海？上海先平今，然后平昨，最后开仓
            //使用do主要是想利用break功能
            //平仓部分
            do
            {
                //指定开仓，直接跳过
                if (nOpenCloseFlag>0)
                    break;

                //表示指定平今与平昨
                if (nOpenCloseFlag<-1)
                {
                    if (-2 == nOpenCloseFlag)
                    {
                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.CloseToday, (byte)TThostFtdcOffsetFlagType.CloseToday };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        //肯定是-3了
                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.CloseYesterday, (byte)TThostFtdcOffsetFlagType.CloseYesterday };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
                    }

                    orderSplitItem.qty = leave;
                    orderSplitItem.szCombOffsetFlag = szCombOffsetFlag;
                    OrderSplitList.Add(orderSplitItem);

                    leave = 0;

                    break;
                }

                if (SupportCloseToday.Contains(altExchange))
                {
                    //先看平今
                    if (leave > 0 && TodayPosition > 0)
                    {
                        int min = Math.Min(TodayPosition, leave);
                        leave -= min;

                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.CloseToday, (byte)TThostFtdcOffsetFlagType.CloseToday };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);

                        orderSplitItem.qty = min;
                        orderSplitItem.szCombOffsetFlag = szCombOffsetFlag;
                        OrderSplitList.Add(orderSplitItem);
                    }
                    if (leave > 0 && YdPosition > 0)
                    {
                        int min = Math.Min(YdPosition, leave);
                        leave -= min;

                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.CloseYesterday, (byte)TThostFtdcOffsetFlagType.CloseYesterday };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);

                        orderSplitItem.qty = min;
                        orderSplitItem.szCombOffsetFlag = szCombOffsetFlag;
                        OrderSplitList.Add(orderSplitItem);
                    }
                }
                else
                {
                    //平仓
                    int position = TodayPosition + YdPosition;
                    if (leave > 0 && position > 0)
                    {
                        int min = Math.Min(position, leave);
                        leave -= min;

                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.Close, (byte)TThostFtdcOffsetFlagType.Close };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);

                        orderSplitItem.qty = min;
                        orderSplitItem.szCombOffsetFlag = szCombOffsetFlag;
                        OrderSplitList.Add(orderSplitItem);
                    }
                }
            } while (false);

            do
            {
                //指定平仓，直接跳过
                if (nOpenCloseFlag<0)
                    break;

                if (leave > 0)
                {
                    byte[] bytes = { (byte)TThostFtdcOffsetFlagType.Open, (byte)TThostFtdcOffsetFlagType.Open };
                    szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);

                    orderSplitItem.qty = leave;
                    orderSplitItem.szCombOffsetFlag = szCombOffsetFlag;
                    OrderSplitList.Add(orderSplitItem);

                    leave = 0;
                }
            } while (false);

            if (leave > 0)
            {
                tdlog.Info("CTP:还剩余{0}手,你应当是强制指定平仓了，但持仓数小于要平手数", leave);
            }

            //将第二腿也设置成一样，这样在使用组合时这地方不用再调整
            byte[] bytes2 = { (byte)HedgeFlagType, (byte)HedgeFlagType };
            string szCombHedgeFlag = System.Text.Encoding.Default.GetString(bytes2, 0, bytes2.Length);

            bool bSupportMarketOrder = SupportMarketOrder.Contains(altExchange);

            foreach (SOrderSplitItem it in OrderSplitList)
            {
                int nRet = 0;

                switch (order.OrdType)
                {
                    case OrdType.Limit:
                        nRet = TraderApi.TD_SendOrder(m_pTdApi,
                            altSymbol,
                            order.Side == Side.Buy ? TThostFtdcDirectionType.Buy : TThostFtdcDirectionType.Sell,
                            it.szCombOffsetFlag,
                            szCombHedgeFlag,
                            it.qty,
                            price,
                            TThostFtdcOrderPriceTypeType.LimitPrice,
                            TThostFtdcTimeConditionType.GFD,
                            TThostFtdcContingentConditionType.Immediately,
                            order.StopPx);
                        break;
                    case OrdType.Market:
                        if (bSupportMarketOrder)
                        {
                            nRet = TraderApi.TD_SendOrder(m_pTdApi,
                            altSymbol,
                            order.Side == Side.Buy ? TThostFtdcDirectionType.Buy : TThostFtdcDirectionType.Sell,
                            it.szCombOffsetFlag,
                            szCombHedgeFlag,
                            it.qty,
                            0,
                            TThostFtdcOrderPriceTypeType.AnyPrice,
                            TThostFtdcTimeConditionType.IOC,
                            TThostFtdcContingentConditionType.Immediately,
                            order.StopPx);
                        } 
                        else
                        {
                            nRet = TraderApi.TD_SendOrder(m_pTdApi,
                            altSymbol,
                            order.Side == Side.Buy ? TThostFtdcDirectionType.Buy : TThostFtdcDirectionType.Sell,
                            it.szCombOffsetFlag,
                            szCombHedgeFlag,
                            it.qty,
                            price,
                            TThostFtdcOrderPriceTypeType.LimitPrice,
                            TThostFtdcTimeConditionType.GFD,
                            TThostFtdcContingentConditionType.Immediately,
                            order.StopPx);
                        }                        
                        break;
                    default:
                        tdlog.Warn("没有实现{0}", order.OrdType);
                        break;
                }

                if (nRet > 0)
                {
                    _OrderRef2Order.Add(string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, nRet), order as SingleOrder);
                }
            }
        }
        #endregion

        #region 报单回报
        private void OnRtnOrder(IntPtr pTraderApi, ref CThostFtdcOrderField pOrder)
        {
            tdlog.Info("{0},{1},{2},开平{3},价{4},原量{5},成交{6},提交{7},状态{8},引用{9},报单编号{10},{11}",
                    pOrder.InsertTime, pOrder.InstrumentID, pOrder.Direction, pOrder.CombOffsetFlag, pOrder.LimitPrice,
                    pOrder.VolumeTotalOriginal, pOrder.VolumeTraded, pOrder.OrderSubmitStatus, pOrder.OrderStatus,
                    pOrder.OrderRef, pOrder.OrderSysID, pOrder.StatusMsg);

            SingleOrder order;
            string strKey = string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pOrder.OrderRef);
            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                order.Text = string.Format("{0}|{1}", order.Text.Substring(0,Math.Min(order.Text.Length,64)), pOrder.StatusMsg);

                //找到对应的报单回应
                Dictionary<string, CThostFtdcOrderField> _Ref2Action;
                if (!_Orders4Cancel.TryGetValue(order, out _Ref2Action))
                {
                    //没找到，自己填一个
                    _Ref2Action = new Dictionary<string, CThostFtdcOrderField>();
                    _Orders4Cancel[order] = _Ref2Action;
                }

                lock (_Ref2Action)
                {
                    switch (pOrder.OrderStatus)
                    {
                        case TThostFtdcOrderStatusType.AllTraded:
                            //已经是最后状态，不能用于撤单了
                            _Ref2Action.Remove(strKey);
                            break;
                        case TThostFtdcOrderStatusType.PartTradedQueueing:
                            //只是部分成交，还可以撤单，所以要记录下来
                            _Ref2Action[strKey] = pOrder;
                            break;
                        case TThostFtdcOrderStatusType.PartTradedNotQueueing:
                            //已经是最后状态，不能用于撤单了
                            _Ref2Action.Remove(strKey);
                            break;
                        case TThostFtdcOrderStatusType.NoTradeQueueing:
                            if (0 == _Ref2Action.Count())
                            {
                                EmitAccepted(order);
                            }
                            _Ref2Action[strKey] = pOrder;
                            break;
                        case TThostFtdcOrderStatusType.NoTradeNotQueueing:
                            //已经是最后状态，不能用于撤单了
                            _Ref2Action.Remove(strKey);
                            break;
                        case TThostFtdcOrderStatusType.Canceled:
                            //已经是最后状态，不能用于撤单了
                            _Ref2Action.Remove(strKey);
                            //分析此报单是否结束，如果结束分析整个Order是否结束
                            switch (pOrder.OrderSubmitStatus)
                            {
                                case TThostFtdcOrderSubmitStatusType.InsertRejected:
                                    //如果是最后一个的状态，同意发出消息
                                    if (0 == _Ref2Action.Count())
                                        EmitRejected(order, pOrder.StatusMsg);
                                    else
                                        Cancel(order);
                                    break;
                                default:
                                    //如果是最后一个的状态，同意发出消息
                                    if (0 == _Ref2Action.Count())
                                        EmitCancelled(order);
                                    else
                                        Cancel(order);
                                    break;
                            }
                            break;
                        case TThostFtdcOrderStatusType.Unknown:
                            switch (pOrder.OrderSubmitStatus)
                            {
                                case TThostFtdcOrderSubmitStatusType.InsertSubmitted:
                                    //新单，新加入记录以便撤单
                                    if (0 == _Ref2Action.Count())
                                    {
                                        EmitAccepted(order);
                                    }
                                    _Ref2Action[strKey] = pOrder;
                                    break;
                            }
                            break;
                        case TThostFtdcOrderStatusType.NotTouched:
                            //没有处理
                            break;
                        case TThostFtdcOrderStatusType.Touched:
                            //没有处理
                            break;
                    }

                    //已经是最后状态了，可以去除了
                    if (0 == _Ref2Action.Count())
                    {
                        _Orders4Cancel.Remove(order);
                    }
                }
            }
            else
            {
                //由第三方软件发出或上次登录时的剩余的单子在这次成交了，先不处理，当不存在
            }
        }
        #endregion

        #region 成交回报

        //用于计算组合成交
        private readonly Dictionary<SingleOrder, DbInMemTrade> _Orders4Combination = new Dictionary<SingleOrder, DbInMemTrade>();

        private void OnRtnTrade(IntPtr pTraderApi, ref CThostFtdcTradeField pTrade)
        {
            tdlog.Info("时{0},合约{1},方向{2},开平{3},价{4},量{5},引用{6},成交编号{7}",
                    pTrade.TradeTime, pTrade.InstrumentID, pTrade.Direction, pTrade.OffsetFlag,
                    pTrade.Price, pTrade.Volume, pTrade.OrderRef, pTrade.TradeID);

            //将仓位计算提前，防止在OnPositionOpened中下平仓时与“C|”配合出错
            if (_dbInMemInvestorPosition.UpdateByTrade(pTrade))
            {
            }
            else
            {
                //本地计算更新失败，重查一次
                TraderApi.TD_ReqQryInvestorPosition(m_pTdApi, pTrade.InstrumentID);
            }

            SingleOrder order;
            //找到自己发送的订单，标记成交
            if (_OrderRef2Order.TryGetValue(string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pTrade.OrderRef), out order))
            {
                if (TThostFtdcTradeTypeType.CombinationDerived == pTrade.TradeType)
                {
                    //组合，得特别处理
                    DbInMemTrade _trade;//用此对象维护组合对
                    if (!_Orders4Combination.TryGetValue(order, out _trade))
                    {
                        _trade = new DbInMemTrade();
                        _Orders4Combination[order] = _trade;
                    }

                    double Price = 0;
                    int Volume = 0;
                    //找到成对交易的，得出价差
                    if (_trade.OnTrade(ref order, ref pTrade, ref Price, ref Volume))
                    {
                        EmitFilled(order, Price, Volume);

                        //完成使命了，删除
                        if (_trade.isEmpty())
                        {
                            _Orders4Combination.Remove(order);
                        }
                    }
                }
                else
                {
                    //普通订单，直接通知即可
                    EmitFilled(order, pTrade.Price, pTrade.Volume);
                }
            }
        }
        #endregion

        #region 撤单报错
        private void OnRspOrderAction(IntPtr pTraderApi, ref CThostFtdcInputOrderActionField pInputOrderAction, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            SingleOrder order;
            if (_OrderRef2Order.TryGetValue(string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pInputOrderAction.OrderRef), out order))
            {
                tdlog.Error("CTP回应：{0},价{1},变化量{2},引用{3},{4}",
                        pInputOrderAction.InstrumentID, pInputOrderAction.LimitPrice,
                        pInputOrderAction.VolumeChange, pInputOrderAction.OrderRef,
                        pRspInfo.ErrorMsg);

                order.Text = string.Format("{0}|{1}", order.Text, pRspInfo.ErrorMsg);
                EmitCancelReject(order, order.Text);
            }
        }

        private void OnErrRtnOrderAction(IntPtr pTraderApi, ref CThostFtdcOrderActionField pOrderAction, ref CThostFtdcRspInfoField pRspInfo)
        {
            SingleOrder order;
            if (_OrderRef2Order.TryGetValue(string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pOrderAction.OrderRef), out order))
            {
                tdlog.Error("交易所回应：{0},价{1},变化量{2},引用{3},{4}",
                        pOrderAction.InstrumentID, pOrderAction.LimitPrice, pOrderAction.VolumeChange, pOrderAction.OrderRef,
                        pRspInfo.ErrorMsg);

                order.Text = string.Format("{0}|{1}", order.Text, pRspInfo.ErrorMsg);
                EmitCancelReject(order,order.Text);
            }
        }
        #endregion

        #region 下单报错
        private void OnRspOrderInsert(IntPtr pTraderApi, ref CThostFtdcInputOrderField pInputOrder, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            SingleOrder order;
            string strKey = string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pInputOrder.OrderRef);
            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                tdlog.Error("CTP回应：{0},{1},开平{2},价{3},原量{4},引用{5},{6}",
                        pInputOrder.InstrumentID, pInputOrder.Direction, pInputOrder.CombOffsetFlag, pInputOrder.LimitPrice,
                        pInputOrder.VolumeTotalOriginal,
                        pInputOrder.OrderRef, pRspInfo.ErrorMsg);

                order.Text = string.Format("{0}|{1}", order.Text, pRspInfo.ErrorMsg);
                EmitRejected(order, order.Text);
                //这些地方没法处理混合报单
                //没得办法，这样全撤了状态就唯一了
                //但由于不知道在错单时是否会有报单回报，所以在这查一次，以防重复撤单出错
                //找到对应的报单回应
                Dictionary<string, CThostFtdcOrderField> _Ref2Action;
                if (_Orders4Cancel.TryGetValue(order, out _Ref2Action))
                {
                    lock (_Ref2Action)
                    {
                        _Ref2Action.Remove(strKey);
                        if (0 == _Ref2Action.Count())
                        {
                            _Orders4Cancel.Remove(order);
                            return;
                        }
                        Cancel(order);
                    }
                }
            }
        }

        private void OnErrRtnOrderInsert(IntPtr pTraderApi, ref CThostFtdcInputOrderField pInputOrder, ref CThostFtdcRspInfoField pRspInfo)
        {
            SingleOrder order;
            string strKey = string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pInputOrder.OrderRef);
            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                tdlog.Error("交易所回应：{0},{1},开平{2},价{3},原量{4},引用{5},{6}",
                        pInputOrder.InstrumentID, pInputOrder.Direction, pInputOrder.CombOffsetFlag, pInputOrder.LimitPrice,
                        pInputOrder.VolumeTotalOriginal,
                        pInputOrder.OrderRef, pRspInfo.ErrorMsg);

                order.Text = string.Format("{0}|{1}", order.Text, pRspInfo.ErrorMsg);
                EmitRejected(order, order.Text);
                //没得办法，这样全撤了状态就唯一了
                Dictionary<string, CThostFtdcOrderField> _Ref2Action;
                if (_Orders4Cancel.TryGetValue(order, out _Ref2Action))
                {
                    lock (_Ref2Action)
                    {
                        _Ref2Action.Remove(strKey);
                        if (0 == _Ref2Action.Count())
                        {
                            _Orders4Cancel.Remove(order);
                            return;
                        }
                        Cancel(order);
                    }
                }
            }
        }
        #endregion

        #region 合约列表
        private void OnRspQryInstrument(IntPtr pTraderApi, ref CThostFtdcInstrumentField pInstrument, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            if (0 == pRspInfo.ErrorID)
            {
                _dictInstruments[pInstrument.InstrumentID] = pInstrument;
                if (bIsLast)
                {
                    tdlog.Info("合约列表已经接收完成,共{0}条",_dictInstruments.Count);
                }
            }
            else
            {
                tdlog.Error("nRequestID:{0},ErrorID:{1},OnRspQryInstrument:{2}", nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitError(nRequestID, pRspInfo.ErrorID, "OnRspQryInstrument:" + pRspInfo.ErrorMsg);
            }
        }
        #endregion

        #region 手续费列表
        private void OnRspQryInstrumentCommissionRate(IntPtr pTraderApi, ref CThostFtdcInstrumentCommissionRateField pInstrumentCommissionRate, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            if (0 == pRspInfo.ErrorID)
            {
                _dictCommissionRate[pInstrumentCommissionRate.InstrumentID] = pInstrumentCommissionRate;
                tdlog.Info("已经接收手续费率 {0}", pInstrumentCommissionRate.InstrumentID);

                //通知单例
                CTPAPI.GetInstance().FireOnRspQryInstrumentCommissionRate(pInstrumentCommissionRate);
            }
            else
            {
                tdlog.Error("nRequestID:{0},ErrorID:{1},OnRspQryInstrumentCommissionRate:{2}", nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitError(nRequestID, pRspInfo.ErrorID, "OnRspQryInstrumentCommissionRate:" + pRspInfo.ErrorMsg);
            }
        }
        #endregion

        #region 保证金率列表
        private void OnRspQryInstrumentMarginRate(IntPtr pTraderApi, ref CThostFtdcInstrumentMarginRateField pInstrumentMarginRate, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            if (0 == pRspInfo.ErrorID)
            {
                _dictMarginRate[pInstrumentMarginRate.InstrumentID] = pInstrumentMarginRate;
                tdlog.Info("已经接收保证金率 {0}", pInstrumentMarginRate.InstrumentID);

                //通知单例
                CTPAPI.GetInstance().FireOnRspQryInstrumentMarginRate(pInstrumentMarginRate);
            }
            else
            {
                tdlog.Error("nRequestID:{0},ErrorID:{1},OnRspQryInstrumentMarginRate:{2}", nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitError(nRequestID, pRspInfo.ErrorID, "OnRspQryInstrumentMarginRate:" + pRspInfo.ErrorMsg);
            }
        }
        #endregion

        #region 持仓回报
        private void OnRspQryInvestorPosition(IntPtr pTraderApi, ref CThostFtdcInvestorPositionField pInvestorPosition, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            if (0 == pRspInfo.ErrorID)
            {
                _dbInMemInvestorPosition.InsertOrReplace(
                    pInvestorPosition.InstrumentID,
                    pInvestorPosition.PosiDirection,
                    pInvestorPosition.HedgeFlag,
                    pInvestorPosition.PositionDate,
                    pInvestorPosition.Position);
                timerPonstion.Enabled = false;
                timerPonstion.Enabled = true;
            }
            else
            {
                tdlog.Error("nRequestID:{0},ErrorID:{1},OnRspQryInvestorPosition:{2}", nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitError(nRequestID, pRspInfo.ErrorID, "OnRspQryInvestorPosition:" + pRspInfo.ErrorMsg);
            }
        }
        #endregion

        #region 资金回报
        CThostFtdcTradingAccountField m_TradingAccount;
        private void OnRspQryTradingAccount(IntPtr pTraderApi, ref CThostFtdcTradingAccountField pTradingAccount, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLastt)
        {
            if (0 == pRspInfo.ErrorID)
            {
                m_TradingAccount = pTradingAccount;
                //有资金信息过来了，重新计时
                timerAccount.Enabled = false;
                timerAccount.Enabled = true;
            }
            else
            {
                tdlog.Error("nRequestID:{0},ErrorID:{1},OnRspQryTradingAccount:{2}", nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitError(nRequestID, pRspInfo.ErrorID, "OnRspQryTradingAccount:" + pRspInfo.ErrorMsg);
            }
        }
        #endregion

        #region 错误回调
        private void OnRspError(IntPtr pApi, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            tdlog.Error("nRequestID:{0},ErrorID:{1},OnRspError:{2}", nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
            EmitError(nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
        }
        #endregion
    }
}
