using System;
using System.ComponentModel;
using System.Reflection;
using SmartQuant;
using SmartQuant.Providers;
using System.IO;

namespace QuantBox.OQ.CTP
{
    public partial class QBProvider : IProvider
    {
        private ProviderStatus status;
        private bool isConnected;

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event ProviderErrorEventHandler Error;

        private bool disposed;

        private static log4net.ILog mdlog = log4net.LogManager.GetLogger("M");
        private static log4net.ILog tdlog = log4net.LogManager.GetLogger("T");

        public QBProvider()
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(@"Bin/CTP.log4net.config"));

            timerDisconnect.Elapsed += timerDisconnect_Elapsed;
            timerAccount.Elapsed += timerAccount_Elapsed;
            timerPonstion.Elapsed += timerPonstion_Elapsed;

            InitCallbacks();
            InitSettings();

            BarFactory = new SmartQuant.Providers.BarFactory();
            status = ProviderStatus.Unknown;
            SmartQuant.Providers.ProviderManager.Add(this);
        }

        //Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).
                }
                // Free your own state (unmanaged objects).
                // Set large fields to null.
                Shutdown();
                disposed = true;
            }
            //base.Dispose(disposing);
        }

        // Use C# destructor syntax for finalization code.
        ~QBProvider()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        #region IProvider
        [Category(CATEGORY_INFO)]
        public byte Id
        {
            get { return 55; }//不能与已经安装的插件ID重复
        }

        [Category(CATEGORY_INFO)]
        public string Name
        {
            get { return "CTP"; }//不能与已经安装的插件Name重复
        }

        [Category(CATEGORY_INFO)]
        public string Title
        {
            get { return "QuantBox CTP Provider"; }
        }

        [Category(CATEGORY_INFO)]
        public string URL
        {
            get { return "www.quantbox.cn"; }
        }

        [Category(CATEGORY_INFO)]
        [Description("插件版本信息")]
        public static string Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        [Category(CATEGORY_STATUS)]
        public bool IsConnected
        {
            get { return isConnected; }
        }

        [Category(CATEGORY_STATUS)]
        public ProviderStatus Status
        {
            get { return status; }
        }

        public void Connect(int timeout)
        {
            Connect();
            ProviderManager.WaitConnected(this, timeout);
        }

        public void Connect()
        {
            _Connect();
        }

        public void Disconnect()
        {
            _Disconnect();
        }

        public void Shutdown()
        {
            Disconnect();
            //特殊的地方,有可能改动了配置就直接关了，还没等保存，所以这地方得保存下
            if (timerSettingsChanged.Enabled)
            {
                SaveAccounts();
                SaveServers();
            }

            timerDisconnect.Elapsed -= timerDisconnect_Elapsed;
            timerAccount.Elapsed -= timerAccount_Elapsed;
            timerPonstion.Elapsed -= timerPonstion_Elapsed;
        }
        
        public event EventHandler StatusChanged;

        private void ChangeStatus(ProviderStatus status)
        {
            this.status = status;
            EmitStatusChangedEvent();
        }

        private void EmitStatusChangedEvent()
        {
            if (StatusChanged != null)
            {
                StatusChanged(this, EventArgs.Empty);
            }
        }

        private void EmitConnectedEvent()
        {
            if (Connected != null)
            {
                Connected(this, EventArgs.Empty);
            }
        }

        private void EmitDisconnectedEvent()
        {
            if (Disconnected != null)
            {
                Disconnected(this, EventArgs.Empty);
            }
        }

        private void EmitError(int id, int code, string message)
        {
            if (Error != null)
                Error(new ProviderErrorEventArgs(new ProviderError(Clock.Now, this, id, code, message)));
        }
        #endregion
    }
}
