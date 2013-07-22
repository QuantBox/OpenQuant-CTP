using System;

using SmartQuant.Data;

#if CTP
using QuantBox.CSharp2CTP;

namespace QuantBox.Helper.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;

namespace QuantBox.Helper.CTPZQ
#endif
{
    public class CTPQuote:Quote
    {
        public CTPQuote():base()
        {
        }

        public CTPQuote(Quote quote): base(quote)
        {
        }

        public CTPQuote(DateTime datetime, double bid, int bidSize, double ask, int askSize)
            : base(datetime, bid, bidSize, ask, askSize)
        {
        }

        public CThostFtdcDepthMarketDataField DepthMarketData;
    }
}
