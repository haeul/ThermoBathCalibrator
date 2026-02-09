using System.Drawing;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    partial class FormComSetting
    {
        private System.ComponentModel.IContainer components = null;

        private GroupBox grpMultiBoard;
        private Label lblMbHost;
        private Label lblMbPort;
        private Label lblMbUnitId;
        private TextBox txtMbHost;
        private NumericUpDown numMbPort;
        private NumericUpDown numMbUnitId;
        private Button btnTestMb;

        private Button btnSave;
        private Button btnClose;

        private TextBox txtLog;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            grpMultiBoard = new GroupBox();
            lblMbHost = new Label();
            lblMbPort = new Label();
            lblMbUnitId = new Label();
            txtMbHost = new TextBox();
            numMbPort = new NumericUpDown();
            numMbUnitId = new NumericUpDown();
            btnTestMb = new Button();

            btnSave = new Button();
            btnClose = new Button();
            txtLog = new TextBox();

            grpMultiBoard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numMbPort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numMbUnitId).BeginInit();
            SuspendLayout();

            // grpMultiBoard
            grpMultiBoard.Controls.Add(lblMbHost);
            grpMultiBoard.Controls.Add(txtMbHost);
            grpMultiBoard.Controls.Add(lblMbPort);
            grpMultiBoard.Controls.Add(numMbPort);
            grpMultiBoard.Controls.Add(lblMbUnitId);
            grpMultiBoard.Controls.Add(numMbUnitId);
            grpMultiBoard.Controls.Add(btnTestMb);
            grpMultiBoard.Location = new Point(12, 12);
            grpMultiBoard.Name = "grpMultiBoard";
            grpMultiBoard.Size = new Size(420, 140);
            grpMultiBoard.TabIndex = 0;
            grpMultiBoard.TabStop = false;
            grpMultiBoard.Text = "MULTI BOARD (Modbus TCP)";

            lblMbHost.Location = new Point(16, 30);
            lblMbHost.Size = new Size(90, 20);
            lblMbHost.Text = "Host";

            txtMbHost.Location = new Point(110, 28);
            txtMbHost.Size = new Size(290, 23);
            txtMbHost.Text = "192.168.1.11";

            lblMbPort.Location = new Point(16, 60);
            lblMbPort.Size = new Size(90, 20);
            lblMbPort.Text = "Port";

            numMbPort.Location = new Point(110, 58);
            numMbPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numMbPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numMbPort.Size = new Size(290, 23);
            numMbPort.Value = new decimal(new int[] { 13000, 0, 0, 0 });

            lblMbUnitId.Location = new Point(16, 90);
            lblMbUnitId.Size = new Size(90, 20);
            lblMbUnitId.Text = "UnitId";

            numMbUnitId.Location = new Point(110, 88);
            numMbUnitId.Maximum = new decimal(new int[] { 247, 0, 0, 0 });
            numMbUnitId.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numMbUnitId.Size = new Size(290, 23);
            numMbUnitId.Value = new decimal(new int[] { 1, 0, 0, 0 });

            btnTestMb.Location = new Point(110, 114);
            btnTestMb.Size = new Size(290, 22);
            btnTestMb.Text = "통신 테스트(FC03 0~13)";

            // txtLog
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.Location = new Point(12, 160);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(420, 220);
            txtLog.TabIndex = 1;

            // btnSave
            btnSave.Location = new Point(232, 388);
            btnSave.Size = new Size(100, 34);
            btnSave.Text = "저장";

            // btnClose
            btnClose.Location = new Point(332, 388);
            btnClose.Size = new Size(100, 34);
            btnClose.Text = "닫기";

            // FormComSetting
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(444, 434);
            Controls.Add(grpMultiBoard);
            Controls.Add(txtLog);
            Controls.Add(btnSave);
            Controls.Add(btnClose);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormComSetting";
            StartPosition = FormStartPosition.CenterParent;
            Text = "COM Settings";

            grpMultiBoard.ResumeLayout(false);
            grpMultiBoard.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numMbPort).EndInit();
            ((System.ComponentModel.ISupportInitialize)numMbUnitId).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
