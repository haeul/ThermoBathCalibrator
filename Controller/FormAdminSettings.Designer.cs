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
            tableLayoutPanel1.Location = new Point(12, 12);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tableLayoutPanel1.Size = new Size(364, 176);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // lblUtBiasCh1
            // 
            lblUtBiasCh1.Dock = DockStyle.Fill;
            lblUtBiasCh1.Location = new Point(3, 0);
            lblUtBiasCh1.Name = "lblUtBiasCh1";
            lblUtBiasCh1.Size = new Size(168, 44);
            lblUtBiasCh1.TabIndex = 0;
            lblUtBiasCh1.Text = "UT Bias CH1";
            lblUtBiasCh1.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblUtBiasCh2
            // 
            lblUtBiasCh2.Dock = DockStyle.Fill;
            lblUtBiasCh2.Location = new Point(3, 44);
            lblUtBiasCh2.Name = "lblUtBiasCh2";
            lblUtBiasCh2.Size = new Size(168, 44);
            lblUtBiasCh2.TabIndex = 1;
            lblUtBiasCh2.Text = "UT Bias CH2";
            lblUtBiasCh2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblSetpointCh1
            // 
            lblSetpointCh1.Dock = DockStyle.Fill;
            lblSetpointCh1.Location = new Point(3, 88);
            lblSetpointCh1.Name = "lblSetpointCh1";
            lblSetpointCh1.Size = new Size(168, 44);
            lblSetpointCh1.TabIndex = 2;
            lblSetpointCh1.Text = "SV CH1 (°C)";
            lblSetpointCh1.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblSetpointCh2
            // 
            lblSetpointCh2.Dock = DockStyle.Fill;
            lblSetpointCh2.Location = new Point(3, 132);
            lblSetpointCh2.Name = "lblSetpointCh2";
            lblSetpointCh2.Size = new Size(168, 44);
            lblSetpointCh2.TabIndex = 3;
            lblSetpointCh2.Text = "SV CH2 (°C)";
            lblSetpointCh2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // nudUtBiasCh1
            // 
            nudUtBiasCh1.DecimalPlaces = 3;
            nudUtBiasCh1.Dock = DockStyle.Fill;
            nudUtBiasCh1.Increment = new decimal(new int[] { 1, 0, 0, 196608 });
            nudUtBiasCh1.Location = new Point(177, 8);
            nudUtBiasCh1.Margin = new Padding(3, 8, 3, 3);
            nudUtBiasCh1.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            nudUtBiasCh1.Minimum = new decimal(new int[] { 5, 0, 0, int.MinValue });
            nudUtBiasCh1.Name = "nudUtBiasCh1";
            nudUtBiasCh1.Size = new Size(184, 31);
            nudUtBiasCh1.TabIndex = 4;
            // 
            // nudUtBiasCh2
            // 
            nudUtBiasCh2.DecimalPlaces = 3;
            nudUtBiasCh2.Dock = DockStyle.Fill;
            nudUtBiasCh2.Increment = new decimal(new int[] { 1, 0, 0, 196608 });
            nudUtBiasCh2.Location = new Point(177, 52);
            nudUtBiasCh2.Margin = new Padding(3, 8, 3, 3);
            nudUtBiasCh2.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            nudUtBiasCh2.Minimum = new decimal(new int[] { 5, 0, 0, int.MinValue });
            nudUtBiasCh2.Name = "nudUtBiasCh2";
            nudUtBiasCh2.Size = new Size(184, 31);
            nudUtBiasCh2.TabIndex = 5;
            // 
            // nudSetpointCh1
            // 
            nudSetpointCh1.DecimalPlaces = 1;
            nudSetpointCh1.Dock = DockStyle.Fill;
            nudSetpointCh1.Increment = 0.1m;
            nudSetpointCh1.Location = new Point(177, 96);
            nudSetpointCh1.Margin = new Padding(3, 8, 3, 3);
            nudSetpointCh1.Maximum = new decimal(new int[] { 80, 0, 0, 0 });
            nudSetpointCh1.Name = "nudSetpointCh1";
            nudSetpointCh1.Size = new Size(184, 31);
            nudSetpointCh1.TabIndex = 6;
            // 
            // nudSetpointCh2
            // 
            nudSetpointCh2.DecimalPlaces = 1;
            nudSetpointCh2.Dock = DockStyle.Fill;
            nudSetpointCh2.Increment = 0.1m;
            nudSetpointCh2.Location = new Point(177, 140);
            nudSetpointCh2.Margin = new Padding(3, 8, 3, 3);
            nudSetpointCh2.Maximum = new decimal(new int[] { 80, 0, 0, 0 });
            nudSetpointCh2.Name = "nudSetpointCh2";
            nudSetpointCh2.Size = new Size(184, 31);
            nudSetpointCh2.TabIndex = 7;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(btnCancel);
            flowLayoutPanel1.Controls.Add(btnOk);
            flowLayoutPanel1.Dock = DockStyle.Bottom;
            flowLayoutPanel1.FlowDirection = FlowDirection.RightToLeft;
            flowLayoutPanel1.Location = new Point(12, 202);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(364, 52);
            flowLayoutPanel1.TabIndex = 1;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(257, 3);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(104, 38);
            btnCancel.TabIndex = 0;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            // 
            // btnOk
            // 
            btnOk.Location = new Point(147, 3);
            btnOk.Name = "btnOk";
            btnOk.Size = new Size(104, 38);
            btnOk.TabIndex = 1;
            btnOk.Text = "OK";
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += BtnOk_Click;
            // 
            // FormAdminSettings
            // 
            AcceptButton = btnOk;
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new Size(388, 266);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(tableLayoutPanel1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormAdminSettings";
            Padding = new Padding(12);
            StartPosition = FormStartPosition.CenterParent;
            Text = "관리자 설정";
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)nudUtBiasCh1).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudUtBiasCh2).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudSetpointCh1).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudSetpointCh2).EndInit();
            flowLayoutPanel1.ResumeLayout(false);
            ResumeLayout(false);
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
    }
}