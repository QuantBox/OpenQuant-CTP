using System.Reflection;

#if OQ
using OpenQuant.API;
#elif QD
using SmartQuant.Data;
#endif

#if CTP
using QuantBox.CSharp2CTP;

namespace QuantBox.Helper.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;

namespace QuantBox.Helper.CTPZQ
#endif
{
    public class DataConvert
    {
        static FieldInfo tradeField;
        static FieldInfo quoteField;

        public static bool TryConvert(Trade trade, ref CThostFtdcDepthMarketDataField DepthMarketData)
        {
#if OQ
            if (tradeField == null)
            {
                tradeField = typeof(Trade).GetField("trade", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            CTPTrade t = tradeField.GetValue(trade) as CTPTrade;
#elif QD
            CTPTrade t = trade as CTPTrade;
#endif
            if (null != t)
            {
                DepthMarketData = t.DepthMarketData;
                return true;
            }

            
            if (null != t)
            {
                DepthMarketData = t.DepthMarketData;
                return true;
            }

            return false;
        }

        public static bool TryConvert(Quote quote, ref CThostFtdcDepthMarketDataField DepthMarketData)
        {
#if OQ
            if (quoteField == null)
            {
                quoteField = typeof(Quote).GetField("quote", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            CTPQuote q = quoteField.GetValue(quote) as CTPQuote;
#elif QD
            CTPQuote q = quote as CTPQuote;
#endif
            if (null != q)
            {
                DepthMarketData = q.DepthMarketData;
                return true;
            }
            return false;
        }
    }
}
