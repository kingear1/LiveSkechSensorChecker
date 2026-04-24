namespace LiveSkechSensorChecker
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            statusLabel = new Label();
            logTextBox = new TextBox();
            peerGrid = new DataGridView();
            pcName = new DataGridViewTextBoxColumn();
            role = new DataGridViewTextBoxColumn();
            process = new DataGridViewTextBoxColumn();
            isRunning = new DataGridViewTextBoxColumn();
            lastHeartbeat = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)peerGrid).BeginInit();
            SuspendLayout();
            // 
            // statusLabel
            // 
            statusLabel.AutoSize = true;
            statusLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            statusLabel.Location = new Point(16, 15);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(150, 20);
            statusLabel.TabIndex = 0;
            statusLabel.Text = "상태 초기화 중...";
            // 
            // logTextBox
            // 
            logTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logTextBox.Location = new Point(16, 315);
            logTextBox.Multiline = true;
            logTextBox.Name = "logTextBox";
            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Size = new Size(946, 183);
            logTextBox.TabIndex = 2;
            // 
            // peerGrid
            // 
            peerGrid.AllowUserToAddRows = false;
            peerGrid.AllowUserToDeleteRows = false;
            peerGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            peerGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            peerGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            peerGrid.Columns.AddRange(new DataGridViewColumn[] { pcName, role, process, isRunning, lastHeartbeat });
            peerGrid.Location = new Point(16, 48);
            peerGrid.Name = "peerGrid";
            peerGrid.ReadOnly = true;
            peerGrid.RowHeadersVisible = false;
            peerGrid.Size = new Size(946, 250);
            peerGrid.TabIndex = 1;
            // 
            // pcName
            // 
            pcName.HeaderText = "PC";
            pcName.Name = "pcName";
            pcName.ReadOnly = true;
            // 
            // role
            // 
            role.HeaderText = "Role";
            role.Name = "role";
            role.ReadOnly = true;
            // 
            // process
            // 
            process.HeaderText = "Process";
            process.Name = "process";
            process.ReadOnly = true;
            // 
            // isRunning
            // 
            isRunning.HeaderText = "상태";
            isRunning.Name = "isRunning";
            isRunning.ReadOnly = true;
            // 
            // lastHeartbeat
            // 
            lastHeartbeat.HeaderText = "마지막 수신";
            lastHeartbeat.Name = "lastHeartbeat";
            lastHeartbeat.ReadOnly = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(980, 515);
            Controls.Add(peerGrid);
            Controls.Add(logTextBox);
            Controls.Add(statusLabel);
            Name = "Form1";
            Text = "LiveSkech Sensor Checker";
            ((System.ComponentModel.ISupportInitialize)peerGrid).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label statusLabel;
        private TextBox logTextBox;
        private DataGridView peerGrid;
        private DataGridViewTextBoxColumn pcName;
        private DataGridViewTextBoxColumn role;
        private DataGridViewTextBoxColumn process;
        private DataGridViewTextBoxColumn isRunning;
        private DataGridViewTextBoxColumn lastHeartbeat;
    }
}
