using System;
using System.Collections.Generic;
using System.Data;
using SmartQuant;
using SmartQuant.Execution;
using SmartQuant.FIX;
using SmartQuant.Providers;
using System.Reflection;

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
    public partial class APIProvider : IExecutionProvider
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
