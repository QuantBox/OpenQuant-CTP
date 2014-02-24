using SmartQuant;
using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Xml.Serialization;
using QuantBox.OQ.CTP;

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
        private const string CATEGORY_ACCOUNT = "Account";
        private const string CATEGORY_BARFACTORY = "Bar Factory";
        private const string CATEGORY_DEBUG = "Debug";
        private const string CATEGORY_EXECUTION = "Settings - Execution";
        private const string CATEGORY_HISTORICAL = "Settings - Historical Data";
        private const string CATEGORY_INFO = "Information";
        private const string CATEGORY_NETWORK = "Settings - Network";
        private const string CATEGORY_STATUS = "Status";
        private const string CATEGORY_TIME = "Settings - Time";
        private const string CATEGORY_OTHER = "Settings - Other";


        public string accountsFile;
        public string serversFile;
        public string brokersFile;

#if CTP
        //交易所常量定义
        private enum ExchangID
        {
            SHFE,
            DCE,
            CZCE,
            CFFEX
        }

        private string _SupportMarketOrder;
        private string _SupportCloseToday;

        [Category(CATEGORY_OTHER)]
        [Description("支持市价单的交易所")]
        public string SupportMarketOrder
        {
            get { return _SupportMarketOrder; }
        }


        [Category(CATEGORY_OTHER)]
        [Description("区分平今与平昨的交易所")]
        public string SupportCloseToday
        {
            get { return _SupportCloseToday; }
        }

#elif CTPZQ
        private enum ExchangID
        {
            SSE,
            SZE
        }
