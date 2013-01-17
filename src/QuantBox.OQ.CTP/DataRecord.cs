using SmartQuant.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuantBox.OQ.CTP
{
    class DataRecord
    {
        public Instrument Instrument;
        public bool TradeRequested;
        public bool QuoteRequested;
        public bool MarketDepthRequested;
    }
}
