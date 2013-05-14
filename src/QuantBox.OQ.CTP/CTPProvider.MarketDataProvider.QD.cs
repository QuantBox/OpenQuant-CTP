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
        #endregion
    }
}
