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
    public class CTPTrade:Trade
    {
        public CTPTrade():base()
        {
        }

        public CTPTrade(Trade trade):base(trade)
        {
        }

        public CTPTrade(DateTime datetime, double price, int size)
            : base(datetime, price, size)
        {
        }

        public CThostFtdcDepthMarketDataField DepthMarketData;
    }
}
