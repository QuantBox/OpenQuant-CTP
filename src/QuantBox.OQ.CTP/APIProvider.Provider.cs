using NLog;
using NLog.Config;
using SmartQuant;
using SmartQuant.Providers;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

#if CTP
using QuantBox.CSharp2CTP;
using QuantBox.Helper.CTP;
using System.Collections.Generic;

namespace QuantBox.OQ.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;
using QuantBox.Helper.CTPZQ;

namespace QuantBox.OQ.CTPZQ
#endif
{
    public partial class APIProvider : IProvider, IDisposable
    {
        private byte _Id;
        private string _Name;

        private ProviderStatus status;
        private bool isConnected;

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event ProviderErrorEventHandler Error;

        private bool disposed;

        private static Logger mdlog;
        private static Logger tdlog;
        
        // Use C# destructor syntax for finalization code.
        ~APIProvider()
        {
            // Simply call Dispose(false).
            Dispose(false);
        }

        static bool bDoOnce = true;
        public APIProvider()
        {
#if CTP
            if (bDoOnce)
            {
                Init(55, "CTP");
                bDoOnce = false;

                // 在QR中用多Provider变相实现多账号功能
                new APIProvider().Init(100, "CTP100");
                new APIProvider().Init(101, "CTP101");
                new APIProvider().Init(102, "CTP102");
                new APIProvider().Init(103, "CTP103");
                new APIProvider().Init(104, "CTP104");
                new APIProvider().Init(105, "CTP105");
            }
#elif CTPZQ
            Init(56,"CTPZQ");
#endif
        }

        public void Init(byte Id,string Name)
        {
            _Id = Id;
            _Name = Name;

            mdlog = LogManager.GetLogger(Name + ".M");
            tdlog = LogManager.GetLogger(Name + ".T");

            try
            {
                LogManager.Configuration = new XmlLoggingConfiguration(@"Bin/QuantBox.nlog");
            }
            catch (Exception ex)
            {
                tdlog.Warn(ex.Message);
            }

            timerConnect.Elapsed += timerConnect_Elapsed;
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

        

        #region IProvider
        [Category(CATEGORY_INFO)]
        public byte Id//不能与已经安装的插件ID重复
        {
            get { return _Id; }
            set { _Id = value; }
        }

        [Category(CATEGORY_INFO)]
        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        [Category(CATEGORY_INFO)]
        public string Title
        {
            get { return  string.Format("QuantBox {0} Provider",this.Name); }
        }

        [Category(CATEGORY_INFO)]
        public string URL
        {
            get { return "www.quantbox.cn"; }
        }

        [Category(CATEGORY_INFO)]
        [Description("插件版本信息")]
        public string Version
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

            SettingsChanged();

            timerConnect.Elapsed -= timerConnect_Elapsed;
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
