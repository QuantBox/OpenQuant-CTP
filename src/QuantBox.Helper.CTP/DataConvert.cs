using System.Reflection;

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

        public static bool TryConvert(SmartQuant.Data.Trade trade, ref CThostFtdcDepthMarketDataField DepthMarketData)
        {
            if (tradeField == null)
            {
                tradeField = typeof(SmartQuant.Data.Trade).GetField("trade", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            CTPTrade t = tradeField.GetValue(trade) as CTPTrade;
            if (null != t)
            {
                DepthMarketData = t.DepthMarketData;
                return true;
            }
            return false;
        }

        public static bool TryConvert(SmartQuant.Data.Quote quote, ref CThostFtdcDepthMarketDataField DepthMarketData)
        {
            if (quoteField == null)
            {
                quoteField = typeof(SmartQuant.Data.Quote).GetField("quote", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            CTPQuote q = quoteField.GetValue(quote) as CTPQuote;
            if (null != q)
            {
                DepthMarketData = q.DepthMarketData;
                return true;
            }
            return false;
        }
    }
}
