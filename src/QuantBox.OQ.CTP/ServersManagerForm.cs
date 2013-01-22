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

namespace QuantBox.OQ.CTP
{
    public partial class ServersManagerForm : Form
    {
        private CTPProvider provider;
        public ServersManagerForm()
        {
            InitializeComponent();
        }

        public void Init(CTPProvider provider)
        {
            this.provider = provider;
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

                string fileName = string.Format(@"{0}\CTP.Brokers.xml", Framework.Installation.IniDir);
                wc.DownloadFile(textBoxUrl.Text, fileName);

                provider.LoadBrokers();

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
