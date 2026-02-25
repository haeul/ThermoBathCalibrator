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
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            pnlBacrground = new Panel();
            tlpRoot = new TableLayoutPanel();
            pnlHeader = new Panel();
            lblHeader = new Label();
            tlpSetting = new TableLayoutPanel();
            btnComSetting = new Button();
            tlpTop = new TableLayoutPanel();
            tlpCh1 = new TableLayoutPanel();
            lblCh1 = new Label();
            lblCh1Temperature = new Label();
            lblCh1OffsetTitle = new Label();
            lblCh1OffsetValue = new Label();
            nudOffsetCh1 = new NumericUpDown();
            btnOffsetApplyCh1 = new Button();
            tlpCh2 = new TableLayoutPanel();
            lblCh2 = new Label();
            lblCh2Temperature = new Label();
            lblCh2OffsetTitle = new Label();
            lblCh2OffsetValue = new Label();
            nudOffsetCh2 = new NumericUpDown();
            btnOffsetApplyCh2 = new Button();
            tlpGlobal = new TableLayoutPanel();
            btnStart = new Button();
            btnStop = new Button();
            tlpGraphs = new TableLayoutPanel();
            pnlCh1GraphWrap = new Panel();
            pnlCh1Graph = new Panel();
            pnlCh1GraphOverlay = new Panel();
            lblCh1GraphOffsetState = new Label();
            chkShowOffsetCh1 = new CheckBox();
            lblCh1GraphOffset = new Label();
            pnlCh2GraphWrap = new Panel();
            pnlCh2Graph = new Panel();
            pnlCh2GraphOverlay = new Panel();
            lblCh2GraphOffsetState = new Label();
            chkShowOffsetCh2 = new CheckBox();
            lblCh2GraphOffset = new Label();
            dataGridView1 = new DataGridView();
            colTimestamp = new DataGridViewTextBoxColumn();
            colUtCh1 = new DataGridViewTextBoxColumn();
            colUtCh2 = new DataGridViewTextBoxColumn();
            colMax1 = new DataGridViewTextBoxColumn();
            colMax2 = new DataGridViewTextBoxColumn();
            colMin1 = new DataGridViewTextBoxColumn();
            colMin2 = new DataGridViewTextBoxColumn();
            colAverage1 = new DataGridViewTextBoxColumn();
            colAverage2 = new DataGridViewTextBoxColumn();
            colOffset1 = new DataGridViewTextBoxColumn();
            colOffset2 = new DataGridViewTextBoxColumn();
            tlpCommStatus = new TableLayoutPanel();
            lblThermoPortStatus = new Label();
            pnlBacrground.SuspendLayout();
            tlpRoot.SuspendLayout();
            pnlHeader.SuspendLayout();
            tlpSetting.SuspendLayout();
            tlpTop.SuspendLayout();
            tlpCh1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudOffsetCh1).BeginInit();
            tlpCh2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudOffsetCh2).BeginInit();
            tlpGlobal.SuspendLayout();
            tlpGraphs.SuspendLayout();
            pnlCh1GraphWrap.SuspendLayout();
            pnlCh1GraphOverlay.SuspendLayout();
            pnlCh2GraphWrap.SuspendLayout();
            pnlCh2GraphOverlay.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            tlpCommStatus.SuspendLayout();
            SuspendLayout();
            // 
            // pnlBacrground
            // 
            pnlBacrground.BackColor = Color.White;
            pnlBacrground.Controls.Add(tlpRoot);
            pnlBacrground.Dock = DockStyle.Fill;
            pnlBacrground.Location = new Point(0, 0);
            pnlBacrground.Margin = new Padding(0);
            pnlBacrground.Name = "pnlBacrground";
            pnlBacrground.Padding = new Padding(12);
            pnlBacrground.Size = new Size(1184, 761);
            pnlBacrground.TabIndex = 0;
            // 
            // tlpRoot
            // 
            tlpRoot.BackColor = Color.White;
            tlpRoot.ColumnCount = 1;
            tlpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpRoot.Controls.Add(pnlHeader, 0, 0);
            tlpRoot.Controls.Add(tlpTop, 0, 1);
            tlpRoot.Controls.Add(tlpGraphs, 0, 2);
            tlpRoot.Controls.Add(dataGridView1, 0, 3);
            tlpRoot.Controls.Add(tlpCommStatus, 0, 4);
            tlpRoot.Dock = DockStyle.Fill;
            tlpRoot.Location = new Point(12, 12);
            tlpRoot.Margin = new Padding(0);
            tlpRoot.Name = "tlpRoot";
            tlpRoot.RowCount = 5;
            tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            tlpRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            tlpRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            tlpRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            tlpRoot.Size = new Size(1160, 737);
            tlpRoot.TabIndex = 0;
            // 
            // pnlHeader
            // 
            pnlHeader.BackColor = SystemColors.Control;
            pnlHeader.Controls.Add(lblHeader);
            pnlHeader.Controls.Add(tlpSetting);
            pnlHeader.Dock = DockStyle.Fill;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Margin = new Padding(0, 0, 0, 10);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Padding = new Padding(10);
            pnlHeader.Size = new Size(1160, 68);
            pnlHeader.TabIndex = 0;
            // 
            // lblHeader
            // 
            lblHeader.Dock = DockStyle.Fill;
            lblHeader.Font = new Font("Segoe UI", 24F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblHeader.ForeColor = SystemColors.ControlDarkDark;
            lblHeader.Location = new Point(10, 10);
            lblHeader.Margin = new Padding(0);
            lblHeader.Name = "lblHeader";
            lblHeader.Size = new Size(938, 48);
            lblHeader.TabIndex = 0;
            lblHeader.Text = "항온조 컨트롤러";
            lblHeader.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // tlpSetting
            // 
            tlpSetting.BackColor = Color.White;
            tlpSetting.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tlpSetting.ColumnCount = 1;
            tlpSetting.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpSetting.Controls.Add(btnComSetting, 0, 0);
            tlpSetting.Dock = DockStyle.Right;
            tlpSetting.Location = new Point(948, 10);
            tlpSetting.Margin = new Padding(0);
            tlpSetting.Name = "tlpSetting";
            tlpSetting.RowCount = 1;
            tlpSetting.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpSetting.Size = new Size(202, 48);
            tlpSetting.TabIndex = 1;
            // 
            // btnComSetting
            // 
            btnComSetting.BackColor = SystemColors.Control;
            btnComSetting.Dock = DockStyle.Fill;
            btnComSetting.Font = new Font("Segoe UI Semibold", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnComSetting.ForeColor = SystemColors.ControlText;
            btnComSetting.Location = new Point(1, 1);
            btnComSetting.Margin = new Padding(0);
            btnComSetting.Name = "btnComSetting";
            btnComSetting.Size = new Size(200, 46);
            btnComSetting.TabIndex = 0;
            btnComSetting.Text = "설정";
            btnComSetting.UseVisualStyleBackColor = false;
            // 
            // tlpTop
            // 
            tlpTop.BackColor = Color.White;
            tlpTop.ColumnCount = 3;
            tlpTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            tlpTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            tlpTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
            tlpTop.Controls.Add(tlpCh1, 0, 0);
            tlpTop.Controls.Add(tlpCh2, 1, 0);
            tlpTop.Controls.Add(tlpGlobal, 2, 0);
            tlpTop.Dock = DockStyle.Fill;
            tlpTop.Location = new Point(0, 78);
            tlpTop.Margin = new Padding(0, 0, 0, 10);
            tlpTop.Name = "tlpTop";
            tlpTop.RowCount = 1;
            tlpTop.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpTop.Size = new Size(1160, 110);
            tlpTop.TabIndex = 1;
            // 
            // tlpCh1
            // 
            tlpCh1.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tlpCh1.ColumnCount = 4;
            tlpCh1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            tlpCh1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            tlpCh1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            tlpCh1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            tlpCh1.Controls.Add(lblCh1, 0, 0);
            tlpCh1.Controls.Add(lblCh1Temperature, 1, 0);
            tlpCh1.Controls.Add(lblCh1OffsetTitle, 0, 1);
            tlpCh1.Controls.Add(lblCh1OffsetValue, 1, 1);
            tlpCh1.Controls.Add(nudOffsetCh1, 2, 1);
            tlpCh1.Controls.Add(btnOffsetApplyCh1, 3, 1);
            tlpCh1.Dock = DockStyle.Fill;
            tlpCh1.Location = new Point(0, 0);
            tlpCh1.Margin = new Padding(0, 0, 10, 0);
            tlpCh1.Name = "tlpCh1";
            tlpCh1.RowCount = 2;
            tlpCh1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpCh1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpCh1.Size = new Size(477, 110);
            tlpCh1.TabIndex = 0;
            // 
            // lblCh1
            // 
            lblCh1.BackColor = SystemColors.Control;
            lblCh1.Dock = DockStyle.Fill;
            lblCh1.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1.Location = new Point(1, 1);
            lblCh1.Margin = new Padding(0);
            lblCh1.Name = "lblCh1";
            lblCh1.Size = new Size(122, 53);
            lblCh1.TabIndex = 0;
            lblCh1.Text = "항온조1";
            lblCh1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh1Temperature
            // 
            lblCh1Temperature.BackColor = SystemColors.Control;
            tlpCh1.SetColumnSpan(lblCh1Temperature, 3);
            lblCh1Temperature.Dock = DockStyle.Fill;
            lblCh1Temperature.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1Temperature.Location = new Point(124, 1);
            lblCh1Temperature.Margin = new Padding(0);
            lblCh1Temperature.Name = "lblCh1Temperature";
            lblCh1Temperature.Size = new Size(352, 53);
            lblCh1Temperature.TabIndex = 1;
            lblCh1Temperature.Text = "-";
            lblCh1Temperature.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh1OffsetTitle
            // 
            lblCh1OffsetTitle.BackColor = SystemColors.Control;
            lblCh1OffsetTitle.Dock = DockStyle.Fill;
            lblCh1OffsetTitle.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1OffsetTitle.Location = new Point(1, 55);
            lblCh1OffsetTitle.Margin = new Padding(0);
            lblCh1OffsetTitle.Name = "lblCh1OffsetTitle";
            lblCh1OffsetTitle.Size = new Size(122, 54);
            lblCh1OffsetTitle.TabIndex = 2;
            lblCh1OffsetTitle.Text = "항온조1 오프셋";
            lblCh1OffsetTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh1OffsetValue
            // 
            lblCh1OffsetValue.BackColor = SystemColors.Control;
            lblCh1OffsetValue.Dock = DockStyle.Fill;
            lblCh1OffsetValue.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1OffsetValue.Location = new Point(124, 55);
            lblCh1OffsetValue.Margin = new Padding(0);
            lblCh1OffsetValue.Name = "lblCh1OffsetValue";
            lblCh1OffsetValue.Size = new Size(122, 54);
            lblCh1OffsetValue.TabIndex = 3;
            lblCh1OffsetValue.Text = "0.0";
            lblCh1OffsetValue.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // nudOffsetCh1
            // 
            nudOffsetCh1.DecimalPlaces = 1;
            nudOffsetCh1.Dock = DockStyle.Fill;
            nudOffsetCh1.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            nudOffsetCh1.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            nudOffsetCh1.Location = new Point(256, 64);
            nudOffsetCh1.Margin = new Padding(9);
            nudOffsetCh1.Maximum = new decimal(new int[] { 9, 0, 0, 0 });
            nudOffsetCh1.Minimum = new decimal(new int[] { 9, 0, 0, int.MinValue });
            nudOffsetCh1.Name = "nudOffsetCh1";
            nudOffsetCh1.Size = new Size(95, 36);
            nudOffsetCh1.TabIndex = 4;
            // 
            // btnOffsetApplyCh1
            // 
            btnOffsetApplyCh1.Dock = DockStyle.Fill;
            btnOffsetApplyCh1.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnOffsetApplyCh1.Location = new Point(367, 61);
            btnOffsetApplyCh1.Margin = new Padding(6);
            btnOffsetApplyCh1.Name = "btnOffsetApplyCh1";
            btnOffsetApplyCh1.Size = new Size(103, 42);
            btnOffsetApplyCh1.TabIndex = 5;
            btnOffsetApplyCh1.Text = "적용";
            // 
            // tlpCh2
            // 
            tlpCh2.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tlpCh2.ColumnCount = 4;
            tlpCh2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            tlpCh2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            tlpCh2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            tlpCh2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            tlpCh2.Controls.Add(lblCh2, 0, 0);
            tlpCh2.Controls.Add(lblCh2Temperature, 1, 0);
            tlpCh2.Controls.Add(lblCh2OffsetTitle, 0, 1);
            tlpCh2.Controls.Add(lblCh2OffsetValue, 1, 1);
            tlpCh2.Controls.Add(nudOffsetCh2, 2, 1);
            tlpCh2.Controls.Add(btnOffsetApplyCh2, 3, 1);
            tlpCh2.Dock = DockStyle.Fill;
            tlpCh2.Location = new Point(497, 0);
            tlpCh2.Margin = new Padding(10, 0, 10, 0);
            tlpCh2.Name = "tlpCh2";
            tlpCh2.RowCount = 2;
            tlpCh2.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpCh2.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpCh2.Size = new Size(467, 110);
            tlpCh2.TabIndex = 1;
            // 
            // lblCh2
            // 
            lblCh2.BackColor = SystemColors.Control;
            lblCh2.Dock = DockStyle.Fill;
            lblCh2.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2.Location = new Point(1, 1);
            lblCh2.Margin = new Padding(0);
            lblCh2.Name = "lblCh2";
            lblCh2.Size = new Size(120, 53);
            lblCh2.TabIndex = 0;
            lblCh2.Text = "항온조2";
            lblCh2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh2Temperature
            // 
            lblCh2Temperature.BackColor = SystemColors.Control;
            tlpCh2.SetColumnSpan(lblCh2Temperature, 3);
            lblCh2Temperature.Dock = DockStyle.Fill;
            lblCh2Temperature.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2Temperature.Location = new Point(122, 1);
            lblCh2Temperature.Margin = new Padding(0);
            lblCh2Temperature.Name = "lblCh2Temperature";
            lblCh2Temperature.Size = new Size(344, 53);
            lblCh2Temperature.TabIndex = 1;
            lblCh2Temperature.Text = "-";
            lblCh2Temperature.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh2OffsetTitle
            // 
            lblCh2OffsetTitle.BackColor = SystemColors.Control;
            lblCh2OffsetTitle.Dock = DockStyle.Fill;
            lblCh2OffsetTitle.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2OffsetTitle.Location = new Point(1, 55);
            lblCh2OffsetTitle.Margin = new Padding(0);
            lblCh2OffsetTitle.Name = "lblCh2OffsetTitle";
            lblCh2OffsetTitle.Size = new Size(120, 54);
            lblCh2OffsetTitle.TabIndex = 2;
            lblCh2OffsetTitle.Text = "항온조2 오프셋";
            lblCh2OffsetTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblCh2OffsetValue
            // 
            lblCh2OffsetValue.BackColor = SystemColors.Control;
            lblCh2OffsetValue.Dock = DockStyle.Fill;
            lblCh2OffsetValue.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2OffsetValue.Location = new Point(122, 55);
            lblCh2OffsetValue.Margin = new Padding(0);
            lblCh2OffsetValue.Name = "lblCh2OffsetValue";
            lblCh2OffsetValue.Size = new Size(120, 54);
            lblCh2OffsetValue.TabIndex = 3;
            lblCh2OffsetValue.Text = "0.0";
            lblCh2OffsetValue.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // nudOffsetCh2
            // 
            nudOffsetCh2.DecimalPlaces = 1;
            nudOffsetCh2.Dock = DockStyle.Fill;
            nudOffsetCh2.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            nudOffsetCh2.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            nudOffsetCh2.Location = new Point(252, 64);
            nudOffsetCh2.Margin = new Padding(9);
            nudOffsetCh2.Maximum = new decimal(new int[] { 9, 0, 0, 0 });
            nudOffsetCh2.Minimum = new decimal(new int[] { 9, 0, 0, int.MinValue });
            nudOffsetCh2.Name = "nudOffsetCh2";
            nudOffsetCh2.Size = new Size(92, 36);
            nudOffsetCh2.TabIndex = 4;
            // 
            // btnOffsetApplyCh2
            // 
            btnOffsetApplyCh2.Dock = DockStyle.Fill;
            btnOffsetApplyCh2.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnOffsetApplyCh2.Location = new Point(360, 61);
            btnOffsetApplyCh2.Margin = new Padding(6);
            btnOffsetApplyCh2.Name = "btnOffsetApplyCh2";
            btnOffsetApplyCh2.Size = new Size(100, 42);
            btnOffsetApplyCh2.TabIndex = 5;
            btnOffsetApplyCh2.Text = "적용";
            // 
            // tlpGlobal
            // 
            tlpGlobal.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tlpGlobal.ColumnCount = 1;
            tlpGlobal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpGlobal.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            tlpGlobal.Controls.Add(btnStart, 0, 0);
            tlpGlobal.Controls.Add(btnStop, 0, 1);
            tlpGlobal.Dock = DockStyle.Fill;
            tlpGlobal.Location = new Point(974, 0);
            tlpGlobal.Margin = new Padding(0);
            tlpGlobal.Name = "tlpGlobal";
            tlpGlobal.RowCount = 2;
            tlpGlobal.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpGlobal.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpGlobal.Size = new Size(186, 110);
            tlpGlobal.TabIndex = 2;
            // 
            // btnStart
            // 
            btnStart.Dock = DockStyle.Fill;
            btnStart.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnStart.Location = new Point(7, 7);
            btnStart.Margin = new Padding(6);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(172, 41);
            btnStart.TabIndex = 1;
            btnStart.Text = "시작";
            // 
            // btnStop
            // 
            btnStop.Dock = DockStyle.Fill;
            btnStop.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnStop.Location = new Point(7, 61);
            btnStop.Margin = new Padding(6);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(172, 42);
            btnStop.TabIndex = 2;
            btnStop.Text = "중지";
            // 
            // tlpGraphs
            // 
            tlpGraphs.ColumnCount = 2;
            tlpGraphs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpGraphs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpGraphs.Controls.Add(pnlCh1GraphWrap, 0, 0);
            tlpGraphs.Controls.Add(pnlCh2GraphWrap, 1, 0);
            tlpGraphs.Dock = DockStyle.Fill;
            tlpGraphs.Location = new Point(0, 198);
            tlpGraphs.Margin = new Padding(0, 0, 0, 10);
            tlpGraphs.Name = "tlpGraphs";
            tlpGraphs.RowCount = 1;
            tlpGraphs.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpGraphs.Size = new Size(1160, 187);
            tlpGraphs.TabIndex = 2;
            // 
            // pnlCh1GraphWrap
            // 
            pnlCh1GraphWrap.BackColor = Color.White;
            pnlCh1GraphWrap.Controls.Add(pnlCh1Graph);
            pnlCh1GraphWrap.Controls.Add(pnlCh1GraphOverlay);
            pnlCh1GraphWrap.Dock = DockStyle.Fill;
            pnlCh1GraphWrap.Location = new Point(0, 0);
            pnlCh1GraphWrap.Margin = new Padding(0, 0, 10, 0);
            pnlCh1GraphWrap.Name = "pnlCh1GraphWrap";
            pnlCh1GraphWrap.Size = new Size(570, 187);
            pnlCh1GraphWrap.TabIndex = 0;
            // 
            // pnlCh1Graph
            // 
            pnlCh1Graph.BackColor = Color.White;
            pnlCh1Graph.Dock = DockStyle.Fill;
            pnlCh1Graph.Location = new Point(0, 0);
            pnlCh1Graph.Margin = new Padding(0);
            pnlCh1Graph.Name = "pnlCh1Graph";
            pnlCh1Graph.Size = new Size(570, 187);
            pnlCh1Graph.TabIndex = 0;
            // 
            // pnlCh1GraphOverlay
            // 
            pnlCh1GraphOverlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlCh1GraphOverlay.BackColor = Color.FromArgb(245, 245, 245);
            pnlCh1GraphOverlay.BorderStyle = BorderStyle.FixedSingle;
            pnlCh1GraphOverlay.Controls.Add(lblCh1GraphOffsetState);
            pnlCh1GraphOverlay.Controls.Add(chkShowOffsetCh1);
            pnlCh1GraphOverlay.Controls.Add(lblCh1GraphOffset);
            pnlCh1GraphOverlay.Location = new Point(381, 10);
            pnlCh1GraphOverlay.Name = "pnlCh1GraphOverlay";
            pnlCh1GraphOverlay.Size = new Size(125, 30);
            pnlCh1GraphOverlay.TabIndex = 1;
            // 
            // lblCh1GraphOffsetState
            // 
            lblCh1GraphOffsetState.Dock = DockStyle.Fill;
            lblCh1GraphOffsetState.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1GraphOffsetState.Location = new Point(75, 0);
            lblCh1GraphOffsetState.Margin = new Padding(0);
            lblCh1GraphOffsetState.Name = "lblCh1GraphOffsetState";
            lblCh1GraphOffsetState.Size = new Size(55, 28);
            lblCh1GraphOffsetState.TabIndex = 0;
            lblCh1GraphOffsetState.Text = "오프셋 그래프";
            lblCh1GraphOffsetState.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // chkShowOffsetCh1
            // 
            chkShowOffsetCh1.Checked = true;
            chkShowOffsetCh1.CheckState = CheckState.Checked;
            chkShowOffsetCh1.Dock = DockStyle.Left;
            chkShowOffsetCh1.Location = new Point(55, 0);
            chkShowOffsetCh1.Margin = new Padding(0);
            chkShowOffsetCh1.Name = "chkShowOffsetCh1";
            chkShowOffsetCh1.Size = new Size(20, 28);
            chkShowOffsetCh1.TabIndex = 1;
            // 
            // lblCh1GraphOffset
            // 
            lblCh1GraphOffset.Dock = DockStyle.Left;
            lblCh1GraphOffset.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh1GraphOffset.Location = new Point(0, 0);
            lblCh1GraphOffset.Margin = new Padding(0);
            lblCh1GraphOffset.Name = "lblCh1GraphOffset";
            lblCh1GraphOffset.Size = new Size(5, 28);
            lblCh1GraphOffset.TabIndex = 2;
            lblCh1GraphOffset.Text = "";
            lblCh1GraphOffset.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pnlCh2GraphWrap
            // 
            pnlCh2GraphWrap.BackColor = Color.White;
            pnlCh2GraphWrap.Controls.Add(pnlCh2Graph);
            pnlCh2GraphWrap.Controls.Add(pnlCh2GraphOverlay);
            pnlCh2GraphWrap.Dock = DockStyle.Fill;
            pnlCh2GraphWrap.Location = new Point(590, 0);
            pnlCh2GraphWrap.Margin = new Padding(10, 0, 0, 0);
            pnlCh2GraphWrap.Name = "pnlCh2GraphWrap";
            pnlCh2GraphWrap.Size = new Size(570, 187);
            pnlCh2GraphWrap.TabIndex = 1;
            // 
            // pnlCh2Graph
            // 
            pnlCh2Graph.BackColor = Color.White;
            pnlCh2Graph.Dock = DockStyle.Fill;
            pnlCh2Graph.Location = new Point(0, 0);
            pnlCh2Graph.Margin = new Padding(0);
            pnlCh2Graph.Name = "pnlCh2Graph";
            pnlCh2Graph.Size = new Size(570, 187);
            pnlCh2Graph.TabIndex = 0;
            // 
            // pnlCh2GraphOverlay
            // 
            pnlCh2GraphOverlay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlCh2GraphOverlay.BackColor = Color.FromArgb(245, 245, 245);
            pnlCh2GraphOverlay.BorderStyle = BorderStyle.FixedSingle;
            pnlCh2GraphOverlay.Controls.Add(lblCh2GraphOffsetState);
            pnlCh2GraphOverlay.Controls.Add(chkShowOffsetCh2);
            pnlCh2GraphOverlay.Controls.Add(lblCh2GraphOffset);
            pnlCh2GraphOverlay.Location = new Point(381, 10);
            pnlCh2GraphOverlay.Name = "pnlCh2GraphOverlay";
            pnlCh2GraphOverlay.Size = new Size(125, 30);
            pnlCh2GraphOverlay.TabIndex = 1;
            // 
            // lblCh2GraphOffsetState
            // 
            lblCh2GraphOffsetState.Dock = DockStyle.Fill;
            lblCh2GraphOffsetState.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2GraphOffsetState.Location = new Point(83, 0);
            lblCh2GraphOffsetState.Margin = new Padding(0);
            lblCh2GraphOffsetState.Name = "lblCh2GraphOffsetState";
            lblCh2GraphOffsetState.Size = new Size(55, 28);
            lblCh2GraphOffsetState.TabIndex = 0;
            lblCh2GraphOffsetState.Text = "오프셋 그래프";
            lblCh2GraphOffsetState.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // chkShowOffsetCh2
            // 
            chkShowOffsetCh2.Checked = true;
            chkShowOffsetCh2.CheckState = CheckState.Checked;
            chkShowOffsetCh2.Dock = DockStyle.Left;
            chkShowOffsetCh2.Location = new Point(55, 0);
            chkShowOffsetCh2.Margin = new Padding(0);
            chkShowOffsetCh2.Name = "chkShowOffsetCh2";
            chkShowOffsetCh2.Size = new Size(20, 28);
            chkShowOffsetCh2.TabIndex = 1;
            // 
            // lblCh2GraphOffset
            // 
            lblCh2GraphOffset.Dock = DockStyle.Left;
            lblCh2GraphOffset.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCh2GraphOffset.Location = new Point(0, 0);
            lblCh2GraphOffset.Margin = new Padding(0);
            lblCh2GraphOffset.Name = "lblCh2GraphOffset";
            lblCh2GraphOffset.Size = new Size(5, 28);
            lblCh2GraphOffset.TabIndex = 2;
            lblCh2GraphOffset.Text = "";
            lblCh2GraphOffset.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
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
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { colTimestamp, colUtCh1, colUtCh2, colMax1, colMax2, colMin1, colMin2, colAverage1, colAverage2, colOffset1, colOffset2 });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = SystemColors.Window;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle3.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dataGridView1.DefaultCellStyle = dataGridViewCellStyle3;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.Location = new Point(0, 395);
            dataGridView1.Margin = new Padding(0);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.Size = new Size(1160, 241);
            dataGridView1.TabIndex = 3;
            // 
            // colTimestamp
            // 
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colTimestamp.DefaultCellStyle = dataGridViewCellStyle2;
            colTimestamp.FillWeight = 18F;
            colTimestamp.HeaderText = "시간";
            colTimestamp.MinimumWidth = 180;
            colTimestamp.Name = "colTimestamp";
            colTimestamp.ReadOnly = true;
            // 
            // colUtCh1
            // 
            colUtCh1.FillWeight = 9F;
            colUtCh1.HeaderText = "항온조1";
            colUtCh1.MinimumWidth = 80;
            colUtCh1.Name = "colUtCh1";
            colUtCh1.ReadOnly = true;
            // 
            // colUtCh2
            // 
            colUtCh2.FillWeight = 9F;
            colUtCh2.HeaderText = "항온조2";
            colUtCh2.MinimumWidth = 80;
            colUtCh2.Name = "colUtCh2";
            colUtCh2.ReadOnly = true;
            // 
            // colMax1
            // 
            colMax1.FillWeight = 9F;
            colMax1.HeaderText = "항온조1 최대값";
            colMax1.MinimumWidth = 80;
            colMax1.Name = "colMax1";
            colMax1.ReadOnly = true;
            // 
            // colMax2
            // 
            colMax2.FillWeight = 9F;
            colMax2.HeaderText = "항온조2 최대값";
            colMax2.MinimumWidth = 80;
            colMax2.Name = "colMax2";
            colMax2.ReadOnly = true;
            // 
            // colMin1
            // 
            colMin1.FillWeight = 9F;
            colMin1.HeaderText = "항온조1 최소값";
            colMin1.MinimumWidth = 80;
            colMin1.Name = "colMin1";
            colMin1.ReadOnly = true;
            // 
            // colMin2
            // 
            colMin2.FillWeight = 9F;
            colMin2.HeaderText = "항온조2 최소값";
            colMin2.MinimumWidth = 80;
            colMin2.Name = "colMin2";
            colMin2.ReadOnly = true;
            // 
            // colAverage1
            // 
            colAverage1.FillWeight = 10F;
            colAverage1.HeaderText = "항온조1 평균값";
            colAverage1.MinimumWidth = 90;
            colAverage1.Name = "colAverage1";
            colAverage1.ReadOnly = true;
            // 
            // colAverage2
            // 
            colAverage2.FillWeight = 10F;
            colAverage2.HeaderText = "항온조2 평균값";
            colAverage2.MinimumWidth = 90;
            colAverage2.Name = "colAverage2";
            colAverage2.ReadOnly = true;
            // 
            // colOffset1
            // 
            colOffset1.FillWeight = 9F;
            colOffset1.HeaderText = "항온조1 오프셋";
            colOffset1.MinimumWidth = 80;
            colOffset1.Name = "colOffset1";
            colOffset1.ReadOnly = true;
            // 
            // colOffset2
            // 
            colOffset2.FillWeight = 9F;
            colOffset2.HeaderText = "항온조2 오프셋";
            colOffset2.MinimumWidth = 80;
            colOffset2.Name = "colOffset2";
            colOffset2.ReadOnly = true;
            // 
            // tlpCommStatus
            // 
            tlpCommStatus.BackColor = SystemColors.Control;
            tlpCommStatus.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tlpCommStatus.ColumnCount = 1;
            tlpCommStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpCommStatus.Controls.Add(lblThermoPortStatus, 0, 0);
            tlpCommStatus.Dock = DockStyle.Fill;
            tlpCommStatus.Location = new Point(0, 636);
            tlpCommStatus.Margin = new Padding(0);
            tlpCommStatus.Name = "tlpCommStatus";
            tlpCommStatus.RowCount = 1;
            tlpCommStatus.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpCommStatus.Size = new Size(1160, 101);
            tlpCommStatus.TabIndex = 4;
            // 
            // lblThermoPortStatus
            // 
            lblThermoPortStatus.Dock = DockStyle.Fill;
            lblThermoPortStatus.Font = new Font("Segoe UI", 30F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblThermoPortStatus.ForeColor = Color.Gray;
            lblThermoPortStatus.Location = new Point(1, 1);
            lblThermoPortStatus.Margin = new Padding(0);
            lblThermoPortStatus.Name = "lblThermoPortStatus";
            lblThermoPortStatus.Size = new Size(1158, 99);
            lblThermoPortStatus.TabIndex = 0;
            lblThermoPortStatus.Text = "BOARD";
            lblThermoPortStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1184, 761);
            Controls.Add(pnlBacrground);
            Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            MinimumSize = new Size(1200, 800);
            Name = "FormMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ThermoBathCalibrator";
            pnlBacrground.ResumeLayout(false);
            tlpRoot.ResumeLayout(false);
            pnlHeader.ResumeLayout(false);
            tlpSetting.ResumeLayout(false);
            tlpTop.ResumeLayout(false);
            tlpCh1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)nudOffsetCh1).EndInit();
            tlpCh2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)nudOffsetCh2).EndInit();
            tlpGlobal.ResumeLayout(false);
            tlpGraphs.ResumeLayout(false);
            pnlCh1GraphWrap.ResumeLayout(false);
            pnlCh1GraphOverlay.ResumeLayout(false);
            pnlCh2GraphWrap.ResumeLayout(false);
            pnlCh2GraphOverlay.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            tlpCommStatus.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlBacrground;

        private TableLayoutPanel tlpRoot;

        private Panel pnlHeader;
        private Label lblHeader;
        private TableLayoutPanel tlpSetting;
        private Button btnComSetting;

        private TableLayoutPanel tlpTop;
        private TableLayoutPanel tlpCh1;
        private TableLayoutPanel tlpCh2;
        private TableLayoutPanel tlpGlobal;

        private Label lblCh1;
        private Label lblCh1Temperature;
        private Label lblCh1OffsetTitle;
        private Label lblCh1OffsetValue;
        private NumericUpDown nudOffsetCh1;
        private Button btnOffsetApplyCh1;

        private Label lblCh2;
        private Label lblCh2Temperature;
        private Label lblCh2OffsetTitle;
        private Label lblCh2OffsetValue;
        private NumericUpDown nudOffsetCh2;
        private Button btnOffsetApplyCh2;

        private TableLayoutPanel tlpGraphs;

        // Graph panels + overlay
        private Panel pnlCh1GraphWrap;
        private Panel pnlCh1Graph;
        private Panel pnlCh1GraphOverlay;
        private Label lblCh1GraphOffset;
        private CheckBox chkShowOffsetCh1;
        private Label lblCh1GraphOffsetState;

        private Panel pnlCh2GraphWrap;
        private Panel pnlCh2Graph;
        private Panel pnlCh2GraphOverlay;
        private Label lblCh2GraphOffset;
        private CheckBox chkShowOffsetCh2;
        private Label lblCh2GraphOffsetState;

        private DataGridView dataGridView1;

        private TableLayoutPanel tlpCommStatus;
        private Label lblThermoPortStatus;
        private DataGridViewTextBoxColumn colTimestamp;
        private DataGridViewTextBoxColumn colUtCh1;
        private DataGridViewTextBoxColumn colUtCh2;
        private DataGridViewTextBoxColumn colMax1;
        private DataGridViewTextBoxColumn colMax2;
        private DataGridViewTextBoxColumn colMin1;
        private DataGridViewTextBoxColumn colMin2;
        private DataGridViewTextBoxColumn colAverage1;
        private DataGridViewTextBoxColumn colAverage2;
        private DataGridViewTextBoxColumn colOffset1;
        private DataGridViewTextBoxColumn colOffset2;
        private Button btnStart;
        private Button btnStop;
    }
}