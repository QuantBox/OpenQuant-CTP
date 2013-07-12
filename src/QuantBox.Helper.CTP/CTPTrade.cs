using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SmartQuant.Data;

using QuantBox.CSharp2CTP;
using System.Reflection;

namespace QuantBox.Helper.CTP
{
    public class CTPTrade:Trade
    {
        public CTPTrade():base()
        {
        }

        public CTPTrade(Trade trade):base(trade)
        {
        }

        public CTPTrade(DateTime datetime, double price, int size):base(datetime, price, size)
        {
        }

        public CThostFtdcDepthMarketDataField DepthMarketData;
    }

    public class TradeConvert
    {
        static FieldInfo field;

        public static bool TryConvert(OpenQuant.API.Trade trade, out CThostFtdcDepthMarketDataField DepthMarketData)
        {
            if(field == null)
            {
                field = typeof(OpenQuant.API.Trade).GetField("trade", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            CTPTrade t = field.GetValue(trade) as CTPTrade;
            if (null != t)
            {
                DepthMarketData = t.DepthMarketData;
                return true;
            }
            DepthMarketData = new CThostFtdcDepthMarketDataField();
            return false;
        }
    }
}
