using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using SmartQuant;
using SmartQuant.Data;
using SmartQuant.Execution;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;
using Newtonsoft.Json;
using QuantBox.OQ.Extensions;

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
        private readonly Dictionary<SingleOrder, OrdStatus> _PendingCancelFlags = new Dictionary<SingleOrder, OrdStatus>();
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

            CThostFtdcOrderField _Order;
            if (_Orders4Cancel.TryGetValue(order, out _Order))
            {
                // 标记下正在撤单
                _PendingCancelFlags[order] = order.OrdStatus;

                //这地方要是过滤下就好了
                TraderApi.TD_CancelOrder(m_pTdApi, ref _Order);
            }
        }
        #endregion

        #region 下单
        private void Send(NewOrderSingle order)
        {
            if (!_bTdConnected)
            {
                EmitError(-1, -1, "交易服务器没有连接，无法报单");
                tdlog.Error("交易服务器没有连接，无法报单");
                return;
            }

            Instrument inst = InstrumentManager.Instruments[order.Symbol];
            string altSymbol = inst.GetSymbol(Name);
            string altExchange = inst.GetSecurityExchange(Name);
            double tickSize = inst.TickSize;

            string apiSymbol = GetApiSymbol(altSymbol);

            CThostFtdcInstrumentField _Instrument;
            if (_dictInstruments.TryGetValue(altSymbol, out _Instrument))
            {
                //从合约列表中取交易所名与tickSize，不再依赖用户手工设置的参数了
                tickSize = _Instrument.PriceTick;
                apiSymbol = _Instrument.InstrumentID;
                altExchange = _Instrument.ExchangeID;
            }

            //最小变动价格修正
            double price = order.Price;

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
                    price = DepthMarket.LastPrice + LastPricePlusNTicks * tickSize;
                }
                else
                {
                    price = DepthMarket.LastPrice - LastPricePlusNTicks * tickSize;
                }
            }

            //没有设置就直接用
            if (tickSize > 0)
            {
                decimal remainder = ((decimal)price % (decimal)tickSize);
                if (remainder != 0)
                {
                    if (order.Side == Side.Buy)
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

            if (0 == DepthMarket.UpperLimitPrice
                && 0 == DepthMarket.LowerLimitPrice)
            {
                //涨跌停无效
            }
            else
            {
                //防止价格超过涨跌停
                if (price >= DepthMarket.UpperLimitPrice)
                    price = DepthMarket.UpperLimitPrice;
                else if (price <= DepthMarket.LowerLimitPrice)
                    price = DepthMarket.LowerLimitPrice;
            }

            int YdPosition = 0;
            int TodayPosition = 0;

            string szCombOffsetFlag;
            if (order.Side == Side.Buy)
            {
                //买，先看有没有空单，有就平空单,没有空单，直接买开多单
                _dbInMemInvestorPosition.GetPositions(altSymbol,
                    TThostFtdcPosiDirectionType.Short, HedgeFlagType, out YdPosition, out TodayPosition);//TThostFtdcHedgeFlagType.Speculation
            }
            else//是否要区分Side.Sell与Side.SellShort呢？
            {
                //卖，先看有没有多单，有就平多单,没有多单，直接买开空单
                _dbInMemInvestorPosition.GetPositions(altSymbol,
                    TThostFtdcPosiDirectionType.Long, HedgeFlagType, out YdPosition, out TodayPosition);
            }

            int nOpenCloseFlag = 0;
            //根据 梦翔 与 马不停蹄 的提示，新加在Text域中指定开平标志的功能
            // 表示特殊的Json格式
            if (order.Text.StartsWith("{") && order.Text.EndsWith("}"))
            {
                //OrderTextRequest request = JsonConvert.DeserializeObject<OrderTextRequest>(order.Text);
                //switch (request.OpenClose)
                //{
                //    case EnumOpenClose.NONE:
                //        break;
                //    case EnumOpenClose.OPEN:
                //        nOpenCloseFlag = 1;
                //        break;
                //    case EnumOpenClose.CLOSE:
                //        nOpenCloseFlag = -1;
                //        break;
                //    case EnumOpenClose.CLOSE_TODAY:
                //        nOpenCloseFlag = -2;
                //        break;
                //    case EnumOpenClose.CLOSE_YESTERDAY:
                //        nOpenCloseFlag = -3;
                //        break;
                //}
            }
            else
            {
                if (order.Text.StartsWith(OpenPrefix))
                {
                    nOpenCloseFlag = 1;
                }
                else if (order.Text.StartsWith(ClosePrefix))
                {
                    nOpenCloseFlag = -1;
                }
                else if (order.Text.StartsWith(CloseTodayPrefix))
                {
                    nOpenCloseFlag = -2;
                }
                else if (order.Text.StartsWith(CloseYesterdayPrefix))
                {
                    nOpenCloseFlag = -3;
                }
            }


            int leave = (int)order.OrderQty;

#if CTP
            {
                byte[] bytes = { (byte)TThostFtdcOffsetFlagType.Open, (byte)TThostFtdcOffsetFlagType.Open };
                szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
            }

            //是否上海？上海先平今，然后平昨，最后开仓
            //使用do主要是想利用break功能
            //平仓部分
            do
            {
                //指定开仓，直接跳过
                if (nOpenCloseFlag > 0)
                    break;

                //表示指定平今与平昨
                if (nOpenCloseFlag < -1)
                {
                    if (-2 == nOpenCloseFlag)
                    {
                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.CloseToday, (byte)TThostFtdcOffsetFlagType.CloseToday };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        //肯定是-3了
                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.CloseYesterday, (byte)TThostFtdcOffsetFlagType.CloseYesterday };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
                    }

                    break;
                }

                if (SupportCloseToday.Contains(altExchange))
                {
                    //先看平今
                    if (TodayPosition > 0)
                    {
                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.CloseToday, (byte)TThostFtdcOffsetFlagType.CloseToday };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
                    }
                    else if (YdPosition > 0)
                    {
                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.CloseYesterday, (byte)TThostFtdcOffsetFlagType.CloseYesterday };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
                    }
                }
                else
                {
                    //平仓
                    int position = TodayPosition + YdPosition;
                    if (position > 0)
                    {
                        byte[] bytes = { (byte)TThostFtdcOffsetFlagType.Close, (byte)TThostFtdcOffsetFlagType.Close };
                        szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
                    }
                }
            } while (false);

            bool bSupportMarketOrder = SupportMarketOrder.Contains(altExchange);
