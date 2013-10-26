using QuantBox.CSharp2CTP;
using QuantBox.OQ.Extensions.Combiner;
using SmartQuant.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuantBox.OQ.CTP
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

        public void Clear()
        {
            OrderRef_OrderItem.Clear();
            Order_OrderItem.Clear();
            OrderItem_OrderField.Clear();
            OrderSysID_OrderRef.Clear();
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

        // 通过界面撤单，找到其中的一腿即可
        public bool TryGetValue(SingleOrder key, out CThostFtdcOrderField value)
        {
            GenericOrderItem item;
            if(Order_OrderItem.TryGetValue(key,out item))
            {
                return OrderItem_OrderField.TryGetValue(item, out value);
            }
            value = new CThostFtdcOrderField();
            return false;
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
