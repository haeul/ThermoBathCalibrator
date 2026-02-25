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
            chkEnableOffsetControl = new CheckBox();
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
            grpMultiBoard.Text = "멀티보드";
            // 
            // lblMbHost
            // 
            lblMbHost.Location = new Point(16, 28);
            lblMbHost.Name = "lblMbHost";
            lblMbHost.Size = new Size(90, 20);
            lblMbHost.TabIndex = 0;
            lblMbHost.Text = "Host";
            // 
            // lblMbPort
            // 
            lblMbPort.Location = new Point(16, 58);
            lblMbPort.Name = "lblMbPort";
            lblMbPort.Size = new Size(90, 20);
            lblMbPort.TabIndex = 1;
            lblMbPort.Text = "Port";
            // 
            // lblMbUnitId
            // 
            lblMbUnitId.Location = new Point(16, 88);
            lblMbUnitId.Name = "lblMbUnitId";
            lblMbUnitId.Size = new Size(90, 20);
            lblMbUnitId.TabIndex = 2;
            lblMbUnitId.Text = "UnitId";
            // 
            // txtMbHost
            // 
            txtMbHost.Location = new Point(110, 25);
            txtMbHost.Name = "txtMbHost";
            txtMbHost.Size = new Size(290, 23);
            txtMbHost.TabIndex = 3;
            // 
            // numMbPort
            // 
            numMbPort.Location = new Point(110, 55);
            numMbPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numMbPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numMbPort.Name = "numMbPort";
            numMbPort.Size = new Size(290, 23);
            numMbPort.TabIndex = 4;
            numMbPort.Value = new decimal(new int[] { 13000, 0, 0, 0 });
            // 
            // numMbUnitId
            // 
            numMbUnitId.Location = new Point(110, 85);
            numMbUnitId.Maximum = new decimal(new int[] { 247, 0, 0, 0 });
            numMbUnitId.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numMbUnitId.Name = "numMbUnitId";
            numMbUnitId.Size = new Size(290, 23);
            numMbUnitId.TabIndex = 5;
            numMbUnitId.Value = new decimal(new int[] { 1, 0, 0, 0 });
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
            grpDeviceSv.Size = new Size(420, 118);
            grpDeviceSv.TabIndex = 1;
            grpDeviceSv.TabStop = false;
            grpDeviceSv.Text = "항온조 설정 온도 (0.1°C)";
            // 
            // lblCh1Sv
            // 
            lblCh1Sv.Location = new Point(16, 29);
            lblCh1Sv.Name = "lblCh1Sv";
            lblCh1Sv.Size = new Size(90, 20);
            lblCh1Sv.TabIndex = 0;
            lblCh1Sv.Text = "항온조1";
            // 
            // lblCh2Sv
            // 
            lblCh2Sv.Location = new Point(16, 79);
            lblCh2Sv.Name = "lblCh2Sv";
            lblCh2Sv.Size = new Size(90, 20);
            lblCh2Sv.TabIndex = 1;
            lblCh2Sv.Text = "항온조2";
            // 
            // nudCh1Sv
            // 
            nudCh1Sv.DecimalPlaces = 1;
            nudCh1Sv.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            nudCh1Sv.Location = new Point(110, 26);
            nudCh1Sv.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            nudCh1Sv.Minimum = new decimal(new int[] { 100, 0, 0, int.MinValue });
            nudCh1Sv.Name = "nudCh1Sv";
            nudCh1Sv.Size = new Size(180, 23);
            nudCh1Sv.TabIndex = 2;
            // 
            // nudCh2Sv
            // 
            nudCh2Sv.DecimalPlaces = 1;
            nudCh2Sv.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            nudCh2Sv.Location = new Point(110, 76);
            nudCh2Sv.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            nudCh2Sv.Minimum = new decimal(new int[] { 100, 0, 0, int.MinValue });
            nudCh2Sv.Name = "nudCh2Sv";
            nudCh2Sv.Size = new Size(180, 23);
            nudCh2Sv.TabIndex = 3;
            // 
            // btnWriteCh1Sv
            // 
            btnWriteCh1Sv.Location = new Point(300, 24);
            btnWriteCh1Sv.Name = "btnWriteCh1Sv";
            btnWriteCh1Sv.Size = new Size(100, 27);
            btnWriteCh1Sv.TabIndex = 4;
            btnWriteCh1Sv.Text = "항온조1 적용";
            // 
            // btnWriteCh2Sv
            // 
            btnWriteCh2Sv.Location = new Point(300, 74);
            btnWriteCh2Sv.Name = "btnWriteCh2Sv";
            btnWriteCh2Sv.Size = new Size(100, 27);
            btnWriteCh2Sv.TabIndex = 5;
            btnWriteCh2Sv.Text = "항온조2 적용";
            // 
            // btnSave
            // 
            btnSave.Location = new Point(232, 306);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(100, 34);
            btnSave.TabIndex = 2;
            btnSave.Text = "저장";
            // 
            // btnClose
            // 
            btnClose.Location = new Point(332, 306);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(100, 34);
            btnClose.TabIndex = 3;
            btnClose.Text = "닫기";
            // 
            // chkEnableOffsetControl
            // 
            chkEnableOffsetControl.AutoSize = true;
            chkEnableOffsetControl.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            chkEnableOffsetControl.Location = new Point(274, 274);
            chkEnableOffsetControl.Name = "chkEnableOffsetControl";
            chkEnableOffsetControl.Size = new Size(156, 24);
            chkEnableOffsetControl.TabIndex = 6;
            chkEnableOffsetControl.Text = "오프셋 보정 활성화";
            chkEnableOffsetControl.UseVisualStyleBackColor = true;
            // 
            // FormSettings
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(444, 355);
            Controls.Add(chkEnableOffsetControl);
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
            ((System.ComponentModel.ISupportInitialize)nudCh1Sv).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudCh2Sv).EndInit();
            ResumeLayout(false);
            PerformLayout();
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
        private CheckBox chkEnableOffsetControl;
    }
}