using NLog;
using QuantBox.CSharp2CTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuantBox.OQ.CTP
{
    public class ApiWrapper
    {
        public Logger Log;

        public fnOnConnect _fnOnConnect_Holder;
        public fnOnDisconnect _fnOnDisconnect_Holder;
        public fnOnRspError _fnOnRspError_Holder;

        public ServerItem server;
        public AccountItem account;

        protected string tempPath;

        protected readonly object _lock = new object();
        private readonly object _lockMsgQueue = new object();

        protected IntPtr m_pMsgQueue = IntPtr.Zero;
        protected IntPtr Api = IntPtr.Zero;

        public volatile bool IsConnected;

        protected void Connect_MsgQueue()
        {
            lock (_lockMsgQueue)
            {
                if (null == m_pMsgQueue || IntPtr.Zero == m_pMsgQueue)
                {
                    m_pMsgQueue = CommApi.CTP_CreateMsgQueue();

                    CommApi.CTP_RegOnConnect(m_pMsgQueue, _fnOnConnect_Holder);
                    CommApi.CTP_RegOnDisconnect(m_pMsgQueue, _fnOnDisconnect_Holder);
                    CommApi.CTP_RegOnRspError(m_pMsgQueue, _fnOnRspError_Holder);

                    CommApi.CTP_StartMsgQueue(m_pMsgQueue);
                }
            }
        }

        protected void Disconnect_MsgQueue()
        {
            lock (_lockMsgQueue)
            {
                if (null != m_pMsgQueue && IntPtr.Zero != m_pMsgQueue)
                {
                    CommApi.CTP_StopMsgQueue(m_pMsgQueue);

                    CommApi.CTP_ReleaseMsgQueue(m_pMsgQueue);
                    m_pMsgQueue = IntPtr.Zero;
                }
            }
        }
    }
}
