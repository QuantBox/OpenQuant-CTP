using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SmartQuant.Providers;
using SmartQuant.Providers.Design;
using System.ComponentModel;
using SmartQuant.Instruments;
using System.Collections;
using SmartQuant.Data;

namespace QuantBox.OQ.CTP
{
    public partial class HistoryDownloader : IHistoricalDataProvider
    {
        [TypeConverter(typeof(BarSizesTypeConverter))]
        [Category(CATEGORY_HISTORICAL)]
        public int[] BarSizes
        {
            get { return new int[] {1,5,30, 60, 120, 300, 900, 0x708, 0xe10 }; }
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

        private Hashtable historicalDataIds = new Hashtable();
        private Hashtable historicalDataRecords = new Hashtable();

        public event HistoricalDataEventHandler HistoricalDataRequestCancelled;

        public event HistoricalDataEventHandler HistoricalDataRequestCompleted;

        public event HistoricalDataErrorEventHandler HistoricalDataRequestError;

        public event HistoricalBarEventHandler NewHistoricalBar;

        public event HistoricalMarketDepthEventHandler NewHistoricalMarketDepth;

        public event HistoricalQuoteEventHandler NewHistoricalQuote;

        public event HistoricalTradeEventHandler NewHistoricalTrade;

        private void EmitHistoricalDataError(HistoricalDataRequest request, string message)
        {
            if (HistoricalDataRequestError != null)
                HistoricalDataRequestError(this,
                    new HistoricalDataErrorEventArgs(request.RequestId, request.Instrument, this, -1, message));
        }

        private void EmitHistoricalDataCompleted(HistoricalDataRequest request)
        {
            if (HistoricalDataRequestCompleted != null)
                HistoricalDataRequestCompleted(this,
                    new HistoricalDataEventArgs(request.RequestId, request.Instrument, this, -1));
        }

        private void EmitHistoricalDataCancelled(HistoricalDataRequest request)
        {
            if (HistoricalDataRequestCancelled != null)
                HistoricalDataRequestCancelled(this,
                    new HistoricalDataEventArgs(request.RequestId, request.Instrument, this, -1));
        }

        private void EmitNewHistoricalBar(HistoricalDataRequest request, DateTime datetime, double open, double high, double low, double close, long volume, long openInt)
        {
            if (NewHistoricalBar != null)
            {
                Bar bar = new Bar(BarType.Time, request.BarSize, datetime, datetime.AddSeconds(request.BarSize), open, high, low, close, volume, openInt);
                NewHistoricalBar(this,
                    new HistoricalBarEventArgs(bar, request.RequestId, request.Instrument, this, -1));
            }
        }

        private void EmitNewHistoricalTrade(HistoricalDataRequest request, DateTime datetime, double price, int size)
        {
            if (NewHistoricalTrade != null)
            {
                Trade trade = new Trade(datetime, price, size);
                NewHistoricalTrade(this,
                    new HistoricalTradeEventArgs(trade, request.RequestId, request.Instrument, this, -1));
            }
        }

        private void EmitNewHistoricalQuote(HistoricalDataRequest request, DateTime datetime, double bid, int bidSize, double ask, int askSize)
        {
            if (NewHistoricalQuote != null)
            {
                Quote quote = new Quote(datetime, bid, bidSize, ask, askSize);
                NewHistoricalQuote(this,
                    new HistoricalQuoteEventArgs(quote, request.RequestId, request.Instrument, this, -1));
            }
        }

        #region IHistoricalDataProvider
        public void CancelHistoricalDataRequest(string requestId)
        {
            if (historicalDataIds.ContainsKey(requestId))
            {
                HistoricalDataRequest request = historicalDataIds[requestId] as HistoricalDataRequest;
                historicalDataIds.Remove(requestId);
                EmitHistoricalDataCancelled(request);
            }
        }

        public void SendHistoricalDataRequest(HistoricalDataRequest request)
        {
            Instrument inst = request.Instrument as Instrument;
            string altSymbol = inst.GetSymbol(Name);
            string altExchange = inst.GetSecurityExchange(Name);

            historicalDataIds.Add(request.RequestId, request);

            if (true)
            {
                historicalDataIds.Remove(request.RequestId);
                EmitHistoricalDataCompleted(request);
            }
            else
            {
                EmitHistoricalDataError(request, "Error");
            }
        }

        #endregion
    }
}
