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
        #region 撤单
        private void Cancel(SingleOrder order)
        {
            if (null == order)
                return;

            if (!_bTdConnected)
            {
                EmitError(-1, -1, "交易服务器没有连接，无法撤单");
                tdlog.Error("交易服务器没有连接，无法撤单");
                return;
            }

            GenericOrderItem item;
            if(orderMap.TryGetValue(order,out item))
            {
                CThostFtdcOrderField _order;
                if (orderMap.TryGetValue(item, out _order))
                {
                    // 标记下正在撤单
                    orderMap.Order_OrdStatus[order] = order.OrdStatus;
                    EmitExecutionReport(order, OrdStatus.PendingCancel);

                    TraderApi.TD_CancelOrder(m_pTdApi, ref _order);
                }
            }
        }
        #endregion

        #region 下单
        private GenericCombiner<CommonOrderItem, TextCommon> CommonOrderCombiner = new GenericCombiner<CommonOrderItem, TextCommon>();
        private GenericCombiner<QuoteOrderItem, TextQuote> QuoteOrderCombiner = new GenericCombiner<QuoteOrderItem, TextQuote>();
        private GenericCombiner<SPOrderItem, TextSP> SPOrderCombiner = new GenericCombiner<SPOrderItem, TextSP>();
        private GenericCombiner<SPCOrderItem, TextSPC> SPCOrderCombiner = new GenericCombiner<SPCOrderItem, TextSPC>();
        private GenericCombiner<SPDOrderItem, TextSPD> SPDOrderCombiner = new GenericCombiner<SPDOrderItem, TextSPD>();

        private void Send(NewOrderSingle order)
        {
            if (!_bTdConnected)
            {
                EmitError(-1, -1, "交易服务器没有连接，无法报单");
                tdlog.Error("交易服务器没有连接，无法报单");
                return;
            }

            // 表示特殊的Json格式
            if (order.Text.StartsWith("{") && order.Text.EndsWith("}"))
            {
                TextParameter parameter = JsonConvert.DeserializeObject<TextParameter>(order.Text);
                switch (parameter.Type)
                {
                    case EnumGroupType.COMMON:
                        {
                            TextCommon t = JsonConvert.DeserializeObject<TextCommon>(order.Text);
                            CommonOrderItem item = CommonOrderCombiner.Add(order as SingleOrder, t);
                            Send(item);
                        }
                        break;
                    case EnumGroupType.QUOTE:
                        {
                            TextQuote t = JsonConvert.DeserializeObject<TextQuote>(order.Text);
                            QuoteOrderItem item = QuoteOrderCombiner.Add(order as SingleOrder, t);
                        }
                        break;
                    case EnumGroupType.SP:
                        {
                            TextSP t = JsonConvert.DeserializeObject<TextSP>(order.Text);
                            SPOrderItem item = SPOrderCombiner.Add(order as SingleOrder, t);
                            Send(item);
                        }
                        break;
                    case EnumGroupType.SPC:
                        {
                            TextSPC t = JsonConvert.DeserializeObject<TextSPC>(order.Text);
                            SPCOrderItem item = SPCOrderCombiner.Add(order as SingleOrder, t);
                            Send(item);
                        }
                        break;
                    case EnumGroupType.SPD:
                        {
                            TextSPD t = JsonConvert.DeserializeObject<TextSPD>(order.Text);
                            SPDOrderItem item = SPDOrderCombiner.Add(order as SingleOrder, t);
                            Send(item);
                        }
                        break;
                }
            }
            else
            {
                // 无法识别的格式，直接发送报单，只开仓
                TextCommon t = new TextCommon()
                {
                    Type = EnumGroupType.COMMON,
                    OpenClose = EnumOpenClose.OPEN
                };
                CommonOrderItem item = CommonOrderCombiner.Add(order as SingleOrder, t);
                Send(item);
            }
        }
        #endregion

        #region 取API信息
        private void GetInstrumentInfoForCTP(Instrument inst, out string apiSymbol, out string apiExchange, out double apiTickSize)
        {
            apiSymbol = inst.GetSymbol(Name);
            apiExchange = inst.GetSecurityExchange(Name);
            apiTickSize = inst.TickSize;

            CThostFtdcInstrumentField _Instrument;
            if (_dictInstruments.TryGetValue(apiSymbol, out _Instrument))
            {
                apiSymbol = _Instrument.InstrumentID;
                apiExchange = _Instrument.ExchangeID;
                apiTickSize = _Instrument.PriceTick;
            }
        }

        private void GetInstrumentInfoForCTPZQ(Instrument inst, out string apiSymbol, out string apiExchange, out double apiTickSize,out string yahooSymbol)
        {
            // 如果设置了altSymbol取到600000;没设，取到600000.SS
            apiSymbol = inst.GetSymbol(Name);
            apiExchange = inst.GetSecurityExchange(Name);
            apiTickSize = inst.TickSize;

            apiSymbol = GetApiSymbol(apiSymbol);
            yahooSymbol = GetYahooSymbol(apiSymbol, apiExchange);

            CThostFtdcInstrumentField _Instrument;
            if (_dictInstruments.TryGetValue(yahooSymbol, out _Instrument))
            {
                apiSymbol = _Instrument.InstrumentID;
                apiExchange = _Instrument.ExchangeID;
                apiTickSize = _Instrument.PriceTick;
            }
        }
        #endregion

        #region 发送普通单
        private void Send(CommonOrderItem item)
        {
            if (item == null)
                return;

            SingleOrder order = item.Leg.Order;

            string apiSymbol;
            string apiExchange;
            double apiTickSize;
            string altSymbol;
#if CTP
            GetInstrumentInfoForCTP(order.Instrument,out apiSymbol,out apiExchange,out apiTickSize);
            altSymbol = apiSymbol;
#elif CTPZQ
            GetInstrumentInfoForCTPZQ(order.Instrument,out apiSymbol,out apiExchange,out apiTickSize,out altSymbol);
#endif
            double price = order.Price;
            int qty = (int)order.OrderQty;

            //市价修正，如果不连接行情，此修正不执行，得策略层处理
            CThostFtdcDepthMarketDataField DepthMarket;
            //如果取出来了，并且为有效的，涨跌停价将不为0
            _dictDepthMarketData.TryGetValue(altSymbol, out DepthMarket);

            //市价单模拟
            if (OrdType.Market == order.OrdType)
            {
                //按买卖调整价格
                if (order.Side == Side.Buy)
                {
                    price = DepthMarket.LastPrice + LastPricePlusNTicks * apiTickSize;
                }
                else
                {
                    price = DepthMarket.LastPrice - LastPricePlusNTicks * apiTickSize;
                }
            }

            price = FixPrice(price, order.Side, apiTickSize, DepthMarket.LowerLimitPrice, DepthMarket.UpperLimitPrice);

            // 是否要做价格调整？
            byte[] bytes = { (byte)CTPAPI.ToCTP(item.Leg.OpenClose)};
            string szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);

            byte[] bytes2 = { (byte)HedgeFlagType};
            string szCombHedgeFlag = System.Text.Encoding.Default.GetString(bytes2, 0, bytes2.Length);

            TThostFtdcDirectionType Direction = order.Side == Side.Buy ? TThostFtdcDirectionType.Buy : TThostFtdcDirectionType.Sell;
            TThostFtdcOrderPriceTypeType OrderPriceType = TThostFtdcOrderPriceTypeType.LimitPrice;
            TThostFtdcTimeConditionType TimeCondition = TThostFtdcTimeConditionType.GFD;
            TThostFtdcContingentConditionType ContingentCondition = TThostFtdcContingentConditionType.Immediately;
            TThostFtdcVolumeConditionType VolumeCondition = TThostFtdcVolumeConditionType.AV;
           

