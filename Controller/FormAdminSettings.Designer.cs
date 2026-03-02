using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace ThermoBathCalibrator
{
    partial class FormAdminSettings
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

        private void InitializeComponent()
        {
            tableLayoutPanel1 = new TableLayoutPanel();
            lblUtBiasCh1 = new Label();
            lblUtBiasCh2 = new Label();
            lblSetpointCh1 = new Label();
            lblSetpointCh2 = new Label();
            nudUtBiasCh1 = new NumericUpDown();
            nudUtBiasCh2 = new NumericUpDown();
            nudSetpointCh1 = new NumericUpDown();
            nudSetpointCh2 = new NumericUpDown();
            flowLayoutPanel1 = new FlowLayoutPanel();
            btnCancel = new Button();
            btnOk = new Button();
            chkEnableOffsetControl = new CheckBox();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudUtBiasCh1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudUtBiasCh2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudSetpointCh1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudSetpointCh2).BeginInit();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            tableLayoutPanel1.Controls.Add(lblUtBiasCh1, 0, 0);
            tableLayoutPanel1.Controls.Add(lblUtBiasCh2, 0, 1);
            tableLayoutPanel1.Controls.Add(lblSetpointCh1, 0, 2);
            tableLayoutPanel1.Controls.Add(lblSetpointCh2, 0, 3);
            tableLayoutPanel1.Controls.Add(nudUtBiasCh1, 1, 0);
            tableLayoutPanel1.Controls.Add(nudUtBiasCh2, 1, 1);
            tableLayoutPanel1.Controls.Add(nudSetpointCh1, 1, 2);
            tableLayoutPanel1.Controls.Add(nudSetpointCh2, 1, 3);
            tableLayoutPanel1.Dock = DockStyle.Top;
            tableLayoutPanel1.Location = new Point(8, 7);
            tableLayoutPanel1.Margin = new Padding(2);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            tableLayoutPanel1.Size = new Size(256, 106);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // lblUtBiasCh1
            // 
            lblUtBiasCh1.Dock = DockStyle.Fill;
            lblUtBiasCh1.Location = new Point(2, 0);
            lblUtBiasCh1.Margin = new Padding(2, 0, 2, 0);
            lblUtBiasCh1.Name = "lblUtBiasCh1";
            lblUtBiasCh1.Size = new Size(118, 26);
            lblUtBiasCh1.TabIndex = 0;
            lblUtBiasCh1.Text = "UT Bias CH1";
            lblUtBiasCh1.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblUtBiasCh2
            // 
            lblUtBiasCh2.Dock = DockStyle.Fill;
            lblUtBiasCh2.Location = new Point(2, 26);
            lblUtBiasCh2.Margin = new Padding(2, 0, 2, 0);
            lblUtBiasCh2.Name = "lblUtBiasCh2";
            lblUtBiasCh2.Size = new Size(118, 26);
            lblUtBiasCh2.TabIndex = 1;
            lblUtBiasCh2.Text = "UT Bias CH2";
            lblUtBiasCh2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblSetpointCh1
            // 
            lblSetpointCh1.Dock = DockStyle.Fill;
            lblSetpointCh1.Location = new Point(2, 52);
            lblSetpointCh1.Margin = new Padding(2, 0, 2, 0);
            lblSetpointCh1.Name = "lblSetpointCh1";
            lblSetpointCh1.Size = new Size(118, 26);
            lblSetpointCh1.TabIndex = 2;
            lblSetpointCh1.Text = "항온조1 (°C)";
            lblSetpointCh1.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblSetpointCh2
            // 
            lblSetpointCh2.Dock = DockStyle.Fill;
            lblSetpointCh2.Location = new Point(2, 78);
            lblSetpointCh2.Margin = new Padding(2, 0, 2, 0);
            lblSetpointCh2.Name = "lblSetpointCh2";
            lblSetpointCh2.Size = new Size(118, 28);
            lblSetpointCh2.TabIndex = 3;
            lblSetpointCh2.Text = "항온조2 (°C)";
            lblSetpointCh2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // nudUtBiasCh1
            // 
            nudUtBiasCh1.DecimalPlaces = 3;
            nudUtBiasCh1.Dock = DockStyle.Fill;
            nudUtBiasCh1.Increment = new decimal(new int[] { 1, 0, 0, 196608 });
            nudUtBiasCh1.Location = new Point(124, 5);
            nudUtBiasCh1.Margin = new Padding(2, 5, 2, 2);
            nudUtBiasCh1.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            nudUtBiasCh1.Minimum = new decimal(new int[] { 5, 0, 0, int.MinValue });
            nudUtBiasCh1.Name = "nudUtBiasCh1";
            nudUtBiasCh1.Size = new Size(130, 23);
            nudUtBiasCh1.TabIndex = 4;
            // 
            // nudUtBiasCh2
            // 
            nudUtBiasCh2.DecimalPlaces = 3;
            nudUtBiasCh2.Dock = DockStyle.Fill;
            nudUtBiasCh2.Increment = new decimal(new int[] { 1, 0, 0, 196608 });
            nudUtBiasCh2.Location = new Point(124, 31);
            nudUtBiasCh2.Margin = new Padding(2, 5, 2, 2);
            nudUtBiasCh2.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            nudUtBiasCh2.Minimum = new decimal(new int[] { 5, 0, 0, int.MinValue });
            nudUtBiasCh2.Name = "nudUtBiasCh2";
            nudUtBiasCh2.Size = new Size(130, 23);
            nudUtBiasCh2.TabIndex = 5;
            // 
            // nudSetpointCh1
            // 
            nudSetpointCh1.DecimalPlaces = 2;
            nudSetpointCh1.Dock = DockStyle.Fill;
            nudSetpointCh1.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
            nudSetpointCh1.Location = new Point(124, 57);
            nudSetpointCh1.Margin = new Padding(2, 5, 2, 2);
            nudSetpointCh1.Maximum = new decimal(new int[] { 80, 0, 0, 0 });
            nudSetpointCh1.Name = "nudSetpointCh1";
            nudSetpointCh1.Size = new Size(130, 23);
            nudSetpointCh1.TabIndex = 6;
            // 
            // nudSetpointCh2
            // 
            nudSetpointCh2.DecimalPlaces = 2;
            nudSetpointCh2.Dock = DockStyle.Fill;
            nudSetpointCh2.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
            nudSetpointCh2.Location = new Point(124, 83);
            nudSetpointCh2.Margin = new Padding(2, 5, 2, 2);
            nudSetpointCh2.Maximum = new decimal(new int[] { 80, 0, 0, 0 });
            nudSetpointCh2.Name = "nudSetpointCh2";
            nudSetpointCh2.Size = new Size(130, 23);
            nudSetpointCh2.TabIndex = 7;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(btnCancel);
            flowLayoutPanel1.Controls.Add(btnOk);
            flowLayoutPanel1.Dock = DockStyle.Bottom;
            flowLayoutPanel1.FlowDirection = FlowDirection.RightToLeft;
            flowLayoutPanel1.Location = new Point(8, 152);
            flowLayoutPanel1.Margin = new Padding(2);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(256, 31);
            flowLayoutPanel1.TabIndex = 1;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(181, 2);
            btnCancel.Margin = new Padding(2);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(73, 23);
            btnCancel.TabIndex = 0;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            // 
            // btnOk
            // 
            btnOk.Location = new Point(104, 2);
            btnOk.Margin = new Padding(2);
            btnOk.Name = "btnOk";
            btnOk.Size = new Size(73, 23);
            btnOk.TabIndex = 1;
            btnOk.Text = "OK";
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += BtnOk_Click;
            // 
            // chkEnableOffsetControl
            // 
            chkEnableOffsetControl.AutoSize = true;
            chkEnableOffsetControl.Font = new System.Drawing.Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            chkEnableOffsetControl.Location = new Point(106, 121);
            chkEnableOffsetControl.Margin = new Padding(2);
            chkEnableOffsetControl.Name = "chkEnableOffsetControl";
            chkEnableOffsetControl.Size = new Size(156, 24);
            chkEnableOffsetControl.TabIndex = 6;
            chkEnableOffsetControl.Text = "오프셋 보정 활성화";
            chkEnableOffsetControl.UseVisualStyleBackColor = true;
            // 
            // FormAdminSettings
            // 
            AcceptButton = btnOk;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new Size(272, 190);
            Controls.Add(chkEnableOffsetControl);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(tableLayoutPanel1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(2);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormAdminSettings";
            Padding = new Padding(8, 7, 8, 7);
            StartPosition = FormStartPosition.CenterParent;
            Text = "관리자 설정";
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)nudUtBiasCh1).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudUtBiasCh2).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudSetpointCh1).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudSetpointCh2).EndInit();
            flowLayoutPanel1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        private TableLayoutPanel tableLayoutPanel1;
        private Label lblUtBiasCh1;
        private Label lblUtBiasCh2;
        private Label lblSetpointCh1;
        private Label lblSetpointCh2;
        private NumericUpDown nudUtBiasCh1;
        private NumericUpDown nudUtBiasCh2;
        private NumericUpDown nudSetpointCh1;
        private NumericUpDown nudSetpointCh2;
        private FlowLayoutPanel flowLayoutPanel1;
        private Button btnCancel;
        private Button btnOk;
        private CheckBox chkEnableOffsetControl;
    }
}