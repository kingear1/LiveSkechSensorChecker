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
            components = new System.ComponentModel.Container();
            statusLabel = new Label();
            logTextBox = new TextBox();
            peerGrid = new DataGridView();
            pcName = new DataGridViewTextBoxColumn();
            role = new DataGridViewTextBoxColumn();
            isRunning = new DataGridViewTextBoxColumn();
            lastHeartbeat = new DataGridViewTextBoxColumn();
            editConfigButton = new Button();
            peerCheckStatePanel = new FlowLayoutPanel();
            trayMenu = new ContextMenuStrip(components);
            trayRestoreMenuItem = new ToolStripMenuItem();
            trayExitMenuItem = new ToolStripMenuItem();
            trayIcon = new NotifyIcon(components);
            ((System.ComponentModel.ISupportInitialize)peerGrid).BeginInit();
            trayMenu.SuspendLayout();
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
            logTextBox.Location = new Point(16, 333);
            logTextBox.Multiline = true;
            logTextBox.Name = "logTextBox";
            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Size = new Size(946, 165);
            logTextBox.TabIndex = 4;
            // 
            // peerGrid
            // 
            peerGrid.AllowUserToAddRows = false;
            peerGrid.AllowUserToDeleteRows = false;
            peerGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            peerGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            peerGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            peerGrid.Columns.AddRange(new DataGridViewColumn[] { pcName, role, isRunning, lastHeartbeat });
            peerGrid.Location = new Point(16, 126);
            peerGrid.Name = "peerGrid";
            peerGrid.ReadOnly = true;
            peerGrid.RowHeadersVisible = false;
            peerGrid.Size = new Size(946, 189);
            peerGrid.TabIndex = 3;
            // 
            // pcName
            // 
            pcName.HeaderText = "PC";
            pcName.Name = "pcName";
            pcName.ReadOnly = true;
            pcName.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // role
            // 
            role.HeaderText = "Role";
            role.Name = "role";
            role.ReadOnly = true;
            role.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // isRunning
            // 
            isRunning.HeaderText = "상태";
            isRunning.Name = "isRunning";
            isRunning.ReadOnly = true;
            isRunning.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // lastHeartbeat
            // 
            lastHeartbeat.HeaderText = "마지막 수신";
            lastHeartbeat.Name = "lastHeartbeat";
            lastHeartbeat.ReadOnly = true;
            lastHeartbeat.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // editConfigButton
            // 
            editConfigButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            editConfigButton.Location = new Point(832, 10);
            editConfigButton.Name = "editConfigButton";
            editConfigButton.Size = new Size(130, 30);
            editConfigButton.TabIndex = 1;
            editConfigButton.Text = "설정 편집";
            editConfigButton.UseVisualStyleBackColor = true;
            editConfigButton.Click += editConfigButton_Click;
            // 
            // peerCheckStatePanel
            // 
            peerCheckStatePanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            peerCheckStatePanel.AutoScroll = true;
            peerCheckStatePanel.FlowDirection = FlowDirection.TopDown;
            peerCheckStatePanel.Location = new Point(16, 44);
            peerCheckStatePanel.Name = "peerCheckStatePanel";
            peerCheckStatePanel.Size = new Size(946, 76);
            peerCheckStatePanel.TabIndex = 2;
            peerCheckStatePanel.WrapContents = false;
            // 
            // trayMenu
            // 
            trayMenu.Items.AddRange(new ToolStripItem[] { trayRestoreMenuItem, trayExitMenuItem });
            trayMenu.Name = "trayMenu";
            trayMenu.Size = new Size(111, 48);
            // 
            // trayRestoreMenuItem
            // 
            trayRestoreMenuItem.Name = "trayRestoreMenuItem";
            trayRestoreMenuItem.Size = new Size(110, 22);
            trayRestoreMenuItem.Text = "복원";
            trayRestoreMenuItem.Click += trayRestoreMenuItem_Click;
            // 
            // trayExitMenuItem
            // 
            trayExitMenuItem.Name = "trayExitMenuItem";
            trayExitMenuItem.Size = new Size(110, 22);
            trayExitMenuItem.Text = "종료";
            trayExitMenuItem.Click += trayExitMenuItem_Click;
            // 
            // trayIcon
            // 
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Text = "LiveSkech Sensor Checker";
            trayIcon.Visible = false;
            trayIcon.MouseDoubleClick += trayIcon_MouseDoubleClick;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(980, 515);
            Controls.Add(peerCheckStatePanel);
            Controls.Add(editConfigButton);
            Controls.Add(peerGrid);
            Controls.Add(logTextBox);
            Controls.Add(statusLabel);
            Name = "Form1";
            Text = "LiveSkech Sensor Checker";
            ((System.ComponentModel.ISupportInitialize)peerGrid).EndInit();
            trayMenu.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label statusLabel;
        private TextBox logTextBox;
        private DataGridView peerGrid;
        private DataGridViewTextBoxColumn pcName;
        private DataGridViewTextBoxColumn role;
        private DataGridViewTextBoxColumn isRunning;
        private DataGridViewTextBoxColumn lastHeartbeat;
        private Button editConfigButton;
        private FlowLayoutPanel peerCheckStatePanel;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem trayRestoreMenuItem;
        private ToolStripMenuItem trayExitMenuItem;
        private NotifyIcon trayIcon;
    }
}
