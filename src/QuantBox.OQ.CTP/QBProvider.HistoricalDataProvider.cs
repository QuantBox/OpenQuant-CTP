using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartQuant.Providers;
using SmartQuant.Providers.Design;
using System.ComponentModel;
using SmartQuant.Instruments;

namespace QuantBox.OQ.CTP
{
    public partial class QBProvider : IHistoricalDataProvider
    {
        [TypeConverter(typeof(BarSizesTypeConverter))]
        [Category(CATEGORY_HISTORICAL)]
        public int[] BarSizes
        {
            get { return new int[] { 1, 5, 15, 30, 60, 120, 300, 900, 0x708, 0xe10}; }
        }

        [Category(CATEGORY_HISTORICAL)]
        public HistoricalDataRange DataRange
        {
            get { return HistoricalDataRange.DaysAgo; }
        }

        [Category(CATEGORY_HISTORICAL)]
        public HistoricalDataType DataType
        {
            get { return (HistoricalDataType.Bar | HistoricalDataType.Trade | HistoricalDataType.Quote); }
        }

        [Category(CATEGORY_HISTORICAL)]
        public int MaxConcurrentRequests
        {
            get { return -1; }
        }

        public event HistoricalDataEventHandler HistoricalDataRequestCancelled;

        public event HistoricalDataEventHandler HistoricalDataRequestCompleted;

        public event HistoricalDataErrorEventHandler HistoricalDataRequestError;

        public event HistoricalBarEventHandler NewHistoricalBar;

        public event HistoricalMarketDepthEventHandler NewHistoricalMarketDepth;

        public event HistoricalQuoteEventHandler NewHistoricalQuote;

        public event HistoricalTradeEventHandler NewHistoricalTrade;

        #region IHistoricalDataProvider
        public void SendHistoricalDataRequest(HistoricalDataRequest request)
        {
            Instrument inst = request.Instrument as Instrument;
            string altSymbol = inst.GetSymbol(Name);
            string altExchange = inst.GetSecurityExchange(Name);
        }

        public void CancelHistoricalDataRequest(string requestId)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
