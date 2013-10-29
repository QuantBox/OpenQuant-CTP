using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using SmartQuant;
using SmartQuant.Data;
using SmartQuant.Execution;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;
using QuantBox.OQ.CTP;

using QuantBox.OQ.Extensions.Combiner;
using QuantBox.OQ.Extensions.OrderItem;

#if CTP
using QuantBox.CSharp2CTP;
using QuantBox.Helper.CTP;

namespace QuantBox.OQ.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;
using QuantBox.Helper.CTPZQ;

namespace QuantBox.OQ.CTPZQ
#endif
{
    partial class APIProvider
    {
        private fnOnConnect _fnOnConnect_Holder;
        private fnOnDisconnect _fnOnDisconnect_Holder;
        private fnOnErrRtnOrderAction _fnOnErrRtnOrderAction_Holder;
        private fnOnErrRtnOrderInsert _fnOnErrRtnOrderInsert_Holder;
        private fnOnRspError _fnOnRspError_Holder;
        private fnOnRspOrderAction _fnOnRspOrderAction_Holder;
        private fnOnRspOrderInsert _fnOnRspOrderInsert_Holder;
        private fnOnRspQryDepthMarketData _fnOnRspQryDepthMarketData_Holder;
        private fnOnRspQryInstrument _fnOnRspQryInstrument_Holder;
        private fnOnRspQryInstrumentCommissionRate _fnOnRspQryInstrumentCommissionRate_Holder;
        private fnOnRspQryInvestorPosition _fnOnRspQryInvestorPosition_Holder;
        private fnOnRspQryTradingAccount _fnOnRspQryTradingAccount_Holder;
        private fnOnRtnDepthMarketData _fnOnRtnDepthMarketData_Holder;
        private fnOnRtnInstrumentStatus _fnOnRtnInstrumentStatus_Holder;
        private fnOnRtnOrder _fnOnRtnOrder_Holder;
        private fnOnRtnTrade _fnOnRtnTrade_Holder;

#if CTP
        private fnOnRspQryInstrumentMarginRate _fnOnRspQryInstrumentMarginRate_Holder;
#endif

        #region 回调
        private void InitCallbacks()
        {
            //由于回调函数可能被GC回收，所以用成员变量将回调函数保存下来
            _fnOnConnect_Holder = OnConnect;
            _fnOnDisconnect_Holder = OnDisconnect;
            _fnOnErrRtnOrderAction_Holder = OnErrRtnOrderAction;
            _fnOnErrRtnOrderInsert_Holder = OnErrRtnOrderInsert;
            _fnOnRspError_Holder = OnRspError;
            _fnOnRspOrderAction_Holder = OnRspOrderAction;
            _fnOnRspOrderInsert_Holder = OnRspOrderInsert;
            _fnOnRspQryDepthMarketData_Holder = OnRspQryDepthMarketData;
            _fnOnRspQryInstrument_Holder = OnRspQryInstrument;
            _fnOnRspQryInstrumentCommissionRate_Holder = OnRspQryInstrumentCommissionRate;
            _fnOnRspQryInvestorPosition_Holder = OnRspQryInvestorPosition;
            _fnOnRspQryTradingAccount_Holder = OnRspQryTradingAccount;
            _fnOnRtnInstrumentStatus_Holder = OnRtnInstrumentStatus;
            _fnOnRtnDepthMarketData_Holder = OnRtnDepthMarketData;
            _fnOnRtnOrder_Holder = OnRtnOrder;
            _fnOnRtnTrade_Holder = OnRtnTrade;

#if CTP
            _fnOnRspQryInstrumentMarginRate_Holder = OnRspQryInstrumentMarginRate;
#endif
        }
        #endregion

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

        // 报单信息维护
        private readonly OrderMap orderMap = new OrderMap();

        //记录账号的实际持仓，保证以最低成本选择开平
        private readonly Dictionary<string, CThostFtdcInvestorPositionField> _dictPositions = new Dictionary<string, CThostFtdcInvestorPositionField>();
        //记录合约实际行情，用于向界面通知行情用，这里应当记录AltSymbol
        private readonly Dictionary<string, CThostFtdcDepthMarketDataField> _dictDepthMarketData = new Dictionary<string, CThostFtdcDepthMarketDataField>();
        //记录合约列表,从实盘合约名到对象的映射
        private readonly Dictionary<string, CThostFtdcInstrumentField> _dictInstruments = new Dictionary<string, CThostFtdcInstrumentField>();
        private Dictionary<string, string> _dictInstruments2 = new Dictionary<string, string>();
        //记录手续费率,从实盘合约名到对象的映射
        private readonly Dictionary<string, CThostFtdcInstrumentCommissionRateField> _dictCommissionRate = new Dictionary<string, CThostFtdcInstrumentCommissionRateField>();
        //记录保证金率,从实盘合约名到对象的映射
        private readonly Dictionary<string, CThostFtdcInstrumentMarginRateField> _dictMarginRate = new Dictionary<string, CThostFtdcInstrumentMarginRateField>();
        //记录
        private readonly Dictionary<string, DataRecord> _dictAltSymbol2Instrument = new Dictionary<string, DataRecord>();

        //用于行情的时间，只在登录时改动，所以要求开盘时能得到更新
        private int _yyyy;
        private int _MM;
        private int _dd;

        private ServerItem server;
        private AccountItem account;

        #region 合约列表
        private void OnRspQryInstrument(IntPtr pTraderApi, ref CThostFtdcInstrumentField pInstrument, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            if (0 == pRspInfo.ErrorID)
            {
#if CTP
                _dictInstruments[pInstrument.InstrumentID] = pInstrument;
#else
                //比较无语，测试平台上会显示很多无效数据，有关期货的还会把正确的数据给覆盖，所以临时这样处理
                if (pInstrument.ProductClass != TThostFtdcProductClassType.Futures)
                {
                    string symbol = GetYahooSymbol(pInstrument.InstrumentID, pInstrument.ExchangeID);
                    _dictInstruments[symbol] = pInstrument;

                    // 行情中可能没有交易所信息，这个容器用于容错处理
                    _dictInstruments2[pInstrument.InstrumentID] = symbol;
                }
#endif

                if (bIsLast)
                {
                    tdlog.Info("合约列表已经接收完成,共{0}条", _dictInstruments.Count);
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
#if CTP
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
#endif
        #endregion

        #region 持仓回报
        private string GetPositionKey(string InstrumentID,
            TThostFtdcPosiDirectionType PosiDirection,
            TThostFtdcHedgeFlagType HedgeFlag,
            TThostFtdcPositionDateType PositionDate)
        {
            return string.Format("{0}:{1}:{2}:{3}", InstrumentID, PosiDirection, HedgeFlag, PositionDate);
        }

        private string GetPositionKey(CThostFtdcInvestorPositionField p)
        {
            return GetPositionKey(p.InstrumentID, p.PosiDirection, p.HedgeFlag, p.PositionDate);
        }

        private void OnRspQryInvestorPosition(IntPtr pTraderApi, ref CThostFtdcInvestorPositionField pInvestorPosition, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            if (0 == pRspInfo.ErrorID)
            {
                string key = GetPositionKey(pInvestorPosition);
                _dictPositions[key] = pInvestorPosition;
                CTPAPI.GetInstance().FireOnRspReqQryInvestorPosition(pInvestorPosition);

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

                //通知单例,还是使用GetBrokerInfo来取呢？
                CTPAPI.GetInstance().__RegTradingAccount(m_TradingAccount);
                CTPAPI.GetInstance().FireOnRspQryTradingAccount(pTradingAccount);
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

        #region 交易所状态
        private void OnRtnInstrumentStatus(IntPtr pTraderApi, ref CThostFtdcInstrumentStatusField pInstrumentStatus)
        {
            tdlog.Info("{0},{1},{2},{3},{4},{5},{6},{7}",
                pInstrumentStatus.ExchangeID, pInstrumentStatus.InstrumentID,
                pInstrumentStatus.InstrumentStatus, pInstrumentStatus.EnterReason,
                pInstrumentStatus.EnterTime,pInstrumentStatus.TradingSegmentSN,
                pInstrumentStatus.ExchangeInstID,pInstrumentStatus.SettlementGroupID);

            //通知单例
            CTPAPI.GetInstance().FireOnRtnInstrumentStatus(pInstrumentStatus);

            // 到IF的交割日，是否会收到两个有关IF的记录？如果在此进行清理是否会有问题？
            // 只会收到一条
            // 遍历是否过期
            if (pInstrumentStatus.InstrumentStatus == TThostFtdcInstrumentStatusType.Closed)
            {
                Dictionary<GenericOrderItem, CThostFtdcOrderField> tmp = new Dictionary<GenericOrderItem, CThostFtdcOrderField>();
                foreach (var pair in orderMap.OrderItem_OrderField)
                {
                    if(pair.Value.ExchangeID == pInstrumentStatus.ExchangeID)
                    {
                        int cnt = pair.Key.GetLegNum();
                        foreach(var pair2 in orderMap.Order_OrderItem)
                        {
                            // 得找到OpenQuant层的单子
                            if(pair.Key == pair2.Value)
                            {
                                --cnt;
                                EmitExpired(pair2.Key);
                                if (cnt <= 0)
                                    break;
                            }
                        }
                        tmp[pair.Key] = pair.Value;
                    }
                }

                foreach (var pair in tmp)
                {
                    OnLastStatus(pair.Key, pair.Value.OrderSysID, pair.Value.OrderRef);
                }
                tmp.Clear();
            }
        }
        #endregion
    }
}
