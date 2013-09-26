using System;
using System.ComponentModel;

using SmartQuant.Data;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;

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
    public partial class APIProvider :ISimulationMarketDataProvider
    {
        #region OpenQuant3接口的新方法
        public void EmitQuote(IFIXInstrument instrument, Quote quote)
        {
            EmitNewQuoteEvent(instrument, quote);
        }

        public void EmitTrade(IFIXInstrument instrument, Trade trade)
        {
            EmitNewTradeEvent(instrument, trade);
        }
        #endregion
    }
}