#if CTP
            bool bSupportMarketOrder = SupportMarketOrder.Contains(apiExchange);
#elif CTPZQ
            bool bSupportMarketOrder = true;
#endif

            switch (order.TimeInForce)
            {
                case TimeInForce.IOC:
                    TimeCondition = TThostFtdcTimeConditionType.IOC;
                    VolumeCondition = TThostFtdcVolumeConditionType.AV;
                    break;
                case TimeInForce.FOK:
                    TimeCondition = TThostFtdcTimeConditionType.IOC;
                    VolumeCondition = TThostFtdcVolumeConditionType.CV;
                    break;
                default:
                    break;
            }

            int nRet = 0;

            switch (order.OrdType)
            {
                case OrdType.Limit:
                    break;
                case OrdType.Market:
                    if (SwitchMakertOrderToLimitOrder || !bSupportMarketOrder)
                    {
                    }
                    else
                    {
                        price = 0;
                        OrderPriceType = TThostFtdcOrderPriceTypeType.AnyPrice;
                        TimeCondition = TThostFtdcTimeConditionType.IOC;
                    }
                    break;
                default:
                    tdlog.Warn("没有实现{0}", order.OrdType);
                    return;
            }

#if CTP
            nRet = TraderApi.TD_SendOrder(m_pTdApi,
                        apiSymbol,
                        Direction,
                        szCombOffsetFlag,
                        szCombHedgeFlag,
                        qty,
                        price,
                        OrderPriceType,
                        TimeCondition,
                        ContingentCondition,
                        order.StopPx,
                        VolumeCondition);
