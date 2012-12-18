using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;
using QuantBox.CSharp2CTP;
using SmartQuant;
using System.Drawing.Design;
using System.Collections.Generic;

namespace QuantBox.OQ.CTP
{
    partial class QBProvider
    {
        private const string CATEGORY_ACCOUNT = "Account";
        private const string CATEGORY_BARFACTORY = "Bar Factory";
        private const string CATEGORY_DEBUG = "Debug";
        private const string CATEGORY_EXECUTION = "Settings - Execution";
        private const string CATEGORY_HISTORICAL = "Settings - Historical Data";
        private const string CATEGORY_INFO = "Information";
        private const string CATEGORY_NETWORK = "Settings - Network";
        private const string CATEGORY_STATUS = "Status";

        //交易所常量定义
        private enum ExchangID
        {
            SHFE,
            DCE,
            CZCE,
            CFFEX
        }

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

        private const string OpenPrefix = "O|";
        private const string ClosePrefix = "C|";
        private const string CloseTodayPrefix = "T|";
        private const string CloseYesterdayPrefix = "Y|";

        #region 参数设置
        private TimeMode _TimeMode;
        private string _SupportMarketOrder;
        private string _SupportCloseToday;
        private string _DefaultOpenClosePrefix;

        [Category("Settings - Other")]
        [Description("设置API生成临时文件的目录")]
        [Editor(typeof(System.Windows.Forms.Design.FolderNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [Browsable(false)]
        public string ApiTempPath { get; set; }

        [Category("Settings - Time")]
        [Description("警告！仅保存行情数据时才用交易所时间。交易时使用交易所时间将导致Bar生成错误")]
        [DefaultValue(TimeMode.LocalTime)]
        public TimeMode DateTimeMode
        {
            get { return _TimeMode; }
            set { _TimeMode = value; }
        }

        [Category("Settings - Time")]
        [Description("修改本地时间。分别是：不修改、登录交易前置机时间、各大交易所时间。以管理员方式运行才有权限")]
        [DefaultValue(SetTimeMode.None)]
        public SetTimeMode SetLocalTimeMode
        {
            get;
            set;
        }

        [Category("Settings - Time")]
        [DefaultValue(0)]
        [Description("修改本地时间时，在取到的时间上添加指定毫秒")]
        public int AddMilliseconds
        {
            get;
            set;
        }

        [Category("Settings - Other")]
        [Description("设置登录后是否接收完整的报单和成交记录")]
        [DefaultValue(THOST_TE_RESUME_TYPE.THOST_TERT_QUICK)]
        public THOST_TE_RESUME_TYPE ResumeType { get; set; }

        [Category("Settings - Order")]
        [Description("设置投机套保标志。Speculation:投机、Arbitrage套利、Hedge套保")]
        [DefaultValue(TThostFtdcHedgeFlagType.Speculation)]
        public TThostFtdcHedgeFlagType HedgeFlagType
        {
            get;
            set;
        }

        [Category("Settings - Order")]
        [Description("支持市价单的交易所")]
        public string SupportMarketOrder
        {
            get { return _SupportMarketOrder; }
        }


        [Category("Settings - Order")]
        [Description("区分平今与平昨的交易所")]
        public string SupportCloseToday
        {
            get { return _SupportCloseToday; }
        }

        [Category("Settings - Order")]
        [Description("指定开平，利用Order的Text域开始部分指定开平，“O|”开仓；“C|”智能平仓；“T|”平今仓；“Y|”平昨仓；")]
        public string DefaultOpenClosePrefix
        {
            get { return _DefaultOpenClosePrefix; }
        }

        [Category("Settings - Order")]
        [Description("在最新价上调整N跳来模拟市价，超过涨跌停价按涨跌停价报")]
        [DefaultValue(10)]
        public int LastPricePlusNTicks
        {
            get;
            set;
        }

        private BindingList<ServerItem> serversList = new BindingList<ServerItem>();
        [CategoryAttribute("Settings")]
        [Description("服务器信息，只选择第一条登录")]
        public BindingList<ServerItem> Server
        {
            get { return serversList; }
            set { serversList = value; }
        }

        private BindingList<AccountItem> accountsList = new BindingList<AccountItem>();
        [CategoryAttribute("Settings")]
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

        [CategoryAttribute("Settings")]
        [Description("连接到行情。此插件不连接行情时底层对不支持市价的报单不会做涨跌停修正，需策略层处理")]
        [DefaultValue(true)]
        public bool ConnectToMarketData
        {
            get { return _bWantMdConnect; }
            set { _bWantMdConnect = value; }
        }

        [CategoryAttribute("Settings")]
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
            ApiTempPath = Framework.Installation.TempDir.FullName;
            ResumeType = THOST_TE_RESUME_TYPE.THOST_TERT_QUICK;
            HedgeFlagType = TThostFtdcHedgeFlagType.Speculation;

            _bWantMdConnect = true;
            _bWantTdConnect = true;

            _SupportMarketOrder = String.Format("{0};{1};{2};", ExchangID.DCE, ExchangID.CZCE, ExchangID.CFFEX);
            _SupportCloseToday = ExchangID.SHFE + ";";
            _DefaultOpenClosePrefix = String.Format("{0};{1};{2};{3}", OpenPrefix, ClosePrefix, CloseTodayPrefix, CloseYesterdayPrefix);
            LastPricePlusNTicks = 10;

            serversList.ListChanged += ServersList_ListChanged;
            accountsList.ListChanged += AccountsList_ListChanged;

            LoadAccounts();
            LoadServers();
        }

        void ServersList_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded) {
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

        private readonly System.Timers.Timer timerSettingsChanged = new System.Timers.Timer(10000);
        void SettingsChanged()
        {
            //发现会多次触发，想法减少频率才好
            if (false == timerSettingsChanged.Enabled)
            {
                timerSettingsChanged.Elapsed += timerSettingsChanged_Elapsed;
                timerSettingsChanged.AutoReset = false;
            }
            //将上次已经开始的停掉
            timerSettingsChanged.Enabled = false;
            timerSettingsChanged.Enabled = true;
        }

        void timerSettingsChanged_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SaveAccounts();
            SaveServers();

            timerSettingsChanged.Elapsed -= timerSettingsChanged_Elapsed;
        }

        private readonly string accountsFile = string.Format(@"{0}\CTP.Accounts.xml", Framework.Installation.IniDir);
        void LoadAccounts()
        {
            accountsList.Clear();

            try
            {
                var accounts = from c in XElement.Load(accountsFile).Elements("Account")
                               select c;
                foreach (var account in accounts)
                {
                    AccountItem ai = new AccountItem() {
                        Label = account.Attribute("Label").Value,
                        InvestorId = account.Attribute("InvestorId").Value,
                        Password = account.Attribute("Password").Value
                    };
                    accountsList.Add(ai);
                }
            }
            catch (Exception)
            {
            }
        }

        void SaveAccounts()
        {
            XElement root = new XElement("Accounts");
            foreach (var account in accountsList)
            {
                XElement acc = new XElement("Account");
                acc.SetAttributeValue("Label", string.IsNullOrEmpty(account.Label) ? "" : account.Label);
                acc.SetAttributeValue("InvestorId", string.IsNullOrEmpty(account.InvestorId) ? "" : account.InvestorId);
                acc.SetAttributeValue("Password", string.IsNullOrEmpty(account.Password) ? "" : account.Password);
                root.Add(acc);
            }
            root.Save(accountsFile);
        }

        private readonly string serversFile = string.Format(@"{0}\CTP.Servers.xml", Framework.Installation.IniDir);
        void LoadServers()
        {
            serversList.Clear();

            try
            {
                var servers = from c in XElement.Load(serversFile).Elements("Server")
                          select c;

                serversList = ParseServers(servers);
            }
            catch (Exception)
            {
            }
        }

        BindingList<ServerItem> ParseServers(IEnumerable<XElement> servers)
        {
            BindingList<ServerItem> serversList = new BindingList<ServerItem>();

            foreach (var server in servers)
            {
                ServerItem si = new ServerItem()
                {
                    Label = server.Attribute("Label").Value,
                    BrokerID = server.Attribute("BrokerID").Value,
                    UserProductInfo = server.Attribute("UserProductInfo").Value,
                    AuthCode = server.Attribute("AuthCode").Value
                };

                string[] tdarr = server.Attribute("Trading").Value.Split(';');
                foreach (string s in tdarr)
                {
                    if (!string.IsNullOrEmpty(s))
                        si.Trading.Add(s);
                }

                string[] mdarr = server.Attribute("MarketData").Value.Split(';');
                foreach (string s in mdarr)
                {
                    if (!string.IsNullOrEmpty(s))
                        si.MarketData.Add(s);
                }

                serversList.Add(si);
            }

            return serversList;
        }

        void SaveServers()
        {
            XElement root = new XElement("Servers");
            foreach (var server in serversList)
            {
                XElement ser = new XElement("Server");
                ser.SetAttributeValue("Label", string.IsNullOrEmpty(server.Label) ? "" : server.Label);
                ser.SetAttributeValue("BrokerID", string.IsNullOrEmpty(server.BrokerID) ? "" : server.BrokerID);
                ser.SetAttributeValue("UserProductInfo", string.IsNullOrEmpty(server.UserProductInfo) ? "" : server.UserProductInfo);
                ser.SetAttributeValue("AuthCode", string.IsNullOrEmpty(server.AuthCode) ? "" : server.AuthCode);

                string tdstr = string.Join(";", server.Trading.ToArray());
                ser.SetAttributeValue("Trading", string.IsNullOrEmpty(tdstr) ? "" : tdstr);

                string mdstr = string.Join(";", server.MarketData.ToArray());
                ser.SetAttributeValue("MarketData", string.IsNullOrEmpty(mdstr) ? "" : mdstr);

                root.Add(ser);
            }
            root.Save(serversFile);
        }

        
        private readonly string brokersFile = string.Format(@"{0}\CTP.Brokers.xml", Framework.Installation.IniDir);
        public void LoadBrokers()
        {
            brokersList.Clear();

            try
            {
                var brokers = from c in XElement.Load(brokersFile).Elements("Broker")
                              select c;

                foreach (var broker in brokers)
                {
                    BrokerItem bi = new BrokerItem()
                    {
                        Label = broker.Attribute("Label").Value
                    };

                    bi.Server = ParseServers(broker.Elements("Server"));

                    brokersList.Add(bi);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
