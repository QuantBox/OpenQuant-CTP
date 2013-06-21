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
        private fnOnRspQryInstrumentMarginRate _fnOnRspQryInstrumentMarginRate_Holder;
        private fnOnRspQryInvestorPosition _fnOnRspQryInvestorPosition_Holder;
        private fnOnRspQryTradingAccount _fnOnRspQryTradingAccount_Holder;
        private fnOnRtnDepthMarketData _fnOnRtnDepthMarketData_Holder;
        private fnOnRtnInstrumentStatus _fnOnRtnInstrumentStatus_Holder;
        private fnOnRtnOrder _fnOnRtnOrder_Holder;
        private fnOnRtnTrade _fnOnRtnTrade_Holder;

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
            _fnOnRspQryInstrumentMarginRate_Holder = OnRspQryInstrumentMarginRate;
            _fnOnRspQryInvestorPosition_Holder = OnRspQryInvestorPosition;
            _fnOnRspQryTradingAccount_Holder = OnRspQryTradingAccount;
            _fnOnRtnInstrumentStatus_Holder = OnRtnInstrumentStatus;
            _fnOnRtnDepthMarketData_Holder = OnRtnDepthMarketData;
            _fnOnRtnOrder_Holder = OnRtnOrder;
            _fnOnRtnTrade_Holder = OnRtnTrade;
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

        //记录界面生成的报单，用于定位收到回报消息时所确定的报单,可以多个Ref对应一个Order
        private readonly Dictionary<string, SingleOrder> _OrderRef2Order = new Dictionary<string, SingleOrder>();
        //一个Order可能分拆成多个报单，如可能由平今与平昨，或开新单组合而成
        private readonly Dictionary<SingleOrder, Dictionary<string, CThostFtdcOrderField>> _Orders4Cancel = new Dictionary<SingleOrder, Dictionary<string, CThostFtdcOrderField>>();
        //交易所信息映射到本地信息
        private readonly Dictionary<string, string> _OrderSysID2OrderRef = new Dictionary<string, string>();

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
                _dictInstruments[pInstrument.InstrumentID] = pInstrument;
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
                foreach(var order in _Orders4Cancel.Keys)
                {
                    string altSymbol = order.Instrument.GetSymbol(Name);
                    string altExchange = order.Instrument.GetSecurityExchange(Name);

                    CThostFtdcInstrumentField _Instrument;
                    if (_dictInstruments.TryGetValue(altSymbol, out _Instrument))
                    {
                        altExchange = _Instrument.ExchangeID;
                    }

                    if (altExchange == pInstrumentStatus.ExchangeID)
                    {
                        EmitExpired(order);
                        // 不知道这个地方会不会出问题
                        _Orders4Cancel.Remove(order);
                    }
                }
            }
        }
        #endregion
    }
}