#elif CTPZQ
                nRet = TraderApi.TD_SendOrder(m_pTdApi,
                            apiSymbol,
                            apiExchange,
                            Direction,
                            szCombOffsetFlag,
                            szCombHedgeFlag,
                            qty,
                            string.Format("{0}", price),
                            OrderPriceType,
                            TimeCondition,
                            ContingentCondition,
                            order.StopPx,
                            VolumeCondition);
#endif
            if (nRet > 0)
            {
                orderMap.CreateNewOrder(string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, nRet), item);
            }
        }
        #endregion

        #region 发送交易所套利单
        private void Send(SPOrderItem item)
        {
            if (item == null)
                return;

            SingleOrder order = item.Leg[0].Order;
            SingleOrder order2 = item.Leg[1].Order;

            string symbol = item.GetSymbol();
            double price = order.Price - order2.Price;
            int qty = (int)order.OrderQty;

            // 是否要做价格调整？
            byte[] bytes = { (byte)CTPAPI.ToCTP(item.Leg[0].OpenClose), (byte)CTPAPI.ToCTP(item.Leg[1].OpenClose) };
            string szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);

            byte[] bytes2 = { (byte)HedgeFlagType, (byte)HedgeFlagType };
            string szCombHedgeFlag = System.Text.Encoding.Default.GetString(bytes2, 0, bytes2.Length);

            TThostFtdcDirectionType Direction = order.Side == Side.Buy ? TThostFtdcDirectionType.Buy : TThostFtdcDirectionType.Sell;
            TThostFtdcOrderPriceTypeType OrderPriceType = TThostFtdcOrderPriceTypeType.LimitPrice;
            TThostFtdcTimeConditionType TimeCondition = TThostFtdcTimeConditionType.GFD;
            TThostFtdcContingentConditionType ContingentCondition = TThostFtdcContingentConditionType.Immediately;
            TThostFtdcVolumeConditionType VolumeCondition = TThostFtdcVolumeConditionType.AV;

            int nRet = 0;
#if CTP
            nRet = TraderApi.TD_SendOrder(m_pTdApi,
                        symbol,
                        Direction,
                        szCombOffsetFlag,
                        szCombHedgeFlag,
                        qty,
                        price,
                        OrderPriceType,
                        TimeCondition,
                        ContingentCondition,
                        0,
                        VolumeCondition);
