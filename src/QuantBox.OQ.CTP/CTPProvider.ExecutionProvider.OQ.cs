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
        #region OpenQuant下的接口
        public void SendOrderCancelReplaceRequest(OrderCancelReplaceRequest request)
        {
            SendOrderCancelReplaceRequest(request as FIXOrderCancelReplaceRequest);
        }

        public void SendOrderCancelRequest(OrderCancelRequest request)
        {
            SendOrderCancelRequest(request as FIXOrderCancelRequest);
        }

        public void SendOrderStatusRequest(OrderStatusRequest request)
        {
            SendOrderStatusRequest(request as FIXOrderStatusRequest);
        }
        #endregion
    }
}
