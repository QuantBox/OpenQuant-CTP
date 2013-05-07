using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuantBox.CSharp2CTP;

namespace QuantBox.Helper.CTP
{
    public interface ISpreadMarketData
    {
        IEnumerable<Tick> CalculateSpread(CThostFtdcDepthMarketDataField pDepthMarketData);
    }
}