#endif
            if (nRet > 0)
            {
                orderMap.CreateNewOrder(string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, nRet), item);
            }
        }
        #endregion

        #region 报单回报
        private void OnRtnOrder(IntPtr pTraderApi, ref CThostFtdcOrderField pOrder)
        {
            tdlog.Info("{0},{1},{2},开平{3},价{4},原量{5},成交{6},提交{7},状态{8},前置{9},会话{10},引用{11},报单编号{12},{13}",
                    pOrder.InsertTime, pOrder.InstrumentID, pOrder.Direction, pOrder.CombOffsetFlag, pOrder.LimitPrice,
                    pOrder.VolumeTotalOriginal, pOrder.VolumeTraded, pOrder.OrderSubmitStatus, pOrder.OrderStatus,
                    pOrder.FrontID,pOrder.SessionID,pOrder.OrderRef, pOrder.OrderSysID, pOrder.StatusMsg);

            // 加上这句只是为了在一个账号多会话高频交易时提前过滤
            if (pOrder.SessionID != _RspUserLogin.SessionID || pOrder.FrontID != _RspUserLogin.FrontID)
            {
                return;
            }

            GenericOrderItem item;
            string strKey = string.Format("{0}:{1}:{2}", pOrder.FrontID, pOrder.SessionID, pOrder.OrderRef);
            if (orderMap.TryGetValue(strKey, out item))
            {
                string strSysID = string.Format("{0}:{1}", pOrder.ExchangeID, pOrder.OrderSysID);

                switch (pOrder.OrderStatus)
                {
                        /// 不用处理
                    case TThostFtdcOrderStatusType.PartTradedQueueing:
                        break;
                        /// 第一个状态，要注册
                    case TThostFtdcOrderStatusType.NoTradeQueueing:
                        OnRtnOrderFirstStatus(item, pOrder,strSysID,strKey);
                        break;
                        /// 最后一个状态，要清理
                    case TThostFtdcOrderStatusType.AllTraded:
                    case TThostFtdcOrderStatusType.PartTradedNotQueueing:
                    case TThostFtdcOrderStatusType.NoTradeNotQueueing:
                        OnRtnOrderLastStatus(item, pOrder, strSysID, strKey);
                        break;
                        /// 其它情况
                    case TThostFtdcOrderStatusType.Canceled:
                        //分析此报单是否结束
                        switch (pOrder.OrderSubmitStatus)
                        {
                            case TThostFtdcOrderSubmitStatusType.InsertRejected:
                                EmitRejected(item,pOrder.StatusMsg);
                                break;
                            default:
                                EmitCancelled(item);
                                break;
                        }
                        OnRtnOrderLastStatus(item, pOrder, strSysID, strKey);
                        break;
                    case TThostFtdcOrderStatusType.Unknown:
                        switch (pOrder.OrderSubmitStatus)
                        {
                            case TThostFtdcOrderSubmitStatusType.InsertSubmitted:
                                OnRtnOrderFirstStatus(item, pOrder, strSysID, strKey);
                                break;
                        }
                        break;
                    case TThostFtdcOrderStatusType.NotTouched:
                        //没有处理
                        break;
                    case TThostFtdcOrderStatusType.Touched:
                        //没有处理
                        break;
                }
            }
        }
        #endregion

        #region 委托与成交事件
        private void OnRtnOrderFirstStatus(GenericOrderItem item, CThostFtdcOrderField pOrder, string OrderSysID, string Key)
        {
            // 向上层报告保单引用
            if(OrderSysID.Length>0)
            {
                orderMap.OrderSysID_OrderRef[OrderSysID] = Key;

                foreach (var o in item.GetLegs())
                {
                    // 这个地方要再查一查，是不是要移动出来？
                    o.Order.OrderID = pOrder.OrderSysID;
                }
            }
            
            // 判断是第一次报单，还是只是撤单时的第一条记录
            if (!orderMap.OrderItem_OrderField.ContainsKey(item))
            {
                // 在Order_OrderItem中记录绑定关系
                foreach (var o in item.GetLegs())
                {
                    orderMap.Order_OrderItem[o.Order] = item;
                }

                EmitAccepted(item);
            }

            // 在OrderItem_OrderField中记录与API的对应关系
            orderMap.OrderItem_OrderField[item] = pOrder;
        }

        private void OnRtnOrderLastStatus(GenericOrderItem item, CThostFtdcOrderField pOrder, string OrderSysID, string Key)
        {
            // 给成交回报用的，由于成交回报返回慢，所以不能删
            //orderMap.OrderSysID_OrderRef.Remove(OrderSysID);

            // 已经是最后状态，不能用于撤单了
            // 这个功能交给用户上层处理，报个重复撤单也没啥
            //orderMap.OrderItem_OrderField.Remove(item);

            OnLastStatus(item, OrderSysID, Key);
        }

        private void OnRtnTradeLastStatus(GenericOrderItem item, CThostFtdcTradeField pTrade, string OrderSysID, string Key)
        {
            OnLastStatus(item,OrderSysID,Key);
        }

        private void OnLastStatus(GenericOrderItem item, string OrderSysID, string Key)
        {
            // 一个单子成交完成，报单组可能还没有完，这个地方一定要留意
            if (!item.IsDone())
                return;

            foreach (var order in item.GetLegs())
            {
                orderMap.Order_OrderItem.Remove(order.Order);
            }

            orderMap.OrderItem_OrderField.Remove(item);
            orderMap.OrderRef_OrderItem.Remove(Key);
            orderMap.OrderSysID_OrderRef.Remove(OrderSysID);
        }

        private void OnLastStatus(SingleOrder order)
        {
            // 一个单子成交完成，报单组可能还没有完，这个地方一定要留意
            if (!order.IsDone)
                return;

             GenericOrderItem item;
             if (orderMap.TryGetValue(order, out item))
             {
                 CThostFtdcOrderField _order;
                 if (orderMap.TryGetValue(item, out _order))
                 {
                     OnLastStatus(item, _order.OrderSysID, _order.OrderRef);
                 }
             }
        }
        #endregion

        #region 成交回报
        private void OnRtnTrade(IntPtr pTraderApi, ref CThostFtdcTradeField pTrade)
        {
            tdlog.Info("时{0},合约{1},方向{2},开平{3},价{4},量{5},引用{6},成交编号{7}",
                    pTrade.TradeTime, pTrade.InstrumentID, pTrade.Direction, pTrade.OffsetFlag,
                    pTrade.Price, pTrade.Volume, pTrade.OrderRef, pTrade.TradeID);

            //找到自己发送的订单，标记成交
            string strSysID = string.Format("{0}:{1}", pTrade.ExchangeID, pTrade.OrderSysID);
            string strKey;
            if (!orderMap.TryGetValue(strSysID, out strKey))
            {
                return;
            }

            GenericOrderItem item;
            if (orderMap.TryGetValue(strKey, out item))
            {
                MultiOrderLeg leg = item.GetLeg(CTPAPI.FromCTP(pTrade.Direction), pTrade.InstrumentID);
                SingleOrder order = leg.Order;
#if CTP
                double Price = pTrade.Price;
#elif CTPZQ
                double Price = Convert.ToDouble(pTrade.Price);
#endif
                int Volume = pTrade.Volume;

                int LeavesQty = (int)order.LeavesQty - Volume;
                EmitFilled(order, Price, Volume,CommType.Absolute,0);

                // 成交完成，清理数据
                OnRtnTradeLastStatus(item, pTrade, strSysID, strKey);
            }
        }
        #endregion

        #region 撤单报错
        private void OnRspOrderAction(IntPtr pTraderApi, ref CThostFtdcInputOrderActionField pInputOrderAction, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            tdlog.Error("CTP回应：{0},价{1},变化量{2},前置{3},会话{4},引用{5},{6}#{7}",
                        pInputOrderAction.InstrumentID, pInputOrderAction.LimitPrice,
                        pInputOrderAction.VolumeChange,
                        pInputOrderAction.FrontID, pInputOrderAction.SessionID, pInputOrderAction.OrderRef,
                        pRspInfo.ErrorID, pRspInfo.ErrorMsg);

            GenericOrderItem item;
            string strKey = string.Format("{0}:{1}:{2}", pInputOrderAction.FrontID, pInputOrderAction.SessionID, pInputOrderAction.OrderRef);
            if (orderMap.TryGetValue(strKey, out item))
            {
                EmitCancelReject(item, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitCancelLastStatus(item);
            }
        }

        private void OnErrRtnOrderAction(IntPtr pTraderApi, ref CThostFtdcOrderActionField pOrderAction, ref CThostFtdcRspInfoField pRspInfo)
        {
            tdlog.Error("交易所回应：{0},价{1},变化量{2},前置{3},会话{4},引用{5},{6}#{7}",
                        pOrderAction.InstrumentID, pOrderAction.LimitPrice,
                        pOrderAction.VolumeChange,
                        pOrderAction.FrontID, pOrderAction.SessionID, pOrderAction.OrderRef,
                        pRspInfo.ErrorID, pRspInfo.ErrorMsg);

            GenericOrderItem item;
            string strKey = string.Format("{0}:{1}:{2}", pOrderAction.FrontID, pOrderAction.SessionID, pOrderAction.OrderRef);
            if (orderMap.TryGetValue(strKey, out item))
            {
                EmitCancelReject(item, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                EmitCancelLastStatus(item);
            }
        }
        #endregion

        #region 下单报错
        private void OnRspOrderInsert(IntPtr pTraderApi, ref CThostFtdcInputOrderField pInputOrder, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            tdlog.Error("CTP回应：{0},{1},开平{2},价{3},原量{4},引用{5},{6}#{7}",
                        pInputOrder.InstrumentID, pInputOrder.Direction, pInputOrder.CombOffsetFlag, pInputOrder.LimitPrice,
                        pInputOrder.VolumeTotalOriginal,
                        pInputOrder.OrderRef, pRspInfo.ErrorID, pRspInfo.ErrorMsg);

            GenericOrderItem item;
            string strKey = string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pInputOrder.OrderRef);
            if (orderMap.TryGetValue(strKey, out item))
            {
                EmitRejected(item, pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                OnLastStatus(item, "", strKey);
            }
        }

        private void OnErrRtnOrderInsert(IntPtr pTraderApi, ref CThostFtdcInputOrderField pInputOrder, ref CThostFtdcRspInfoField pRspInfo)
        {
            tdlog.Error("交易所回应：{0},{1},开平{2},价{3},原量{4},引用{5},{6}#{7}",
                        pInputOrder.InstrumentID, pInputOrder.Direction, pInputOrder.CombOffsetFlag, pInputOrder.LimitPrice,
                        pInputOrder.VolumeTotalOriginal,
                        pInputOrder.OrderRef, pRspInfo.ErrorID, pRspInfo.ErrorMsg);

            GenericOrderItem item;
            string strKey = string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pInputOrder.OrderRef);
            if (orderMap.TryGetValue(strKey, out item))
            {
                EmitRejected(item,pRspInfo.ErrorID,pRspInfo.ErrorMsg);
                OnLastStatus(item, "", strKey);
            }
        }
        #endregion

        #region 价格修正
        public double FixPrice(double price, Side Side, double tickSize, double LowerLimitPrice, double UpperLimitPrice)
        {
            //没有设置就直接用
            if (tickSize > 0)
            {
                decimal remainder = ((decimal)price % (decimal)tickSize);
                if (remainder != 0)
                {
                    if (Side == Side.Buy)
                    {
                        price = Math.Ceiling(price / tickSize) * tickSize;
                    }
                    else
                    {
                        price = Math.Floor(price / tickSize) * tickSize;
                    }
                }
                else
                {
                    //正好能整除，不操作
                }
            }

            if (0 == UpperLimitPrice
                && 0 == LowerLimitPrice)
            {
                //涨跌停无效
            }
            else
            {
                //防止价格超过涨跌停
                if (price >= UpperLimitPrice)
                    price = UpperLimitPrice;
                else if (price <= LowerLimitPrice)
                    price = LowerLimitPrice;
            }
            return price;
        }
        #endregion

        #region 回报通知
        private void UpdateOrderText(GenericOrderItem item, string message)
        {
            foreach (var leg in item.GetLegs())
            {
                leg.Order.Text = message;
            }
        }

        private void EmitAccepted(GenericOrderItem item)
        {
            foreach (var leg in item.GetLegs())
            {
                EmitAccepted(leg.Order);
            }
        }

        private void EmitRejected(GenericOrderItem item, string message)
        {
            TextResponse r = new TextResponse()
            {
                Error = EnumError.OTHER,
                StatusMsg = message,
            };

            foreach (var leg in item.GetLegs())
            {
                r.OpenClose = leg.OpenClose;
                leg.Order.Text = r.ToString();
                EmitRejected(leg.Order, r.ToString());
            }
        }

        private void EmitRejected(GenericOrderItem item, int error_id, string message)
        {
            TextResponse r = new TextResponse()
            {
                Error = CTPAPI.FromCTP(error_id),
                ErrorID = error_id,
                ErrorMsg = message,
            };

            foreach (var leg in item.GetLegs())
            {
                r.OpenClose = leg.OpenClose;
                leg.Order.Text = r.ToString();
                EmitRejected(leg.Order, r.ToString());
            }
        }

        private void EmitCancelled(GenericOrderItem item)
        {
            foreach (var leg in item.GetLegs())
            {
                EmitCancelled(leg.Order);
            }
        }

        private void EmitCancelReject(GenericOrderItem item,int error_id,string message)
        {
            TextResponse r = new TextResponse()
            {
                Error = CTPAPI.FromCTP(error_id),
                ErrorID = error_id,
                ErrorMsg = message,
            };

            foreach (var leg in item.GetLegs())
            {
                r.OpenClose = leg.OpenClose;
                leg.Order.Text = r.ToString();
                EmitCancelReject(leg.Order, leg.Order.OrdStatus, r.ToString());
            }
        }

        private void EmitCancelLastStatus(GenericOrderItem item)
        {
            foreach (var leg in item.GetLegs())
            {
                OrdStatus status;
                if (orderMap.TryGetValue(leg.Order , out status))
                {
                    EmitExecutionReport(leg.Order, status);
                    orderMap.Order_OrdStatus.Remove(leg.Order);
                }
            }
        }
        #endregion
    }
}
