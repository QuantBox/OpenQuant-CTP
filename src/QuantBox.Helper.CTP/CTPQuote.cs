using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SmartQuant.Data;

using QuantBox.CSharp2CTP;
using System.Reflection;

namespace QuantBox.Helper.CTP
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
