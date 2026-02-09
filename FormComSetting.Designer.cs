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
        private Button btnReadbackNow;
        private Button btnReadbackDelay;

        private GroupBox grpBathTest;
        private Label lblTestSv;
        private Label lblTestOffset;
        private NumericUpDown nudTestSv;
        private NumericUpDown nudTestOffset;
        private Button btnCh1SvSet;
        private Button btnCh1OffSet;
        private Button btnCh2SvSet;
        private Button btnCh2OffSet;

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
            btnReadbackNow = new Button();
            btnReadbackDelay = new Button();

            grpBathTest = new GroupBox();
            lblTestSv = new Label();
            lblTestOffset = new Label();
            nudTestSv = new NumericUpDown();
            nudTestOffset = new NumericUpDown();
            btnCh1SvSet = new Button();
            btnCh1OffSet = new Button();
            btnCh2SvSet = new Button();
            btnCh2OffSet = new Button();

            btnSave = new Button();
            btnClose = new Button();
            txtLog = new TextBox();

            grpMultiBoard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numMbPort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numMbUnitId).BeginInit();

            grpBathTest.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudTestSv).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudTestOffset).BeginInit();

            SuspendLayout();

            // =========================
            // grpMultiBoard
            // =========================
            grpMultiBoard.Controls.Add(lblMbHost);
            grpMultiBoard.Controls.Add(txtMbHost);
            grpMultiBoard.Controls.Add(lblMbPort);
            grpMultiBoard.Controls.Add(numMbPort);
            grpMultiBoard.Controls.Add(lblMbUnitId);
            grpMultiBoard.Controls.Add(numMbUnitId);
            grpMultiBoard.Controls.Add(btnTestMb);
            grpMultiBoard.Controls.Add(btnReadbackNow);
            grpMultiBoard.Controls.Add(btnReadbackDelay);
            grpMultiBoard.Location = new Point(12, 12);
            grpMultiBoard.Name = "grpMultiBoard";
            grpMultiBoard.Size = new Size(420, 166);
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
            numMbPort.Maximum = 65535;
            numMbPort.Minimum = 1;
            numMbPort.Size = new Size(290, 23);
            numMbPort.Value = 13000;

            lblMbUnitId.Location = new Point(16, 90);
            lblMbUnitId.Size = new Size(90, 20);
            lblMbUnitId.Text = "UnitId";

            numMbUnitId.Location = new Point(110, 88);
            numMbUnitId.Maximum = 247;
            numMbUnitId.Minimum = 1;
            numMbUnitId.Size = new Size(290, 23);
            numMbUnitId.Value = 1;

            btnTestMb.Location = new Point(110, 114);
            btnTestMb.Size = new Size(290, 22);
            btnTestMb.Text = "통신 테스트(FC03 0~13)";

            btnReadbackNow.Location = new Point(110, 138);
            btnReadbackNow.Size = new Size(140, 22);
            btnReadbackNow.Text = "READBACK 즉시";

            btnReadbackDelay.Location = new Point(260, 138);
            btnReadbackDelay.Size = new Size(140, 22);
            btnReadbackDelay.Text = "READBACK(800ms)";

            // =========================
            // grpBathTest
            // =========================
            grpBathTest.Controls.Add(lblTestSv);
            grpBathTest.Controls.Add(nudTestSv);
            grpBathTest.Controls.Add(lblTestOffset);
            grpBathTest.Controls.Add(nudTestOffset);
            grpBathTest.Controls.Add(btnCh1SvSet);
            grpBathTest.Controls.Add(btnCh1OffSet);
            grpBathTest.Controls.Add(btnCh2SvSet);
            grpBathTest.Controls.Add(btnCh2OffSet);
            grpBathTest.Location = new Point(12, 186);
            grpBathTest.Name = "grpBathTest";
            grpBathTest.Size = new Size(420, 160);
            grpBathTest.TabIndex = 1;
            grpBathTest.TabStop = false;
            grpBathTest.Text = "항온조 커맨드 테스트 (FC10 20~26)";

            lblTestSv.Location = new Point(16, 28);
            lblTestSv.Size = new Size(90, 20);
            lblTestSv.Text = "SV(℃)";

            nudTestSv.Location = new Point(110, 26);
            nudTestSv.Size = new Size(290, 23);
            nudTestSv.DecimalPlaces = 1;
            nudTestSv.Increment = 0.1M;
            nudTestSv.Minimum = -100;
            nudTestSv.Maximum = 200;
            nudTestSv.Value = 25;

            lblTestOffset.Location = new Point(16, 58);
            lblTestOffset.Size = new Size(90, 20);
            lblTestOffset.Text = "Offset(℃)";

            nudTestOffset.Location = new Point(110, 56);
            nudTestOffset.Size = new Size(290, 23);
            nudTestOffset.DecimalPlaces = 1;
            nudTestOffset.Increment = 0.1M;
            nudTestOffset.Minimum = -1;
            nudTestOffset.Maximum = 1;
            nudTestOffset.Value = 0;

            btnCh1SvSet.Location = new Point(110, 90);
            btnCh1SvSet.Size = new Size(140, 28);
            btnCh1SvSet.Text = "CH1 SV 설정";

            btnCh1OffSet.Location = new Point(260, 90);
            btnCh1OffSet.Size = new Size(140, 28);
            btnCh1OffSet.Text = "CH1 Offset 설정";

            btnCh2SvSet.Location = new Point(110, 122);
            btnCh2SvSet.Size = new Size(140, 28);
            btnCh2SvSet.Text = "CH2 SV 설정";

            btnCh2OffSet.Location = new Point(260, 122);
            btnCh2OffSet.Size = new Size(140, 28);
            btnCh2OffSet.Text = "CH2 Offset 설정";

            // =========================
            // txtLog
            // =========================
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.Location = new Point(12, 356);
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(420, 220);

            // =========================
            // btnSave / btnClose
            // =========================
            btnSave.Location = new Point(232, 584);
            btnSave.Size = new Size(100, 34);
            btnSave.Text = "저장";

            btnClose.Location = new Point(332, 584);
            btnClose.Size = new Size(100, 34);
            btnClose.Text = "닫기";

            // =========================
            // FormComSetting
            // =========================
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(444, 630);
            Controls.Add(grpMultiBoard);
            Controls.Add(grpBathTest);
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

            grpBathTest.ResumeLayout(false);
            grpBathTest.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudTestSv).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudTestOffset).EndInit();

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
