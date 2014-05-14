using System;
using System.Collections.Generic;

using SmartQuant.Execution;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using Newtonsoft.Json;
using QuantBox.OQ.Extensions;
using QuantBox.OQ.Extensions.OrderText;
using QuantBox.OQ.Extensions.Combiner;
using QuantBox.OQ.Extensions.OrderItem;

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

        #region 发双向报价单
        private void Send(QuoteOrderItem item)
        {
            if (item == null)
                return;

            SingleOrder AskOrder = item.Sell.Order;
            SingleOrder BidOrder = item.Buy.Order;
            
            string symbol = item.Buy.Order.Symbol;

            double AskPrice = AskOrder.Price;
            double BidPrice = BidOrder.Price;
            int AskVolume = (int)AskOrder.OrderQty;
            int BidVolume = (int)BidOrder.OrderQty;

            TThostFtdcOffsetFlagType AskOffsetFlag = CTPAPI.ToCTP(item.Sell.OpenClose);
            TThostFtdcOffsetFlagType BidOffsetFlag = CTPAPI.ToCTP(item.Buy.OpenClose);

            TThostFtdcHedgeFlagType AskHedgeFlag = HedgeFlagType;
            TThostFtdcHedgeFlagType BidHedgeFlag = HedgeFlagType;

            int nRet = 0;
#if CTP
            nRet = TraderApi.TD_SendQuote(m_pTdApi,
                -1,
                        symbol,
                        AskPrice,
                        BidPrice,
                        AskVolume,
                        BidVolume,
                        AskOffsetFlag,
                        BidOffsetFlag,
                        AskHedgeFlag,
                        BidHedgeFlag);
#endif
            if (nRet > 0)
            {
                orderMap.CreateNewOrder(string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, nRet), item);
            }
        }
        #endregion
    }
}
