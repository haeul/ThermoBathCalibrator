using System.Drawing;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    partial class FormSettings
    {
        private System.ComponentModel.IContainer components = null;

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
            grpDeviceSv = new GroupBox();
            lblCh1Sv = new Label();
            lblCh2Sv = new Label();
            nudCh1Sv = new NumericUpDown();
            nudCh2Sv = new NumericUpDown();
            btnWriteCh1Sv = new Button();
            btnWriteCh2Sv = new Button();
            btnSave = new Button();
            btnClose = new Button();
            grpMultiBoard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numMbPort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numMbUnitId).BeginInit();
            grpDeviceSv.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudCh1Sv).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudCh2Sv).BeginInit();
            SuspendLayout();
            // 
            // grpMultiBoard
            // 
            grpMultiBoard.Controls.Add(lblMbHost);
            grpMultiBoard.Controls.Add(lblMbPort);
            grpMultiBoard.Controls.Add(lblMbUnitId);
            grpMultiBoard.Controls.Add(txtMbHost);
            grpMultiBoard.Controls.Add(numMbPort);
            grpMultiBoard.Controls.Add(numMbUnitId);
            grpMultiBoard.Location = new Point(12, 12);
            grpMultiBoard.Name = "grpMultiBoard";
            grpMultiBoard.Size = new Size(420, 126);
            grpMultiBoard.TabIndex = 0;
            grpMultiBoard.TabStop = false;
            grpMultiBoard.Text = "MULTI BOARD";
            // 
            // lblMbHost
            // 
            lblMbHost.Location = new Point(16, 28);
            lblMbHost.Size = new Size(90, 20);
            lblMbHost.Text = "Host";
            // 
            // lblMbPort
            // 
            lblMbPort.Location = new Point(16, 58);
            lblMbPort.Size = new Size(90, 20);
            lblMbPort.Text = "Port";
            // 
            // lblMbUnitId
            // 
            lblMbUnitId.Location = new Point(16, 88);
            lblMbUnitId.Size = new Size(90, 20);
            lblMbUnitId.Text = "UnitId";
            // 
            // txtMbHost
            // 
            txtMbHost.Location = new Point(110, 25);
            txtMbHost.Size = new Size(290, 23);
            // 
            // numMbPort
            // 
            numMbPort.Location = new Point(110, 55);
            numMbPort.Minimum = 1;
            numMbPort.Maximum = 65535;
            numMbPort.Size = new Size(290, 23);
            numMbPort.Value = 13000;
            // 
            // numMbUnitId
            // 
            numMbUnitId.Location = new Point(110, 85);
            numMbUnitId.Minimum = 1;
            numMbUnitId.Maximum = 247;
            numMbUnitId.Size = new Size(290, 23);
            numMbUnitId.Value = 1;
            // 
            // grpDeviceSv
            // 
            grpDeviceSv.Controls.Add(lblCh1Sv);
            grpDeviceSv.Controls.Add(lblCh2Sv);
            grpDeviceSv.Controls.Add(nudCh1Sv);
            grpDeviceSv.Controls.Add(nudCh2Sv);
            grpDeviceSv.Controls.Add(btnWriteCh1Sv);
            grpDeviceSv.Controls.Add(btnWriteCh2Sv);
            grpDeviceSv.Location = new Point(12, 148);
            grpDeviceSv.Name = "grpDeviceSv";
            grpDeviceSv.Size = new Size(420, 126);
            grpDeviceSv.TabIndex = 1;
            grpDeviceSv.TabStop = false;
            grpDeviceSv.Text = "DEVICE SV (0.1°C)";
            // 
            // lblCh1Sv
            // 
            lblCh1Sv.Location = new Point(16, 29);
            lblCh1Sv.Size = new Size(90, 20);
            lblCh1Sv.Text = "CH1 SV";
            // 
            // lblCh2Sv
            // 
            lblCh2Sv.Location = new Point(16, 79);
            lblCh2Sv.Size = new Size(90, 20);
            lblCh2Sv.Text = "CH2 SV";
            // 
            // nudCh1Sv
            // 
            nudCh1Sv.DecimalPlaces = 1;
            nudCh1Sv.Increment = 0.1M;
            nudCh1Sv.Minimum = -100;
            nudCh1Sv.Maximum = 200;
            nudCh1Sv.Location = new Point(110, 26);
            nudCh1Sv.Size = new Size(180, 23);
            // 
            // nudCh2Sv
            // 
            nudCh2Sv.DecimalPlaces = 1;
            nudCh2Sv.Increment = 0.1M;
            nudCh2Sv.Minimum = -100;
            nudCh2Sv.Maximum = 200;
            nudCh2Sv.Location = new Point(110, 76);
            nudCh2Sv.Size = new Size(180, 23);
            // 
            // btnWriteCh1Sv
            // 
            btnWriteCh1Sv.Location = new Point(300, 24);
            btnWriteCh1Sv.Size = new Size(100, 27);
            btnWriteCh1Sv.Text = "CH1 적용";
            // 
            // btnWriteCh2Sv
            // 
            btnWriteCh2Sv.Location = new Point(300, 74);
            btnWriteCh2Sv.Size = new Size(100, 27);
            btnWriteCh2Sv.Text = "CH2 적용";
            // 
            // btnSave
            // 
            btnSave.Location = new Point(232, 284);
            btnSave.Size = new Size(100, 34);
            btnSave.Text = "저장";
            // 
            // btnClose
            // 
            btnClose.Location = new Point(332, 284);
            btnClose.Size = new Size(100, 34);
            btnClose.Text = "닫기";
            // 
            // FormSettings
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(444, 330);
            Controls.Add(grpMultiBoard);
            Controls.Add(grpDeviceSv);
            Controls.Add(btnSave);
            Controls.Add(btnClose);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormSettings";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Settings";
            grpMultiBoard.ResumeLayout(false);
            grpMultiBoard.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numMbPort).EndInit();
            ((System.ComponentModel.ISupportInitialize)numMbUnitId).EndInit();
            grpDeviceSv.ResumeLayout(false);
            grpDeviceSv.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudCh1Sv).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudCh2Sv).EndInit();
            ResumeLayout(false);
        }

        private GroupBox grpMultiBoard;
        private Label lblMbHost;
        private Label lblMbPort;
        private Label lblMbUnitId;
        private TextBox txtMbHost;
        private NumericUpDown numMbPort;
        private NumericUpDown numMbUnitId;
        private GroupBox grpDeviceSv;
        private Label lblCh1Sv;
        private Label lblCh2Sv;
        private NumericUpDown nudCh1Sv;
        private NumericUpDown nudCh2Sv;
        private Button btnWriteCh1Sv;
        private Button btnWriteCh2Sv;
        private Button btnSave;
        private Button btnClose;
    }
}