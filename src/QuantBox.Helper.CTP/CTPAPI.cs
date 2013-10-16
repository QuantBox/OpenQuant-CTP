using System;
using System.Collections.Generic;

#if CTP
using QuantBox.CSharp2CTP;

namespace QuantBox.Helper.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;

namespace QuantBox.Helper.CTPZQ
#endif
{
    public sealed class CTPAPI
    {
        private static readonly CTPAPI instance = new CTPAPI();
        private CTPAPI()
        {
        }
        public static CTPAPI GetInstance()
        {
            return instance;
        }


        private IntPtr m_pMdApi = IntPtr.Zero;      //行情对象指针
        private IntPtr m_pTdApi = IntPtr.Zero;      //交易对象指针

        public void __RegTdApi(IntPtr pTdApi)
        {
            m_pTdApi = pTdApi;
        }

        public void __RegMdApi(IntPtr pMdApi)
        {
            m_pMdApi = pMdApi;
        }

        #region 合列列表
        public Dictionary<string, CThostFtdcInstrumentField> Instruments { get; private set; }
        public void __RegInstrumentDictionary(Dictionary<string, CThostFtdcInstrumentField> dict)
        {
            Instruments = dict;
        }

        public delegate void RspQryInstrument(CThostFtdcInstrumentField pInstrument);
        public event RspQryInstrument OnRspQryInstrument;
        public void FireOnRspQryInstrument(CThostFtdcInstrumentField pInstrument)
        {
            if (null != OnRspQryInstrument)
            {
                OnRspQryInstrument(pInstrument);
            }
        }

        public void ReqQryInstrument(string instrument)
        {
            if (null != Instruments)
            {
                CThostFtdcInstrumentField value;
                if (Instruments.TryGetValue(instrument, out value))
                {
                    FireOnRspQryInstrument(value);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(instrument)
                    && null != m_pTdApi
                    && IntPtr.Zero != m_pTdApi)
            {
                TraderApi.TD_ReqQryInstrument(m_pTdApi, instrument);
            }
        }
        #endregion

        #region 保证金率
#if CTP
        public Dictionary<string, CThostFtdcInstrumentMarginRateField> MarginRates { get; private set; }
        public void __RegInstrumentMarginRateDictionary(Dictionary<string, CThostFtdcInstrumentMarginRateField> dict)
        {
            MarginRates = dict;
        }
        public void ReqQryInstrumentMarginRate(string instrument, TThostFtdcHedgeFlagType HedgeFlag)
        {
            if (null != MarginRates)
            {
                CThostFtdcInstrumentMarginRateField value;
                if (MarginRates.TryGetValue(instrument, out value))
                {
                    FireOnRspQryInstrumentMarginRate(value);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(instrument)
                && null != m_pTdApi
                && IntPtr.Zero != m_pTdApi)
            {
                TraderApi.TD_ReqQryInstrumentMarginRate(m_pTdApi, instrument, HedgeFlag);
            }
        }        

        public delegate void RspQryInstrumentMarginRate(CThostFtdcInstrumentMarginRateField pInstrumentMarginRate);
        public event RspQryInstrumentMarginRate OnRspQryInstrumentMarginRate;
        public void FireOnRspQryInstrumentMarginRate(CThostFtdcInstrumentMarginRateField pInstrumentMarginRate)
        {
            if (null != OnRspQryInstrumentMarginRate)
            {
                OnRspQryInstrumentMarginRate(pInstrumentMarginRate);
            }
        }
#endif
        #endregion

        #region 手续费率
        public Dictionary<string, CThostFtdcInstrumentCommissionRateField> CommissionRates { get; private set; }
        public void __RegInstrumentCommissionRateDictionary(Dictionary<string, CThostFtdcInstrumentCommissionRateField> dict)
        {
            CommissionRates = dict;
        }

        public void ReqQryInstrumentCommissionRate(string instrument)
        {
            if (null != CommissionRates)
            {
                CThostFtdcInstrumentCommissionRateField value;
                if (CommissionRates.TryGetValue(instrument, out value))
                {
                    FireOnRspQryInstrumentCommissionRate(value);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(instrument)
                && null != m_pTdApi
                && IntPtr.Zero != m_pTdApi)
            {
                TraderApi.TD_ReqQryInstrumentCommissionRate(m_pTdApi, instrument);
            }
        }

        public delegate void RspQryInstrumentCommissionRate(CThostFtdcInstrumentCommissionRateField pInstrumentCommissionRate);
        public event RspQryInstrumentCommissionRate OnRspQryInstrumentCommissionRate;
        public void FireOnRspQryInstrumentCommissionRate(CThostFtdcInstrumentCommissionRateField pInstrumentCommissionRate)
        {
            if (null != OnRspQryInstrumentCommissionRate)
            {
                OnRspQryInstrumentCommissionRate(pInstrumentCommissionRate);
            }
        }
        #endregion

        #region 深度行情1
        public Dictionary<string, CThostFtdcDepthMarketDataField> DepthMarketDatas { get; private set; }
        public void __RegDepthMarketDataDictionary(Dictionary<string, CThostFtdcDepthMarketDataField> dict)
        {
            DepthMarketDatas = dict;
        }

        public void ReqQryDepthMarketData(string instrument)
        {
            if (null != DepthMarketDatas)
            {
                CThostFtdcDepthMarketDataField value;
                if (DepthMarketDatas.TryGetValue(instrument, out value))
                {
                    FireOnRspQryDepthMarketData(value);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(instrument)
                && null != m_pTdApi
                && IntPtr.Zero != m_pTdApi)
            {
                TraderApi.TD_ReqQryDepthMarketData(m_pTdApi, instrument);
            }
        }

        public delegate void RspQryDepthMarketData(CThostFtdcDepthMarketDataField pDepthMarketData);
        public event RspQryDepthMarketData OnRspQryDepthMarketData;
        public void FireOnRspQryDepthMarketData(CThostFtdcDepthMarketDataField pDepthMarketData)
        {
            if (null != OnRspQryDepthMarketData)
            {
                OnRspQryDepthMarketData(pDepthMarketData);
            }
        }
        #endregion

        #region 交易所状态
        public delegate void RtnInstrumentStatus(CThostFtdcInstrumentStatusField pInstrumentStatus);
        public event RtnInstrumentStatus OnRtnInstrumentStatus;
        public void FireOnRtnInstrumentStatus(CThostFtdcInstrumentStatusField pInstrumentStatus)
        {
            if (null != OnRtnInstrumentStatus)
            {
                OnRtnInstrumentStatus(pInstrumentStatus);
            }
        }
        #endregion

        #region 主动请求资金
        public CThostFtdcTradingAccountField TradingAccount { get; private set; }
        public void __RegTradingAccount(CThostFtdcTradingAccountField pTradingAccount)
        {
            TradingAccount = pTradingAccount;
        }

        public void ReqQryTradingAccount()
        {
            if (m_pTdApi == null || m_pTdApi == IntPtr.Zero)
                return;

            TraderApi.TD_ReqQryTradingAccount(m_pTdApi);
        }

        public delegate void RspQryTradingAccount(CThostFtdcTradingAccountField pTradingAccount);
        public event RspQryTradingAccount OnRspQryTradingAccount;
        public void FireOnRspQryTradingAccount(CThostFtdcTradingAccountField pTradingAccount)
        {
            if (null != OnRspQryTradingAccount)
            {
                OnRspQryTradingAccount(pTradingAccount);
            }
        }
        #endregion

        #region OnStrategyStart
        public EventHandler OnLive;
        public void EmitOnLive()
        {
            if (OnLive != null)
                OnLive(null, EventArgs.Empty);
        }
        #endregion


    }
}
