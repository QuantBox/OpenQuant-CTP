using System;
using System.ComponentModel;
using QuantBox.CSharp2CTP;
using SmartQuant.Data;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;

namespace QuantBox.OQ.CTP
{
    public partial class CTPProvider:IMarketDataProvider
    {
        private void OnNewBar(object sender, BarEventArgs args)
        {
            if (NewBar != null)
            {
                CThostFtdcDepthMarketDataField DepthMarket;
                Instrument inst = InstrumentManager.Instruments[args.Instrument.Symbol];
                string altSymbol = inst.GetSymbol(Name);

                Bar bar = args.Bar;
                if (_dictDepthMarketData.TryGetValue(altSymbol, out DepthMarket))
                {
                    bar = new Bar(args.Bar);
                    bar.OpenInt = (long)DepthMarket.OpenInterest;
                }
                
                if (null != MarketDataFilter)
                {
                    Bar b = MarketDataFilter.FilterBar(bar, args.Instrument.Symbol);
                    if (null != b)
                    {
                        NewBar(this, new BarEventArgs(b, args.Instrument, this));
                    }
                }
                else
                {
                    NewBar(this, new BarEventArgs(bar, args.Instrument, this));
                }
            }
        }

        private void OnNewBarOpen(object sender, BarEventArgs args)
        {
            if (NewBarOpen != null)
            {
                CThostFtdcDepthMarketDataField DepthMarket;
                Instrument inst = InstrumentManager.Instruments[args.Instrument.Symbol];
                string altSymbol = inst.GetSymbol(Name);

                Bar bar = args.Bar;
                if (_dictDepthMarketData.TryGetValue(altSymbol, out DepthMarket))
                {
                    bar = new Bar(args.Bar);
                    bar.OpenInt = (long)DepthMarket.OpenInterest;
                }

                if (null != MarketDataFilter)
                {
                    Bar b = MarketDataFilter.FilterBarOpen(bar, args.Instrument.Symbol);
                    if (null != b)
                    {
                        NewBarOpen(this, new BarEventArgs(b, args.Instrument, this));
                    }
                }
                else
                {
                    NewBarOpen(this, new BarEventArgs(bar, args.Instrument, this));
                }
            }
        }

        private void EmitNewQuoteEvent(IFIXInstrument instrument, Quote quote)
        {
            if (this.MarketDataFilter != null)
            {
                quote = this.MarketDataFilter.FilterQuote(quote, instrument.Symbol);
            }

            if (quote != null)
            {
                if (NewQuote != null)
                {
                    NewQuote(this, new QuoteEventArgs(quote, instrument, this));
                }
                if (factory != null)
                {
                    factory.OnNewQuote(instrument, quote);
                }
            }
        }

        private void EmitNewTradeEvent(IFIXInstrument instrument, Trade trade)
        {
            if (this.MarketDataFilter != null)
            {
                trade = this.MarketDataFilter.FilterTrade(trade, instrument.Symbol);
            }

            if (trade != null)
            {
                if (NewTrade != null)
                {
                    NewTrade(this, new TradeEventArgs(trade, instrument, this));
                }
                if (factory != null)
                {
                    factory.OnNewTrade(instrument, trade);
                }
            }
        }

        #region OpenQuant3接口的新方法
        public IMarketDataFilter MarketDataFilter { get; set; }
        #endregion
    }
}
