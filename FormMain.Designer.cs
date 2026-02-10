using System.Drawing;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    partial class FormMain
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
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            pnlHeader = new Panel();
            tlpSetting = new TableLayoutPanel();
            btnComSetting = new Button();
            lblHeader = new Label();
            pnlBacrground = new Panel();
            pnlCh2Graph = new Panel();
            pnlCh1Graph = new Panel();
            tlpbutton = new TableLayoutPanel();
            lblCh1 = new Label();
            lblCh1Temperature = new Label();
            lblCh2 = new Label();
            lblCh2Temperature = new Label();
            chkEnableOffsetControl = new CheckBox();
            btnStart = new Button();
            lblCh1OffsetTitle = new Label();
            lblCh1OffsetValue = new Label();
            nudOffsetCh1 = new NumericUpDown();
            btnOffsetApplyCh1 = new Button();
            lblCh2OffsetTitle = new Label();
            lblCh2OffsetValue = new Label();
            nudOffsetCh2 = new NumericUpDown();
            btnOffsetApplyCh2 = new Button();
            btnStop = new Button();
            dataGridView1 = new DataGridView();
            colTimestamp = new DataGridViewTextBoxColumn();
            colUtCh1 = new DataGridViewTextBoxColumn();
            colUtCh2 = new DataGridViewTextBoxColumn();
            colUtTj = new DataGridViewTextBoxColumn();
            colBath1Pv = new DataGridViewTextBoxColumn();
            colBath2Pv = new DataGridViewTextBoxColumn();
            colErr1 = new DataGridViewTextBoxColumn();
            colErr2 = new DataGridViewTextBoxColumn();
            colBath1SetTemp = new DataGridViewTextBoxColumn();
            colBath2SetTemp = new DataGridViewTextBoxColumn();
            tlpCommStatus = new TableLayoutPanel();
            lblThermoPortStatus = new Label();
            pnlHeader.SuspendLayout();
            tlpSetting.SuspendLayout();
            pnlBacrground.SuspendLayout();
            tlpbutton.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudOffsetCh1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudOffsetCh2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            tlpCommStatus.SuspendLayout();
            SuspendLayout();
            // 
            // pnlHeader
            // 
            pnlHeader.BackColor = SystemColors.Control;
            pnlHeader.Controls.Add(tlpSetting);
            pnlHeader.Controls.Add(lblHeader);
            pnlHeader.Location = new Point(10, 10);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(1453, 65);
            pnlHeader.TabIndex = 0;
            // 
            // tlpSetting
            // 
            tlpSetting.BackColor = Color.White;
            tlpSetting.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tlpSetting.ColumnCount = 1;
            tlpSetting.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpSetting.Controls.Add(btnComSetting, 0, 0);
            tlpSetting.Location = new Point(1188, 3);
            tlpSetting.Name = "tlpSetting";
            tlpSetting.RowCount = 1;
            tlpSetting.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpSetting.Size = new Size(262, 59);
            tlpSetting.TabIndex = 4;
            // 
            // btnComSetting
            // 
            btnComSetting.BackColor = SystemColors.Control;
            btnComSetting.Font = new Font("Segoe UI Semibold", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnComSetting.ForeColor = SystemColors.ControlText;
            btnComSetting.Location = new Point(4, 4);
            btnComSetting.Name = "btnComSetting";
            btnComSetting.Size = new Size(254, 51);
            btnComSetting.TabIndex = 3;
            btnComSetting.Text = "Port Setting";
            btnComSetting.UseVisualStyleBackColor = false;
            // 
            // lblHeader
            // 
            lblHeader.AutoSize = true;
            lblHeader.Font = new Font("Segoe UI", 24F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblHeader.ForeColor = SystemColors.ControlDarkDark;
            lblHeader.Location = new Point(612, 7);
            lblHeader.Name = "lblHeader";
            lblHeader.Size = new Size(221, 45);
            lblHeader.TabIndex = 1;
            lblHeader.Text = "데이터 수집기";
            // 
            // pnlBacrground
            // 
            pnlBacrground.BackColor = Color.White;
            pnlBacrground.Controls.Add(pnlCh2Graph);
            pnlBacrground.Controls.Add(pnlCh1Graph);
            pnlBacrground.Controls.Add(tlpbutton);
            pnlBacrground.Controls.Add(pnlHeader);
            pnlBacrground.Controls.Add(dataGridView1);
            pnlBacrground.Controls.Add(tlpCommStatus);
            pnlBacrground.Location = new Point(6, 6);
            pnlBacrground.Name = "pnlBacrground";
            pnlBacrground.Size = new Size(1471, 1300);
            pnlBacrground.TabIndex = 7;
            // 
            // pnlCh2Graph
            // 
            pnlCh2Graph.BackColor = Color.White;
            pnlCh2Graph.BorderStyle = BorderStyle.FixedSingle;
            pnlCh2Graph.Location = new Point(743, 200);
            pnlCh2Graph.Name = "pnlCh2Graph";
            pnlCh2Graph.Size = new Size(720, 424);
            pnlCh2Graph.TabIndex = 10;
            // 
            // pnlCh1Graph
            // 
            pnlCh1Graph.BackColor = Color.White;
            pnlCh1Graph.BorderStyle = BorderStyle.FixedSingle;
            pnlCh1Graph.Location = new Point(11, 200);
            pnlCh1Graph.Name = "pnlCh1Graph";
            pnlCh1Graph.Size = new Size(720, 424);
            pnlCh1Graph.TabIndex = 9;
            // 
            // tlpbutton
            // 
            tlpbutton.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tlpbutton.ColumnCount = 9;
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            tlpbutton.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12F));
            tlpbutton.Controls.Add(lblCh1, 0, 0);
            tlpbutton.Controls.Add(lblCh1Temperature, 1, 0);
            tlpbutton.Controls.Add(lblCh2, 4, 0);
            tlpbutton.Controls.Add(lblCh2Temperature, 5, 0);
            tlpbutton.Controls.Add(chkEnableOffsetControl, 7, 0);
            tlpbutton.Controls.Add(btnStart, 8, 0);
            tlpbutton.Controls.Add(lblCh1OffsetTitle, 0, 1);
            tlpbutton.Controls.Add(lblCh1OffsetValue, 1, 1);
            tlpbutton.Controls.Add(nudOffsetCh1, 2, 1);
            tlpbutton.Controls.Add(btnOffsetApplyCh1, 3, 1);
            tlpbutton.Controls.Add(lblCh2OffsetTitle, 4, 1);
            tlpbutton.Controls.Add(lblCh2OffsetValue, 5, 1);
            tlpbutton.Controls.Add(nudOffsetCh2, 6, 1);
            tlpbutton.Controls.Add(btnOffsetApplyCh2, 7, 1);
            tlpbutton.Controls.Add(btnStop, 8, 1);
            tlpbutton.Location = new Point(11, 87);
            tlpbutton.Name = "tlpbutton";
            tlpbutton.RowCount = 2;
            tlpbutton.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpbutton.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpbutton.Size = new Size(1452, 101);
            tlpbutton.TabIndex = 7;
            // 
            // lblCh1
            // 
            lblCh1.BackColor = SystemColors.Control;
            lblCh1.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1.Location = new Point(4, 1);
            lblCh1.Name = "lblCh1";
            lblCh1.Size = new Size(150, 49);
            lblCh1.TabIndex = 15;
            lblCh1.Text = "CH1";
            lblCh1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh1Temperature
            // 
            lblCh1Temperature.BackColor = SystemColors.Control;
            lblCh1Temperature.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1Temperature.Location = new Point(163, 1);
            lblCh1Temperature.Name = "lblCh1Temperature";
            lblCh1Temperature.Size = new Size(152, 49);
            lblCh1Temperature.TabIndex = 17;
            lblCh1Temperature.Text = "0";
            lblCh1Temperature.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh2
            // 
            lblCh2.BackColor = SystemColors.Control;
            lblCh2.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2.Location = new Point(640, 1);
            lblCh2.Name = "lblCh2";
            lblCh2.Size = new Size(150, 49);
            lblCh2.TabIndex = 16;
            lblCh2.Text = "CH2";
            lblCh2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh2Temperature
            // 
            lblCh2Temperature.BackColor = SystemColors.Control;
            lblCh2Temperature.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2Temperature.Location = new Point(799, 1);
            lblCh2Temperature.Name = "lblCh2Temperature";
            lblCh2Temperature.Size = new Size(152, 49);
            lblCh2Temperature.TabIndex = 18;
            lblCh2Temperature.Text = "0";
            lblCh2Temperature.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // chkEnableOffsetControl
            // 
            chkEnableOffsetControl.BackColor = SystemColors.Control;
            chkEnableOffsetControl.Dock = DockStyle.Fill;
            chkEnableOffsetControl.Font = new Font("Segoe UI Semibold", 12.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            chkEnableOffsetControl.Location = new Point(1114, 1);
            chkEnableOffsetControl.Margin = new Padding(0);
            chkEnableOffsetControl.Name = "chkEnableOffsetControl";
            chkEnableOffsetControl.Size = new Size(158, 49);
            chkEnableOffsetControl.TabIndex = 25;
            chkEnableOffsetControl.Text = "Offset 보정";
            chkEnableOffsetControl.TextAlign = ContentAlignment.MiddleCenter;
            chkEnableOffsetControl.UseVisualStyleBackColor = false;
            // 
            // btnStart
            // 
            btnStart.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnStart.Location = new Point(1276, 4);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(165, 43);
            btnStart.TabIndex = 13;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            // 
            // lblCh1OffsetTitle
            // 
            lblCh1OffsetTitle.BackColor = SystemColors.Control;
            lblCh1OffsetTitle.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1OffsetTitle.Location = new Point(4, 51);
            lblCh1OffsetTitle.Name = "lblCh1OffsetTitle";
            lblCh1OffsetTitle.Size = new Size(150, 49);
            lblCh1OffsetTitle.TabIndex = 19;
            lblCh1OffsetTitle.Text = "CH1 Off";
            lblCh1OffsetTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh1OffsetValue
            // 
            lblCh1OffsetValue.BackColor = SystemColors.Control;
            lblCh1OffsetValue.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1OffsetValue.Location = new Point(163, 51);
            lblCh1OffsetValue.Name = "lblCh1OffsetValue";
            lblCh1OffsetValue.Size = new Size(152, 49);
            lblCh1OffsetValue.TabIndex = 20;
            lblCh1OffsetValue.Text = "0.0";
            lblCh1OffsetValue.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // nudOffsetCh1
            // 
            nudOffsetCh1.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            nudOffsetCh1.Location = new Point(322, 54);
            nudOffsetCh1.Name = "nudOffsetCh1";
            nudOffsetCh1.Size = new Size(152, 43);
            nudOffsetCh1.TabIndex = 11;
            // 
            // btnOffsetApplyCh1
            // 
            btnOffsetApplyCh1.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnOffsetApplyCh1.Location = new Point(481, 54);
            btnOffsetApplyCh1.Name = "btnOffsetApplyCh1";
            btnOffsetApplyCh1.Size = new Size(152, 43);
            btnOffsetApplyCh1.TabIndex = 12;
            btnOffsetApplyCh1.Text = "Apply";
            btnOffsetApplyCh1.UseVisualStyleBackColor = true;
            // 
            // lblCh2OffsetTitle
            // 
            lblCh2OffsetTitle.BackColor = SystemColors.Control;
            lblCh2OffsetTitle.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2OffsetTitle.Location = new Point(640, 51);
            lblCh2OffsetTitle.Name = "lblCh2OffsetTitle";
            lblCh2OffsetTitle.Size = new Size(150, 49);
            lblCh2OffsetTitle.TabIndex = 21;
            lblCh2OffsetTitle.Text = "CH2 Off";
            lblCh2OffsetTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh2OffsetValue
            // 
            lblCh2OffsetValue.BackColor = SystemColors.Control;
            lblCh2OffsetValue.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2OffsetValue.Location = new Point(799, 51);
            lblCh2OffsetValue.Name = "lblCh2OffsetValue";
            lblCh2OffsetValue.Size = new Size(152, 49);
            lblCh2OffsetValue.TabIndex = 22;
            lblCh2OffsetValue.Text = "0.0";
            lblCh2OffsetValue.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // nudOffsetCh2
            // 
            nudOffsetCh2.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            nudOffsetCh2.Location = new Point(958, 54);
            nudOffsetCh2.Name = "nudOffsetCh2";
            nudOffsetCh2.Size = new Size(152, 43);
            nudOffsetCh2.TabIndex = 23;
            // 
            // btnOffsetApplyCh2
            // 
            btnOffsetApplyCh2.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnOffsetApplyCh2.Location = new Point(1117, 54);
            btnOffsetApplyCh2.Name = "btnOffsetApplyCh2";
            btnOffsetApplyCh2.Size = new Size(152, 43);
            btnOffsetApplyCh2.TabIndex = 24;
            btnOffsetApplyCh2.Text = "Apply";
            btnOffsetApplyCh2.UseVisualStyleBackColor = true;
            // 
            // btnStop
            // 
            btnStop.Font = new Font("Segoe UI Semibold", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnStop.Location = new Point(1276, 54);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(165, 43);
            btnStop.TabIndex = 14;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridView1.BackgroundColor = SystemColors.Control;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 12.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.False;
            dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dataGridView1.ColumnHeadersHeight = 40;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { colTimestamp, colUtCh1, colUtCh2, colUtTj, colBath1Pv, colBath2Pv, colErr1, colErr2, colBath1SetTemp, colBath2SetTemp });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = SystemColors.Window;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle3.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dataGridView1.DefaultCellStyle = dataGridViewCellStyle3;
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.Location = new Point(11, 638);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.Size = new Size(1452, 601);
            dataGridView1.TabIndex = 0;
            // 
            // colTimestamp
            // 
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            colTimestamp.DefaultCellStyle = dataGridViewCellStyle2;
            colTimestamp.HeaderText = "시간";
            colTimestamp.MinimumWidth = 210;
            colTimestamp.Name = "colTimestamp";
            colTimestamp.ReadOnly = true;
            colTimestamp.Width = 230;
            // 
            // colUtCh1
            // 
            colUtCh1.HeaderText = "UT1";
            colUtCh1.Name = "colUtCh1";
            colUtCh1.ReadOnly = true;
            colUtCh1.Width = 130;
            // 
            // colUtCh2
            // 
            colUtCh2.HeaderText = "UT2";
            colUtCh2.Name = "colUtCh2";
            colUtCh2.ReadOnly = true;
            colUtCh2.Width = 130;
            // 
            // colUtTj
            // 
            colUtTj.HeaderText = "TJ";
            colUtTj.Name = "colUtTj";
            colUtTj.ReadOnly = true;
            colUtTj.Width = 130;
            // 
            // colBath1Pv
            // 
            colBath1Pv.HeaderText = "B1 PV";
            colBath1Pv.Name = "colBath1Pv";
            colBath1Pv.ReadOnly = true;
            colBath1Pv.Width = 130;
            // 
            // colBath2Pv
            // 
            colBath2Pv.HeaderText = "B2 PV";
            colBath2Pv.Name = "colBath2Pv";
            colBath2Pv.ReadOnly = true;
            colBath2Pv.Width = 130;
            // 
            // colErr1
            // 
            colErr1.HeaderText = "Err1";
            colErr1.Name = "colErr1";
            colErr1.ReadOnly = true;
            colErr1.Width = 130;
            // 
            // colErr2
            // 
            colErr2.HeaderText = "Err2";
            colErr2.Name = "colErr2";
            colErr2.ReadOnly = true;
            colErr2.Width = 130;
            // 
            // colBath1SetTemp
            // 
            colBath1SetTemp.HeaderText = "B1 Set";
            colBath1SetTemp.Name = "colBath1SetTemp";
            colBath1SetTemp.ReadOnly = true;
            colBath1SetTemp.Width = 130;
            // 
            // colBath2SetTemp
            // 
            colBath2SetTemp.HeaderText = "B2 Set";
            colBath2SetTemp.Name = "colBath2SetTemp";
            colBath2SetTemp.ReadOnly = true;
            colBath2SetTemp.Width = 130;
            // 
            // tlpCommStatus
            // 
            tlpCommStatus.BackColor = SystemColors.Control;
            tlpCommStatus.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tlpCommStatus.ColumnCount = 1;
            tlpCommStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpCommStatus.Controls.Add(lblThermoPortStatus, 0, 0);
            tlpCommStatus.Location = new Point(10, 1252);
            tlpCommStatus.Name = "tlpCommStatus";
            tlpCommStatus.RowCount = 1;
            tlpCommStatus.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpCommStatus.Size = new Size(1453, 36);
            tlpCommStatus.TabIndex = 8;
            // 
            // lblThermoPortStatus
            // 
            lblThermoPortStatus.Dock = DockStyle.Fill;
            lblThermoPortStatus.Font = new Font("Segoe UI Semibold", 12.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblThermoPortStatus.ForeColor = Color.Gray;
            lblThermoPortStatus.Location = new Point(1, 1);
            lblThermoPortStatus.Margin = new Padding(0);
            lblThermoPortStatus.Name = "lblThermoPortStatus";
            lblThermoPortStatus.Size = new Size(1451, 34);
            lblThermoPortStatus.TabIndex = 2;
            lblThermoPortStatus.Text = "BOARD";
            lblThermoPortStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1481, 1308);
            Controls.Add(pnlBacrground);
            Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Name = "FormMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ThermoBathCalibrator";
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            tlpSetting.ResumeLayout(false);
            pnlBacrground.ResumeLayout(false);
            tlpbutton.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)nudOffsetCh1).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudOffsetCh2).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            tlpCommStatus.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlHeader;
        private Label lblHeader;
        private Button btnComSetting;
        private Panel pnlBacrground;
        private DataGridView dataGridView1;
        private TableLayoutPanel tlpbutton;
        private TableLayoutPanel tlpSetting;

        private TableLayoutPanel tlpCommStatus;
        private Label lblThermoPortStatus;

        private Panel pnlCh2Graph;
        private Panel pnlCh1Graph;

        private Label lblCh2Temperature;
        private Label lblCh1Temperature;
        private Button btnStart;
        private Button btnStop;
        private Label lblCh1;
        private Label lblCh2;

        // CH1 Offset UI
        private Label lblCh1OffsetTitle;
        private Label lblCh1OffsetValue;
        private NumericUpDown nudOffsetCh1;
        private Button btnOffsetApplyCh1;

        // CH2 Offset UI
        private Label lblCh2OffsetTitle;
        private Label lblCh2OffsetValue;
        private NumericUpDown nudOffsetCh2;
        private Button btnOffsetApplyCh2;

        // NEW: Offset enable checkbox
        private CheckBox chkEnableOffsetControl;

        private DataGridViewTextBoxColumn colTimestamp;
        private DataGridViewTextBoxColumn colUtCh1;
        private DataGridViewTextBoxColumn colUtCh2;
        private DataGridViewTextBoxColumn colUtTj;
        private DataGridViewTextBoxColumn colBath1Pv;
        private DataGridViewTextBoxColumn colBath2Pv;
        private DataGridViewTextBoxColumn colErr1;
        private DataGridViewTextBoxColumn colErr2;
        private DataGridViewTextBoxColumn colBath1SetTemp;
        private DataGridViewTextBoxColumn colBath2SetTemp;
    }
}