#elif CTPZQ
            {
                //开平已经没有意义了
                byte[] bytes = { (byte)TThostFtdcOffsetFlagType.Open, (byte)TThostFtdcOffsetFlagType.Open };
                szCombOffsetFlag = System.Text.Encoding.Default.GetString(bytes, 0, bytes.Length);
            }

            bool bSupportMarketOrder = true;
#endif

            //将第二腿也设置成一样，这样在使用组合时这地方不用再调整
            byte[] bytes2 = { (byte)HedgeFlagType, (byte)HedgeFlagType };
            string szCombHedgeFlag = System.Text.Encoding.Default.GetString(bytes2, 0, bytes2.Length);

            tdlog.Info("Side:{0},Price:{1},LastPrice:{2},Qty:{3},Text:{4},YdPosition:{5},TodayPosition:{6}",
                order.Side, order.Price, DepthMarket.LastPrice, order.OrderQty, order.Text, YdPosition, TodayPosition);

            TThostFtdcDirectionType Direction = order.Side == Side.Buy ? TThostFtdcDirectionType.Buy : TThostFtdcDirectionType.Sell;
            TThostFtdcOrderPriceTypeType OrderPriceType = TThostFtdcOrderPriceTypeType.LimitPrice;
            TThostFtdcTimeConditionType TimeCondition = TThostFtdcTimeConditionType.GFD;
            TThostFtdcContingentConditionType ContingentCondition = TThostFtdcContingentConditionType.Immediately;
            TThostFtdcVolumeConditionType VolumeCondition = TThostFtdcVolumeConditionType.AV;

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
                        altSymbol,
                        Direction,
                        szCombOffsetFlag,
                        szCombHedgeFlag,
                        leave,
                        price,
                        OrderPriceType,
                        TimeCondition,
                        ContingentCondition,
                        order.StopPx,
                        VolumeCondition);
#elif CTPZQ
                nRet = TraderApi.TD_SendOrder(m_pTdApi,
                            apiSymbol,
                            altExchange,
                            Direction,
                            szCombOffsetFlag,
                            szCombHedgeFlag,
                            leave,
                            string.Format("{0}", price),
                            OrderPriceType,
                            TimeCondition,
                            ContingentCondition,
                            order.StopPx);
