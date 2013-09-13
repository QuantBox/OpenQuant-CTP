using NLog;
using QuantBox.CSharp2CTP;
using SmartQuant;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace QuantBox.OQ.CTP
{
    public class QuoteApiWrapper : ApiWrapper
    {
        public fnOnRtnDepthMarketData _fnOnRtnDepthMarketData_Holder;

        public void Connect(ServerItem server, AccountItem account)
        {
            tempPath = Framework.Installation.TempDir.FullName + Path.DirectorySeparatorChar + server.BrokerID + Path.DirectorySeparatorChar + account.InvestorId;
            Directory.CreateDirectory(tempPath);

            Disconnect_MD();

            Connect_MsgQueue();
            Connect_MD();
        }

        public void Disconnect()
        {
            Disconnect_MD();
            Disconnect_MsgQueue();
        }

        private void Connect_MD()
        {
            lock (_lock)
            {
                if (null == Api || IntPtr.Zero == Api)
                {
                    Api = MdApi.MD_CreateMdApi();
                    MdApi.CTP_RegOnRtnDepthMarketData(m_pMsgQueue, _fnOnRtnDepthMarketData_Holder);
                    MdApi.MD_RegMsgQueue2MdApi(Api, m_pMsgQueue);
                    MdApi.MD_Connect(Api, tempPath, string.Join(";", server.MarketData.ToArray()), server.BrokerID, account.InvestorId, account.Password);
                }
            }
        }

        private void Disconnect_MD()
        {
            lock (_lock)
            {
                if (null != Api && IntPtr.Zero != Api)
                {
                    MdApi.MD_RegMsgQueue2MdApi(Api, IntPtr.Zero);
                    MdApi.MD_ReleaseMdApi(Api);
                    Api = IntPtr.Zero;
                }
                IsConnected = false;
            }
        }
    }
}