#endif

        public enum TimeMode
        {
            LocalTime,
            ExchangeTime
        }

        public enum SetTimeMode
        {
            None,
            LoginTime,
            SHFETime,
            DCETime,
            CZCETime,
            FFEXTime
        }

        #region 参数设置
        private TimeMode _TimeMode;

        [Category(CATEGORY_OTHER)]
        [Description("设置API生成临时文件的目录")]
        [Editor(typeof(System.Windows.Forms.Design.FolderNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [Browsable(false)]
        public string ApiTempPath { get; set; }

        [Category(CATEGORY_TIME)]
        [Description("警告！仅保存行情数据时才用交易所时间。交易时使用交易所时间将导致Bar生成错误")]
        [DefaultValue(TimeMode.LocalTime)]
        public TimeMode DateTimeMode
        {
            get { return _TimeMode; }
            set { _TimeMode = value; }
        }

        [Category(CATEGORY_TIME)]
        [Description("修改本地时间。分别是：不修改、登录交易前置机时间、各大交易所时间。以管理员方式运行才有权限")]
        [DefaultValue(SetTimeMode.None)]
        public SetTimeMode SetLocalTimeMode
        {
            get;
            set;
        }

        [Category(CATEGORY_TIME)]
        [DefaultValue(0)]
        [Description("修改本地时间时，在取到的时间上添加指定毫秒")]
        public int AddMilliseconds
        {
            get;
            set;
        }

        [Category(CATEGORY_OTHER)]
        [Description("设置登录后是否接收完整的报单和成交记录")]
        [DefaultValue(THOST_TE_RESUME_TYPE.THOST_TERT_QUICK)]
        public THOST_TE_RESUME_TYPE ResumeType { get; set; }

        [Category(CATEGORY_OTHER)]
        [Description("设置投机套保标志。\nSpeculation - 投机\nArbitrage - 套利\nHedge - 套保")]
        [DefaultValue(TThostFtdcHedgeFlagType.Speculation)]
        public TThostFtdcHedgeFlagType HedgeFlagType
        {
            get;
            set;
        }

        

        [Category(CATEGORY_OTHER)]
        [Description("在最新价上调整N跳来模拟市价，超过涨跌停价按涨跌停价报单")]
        [DefaultValue(10)]
        public int LastPricePlusNTicks
        {
            get;
            set;
        }

        [Category(CATEGORY_OTHER)]
        [Description("True - 所有市价单都用限价单来模拟\nFalse - 仅对上期所进行模拟")]
        [DefaultValue(false)]
        public bool SwitchMakertOrderToLimitOrder
        {
            get;
            set;
        }

        private BindingList<ServerItem> serversList = new BindingList<ServerItem>();
        [Category("Settings")]
        [Description("服务器信息，只选择第一条登录")]
        public BindingList<ServerItem> Server
        {
            get { return serversList; }
            set { serversList = value; }
        }

        private BindingList<AccountItem> accountsList = new BindingList<AccountItem>();
        [Category("Settings")]
        [Description("账号信息，只选择第一条登录")]
        public BindingList<AccountItem> Account
        {
            get { return accountsList; }
            set { accountsList = value; }
        }

        private BindingList<BrokerItem> brokersList = new BindingList<BrokerItem>();
        [Category("Settings"), Editor(typeof(ServersManagerTypeEditor), typeof(UITypeEditor)),
        Description("点击(...)查看经纪商列表")]
        public BindingList<BrokerItem> Brokers
        {
            get { return brokersList; }
            set { brokersList = value; }
        }

        [Category("Settings")]
        [Description("连接到行情。此插件不连接行情时底层对不支持市价的报单不会做涨跌停修正，需策略层处理")]
        [DefaultValue(true)]
        public bool ConnectToMarketData
        {
            get { return _bWantMdConnect; }
            set { _bWantMdConnect = value; }
        }

        [Category("Settings")]
        [Description("连接到交易")]
        [DefaultValue(true)]
        public bool ConnectToTrading
        {
            get { return _bWantTdConnect; }
            set { _bWantTdConnect = value; }
        }

        #endregion
        private void InitSettings()
        {
            accountsFile = string.Format(@"{0}\{1}.Accounts.xml", Framework.Installation.IniDir,this.Name);
            serversFile = string.Format(@"{0}\{1}.Servers.xml", Framework.Installation.IniDir, this.Name);
            brokersFile = string.Format(@"{0}\{1}.Brokers.xml", Framework.Installation.IniDir, this.Name);

            ApiTempPath = Framework.Installation.TempDir.FullName;
            ResumeType = THOST_TE_RESUME_TYPE.THOST_TERT_QUICK;
            HedgeFlagType = TThostFtdcHedgeFlagType.Speculation;
            SwitchMakertOrderToLimitOrder = false;

            _bWantMdConnect = true;
            _bWantTdConnect = true;

#if CTP
            _SupportMarketOrder = String.Format("{0};{1};{2};", ExchangID.DCE, ExchangID.CZCE, ExchangID.CFFEX);
            _SupportCloseToday = ExchangID.SHFE + ";";
#elif CTPZQ

#endif
            LastPricePlusNTicks = 10;

            LoadAccounts();
            LoadServers();

            serversList.ListChanged += ServersList_ListChanged;
            accountsList.ListChanged += AccountsList_ListChanged;
        }

        void ServersList_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                serversList[e.NewIndex].Changed += ServerItem_ListChanged;
            }
            SettingsChanged();
        }

        void AccountsList_ListChanged(object sender, EventArgs e)
        {
            SettingsChanged();
        }

        void ServerItem_ListChanged(object sender, EventArgs e)
        {
            SettingsChanged();
        }

        public void SettingsChanged()
        {
            SaveAccounts();
            SaveServers();
        }


        void LoadAccounts()
        {
            accountsList.Clear();

            try
            {
                XmlSerializer serializer = new XmlSerializer(accountsList.GetType());
                using (FileStream stream = new FileStream(accountsFile, FileMode.Open))
                {
                    accountsList = (BindingList<AccountItem>)serializer.Deserialize(stream);
                    stream.Close();
                }
            }
            catch (Exception)
            {
            }
        }

        void SaveAccounts()
        {
            XmlSerializer serializer = new XmlSerializer(accountsList.GetType());
            using (TextWriter writer = new StreamWriter(accountsFile))
            {
                serializer.Serialize(writer, accountsList);
                writer.Close();
            }
        }
                
        void LoadServers()
        {
            serversList.Clear();

            try
            {
                XmlSerializer serializer = new XmlSerializer(serversList.GetType());
                using (FileStream stream = new FileStream(serversFile, FileMode.Open))
                {
                    serversList = (BindingList<ServerItem>)serializer.Deserialize(stream);
                    stream.Close();
                }
            }
            catch (Exception)
            {
            }
        }

        void SaveServers()
        {
            XmlSerializer serializer = new XmlSerializer(serversList.GetType());
            using (TextWriter writer = new StreamWriter(serversFile))
            {
                serializer.Serialize(writer, serversList);
                writer.Close();
            }
        }
                
        public void LoadBrokers()
        {
            brokersList.Clear();

            try
            {
                XmlSerializer serializer = new XmlSerializer(brokersList.GetType());
                using (FileStream stream = new FileStream(brokersFile, FileMode.Open))
                {
                    brokersList = (BindingList<BrokerItem>)serializer.Deserialize(stream);
                    stream.Close();
                }
            }
            catch (Exception)
            {
            }
        }

        void SaveBrokers()
        {
            XmlSerializer serializer = new XmlSerializer(brokersList.GetType());
            using (TextWriter writer = new StreamWriter(brokersFile))
            {
                serializer.Serialize(writer, brokersList);
                writer.Close();
            }
        }
    }
}
