using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using QuantBox.CSharp2CTP;
using QuantBox.Helper.CTP;
using SmartQuant;
using SmartQuant.Data;
using SmartQuant.Execution;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;


namespace QuantBox.OQ.CTP
{
    partial class CTPProvider
    {
        #region 深度行情回调
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
