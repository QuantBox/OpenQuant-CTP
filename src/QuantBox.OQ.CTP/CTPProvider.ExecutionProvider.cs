using System;
using System.Collections.Generic;
using System.Data;
using QuantBox.CSharp2CTP;
using SmartQuant;
using SmartQuant.Execution;
using SmartQuant.FIX;
using SmartQuant.Providers;
using System.Reflection;

namespace QuantBox.OQ.CTP
{
    public partial class CTPProvider : IExecutionProvider
    {
        private readonly Dictionary<SingleOrder, OrderRecord> orderRecords = new Dictionary<SingleOrder, OrderRecord>();

        public event ExecutionReportEventHandler ExecutionReport;
        public event OrderCancelRejectEventHandler OrderCancelReject;

        public BrokerInfo GetBrokerInfo()
        {
            BrokerInfo brokerInfo = new BrokerInfo();

            if (IsConnected)
            {
                if (_bTdConnected)
                {
                    //tdlog.Info("GetBrokerInfo");
                }
                else
                {
                    //if (nGetBrokerInfoCount < 5)
                    //{
                    //    tdlog.Info("GetBrokerInfo,交易没有连接，查询无效,5次后将不显示");
                    //    ++nGetBrokerInfoCount;
                    //}
                    return null;
                }

                BrokerAccount brokerAccount = new BrokerAccount(m_TradingAccount.AccountID) { BuyingPower = m_TradingAccount.Available };

                Type t = typeof(CThostFtdcTradingAccountField);
                FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    brokerAccount.AddField(field.Name, field.GetValue(m_TradingAccount).ToString());
                }

                DataRow[] rows = _dbInMemInvestorPosition.SelectAll();

                foreach (DataRow dr in rows)
                {
                    BrokerPosition brokerPosition = new BrokerPosition {
                        Symbol = dr[DbInMemInvestorPosition.InstrumentID].ToString()
                    };

                    int pos = (int)dr[DbInMemInvestorPosition.Position];
                    TThostFtdcPosiDirectionType PosiDirection = (TThostFtdcPosiDirectionType)dr[DbInMemInvestorPosition.PosiDirection];
                    if (TThostFtdcPosiDirectionType.Long == PosiDirection)
                    {
                        brokerPosition.LongQty = pos;
                    }
                    else if (TThostFtdcPosiDirectionType.Short == PosiDirection)
                    {
                        brokerPosition.ShortQty = pos;
                    }
                    else
                    {
                        if (pos >= 0)//净NET这个概念是什么情况？
                            brokerPosition.LongQty = pos;
                        else
                            brokerPosition.ShortQty = -pos;
                    }
                    brokerPosition.Qty = brokerPosition.LongQty - brokerPosition.ShortQty;
                    brokerPosition.AddCustomField(DbInMemInvestorPosition.PosiDirection, PosiDirection.ToString());
                    brokerPosition.AddCustomField(DbInMemInvestorPosition.HedgeFlag, ((TThostFtdcHedgeFlagType)dr[DbInMemInvestorPosition.HedgeFlag]).ToString());
                    brokerPosition.AddCustomField(DbInMemInvestorPosition.PositionDate, ((TThostFtdcPositionDateType)dr[DbInMemInvestorPosition.PositionDate]).ToString());
                    brokerAccount.AddPosition(brokerPosition);
                }
                brokerInfo.Accounts.Add(brokerAccount);
            }            

            return brokerInfo;
        }

        public void SendNewOrderSingle(NewOrderSingle order)
        {
            SingleOrder key = order as SingleOrder;
            orderRecords.Add(key, new OrderRecord(key));
            Send(key);
        }

        #region QuantDeveloper下的接口
        public void SendOrderCancelReplaceRequest(FIXOrderCancelReplaceRequest request)
        {
            //IOrder order = OrderManager.Orders.All[request.OrigClOrdID];
            //SingleOrder order2 = order as SingleOrder;
            //this.provider.CallReplace(order2);
            EmitError(-1,-1,"不支持改单指令");
            tdlog.Error("不支持改单指令");
        }

        public void SendOrderCancelRequest(FIXOrderCancelRequest request)
        {
            IOrder order = OrderManager.Orders.All[request.OrigClOrdID];
            SingleOrder order2 = order as SingleOrder;
            Cancel(order2);
        }

        public void SendOrderStatusRequest(FIXOrderStatusRequest request)
        {
            throw new NotImplementedException();
        }
        #endregion

        private void EmitExecutionReport(ExecutionReport report)
        {
            if (ExecutionReport != null)
            {
                ExecutionReport(this, new ExecutionReportEventArgs(report));
            }
        }