#endif
            if (nRet > 0)
            {
                _OrderRef2Order.Add(string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, nRet), order as SingleOrder);
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

            SingleOrder order;
            string strKey = string.Format("{0}:{1}:{2}", pOrder.FrontID, pOrder.SessionID, pOrder.OrderRef);
            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                order.Text = string.Format("{0}|{1}", order.Text.Substring(0, Math.Min(order.Text.Length, 64)), pOrder.StatusMsg);                
                //order.Text = new OrderTextResponse()
                //{
                //    OpenClose = CTPAPI.ToOpenClose(order.PositionEffect),
                //    StatusMsg = pOrder.StatusMsg,
                //}.ToString();
                order.OrderID = pOrder.OrderSysID;

                // 有对它进行过撤单操作，这地方是为了加正在撤单状态
                OrdStatus status;
                if (_PendingCancelFlags.TryGetValue(order, out status))
                {
                    // 记下当时的状态
                    _PendingCancelFlags[order] = order.OrdStatus;
                    EmitPendingCancel(order);
                }

                lock (_Orders4Cancel)
                {
                    string strSysID = string.Format("{0}:{1}", pOrder.ExchangeID, pOrder.OrderSysID);

                    switch (pOrder.OrderStatus)
                    {
                        case TThostFtdcOrderStatusType.AllTraded:
                            //已经是最后状态，不能用于撤单了
                            _PendingCancelFlags.Remove(order);
                            _Orders4Cancel.Remove(order);
                            break;
                        case TThostFtdcOrderStatusType.PartTradedQueueing:
                            break;
                        case TThostFtdcOrderStatusType.PartTradedNotQueueing:
                            //已经是最后状态，不能用于撤单了
                            _PendingCancelFlags.Remove(order);
                            _Orders4Cancel.Remove(order);
                            break;
                        case TThostFtdcOrderStatusType.NoTradeQueueing:
                            // 用于收到成交回报时定位
                            _OrderSysID2OrderRef[strSysID] = strKey;
                            
                            if (!_Orders4Cancel.ContainsKey(order))
                            {
                                _Orders4Cancel[order] = pOrder;
                                EmitAccepted(order);
                            }
                            break;
                        case TThostFtdcOrderStatusType.NoTradeNotQueueing:
                            //已经是最后状态，不能用于撤单了
                            _PendingCancelFlags.Remove(order);
                            _Orders4Cancel.Remove(order);
                            break;
                        case TThostFtdcOrderStatusType.Canceled:
                            // 将撤单中记录表清理下
                            _PendingCancelFlags.Remove(order);
                            //已经是最后状态，不能用于撤单了
                            _Orders4Cancel.Remove(order);
                            //分析此报单是否结束，如果结束分析整个Order是否结束
                            switch (pOrder.OrderSubmitStatus)
                            {
                                case TThostFtdcOrderSubmitStatusType.InsertRejected:
                                    //如果是最后一个的状态，同意发出消息
                                    EmitRejected(order, pOrder.StatusMsg);
                                    break;
                                default:
                                    //如果是最后一个的状态，同意发出消息
                                    EmitCancelled(order);
                                    break;
                            }
                            break;
                        case TThostFtdcOrderStatusType.Unknown:
                            switch (pOrder.OrderSubmitStatus)
                            {
                                case TThostFtdcOrderSubmitStatusType.InsertSubmitted:
                                    // 有可能头两个报单就这状态，就是报单编号由空变为了有。为空时，也记，没有关系
                                    _OrderSysID2OrderRef[strSysID] = strKey;

                                    // 这种情况下
                                    if(!_Orders4Cancel.ContainsKey(order))
                                    {
                                        _Orders4Cancel[order] = pOrder;
                                        EmitAccepted(order);
                                    }
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
            else
            {
                //由第三方软件发出或上次登录时的剩余的单子在这次成交了，先不处理，当不存在
            }
        }
        #endregion

        #region 成交回报

        //用于计算组合成交
        private readonly Dictionary<SingleOrder, DbInMemTrade> _Orders4Combination = new Dictionary<SingleOrder, DbInMemTrade>();

        private void OnRtnTrade(IntPtr pTraderApi, ref CThostFtdcTradeField pTrade)
        {
            tdlog.Info("时{0},合约{1},方向{2},开平{3},价{4},量{5},引用{6},成交编号{7}",
                    pTrade.TradeTime, pTrade.InstrumentID, pTrade.Direction, pTrade.OffsetFlag,
                    pTrade.Price, pTrade.Volume, pTrade.OrderRef, pTrade.TradeID);

            //将仓位计算提前，防止在OnPositionOpened中下平仓时与“C|”配合出错
            if (_dbInMemInvestorPosition.UpdateByTrade(pTrade))
            {
            }
            else
            {
                //本地计算更新失败，重查一次
                TraderApi.TD_ReqQryInvestorPosition(m_pTdApi, pTrade.InstrumentID);
            }

            SingleOrder order;
            //找到自己发送的订单，标记成交
            string strSysID = string.Format("{0}:{1}", pTrade.ExchangeID, pTrade.OrderSysID);
            string strKey;
            if (!_OrderSysID2OrderRef.TryGetValue(strSysID,out strKey))
            {
                return;
            }

            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                double Price = 0;
                int Volume = 0;
#if CTP
                if (TThostFtdcTradeTypeType.CombinationDerived == pTrade.TradeType)
                {
                    //组合，得特别处理
                    DbInMemTrade _trade;//用此对象维护组合对
                    if (!_Orders4Combination.TryGetValue(order, out _trade))
                    {
                        _trade = new DbInMemTrade();
                        _Orders4Combination[order] = _trade;
                    }

                    //找到成对交易的，得出价差
                    if (_trade.OnTrade(ref order, ref pTrade, ref Price, ref Volume))
                    {
                        //完成使命了，删除
                        //if (_trade.isEmpty())
                        //{
                        //    _Orders4Combination.Remove(order);
                        //}
                    }
                }
                else
                {
                    //普通订单，直接通知即可
                    Price = pTrade.Price;
                    Volume = pTrade.Volume;
                }
#elif CTPZQ
                {
                    Price = Convert.ToDouble(pTrade.Price);
                    Volume = pTrade.Volume;
                }
#endif

                int LeavesQty = (int)order.LeavesQty - Volume;
                EmitFilled(order, Price, Volume,CommType.Absolute,0);

                //成交完全，清理
                if (LeavesQty <= 0)
                {
                    _OrderRef2Order.Remove(strKey);
                    _OrderSysID2OrderRef.Remove(strSysID);
                    _Orders4Combination.Remove(order);
                    _Orders4Cancel.Remove(order);
                }
            }
        }
        #endregion

        #region 撤单报错
        private void OnRspOrderAction(IntPtr pTraderApi, ref CThostFtdcInputOrderActionField pInputOrderAction, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            SingleOrder order;
            string strKey = string.Format("{0}:{1}:{2}", pInputOrderAction.FrontID, pInputOrderAction.SessionID, pInputOrderAction.OrderRef);
            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                OrdStatus status;
                if (_PendingCancelFlags.TryGetValue(order, out status))
                {
                    _PendingCancelFlags.Remove(order);
                    EmitExecutionReport(order, status);
                }

                tdlog.Error("CTP回应：{0},价{1},变化量{2},前置{3},会话{4},引用{5},{6}#{7}",
                        pInputOrderAction.InstrumentID, pInputOrderAction.LimitPrice,
                        pInputOrderAction.VolumeChange,
                        pInputOrderAction.FrontID, pInputOrderAction.SessionID, pInputOrderAction.OrderRef,
                        pRspInfo.ErrorID, pRspInfo.ErrorMsg);

                order.Text = string.Format("{0}|{1}#{2}", order.Text.Substring(0, Math.Min(order.Text.Length, 64)), pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                //order.Text = new OrderTextResponse()
                //{
                //    OpenClose = CTPAPI.ToOpenClose(order.PositionEffect),
                //    Error = CTPAPI.ToQBError(pRspInfo.ErrorID),
                //    ErrorID = pRspInfo.ErrorID,
                //    ErrorMsg = pRspInfo.ErrorMsg,
                //}.ToString();

                EmitCancelReject(order, order.OrdStatus, pRspInfo.ErrorMsg);
            }
        }

        private void OnErrRtnOrderAction(IntPtr pTraderApi, ref CThostFtdcOrderActionField pOrderAction, ref CThostFtdcRspInfoField pRspInfo)
        {
            SingleOrder order;
            string strKey = string.Format("{0}:{1}:{2}", pOrderAction.FrontID, pOrderAction.SessionID, pOrderAction.OrderRef);
            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                OrdStatus status;
                if (_PendingCancelFlags.TryGetValue(order, out status))
                {
                    _PendingCancelFlags.Remove(order);
                    EmitExecutionReport(order, status);
                }

                tdlog.Error("交易所回应：{0},价{1},变化量{2},前置{3},会话{4},引用{5},{6}#{7}",
                        pOrderAction.InstrumentID, pOrderAction.LimitPrice,
                        pOrderAction.VolumeChange,
                        pOrderAction.FrontID, pOrderAction.SessionID, pOrderAction.OrderRef,
                        pRspInfo.ErrorID, pRspInfo.ErrorMsg);

                order.Text = string.Format("{0}|{1}#{2}", order.Text.Substring(0, Math.Min(order.Text.Length, 64)), pRspInfo.ErrorID, pRspInfo.ErrorMsg);

                //order.Text = new OrderTextResponse()
                //{
                //    OpenClose = CTPAPI.ToOpenClose(order.PositionEffect),
                //    Error = CTPAPI.ToQBError(pRspInfo.ErrorID),
                //    ErrorID = pRspInfo.ErrorID,
                //    ErrorMsg = pRspInfo.ErrorMsg,
                //    StatusMsg = pOrderAction.StatusMsg,
                //}.ToString();

                EmitCancelReject(order, order.OrdStatus, pRspInfo.ErrorMsg);
            }
        }
        #endregion

        #region 下单报错
        private void OnRspOrderInsert(IntPtr pTraderApi, ref CThostFtdcInputOrderField pInputOrder, ref CThostFtdcRspInfoField pRspInfo, int nRequestID, bool bIsLast)
        {
            SingleOrder order;
            string strKey = string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pInputOrder.OrderRef);
            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                tdlog.Error("CTP回应：{0},{1},开平{2},价{3},原量{4},引用{5},{6}#{7}",
                        pInputOrder.InstrumentID, pInputOrder.Direction, pInputOrder.CombOffsetFlag, pInputOrder.LimitPrice,
                        pInputOrder.VolumeTotalOriginal,
                        pInputOrder.OrderRef, pRspInfo.ErrorID, pRspInfo.ErrorMsg);

                order.Text = string.Format("{0}|{1}#{2}", order.Text.Substring(0, Math.Min(order.Text.Length, 64)), pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                //order.Text = new OrderTextResponse()
                //{
                //    OpenClose = CTPAPI.ToOpenClose(order.PositionEffect),
                //    Error = CTPAPI.ToQBError(pRspInfo.ErrorID),
                //    ErrorID = pRspInfo.ErrorID,
                //    ErrorMsg = pRspInfo.ErrorMsg,
                //}.ToString();

                EmitRejected(order, pRspInfo.ErrorMsg);
                _Orders4Cancel.Remove(order);
            }
        }

        private void OnErrRtnOrderInsert(IntPtr pTraderApi, ref CThostFtdcInputOrderField pInputOrder, ref CThostFtdcRspInfoField pRspInfo)
        {
            SingleOrder order;
            string strKey = string.Format("{0}:{1}:{2}", _RspUserLogin.FrontID, _RspUserLogin.SessionID, pInputOrder.OrderRef);
            if (_OrderRef2Order.TryGetValue(strKey, out order))
            {
                tdlog.Error("交易所回应：{0},{1},开平{2},价{3},原量{4},引用{5},{6}#{7}",
                        pInputOrder.InstrumentID, pInputOrder.Direction, pInputOrder.CombOffsetFlag, pInputOrder.LimitPrice,
                        pInputOrder.VolumeTotalOriginal,
                        pInputOrder.OrderRef, pRspInfo.ErrorID, pRspInfo.ErrorMsg);

                order.Text = string.Format("{0}|{1}#{2}", order.Text.Substring(0, Math.Min(order.Text.Length, 64)), pRspInfo.ErrorID, pRspInfo.ErrorMsg);
                //order.Text = new OrderTextResponse()
                //{
                //    OpenClose = CTPAPI.ToOpenClose(order.PositionEffect),
                //    Error = CTPAPI.ToQBError(pRspInfo.ErrorID),
                //    ErrorID = pRspInfo.ErrorID,
                //    ErrorMsg = pRspInfo.ErrorMsg,
                //}.ToString();

                EmitRejected(order, pRspInfo.ErrorMsg);
                _Orders4Cancel.Remove(order);
            }
        }
        #endregion
    }
}
