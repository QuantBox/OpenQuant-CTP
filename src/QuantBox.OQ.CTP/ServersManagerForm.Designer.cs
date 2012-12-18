namespace QuantBox.OQ.CTP
{
    partial class ServersManagerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.listBoxBroker = new System.Windows.Forms.ListBox();
            this.brokerItemBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.listBoxServer = new System.Windows.Forms.ListBox();
            this.serverItemBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.textBoxUrl = new System.Windows.Forms.TextBox();
            this.buttonUpdate = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.brokerItemBindingSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.serverItemBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // listBoxBroker
            // 
            this.listBoxBroker.DataSource = this.brokerItemBindingSource;
            this.listBoxBroker.DisplayMember = "Label";
            this.listBoxBroker.FormattingEnabled = true;
            this.listBoxBroker.Location = new System.Drawing.Point(12, 50);
            this.listBoxBroker.Name = "listBoxBroker";
            this.listBoxBroker.Size = new System.Drawing.Size(173, 264);
            this.listBoxBroker.TabIndex = 0;
            // 
            // brokerItemBindingSource
            // 
            this.brokerItemBindingSource.DataSource = typeof(QuantBox.OQ.CTP.BrokerItem);
            // 
            // listBoxServer
            // 
            this.listBoxServer.DataSource = this.serverItemBindingSource;
            this.listBoxServer.DisplayMember = "Label";
            this.listBoxServer.FormattingEnabled = true;
            this.listBoxServer.Location = new System.Drawing.Point(246, 50);
            this.listBoxServer.Name = "listBoxServer";
            this.listBoxServer.Size = new System.Drawing.Size(171, 264);
            this.listBoxServer.TabIndex = 0;
            // 
            // serverItemBindingSource
            // 
            this.serverItemBindingSource.DataSource = typeof(QuantBox.OQ.CTP.ServerItem);
            // 
            // buttonAdd
            // 
            this.buttonAdd.Location = new System.Drawing.Point(191, 50);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(49, 23);
            this.buttonAdd.TabIndex = 1;
            this.buttonAdd.Text = "=>";
            this.buttonAdd.UseVisualStyleBackColor = true;
            this.buttonAdd.Click += new System.EventHandler(this.buttonAdd_Click);
            // 
            // buttonRemove
            // 
            this.buttonRemove.Location = new System.Drawing.Point(191, 79);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(49, 23);
            this.buttonRemove.TabIndex = 1;
            this.buttonRemove.Text = "<-";
            this.buttonRemove.UseVisualStyleBackColor = true;
            this.buttonRemove.Click += new System.EventHandler(this.buttonRemove_Click);
            // 
            // textBoxUrl
            // 
            this.textBoxUrl.Location = new System.Drawing.Point(12, 12);
            this.textBoxUrl.Name = "textBoxUrl";
            this.textBoxUrl.Size = new System.Drawing.Size(319, 20);
            this.textBoxUrl.TabIndex = 2;
            this.textBoxUrl.Text = "https://github.com/QuantBox/OpenQuant-CTP/raw/master/CTP.Brokers.xml";
            // 
            // buttonUpdate
            // 
            this.buttonUpdate.Location = new System.Drawing.Point(344, 10);
            this.buttonUpdate.Name = "buttonUpdate";
            this.buttonUpdate.Size = new System.Drawing.Size(75, 23);
            this.buttonUpdate.TabIndex = 3;
            this.buttonUpdate.Text = "在线更新";
            this.buttonUpdate.UseVisualStyleBackColor = true;
            this.buttonUpdate.Click += new System.EventHandler(this.buttonUpdate_Click);
            // 
            // ServersManagerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(431, 334);
            this.Controls.Add(this.buttonUpdate);
            this.Controls.Add(this.textBoxUrl);
            this.Controls.Add(this.buttonRemove);
            this.Controls.Add(this.buttonAdd);
            this.Controls.Add(this.listBoxServer);
            this.Controls.Add(this.listBoxBroker);
            this.Name = "ServersManagerForm";
            this.Text = "服务器地址列表";
            this.Load += new System.EventHandler(this.ServersManagerForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.brokerItemBindingSource)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.serverItemBindingSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxBroker;
        private System.Windows.Forms.ListBox listBoxServer;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.BindingSource serverItemBindingSource;
        private System.Windows.Forms.BindingSource brokerItemBindingSource;
        private System.Windows.Forms.TextBox textBoxUrl;
        private System.Windows.Forms.Button buttonUpdate;
    }
}