        private void EmitOrderCancelReject(OrderCancelReject reject)
        {
            if (OrderCancelReject != null)
            {
                OrderCancelReject(this, new OrderCancelRejectEventArgs(reject));
            }
        }

        public void EmitExecutionReport(SingleOrder order, OrdStatus status)
        {
            EmitExecutionReport(order, status, "");
        }

        public void EmitExecutionReport(SingleOrder order, OrdStatus status, string text)
        {
            OrderRecord record = orderRecords[order];
            EmitExecutionReport(record, status, 0.0, 0, text);
        }

        public void EmitExecutionReport(SingleOrder order, double price, int quantity)
        {
            OrderRecord record = orderRecords[order];
            EmitExecutionReport(record, OrdStatus.Undefined, price, quantity, "");
        }

        private void EmitExecutionReport(OrderRecord record, OrdStatus ordStatus, double lastPx, int lastQty, string text)
        {
            ExecutionReport report = new ExecutionReport
            {
                TransactTime = Clock.Now,
                ClOrdID = record.Order.ClOrdID,
                OrigClOrdID = record.Order.ClOrdID,
                OrderID = record.Order.OrderID,
                Symbol = record.Order.Symbol,
                SecurityType = record.Order.SecurityType,
                SecurityExchange = record.Order.SecurityExchange,
                Currency = record.Order.Currency,
                Side = record.Order.Side,
                OrdType = record.Order.OrdType,
                TimeInForce = record.Order.TimeInForce,
                OrderQty = record.Order.OrderQty,
                Price = record.Order.Price,
                StopPx = record.Order.StopPx,
                LastPx = lastPx,
                LastQty = lastQty
            };
            if (ordStatus == OrdStatus.Undefined)
            {
                record.AddFill(lastPx, lastQty);
                if (record.LeavesQty > 0)
                {
                    ordStatus = OrdStatus.PartiallyFilled;
                }
                else
                {
                    ordStatus = OrdStatus.Filled;
                }
            }
            report.AvgPx = record.AvgPx;
            report.CumQty = record.CumQty;
            report.LeavesQty = record.LeavesQty;
            report.ExecType = CTPProvider.GetExecType(ordStatus);
            report.OrdStatus = ordStatus;
            report.Text = text;

            EmitExecutionReport(report);
        }

        protected void EmitAccepted(SingleOrder order)
        {
            EmitExecutionReport(order, OrdStatus.New);
        }

        protected void EmitCancelled(SingleOrder order)
        {
            EmitExecutionReport(order, OrdStatus.Cancelled);
        }

        protected void EmitExpired(SingleOrder order)
        {
            EmitExecutionReport(order, OrdStatus.Expired);
        }

        protected void EmitCancelReject(SingleOrder order, OrdStatus status, string message)
        {
            OrderCancelReject reject  = new OrderCancelReject
            {
                TransactTime = Clock.Now,
                ClOrdID = order.ClOrdID,
                OrigClOrdID = order.ClOrdID,
                OrderID = order.OrderID,

                CxlRejReason = CxlRejReason.BrokerOption,
                CxlRejResponseTo = CxlRejResponseTo.CancelRequest,
                OrdStatus = status
            };

            EmitOrderCancelReject(reject);
        }

        protected void EmitFilled(SingleOrder order, double price, int quantity)
        {
            EmitExecutionReport(order, price, quantity);
        }

        protected void EmitRejected(SingleOrder order, string message)
        {
            EmitExecutionReport(order, OrdStatus.Rejected, message);
        }

        private static ExecType GetExecType(OrdStatus status)
        {
            switch (status)
            {
                case OrdStatus.New:
                    return ExecType.New;
                case OrdStatus.PartiallyFilled:
                    return ExecType.PartialFill;
                case OrdStatus.Filled:
                    return ExecType.Fill;
                case OrdStatus.Cancelled:
                    return ExecType.Cancelled;
                case OrdStatus.Replaced:
                    return ExecType.Replace;
                case OrdStatus.PendingCancel:
                    return ExecType.PendingCancel;
                case OrdStatus.Rejected:
                    return ExecType.Rejected;
                case OrdStatus.PendingReplace:
                    return ExecType.PendingReplace;
                case OrdStatus.Expired:
                    return ExecType.Expired;
            }
            throw new ArgumentException(string.Format("Cannot find exec type for ord status - {0}", status));
        }

        #region OpenQuant3接口中的新方法
        public void RegisterOrder(NewOrderSingle order)
        {
            tdlog.Info("RegisterOrder");
        }
        #endregion
    }
}
