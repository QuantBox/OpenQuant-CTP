using System;
using System.ComponentModel;
using SmartQuant.Data;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;
using QuantBox.OQ.CTP;

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
    public partial class APIProvider:IMarketDataProvider
    {
        private IBarFactory factory;

        public event MarketDataRequestRejectEventHandler MarketDataRequestReject;
        public event MarketDataSnapshotEventHandler MarketDataSnapshot;
        public event BarEventHandler NewBar;
        public event BarEventHandler NewBarOpen;
        public event BarSliceEventHandler NewBarSlice;
        public event CorporateActionEventHandler NewCorporateAction;
        public event FundamentalEventHandler NewFundamental;
        public event BarEventHandler NewMarketBar;
        public event MarketDataEventHandler NewMarketData;
        public event MarketDepthEventHandler NewMarketDepth;
        public event QuoteEventHandler NewQuote;
        public event TradeEventHandler NewTrade;        

        #region IMarketDataProvider
        [Category(CATEGORY_BARFACTORY)]
        public IBarFactory BarFactory
        {
            get
            {
                return factory;
            }
            set
            {
                if (factory != null)
                {
                    factory.NewBar -= OnNewBar;
                    factory.NewBarOpen -= OnNewBarOpen;
                    factory.NewBarSlice -= OnNewBarSlice;
                }
                factory = value;
                if (factory != null)
                {
                    factory.NewBar += OnNewBar;
                    factory.NewBarOpen += OnNewBarOpen;
                    factory.NewBarSlice += OnNewBarSlice;
                }
            }
        }

        private void OnNewBarSlice(object sender, BarSliceEventArgs args)
        {
            if (NewBarSlice != null)
            {
                NewBarSlice(this, new BarSliceEventArgs(args.BarSize, this));
            }
        }

        public void SendMarketDataRequest(FIXMarketDataRequest request)
        {
            if (!_bMdConnected)
            {
                EmitError(-1, -1, "行情服务器没有连接");
                mdlog.Error("行情服务器没有连接");
                return;
            }

            bool bSubscribe = false;
            bool bTrade = false;
            bool bQuote = false;
            bool bMarketDepth = false;
            if (request.NoMDEntryTypes > 0)
            {
                switch (request.GetMDEntryTypesGroup(0).MDEntryType)
                {
                    case FIXMDEntryType.Bid:
                    case FIXMDEntryType.Offer:
                        if (request.MarketDepth != 1)
                        {
                            bMarketDepth = true;
                            break;
                        }
                        bQuote = true;
                        break;
                    case FIXMDEntryType.Trade:
                        bTrade = true;
                        break;
                }
            }
            bSubscribe = (request.SubscriptionRequestType == DataManager.MARKET_DATA_SUBSCRIBE);

            if (bSubscribe)
            {
                for (int i = 0; i < request.NoRelatedSym; ++i)
                {
                    FIXRelatedSymGroup group = request.GetRelatedSymGroup(i);
                    Instrument inst = InstrumentManager.Instruments[group.Symbol];

                    //将用户合约转成交易所合约
                    string altSymbol = inst.GetSymbol(this.Name);
                    string altExchange = inst.GetSecurityExchange(this.Name);
                    string _altSymbol = GetApiSymbol(altSymbol);
                    CThostFtdcInstrumentField _Instrument;
                    if (_dictInstruments.TryGetValue(altSymbol, out _Instrument))
                    {
                        _altSymbol = _Instrument.InstrumentID;
                        altExchange = _Instrument.ExchangeID;
                    }

                    DataRecord record;
                    if (!_dictAltSymbol2Instrument.TryGetValue(altSymbol, out record))
                    {
                        record = new DataRecord();
                        record.Instrument = inst;
                        record.Symbol = _altSymbol;
                        record.Exchange = altExchange;
                        _dictAltSymbol2Instrument[altSymbol] = record;

                        mdlog.Info("订阅合约 {0} {1} {2}", altSymbol, record.Symbol, record.Exchange);

                        if (_bTdConnected)
                        {
                            TraderApi.TD_ReqQryInvestorPosition(m_pTdApi, null);
                            timerPonstion.Enabled = false;
                            timerPonstion.Enabled = true;
                        }
                    }

                    //记录行情,同时对用户合约与交易所合约进行映射
                    CThostFtdcDepthMarketDataField DepthMarket;
                    if (!_dictDepthMarketData.TryGetValue(altSymbol, out DepthMarket))
                    {
                        _dictDepthMarketData[altSymbol] = DepthMarket;
                    }

                    // 多次订阅也无所谓
                    MdApi.MD_Subscribe(m_pMdApi, record.Symbol, record.Exchange);

                    if (bTrade)
                        record.TradeRequested = true;
                    if (bQuote)
                        record.QuoteRequested = true;
                    if (bMarketDepth)
                        record.MarketDepthRequested = true;

                    if (bMarketDepth)
                    {
                        inst.OrderBook.Clear();
                    }
                }
            }
            else
            {
                for (int i = 0; i < request.NoRelatedSym; ++i)
                {
                    FIXRelatedSymGroup group = request.GetRelatedSymGroup(i);
                    Instrument inst = InstrumentManager.Instruments[group.Symbol];

                    //将用户合约转成交易所合约
                    string altSymbol = inst.GetSymbol(this.Name);
                    string altExchange = inst.GetSecurityExchange(this.Name);

                    DataRecord record;
                    if (!_dictAltSymbol2Instrument.TryGetValue(altSymbol, out record))
                    {
                        break;
                    }

                    if (bTrade)
                        record.TradeRequested = false;
                    if (bQuote)
                        record.QuoteRequested = false;
                    if (bMarketDepth)
                        record.MarketDepthRequested = false;

                    if (!record.TradeRequested && !record.QuoteRequested && !record.MarketDepthRequested)
                    {
                        _dictDepthMarketData.Remove(altSymbol);
                        _dictAltSymbol2Instrument.Remove(altSymbol);
                        mdlog.Info("取消合约 {0} {1} {2}", altSymbol, record.Symbol, record.Exchange);
                        MdApi.MD_Unsubscribe(m_pMdApi, record.Symbol, record.Exchange);
                    }
                    else
                    {
                        // 只要有一种类型说要订阅，就给订上
                        MdApi.MD_Subscribe(m_pMdApi, record.Symbol, record.Exchange);
                    }
                }
            }
        }

        private void EmitNewMarketDepth(IFIXInstrument instrument, MarketDepth marketDepth)
        {
            if (NewMarketDepth != null)
            {
                NewMarketDepth(this, new MarketDepthEventArgs(marketDepth, instrument, this));
            }
        }
        #endregion
    }
}
