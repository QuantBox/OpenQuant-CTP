using System;
using System.ComponentModel;
using SmartQuant.Data;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;

#if CTP
using QuantBox.CSharp2CTP;

namespace QuantBox.OQ.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;

namespace QuantBox.OQ.CTPZQ
#endif
{
    public partial class APIProvider:IMarketDataProvider
    {
        #region QD的Bar事件
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

                NewBar(this, new BarEventArgs(bar, args.Instrument, this));
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

                NewBarOpen(this, new BarEventArgs(bar, args.Instrument, this));
            }
        }

        private void EmitNewQuoteEvent(IFIXInstrument instrument, Quote quote)
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

        private void EmitNewTradeEvent(IFIXInstrument instrument, Trade trade)
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
        #endregion
    }
}
