using QuantBox.OQ.Extensions.OrderItem;
using SmartQuant.Execution;
using SmartQuant.FIX;
using System.Collections.Generic;

#if CTP
using QuantBox.CSharp2CTP;

namespace QuantBox.OQ.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;

namespace QuantBox.OQ.CTPZQ
#endif
{
    public class OrderMap
    {
        // API中的报单引用，一个Ref只对应一个报单组合
        public readonly Dictionary<string, GenericOrderItem> OrderRef_OrderItem = new Dictionary<string, GenericOrderItem>();
        // 记录界面报单到报单组合的关系
        public readonly Dictionary<SingleOrder, GenericOrderItem> Order_OrderItem = new Dictionary<SingleOrder, GenericOrderItem>();
        // 记录报单组合到报单回报的关系，用于撤单
        public readonly Dictionary<GenericOrderItem, CThostFtdcOrderField> OrderItem_OrderField = new Dictionary<GenericOrderItem, CThostFtdcOrderField>();
        // 交易所信息映射到本地信息
        public readonly Dictionary<string, string> OrderSysID_OrderRef = new Dictionary<string, string>();
        // 标记正在撤单
        public readonly Dictionary<SingleOrder, OrdStatus> Order_OrdStatus = new Dictionary<SingleOrder, OrdStatus>();

        public void Clear()
        {
            OrderRef_OrderItem.Clear();
            Order_OrderItem.Clear();
            OrderItem_OrderField.Clear();
            OrderSysID_OrderRef.Clear();
            Order_OrdStatus.Clear();
        }

        // 用于收到委托回报信息后进行处理
        public bool TryGetValue(string key,out GenericOrderItem value)
        {
            return OrderRef_OrderItem.TryGetValue(key, out value);
        }

        // 用于收到成交回报信息后找到原始的报单引用
        public bool TryGetValue(string key, out string value)
        {
            return OrderSysID_OrderRef.TryGetValue(key, out value);
        }

        // 界面撤单时用于找到对应的报单组合
        public bool TryGetValue(SingleOrder key, out GenericOrderItem value)
        {
            return Order_OrderItem.TryGetValue(key, out value);
        }

        // 通过报单组合，实际的向交易所撤单
        public bool TryGetValue(GenericOrderItem key, out CThostFtdcOrderField value)
        {
            return OrderItem_OrderField.TryGetValue(key, out value);
        }

        // 返回状态
        public bool TryGetValue(SingleOrder key, out OrdStatus value)
        {
            return Order_OrdStatus.TryGetValue(key, out value);
        }

        public void CreateNewOrder(string key,GenericOrderItem value)
        {
            OrderRef_OrderItem[key] = value;
        }

        // 通过一个报单引用，把相关的单子全删了
        public void Remove(string key)
        {

        }
    }
}
