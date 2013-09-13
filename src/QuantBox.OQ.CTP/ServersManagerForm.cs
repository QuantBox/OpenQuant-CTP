using SmartQuant;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using QuantBox.OQ.CTP;

#if CTP
namespace QuantBox.OQ.CTP
#elif CTPZQ
namespace QuantBox.OQ.CTPZQ
#endif
{
    public partial class ServersManagerForm : Form
    {
        private APIProvider provider;
        public void Init(APIProvider provider)     
        {
            this.provider = provider;

            textBoxUrl.Text = string.Format(@"https://raw.github.com/QuantBox/OpenQuant-CTP/master/{0}.Brokers.xml", provider.Name);
        }

        public ServersManagerForm()
        {
            InitializeComponent();           
        }

        private void ServersManagerForm_Load(object sender, EventArgs e)
        {
            provider.LoadBrokers();

            serverItemBindingSource.DataSource = provider.Server;
            brokerItemBindingSource.DataSource = provider.Brokers;
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            int nSel = listBoxServer.SelectedIndex;
            if (nSel >= 0)
            {
                provider.Server.RemoveAt(nSel);
                provider.SettingsChanged();
            }
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            int nSel = listBoxBroker.SelectedIndex;
            if (nSel >= 0)
            {
                BrokerItem bi = provider.Brokers[nSel];
                foreach(ServerItem si in bi.Server)
                {
                    provider.Server.Add(si);
                }
                provider.SettingsChanged();
            }
        }
        
        private void buttonUpdate_Click(object sender, EventArgs e)
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            WebClient wc = new WebClient();
            try
            {
                buttonUpdate.Enabled = false;

                wc.DownloadFile(textBoxUrl.Text, provider.brokersFile);

                provider.LoadBrokers();
                brokerItemBindingSource.DataSource = provider.Brokers;

                MessageBox.Show("远程配置下载成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                wc.Dispose();
                buttonUpdate.Enabled = true;
            }
        }
    }
}
