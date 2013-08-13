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
    public class TraderApiWrapper:ApiWrapper
    {
        public fnOnErrRtnOrderAction _fnOnErrRtnOrderAction_Holder;
        public fnOnErrRtnOrderInsert _fnOnErrRtnOrderInsert_Holder;
        public fnOnRspOrderAction _fnOnRspOrderAction_Holder;
        public fnOnRspOrderInsert _fnOnRspOrderInsert_Holder;
        public fnOnRspQryDepthMarketData _fnOnRspQryDepthMarketData_Holder;
        public fnOnRspQryInstrument _fnOnRspQryInstrument_Holder;
        public fnOnRspQryInstrumentCommissionRate _fnOnRspQryInstrumentCommissionRate_Holder;
        public fnOnRspQryInvestorPosition _fnOnRspQryInvestorPosition_Holder;
        public fnOnRspQryTradingAccount _fnOnRspQryTradingAccount_Holder;
        public fnOnRtnInstrumentStatus _fnOnRtnInstrumentStatus_Holder;
        public fnOnRtnOrder _fnOnRtnOrder_Holder;
        public fnOnRtnTrade _fnOnRtnTrade_Holder;

#if CTP
        public fnOnRspQryInstrumentMarginRate _fnOnRspQryInstrumentMarginRate_Holder;
#endif

        private THOST_TE_RESUME_TYPE ResumeType;

        public void Connect(ServerItem server, AccountItem account,THOST_TE_RESUME_TYPE resumeType)
        {
            tempPath = Framework.Installation.TempDir.FullName + Path.DirectorySeparatorChar + server.BrokerID + Path.DirectorySeparatorChar + account.InvestorId;
            Directory.CreateDirectory(tempPath);
            ResumeType = resumeType;

            Disconnect_TD();

            Connect_MsgQueue();
            Connect_TD();
        }

        public void Disconnect()
        {
            Disconnect_TD();
            Disconnect_MsgQueue();
        }

        //建立交易
        private void Connect_TD()
        {
            lock (_lock)
            {
                if (null == Api || IntPtr.Zero == Api)
                {
                    Api = TraderApi.TD_CreateTdApi();
                    TraderApi.CTP_RegOnErrRtnOrderAction(m_pMsgQueue, _fnOnErrRtnOrderAction_Holder);
                    TraderApi.CTP_RegOnErrRtnOrderInsert(m_pMsgQueue, _fnOnErrRtnOrderInsert_Holder);
                    TraderApi.CTP_RegOnRspOrderAction(m_pMsgQueue, _fnOnRspOrderAction_Holder);
                    TraderApi.CTP_RegOnRspOrderInsert(m_pMsgQueue, _fnOnRspOrderInsert_Holder);
                    TraderApi.CTP_RegOnRspQryDepthMarketData(m_pMsgQueue, _fnOnRspQryDepthMarketData_Holder);
                    TraderApi.CTP_RegOnRspQryInstrument(m_pMsgQueue, _fnOnRspQryInstrument_Holder);
                    TraderApi.CTP_RegOnRspQryInstrumentCommissionRate(m_pMsgQueue, _fnOnRspQryInstrumentCommissionRate_Holder);
                    TraderApi.CTP_RegOnRspQryInvestorPosition(m_pMsgQueue, _fnOnRspQryInvestorPosition_Holder);
                    TraderApi.CTP_RegOnRspQryTradingAccount(m_pMsgQueue, _fnOnRspQryTradingAccount_Holder);
                    TraderApi.CTP_RegOnRtnInstrumentStatus(m_pMsgQueue, _fnOnRtnInstrumentStatus_Holder);
                    TraderApi.CTP_RegOnRtnOrder(m_pMsgQueue, _fnOnRtnOrder_Holder);
                    TraderApi.CTP_RegOnRtnTrade(m_pMsgQueue, _fnOnRtnTrade_Holder);
#if CTP
                    TraderApi.CTP_RegOnRspQryInstrumentMarginRate(m_pMsgQueue, _fnOnRspQryInstrumentMarginRate_Holder);
#endif
                    TraderApi.TD_RegMsgQueue2TdApi(Api, m_pMsgQueue);
                    TraderApi.TD_Connect(Api, tempPath, string.Join(";", server.Trading.ToArray()),
                        server.BrokerID, account.InvestorId, account.Password,
                        ResumeType,
                        server.UserProductInfo, server.AuthCode);

                    //向单例对象中注入操作用句柄
                    //CTPAPI.GetInstance().__RegTdApi(m_pTdApi);
                }
            }
        }

        private void Disconnect_TD()
        {
            lock (_lock)
            {
                if (null != Api && IntPtr.Zero != Api)
                {
                    TraderApi.TD_RegMsgQueue2TdApi(Api, IntPtr.Zero);
                    TraderApi.TD_ReleaseTdApi(Api);
                    Api = IntPtr.Zero;

                    //CTPAPI.GetInstance().__RegTdApi(m_pTdApi);
                }
                IsConnected = false;
            }
        }
    }
}
