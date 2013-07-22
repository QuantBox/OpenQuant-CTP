using System;
using SmartQuant;
using SmartQuant.Data;
using SmartQuant.Instruments;
using QuantBox.OQ.CTP;

#if CTP
using QuantBox.CSharp2CTP;
using QuantBox.Helper.CTP;

namespace QuantBox.OQ.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;
using QuantBox.Helper.CTPZQ;

namespace QuantBox.OQ.CTPZQ
#endif
{
    partial class APIProvider
    {
        #region 深度行情回调
        private DateTime _dateTime = DateTime.Now;
        private void OnRtnDepthMarketData(IntPtr pApi, ref CThostFtdcDepthMarketDataField pDepthMarketData)
        {
#if CTP
            string symbol = pDepthMarketData.InstrumentID;
#elif CTPZQ
            string symbol = GetYahooSymbol(pDepthMarketData.InstrumentID, pDepthMarketData.ExchangeID);
#endif
            DataRecord record;
            if (!_dictAltSymbol2Instrument.TryGetValue(symbol, out record))
            {
                mdlog.Warn("合约{0}不在订阅列表中却收到了数据", symbol);
                return;
            }

            Instrument instrument = record.Instrument;

            CThostFtdcDepthMarketDataField DepthMarket;
            _dictDepthMarketData.TryGetValue(symbol, out DepthMarket);

            //将更新字典的功能提前，因为如果一开始就OnTrade中下单，涨跌停没有更新
            _dictDepthMarketData[symbol] = pDepthMarketData;

            if (TimeMode.LocalTime == _TimeMode)
            {
                //为了生成正确的Bar,使用本地时间
                _dateTime = Clock.Now;
            }
            else
            {
                //直接按HH:mm:ss来解析，测试过这种方法目前是效率比较高的方法
                try
                {
                    // 只有使用交易所行情时才需要处理跨天的问题
#if CTP
                    ChangeActionDay(pDepthMarketData.ActionDay);
#else
                    ChangeActionDay(pDepthMarketData.TradingDay);
#endif

                    int HH = int.Parse(pDepthMarketData.UpdateTime.Substring(0, 2));
                    int mm = int.Parse(pDepthMarketData.UpdateTime.Substring(3, 2));
                    int ss = int.Parse(pDepthMarketData.UpdateTime.Substring(6, 2));

                    _dateTime = new DateTime(_yyyy, _MM, _dd, HH, mm, ss, pDepthMarketData.UpdateMillisec);
                }
                catch (Exception)
                {
                    _dateTime = Clock.Now;
                }
            }

            if (record.TradeRequested)
            {
                //通过测试，发现IB的Trade与Quote在行情过来时数量是不同的，在这也做到不同
                if (DepthMarket.LastPrice == pDepthMarketData.LastPrice
                    && DepthMarket.Volume == pDepthMarketData.Volume)
                { }
                else
                {
                    //行情过来时是今天累计成交量，得转换成每个tick中成交量之差
                    int volume = pDepthMarketData.Volume - DepthMarket.Volume;
                    if (0 == DepthMarket.Volume)
                    {
                        //没有接收到最开始的一条，所以这计算每个Bar的数据时肯定超大，强行设置为0
                        volume = 0;
                    }
                    else if (volume < 0)
                    {
                        //如果隔夜运行，会出现今早成交量0-昨收盘成交量，出现负数，所以当发现为负时要修改
                        volume = pDepthMarketData.Volume;
                    }

                    // 使用新的类,保存更多信息
                    CTPTrade trade = new CTPTrade(_dateTime,
                        pDepthMarketData.LastPrice == double.MaxValue ? 0 : pDepthMarketData.LastPrice,
                        volume);

                    // 记录深度数据
                    trade.DepthMarketData = pDepthMarketData;

                    EmitNewTradeEvent(instrument, trade);
                }
            }

            if (record.QuoteRequested)
            {
                //if (
                //DepthMarket.BidVolume1 == pDepthMarketData.BidVolume1
                //&& DepthMarket.AskVolume1 == pDepthMarketData.AskVolume1
                //&& DepthMarket.BidPrice1 == pDepthMarketData.BidPrice1
                //&& DepthMarket.AskPrice1 == pDepthMarketData.AskPrice1
                //)
                //{ }
                //else
                {
                    CTPQuote quote = new CTPQuote(_dateTime,
                        pDepthMarketData.BidPrice1 == double.MaxValue ? 0 : pDepthMarketData.BidPrice1,
                        pDepthMarketData.BidVolume1,
                        pDepthMarketData.AskPrice1 == double.MaxValue ? 0 : pDepthMarketData.AskPrice1,
                        pDepthMarketData.AskVolume1
                    );

                    quote.DepthMarketData = pDepthMarketData;

                    EmitNewQuoteEvent(instrument, quote);
                }
            }

            if (record.MarketDepthRequested)
            {
                EmitNewMarketDepth(instrument, _dateTime, 0, MDSide.Ask, pDepthMarketData.AskPrice1, pDepthMarketData.AskVolume1);
                EmitNewMarketDepth(instrument, _dateTime, 0, MDSide.Bid, pDepthMarketData.BidPrice1, pDepthMarketData.BidVolume1);
#if CTPZQ
                EmitNewMarketDepth(instrument, _dateTime, 1, MDSide.Ask, pDepthMarketData.AskPrice2, pDepthMarketData.AskVolume2);
                EmitNewMarketDepth(instrument, _dateTime, 1, MDSide.Bid, pDepthMarketData.BidPrice2, pDepthMarketData.BidVolume2);

                EmitNewMarketDepth(instrument, _dateTime, 2, MDSide.Ask, pDepthMarketData.AskPrice3, pDepthMarketData.AskVolume3);
                EmitNewMarketDepth(instrument, _dateTime, 2, MDSide.Bid, pDepthMarketData.BidPrice3, pDepthMarketData.BidVolume3);

                EmitNewMarketDepth(instrument, _dateTime, 3, MDSide.Ask, pDepthMarketData.AskPrice4, pDepthMarketData.AskVolume4);
                EmitNewMarketDepth(instrument, _dateTime, 3, MDSide.Bid, pDepthMarketData.BidPrice4, pDepthMarketData.BidVolume4);

                EmitNewMarketDepth(instrument, _dateTime, 4, MDSide.Ask, pDepthMarketData.AskPrice5, pDepthMarketData.AskVolume5);
                EmitNewMarketDepth(instrument, _dateTime, 4, MDSide.Bid, pDepthMarketData.BidPrice5, pDepthMarketData.BidVolume5);
#endif
            }
        }


        private void EmitNewMarketDepth(Instrument instrument, DateTime datatime, int position, MDSide ask, double price, int size)
        {
            MDOperation insert = MDOperation.Update;
            if (MDSide.Ask == ask)
            {
                if (position >= instrument.OrderBook.Ask.Count)
                {
                    insert = MDOperation.Insert;
                }
            }
            else
            {
                if (position >= instrument.OrderBook.Bid.Count)
                {
                    insert = MDOperation.Insert;
                }
            }

            if (price != 0 && size != 0)
            {
                EmitNewMarketDepth(instrument, new MarketDepth(datatime, "", position, insert, ask, price, size));
            }
        }

        public void OnRspQryDepthMarketData(IntPtr pTraderApi, ref CThostFtdcDepthMarketDataField pDepthMarketData, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            if (0 == pRspInfo.ErrorID)
            {
                CThostFtdcDepthMarketDataField DepthMarket;
                if (!_dictDepthMarketData.TryGetValue(pDepthMarketData.InstrumentID, out DepthMarket))
                {
                    //没找到此元素，保存一下
                    _dictDepthMarketData[pDepthMarketData.InstrumentID] = pDepthMarketData;
                }

                tdlog.Info("已经接收查询深度行情 {0}", pDepthMarketData.InstrumentID);
                //通知单例
                CTPAPI.GetInstance().FireOnRspQryDepthMarketData(pDepthMarketData);
            }
            else
            {
                tdlog.Error("nRequestID:{0},ErrorID:{1},OnRspQryDepthMarketData:{2}", nRequestID, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitError(nRequestID, pRspInfo.ErrorID, "OnRspQryDepthMarketData:" + pRspInfo.ErrorMsg);
            }
        }

        #endregion
    }
}
