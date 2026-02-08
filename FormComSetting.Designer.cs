using System.Drawing;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    partial class FormComSetting
    {
        private System.ComponentModel.IContainer components = null;

        private GroupBox grpBath1;
        private GroupBox grpBath2;
        private GroupBox grpUt;

        // 추가: 멀티보드 그룹
        private GroupBox grpMultiBoard;
        private Label lblMbHost;
        private Label lblMbPort;
        private Label lblMbUnitId;
        private TextBox txtMbHost;
        private NumericUpDown numMbPort;
        private NumericUpDown numMbUnitId;
        private Button btnTestMb;

        private Label lblBath1Port;
        private Label lblBath1Baud;
        private Label lblBath1Parity;
        private Label lblBath1DataBits;
        private Label lblBath1StopBits;
        private Label lblBath1Addr;
        private Label lblBath1Bcc;

        private ComboBox cmbBath1Port;
        private ComboBox cmbBath1Baud;
        private ComboBox cmbBath1Parity;
        private ComboBox cmbBath1DataBits;
        private ComboBox cmbBath1StopBits;
        private NumericUpDown numBath1Addr;
        private CheckBox chkBath1Bcc;

        private Button btnOpenBath1;
        private Button btnTestBath1;

        private Label lblBath2Port;
        private Label lblBath2Baud;
        private Label lblBath2Parity;
        private Label lblBath2DataBits;
        private Label lblBath2StopBits;
        private Label lblBath2Addr;
        private Label lblBath2Bcc;

        private ComboBox cmbBath2Port;
        private ComboBox cmbBath2Baud;
        private ComboBox cmbBath2Parity;
        private ComboBox cmbBath2DataBits;
        private ComboBox cmbBath2StopBits;
        private NumericUpDown numBath2Addr;
        private CheckBox chkBath2Bcc;

        private Button btnOpenBath2;
        private Button btnTestBath2;

        private Label lblUtPort;
        private Label lblUtBaud;
        private Label lblUtParity;
        private Label lblUtDataBits;
        private Label lblUtStopBits;
        private Label lblUtTerm;

        private ComboBox cmbUtPort;
        private ComboBox cmbUtBaud;
        private ComboBox cmbUtParity;
        private ComboBox cmbUtDataBits;
        private ComboBox cmbUtStopBits;
        private ComboBox cmbUtTerm;

        private Button btnOpenUt;
        private Button btnTestUt;

        private Button btnReload;
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
            grpBath1 = new GroupBox();
            lblBath1Port = new Label();
            cmbBath1Port = new ComboBox();
            lblBath1Baud = new Label();
            cmbBath1Baud = new ComboBox();
            lblBath1Parity = new Label();
            cmbBath1Parity = new ComboBox();
            lblBath1DataBits = new Label();
            cmbBath1DataBits = new ComboBox();
            lblBath1StopBits = new Label();
            cmbBath1StopBits = new ComboBox();
            lblBath1Addr = new Label();
            numBath1Addr = new NumericUpDown();
            lblBath1Bcc = new Label();
            chkBath1Bcc = new CheckBox();
            btnOpenBath1 = new Button();
            btnTestBath1 = new Button();

            grpBath2 = new GroupBox();
            lblBath2Port = new Label();
            cmbBath2Port = new ComboBox();
            lblBath2Baud = new Label();
            cmbBath2Baud = new ComboBox();
            lblBath2Parity = new Label();
            cmbBath2Parity = new ComboBox();
            lblBath2DataBits = new Label();
            cmbBath2DataBits = new ComboBox();
            lblBath2StopBits = new Label();
            cmbBath2StopBits = new ComboBox();
            lblBath2Addr = new Label();
            numBath2Addr = new NumericUpDown();
            lblBath2Bcc = new Label();
            chkBath2Bcc = new CheckBox();
            btnOpenBath2 = new Button();
            btnTestBath2 = new Button();

            grpUt = new GroupBox();
            lblUtPort = new Label();
            cmbUtPort = new ComboBox();
            lblUtBaud = new Label();
            cmbUtBaud = new ComboBox();
            lblUtParity = new Label();
            cmbUtParity = new ComboBox();
            lblUtDataBits = new Label();
            cmbUtDataBits = new ComboBox();
            lblUtStopBits = new Label();
            cmbUtStopBits = new ComboBox();
            lblUtTerm = new Label();
            cmbUtTerm = new ComboBox();
            btnOpenUt = new Button();
            btnTestUt = new Button();

            // 추가: 멀티보드 그룹
            grpMultiBoard = new GroupBox();
            lblMbHost = new Label();
            lblMbPort = new Label();
            lblMbUnitId = new Label();
            txtMbHost = new TextBox();
            numMbPort = new NumericUpDown();
            numMbUnitId = new NumericUpDown();
            btnTestMb = new Button();

            btnReload = new Button();
            btnSave = new Button();
            btnClose = new Button();
            txtLog = new TextBox();

            grpBath1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numBath1Addr).BeginInit();
            grpBath2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numBath2Addr).BeginInit();
            grpUt.SuspendLayout();
            grpMultiBoard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numMbPort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numMbUnitId).BeginInit();
            SuspendLayout();

            // grpBath1 (기존 그대로)
            grpBath1.Controls.Add(lblBath1Port);
            grpBath1.Controls.Add(cmbBath1Port);
            grpBath1.Controls.Add(lblBath1Baud);
            grpBath1.Controls.Add(cmbBath1Baud);
            grpBath1.Controls.Add(lblBath1Parity);
            grpBath1.Controls.Add(cmbBath1Parity);
            grpBath1.Controls.Add(lblBath1DataBits);
            grpBath1.Controls.Add(cmbBath1DataBits);
            grpBath1.Controls.Add(lblBath1StopBits);
            grpBath1.Controls.Add(cmbBath1StopBits);
            grpBath1.Controls.Add(lblBath1Addr);
            grpBath1.Controls.Add(numBath1Addr);
            grpBath1.Controls.Add(lblBath1Bcc);
            grpBath1.Controls.Add(chkBath1Bcc);
            grpBath1.Controls.Add(btnOpenBath1);
            grpBath1.Controls.Add(btnTestBath1);
            grpBath1.Location = new Point(12, 12);
            grpBath1.Name = "grpBath1";
            grpBath1.Size = new Size(320, 300);
            grpBath1.TabIndex = 0;
            grpBath1.TabStop = false;
            grpBath1.Text = "BATH #1 (HEBC RS-485)";

            lblBath1Port.Location = new Point(16, 28);
            lblBath1Port.Name = "lblBath1Port";
            lblBath1Port.Size = new Size(90, 20);
            lblBath1Port.TabIndex = 0;
            lblBath1Port.Text = "Port";

            cmbBath1Port.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath1Port.Location = new Point(110, 26);
            cmbBath1Port.Name = "cmbBath1Port";
            cmbBath1Port.Size = new Size(190, 23);
            cmbBath1Port.TabIndex = 1;

            lblBath1Baud.Location = new Point(16, 56);
            lblBath1Baud.Name = "lblBath1Baud";
            lblBath1Baud.Size = new Size(90, 20);
            lblBath1Baud.TabIndex = 2;
            lblBath1Baud.Text = "Baud";

            cmbBath1Baud.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath1Baud.Location = new Point(110, 54);
            cmbBath1Baud.Name = "cmbBath1Baud";
            cmbBath1Baud.Size = new Size(190, 23);
            cmbBath1Baud.TabIndex = 3;

            lblBath1Parity.Location = new Point(16, 84);
            lblBath1Parity.Name = "lblBath1Parity";
            lblBath1Parity.Size = new Size(90, 20);
            lblBath1Parity.TabIndex = 4;
            lblBath1Parity.Text = "Parity";

            cmbBath1Parity.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath1Parity.Location = new Point(110, 82);
            cmbBath1Parity.Name = "cmbBath1Parity";
            cmbBath1Parity.Size = new Size(190, 23);
            cmbBath1Parity.TabIndex = 5;

            lblBath1DataBits.Location = new Point(16, 112);
            lblBath1DataBits.Name = "lblBath1DataBits";
            lblBath1DataBits.Size = new Size(90, 20);
            lblBath1DataBits.TabIndex = 6;
            lblBath1DataBits.Text = "DataBits";

            cmbBath1DataBits.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath1DataBits.Location = new Point(110, 110);
            cmbBath1DataBits.Name = "cmbBath1DataBits";
            cmbBath1DataBits.Size = new Size(190, 23);
            cmbBath1DataBits.TabIndex = 7;

            lblBath1StopBits.Location = new Point(16, 140);
            lblBath1StopBits.Name = "lblBath1StopBits";
            lblBath1StopBits.Size = new Size(90, 20);
            lblBath1StopBits.TabIndex = 8;
            lblBath1StopBits.Text = "StopBits";

            cmbBath1StopBits.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath1StopBits.Location = new Point(110, 138);
            cmbBath1StopBits.Name = "cmbBath1StopBits";
            cmbBath1StopBits.Size = new Size(190, 23);
            cmbBath1StopBits.TabIndex = 9;

            lblBath1Addr.Location = new Point(16, 168);
            lblBath1Addr.Name = "lblBath1Addr";
            lblBath1Addr.Size = new Size(90, 20);
            lblBath1Addr.TabIndex = 10;
            lblBath1Addr.Text = "Address";

            numBath1Addr.Location = new Point(110, 166);
            numBath1Addr.Maximum = new decimal(new int[] { 99, 0, 0, 0 });
            numBath1Addr.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numBath1Addr.Name = "numBath1Addr";
            numBath1Addr.Size = new Size(190, 23);
            numBath1Addr.TabIndex = 11;
            numBath1Addr.Value = new decimal(new int[] { 1, 0, 0, 0 });

            lblBath1Bcc.Location = new Point(16, 196);
            lblBath1Bcc.Name = "lblBath1Bcc";
            lblBath1Bcc.Size = new Size(90, 20);
            lblBath1Bcc.TabIndex = 12;
            lblBath1Bcc.Text = "BCC";

            chkBath1Bcc.Location = new Point(110, 194);
            chkBath1Bcc.Name = "chkBath1Bcc";
            chkBath1Bcc.Size = new Size(190, 24);
            chkBath1Bcc.TabIndex = 13;
            chkBath1Bcc.Text = "Use BCC";

            btnOpenBath1.Location = new Point(110, 222);
            btnOpenBath1.Name = "btnOpenBath1";
            btnOpenBath1.Size = new Size(190, 32);
            btnOpenBath1.TabIndex = 14;
            btnOpenBath1.Text = "OPEN";

            btnTestBath1.Location = new Point(110, 254);
            btnTestBath1.Name = "btnTestBath1";
            btnTestBath1.Size = new Size(190, 32);
            btnTestBath1.TabIndex = 15;
            btnTestBath1.Text = "통신 테스트(PV1)";

            // grpBath2 (기존 그대로)
            grpBath2.Controls.Add(lblBath2Port);
            grpBath2.Controls.Add(cmbBath2Port);
            grpBath2.Controls.Add(lblBath2Baud);
            grpBath2.Controls.Add(cmbBath2Baud);
            grpBath2.Controls.Add(lblBath2Parity);
            grpBath2.Controls.Add(cmbBath2Parity);
            grpBath2.Controls.Add(lblBath2DataBits);
            grpBath2.Controls.Add(cmbBath2DataBits);
            grpBath2.Controls.Add(lblBath2StopBits);
            grpBath2.Controls.Add(cmbBath2StopBits);
            grpBath2.Controls.Add(lblBath2Addr);
            grpBath2.Controls.Add(numBath2Addr);
            grpBath2.Controls.Add(lblBath2Bcc);
            grpBath2.Controls.Add(chkBath2Bcc);
            grpBath2.Controls.Add(btnOpenBath2);
            grpBath2.Controls.Add(btnTestBath2);
            grpBath2.Location = new Point(340, 12);
            grpBath2.Name = "grpBath2";
            grpBath2.Size = new Size(320, 300);
            grpBath2.TabIndex = 1;
            grpBath2.TabStop = false;
            grpBath2.Text = "BATH #2 (HEBC RS-485)";

            lblBath2Port.Location = new Point(16, 28);
            lblBath2Port.Name = "lblBath2Port";
            lblBath2Port.Size = new Size(90, 20);
            lblBath2Port.TabIndex = 0;
            lblBath2Port.Text = "Port";

            cmbBath2Port.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath2Port.Location = new Point(110, 26);
            cmbBath2Port.Name = "cmbBath2Port";
            cmbBath2Port.Size = new Size(190, 23);
            cmbBath2Port.TabIndex = 1;

            lblBath2Baud.Location = new Point(16, 56);
            lblBath2Baud.Name = "lblBath2Baud";
            lblBath2Baud.Size = new Size(90, 20);
            lblBath2Baud.TabIndex = 2;
            lblBath2Baud.Text = "Baud";

            cmbBath2Baud.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath2Baud.Location = new Point(110, 54);
            cmbBath2Baud.Name = "cmbBath2Baud";
            cmbBath2Baud.Size = new Size(190, 23);
            cmbBath2Baud.TabIndex = 3;

            lblBath2Parity.Location = new Point(16, 84);
            lblBath2Parity.Name = "lblBath2Parity";
            lblBath2Parity.Size = new Size(90, 20);
            lblBath2Parity.TabIndex = 4;
            lblBath2Parity.Text = "Parity";

            cmbBath2Parity.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath2Parity.Location = new Point(110, 82);
            cmbBath2Parity.Name = "cmbBath2Parity";
            cmbBath2Parity.Size = new Size(190, 23);
            cmbBath2Parity.TabIndex = 5;

            lblBath2DataBits.Location = new Point(16, 112);
            lblBath2DataBits.Name = "lblBath2DataBits";
            lblBath2DataBits.Size = new Size(90, 20);
            lblBath2DataBits.TabIndex = 6;
            lblBath2DataBits.Text = "DataBits";

            cmbBath2DataBits.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath2DataBits.Location = new Point(110, 110);
            cmbBath2DataBits.Name = "cmbBath2DataBits";
            cmbBath2DataBits.Size = new Size(190, 23);
            cmbBath2DataBits.TabIndex = 7;

            lblBath2StopBits.Location = new Point(16, 140);
            lblBath2StopBits.Name = "lblBath2StopBits";
            lblBath2StopBits.Size = new Size(90, 20);
            lblBath2StopBits.TabIndex = 8;
            lblBath2StopBits.Text = "StopBits";

            cmbBath2StopBits.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBath2StopBits.Location = new Point(110, 138);
            cmbBath2StopBits.Name = "cmbBath2StopBits";
            cmbBath2StopBits.Size = new Size(190, 23);
            cmbBath2StopBits.TabIndex = 9;

            lblBath2Addr.Location = new Point(16, 168);
            lblBath2Addr.Name = "lblBath2Addr";
            lblBath2Addr.Size = new Size(90, 20);
            lblBath2Addr.TabIndex = 10;
            lblBath2Addr.Text = "Address";

            numBath2Addr.Location = new Point(110, 166);
            numBath2Addr.Maximum = new decimal(new int[] { 99, 0, 0, 0 });
            numBath2Addr.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numBath2Addr.Name = "numBath2Addr";
            numBath2Addr.Size = new Size(190, 23);
            numBath2Addr.TabIndex = 11;
            numBath2Addr.Value = new decimal(new int[] { 1, 0, 0, 0 });

            lblBath2Bcc.Location = new Point(16, 196);
            lblBath2Bcc.Name = "lblBath2Bcc";
            lblBath2Bcc.Size = new Size(90, 20);
            lblBath2Bcc.TabIndex = 12;
            lblBath2Bcc.Text = "BCC";

            chkBath2Bcc.Location = new Point(110, 194);
            chkBath2Bcc.Name = "chkBath2Bcc";
            chkBath2Bcc.Size = new Size(190, 24);
            chkBath2Bcc.TabIndex = 13;
            chkBath2Bcc.Text = "Use BCC";

            btnOpenBath2.Location = new Point(110, 222);
            btnOpenBath2.Name = "btnOpenBath2";
            btnOpenBath2.Size = new Size(190, 32);
            btnOpenBath2.TabIndex = 14;
            btnOpenBath2.Text = "OPEN";

            btnTestBath2.Location = new Point(110, 254);
            btnTestBath2.Name = "btnTestBath2";
            btnTestBath2.Size = new Size(190, 32);
            btnTestBath2.TabIndex = 15;
            btnTestBath2.Text = "통신 테스트(PV1)";

            // grpUt (기존 그대로)
            grpUt.Controls.Add(lblUtPort);
            grpUt.Controls.Add(cmbUtPort);
            grpUt.Controls.Add(lblUtBaud);
            grpUt.Controls.Add(cmbUtBaud);
            grpUt.Controls.Add(lblUtParity);
            grpUt.Controls.Add(cmbUtParity);
            grpUt.Controls.Add(lblUtDataBits);
            grpUt.Controls.Add(cmbUtDataBits);
            grpUt.Controls.Add(lblUtStopBits);
            grpUt.Controls.Add(cmbUtStopBits);
            grpUt.Controls.Add(lblUtTerm);
            grpUt.Controls.Add(cmbUtTerm);
            grpUt.Controls.Add(btnOpenUt);
            grpUt.Controls.Add(btnTestUt);
            grpUt.Location = new Point(668, 12);
            grpUt.Name = "grpUt";
            grpUt.Size = new Size(320, 300);
            grpUt.TabIndex = 2;
            grpUt.TabStop = false;
            grpUt.Text = "UT-ONE (RS-232)";

            lblUtPort.Location = new Point(16, 28);
            lblUtPort.Name = "lblUtPort";
            lblUtPort.Size = new Size(90, 20);
            lblUtPort.TabIndex = 0;
            lblUtPort.Text = "Port";

            cmbUtPort.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUtPort.Location = new Point(110, 26);
            cmbUtPort.Name = "cmbUtPort";
            cmbUtPort.Size = new Size(190, 23);
            cmbUtPort.TabIndex = 1;

            lblUtBaud.Location = new Point(16, 56);
            lblUtBaud.Name = "lblUtBaud";
            lblUtBaud.Size = new Size(90, 20);
            lblUtBaud.TabIndex = 2;
            lblUtBaud.Text = "Baud";

            cmbUtBaud.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUtBaud.Location = new Point(110, 54);
            cmbUtBaud.Name = "cmbUtBaud";
            cmbUtBaud.Size = new Size(190, 23);
            cmbUtBaud.TabIndex = 3;

            lblUtParity.Location = new Point(16, 84);
            lblUtParity.Name = "lblUtParity";
            lblUtParity.Size = new Size(90, 20);
            lblUtParity.TabIndex = 4;
            lblUtParity.Text = "Parity";

            cmbUtParity.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUtParity.Location = new Point(110, 82);
            cmbUtParity.Name = "cmbUtParity";
            cmbUtParity.Size = new Size(190, 23);
            cmbUtParity.TabIndex = 5;

            lblUtDataBits.Location = new Point(16, 112);
            lblUtDataBits.Name = "lblUtDataBits";
            lblUtDataBits.Size = new Size(90, 20);
            lblUtDataBits.TabIndex = 6;
            lblUtDataBits.Text = "DataBits";

            cmbUtDataBits.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUtDataBits.Location = new Point(110, 110);
            cmbUtDataBits.Name = "cmbUtDataBits";
            cmbUtDataBits.Size = new Size(190, 23);
            cmbUtDataBits.TabIndex = 7;

            lblUtStopBits.Location = new Point(16, 140);
            lblUtStopBits.Name = "lblUtStopBits";
            lblUtStopBits.Size = new Size(90, 20);
            lblUtStopBits.TabIndex = 8;
            lblUtStopBits.Text = "StopBits";

            cmbUtStopBits.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUtStopBits.Location = new Point(110, 138);
            cmbUtStopBits.Name = "cmbUtStopBits";
            cmbUtStopBits.Size = new Size(190, 23);
            cmbUtStopBits.TabIndex = 9;

            lblUtTerm.Location = new Point(16, 168);
            lblUtTerm.Name = "lblUtTerm";
            lblUtTerm.Size = new Size(90, 20);
            lblUtTerm.TabIndex = 10;
            lblUtTerm.Text = "LineTerm";

            cmbUtTerm.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbUtTerm.Location = new Point(110, 166);
            cmbUtTerm.Name = "cmbUtTerm";
            cmbUtTerm.Size = new Size(190, 23);
            cmbUtTerm.TabIndex = 11;

            btnOpenUt.Location = new Point(110, 222);
            btnOpenUt.Name = "btnOpenUt";
            btnOpenUt.Size = new Size(190, 32);
            btnOpenUt.TabIndex = 12;
            btnOpenUt.Text = "OPEN";

            btnTestUt.Location = new Point(110, 254);
            btnTestUt.Name = "btnTestUt";
            btnTestUt.Size = new Size(190, 32);
            btnTestUt.TabIndex = 13;
            btnTestUt.Text = "통신 테스트(MT?)";

            // grpMultiBoard (추가)
            grpMultiBoard.Controls.Add(lblMbHost);
            grpMultiBoard.Controls.Add(txtMbHost);
            grpMultiBoard.Controls.Add(lblMbPort);
            grpMultiBoard.Controls.Add(numMbPort);
            grpMultiBoard.Controls.Add(lblMbUnitId);
            grpMultiBoard.Controls.Add(numMbUnitId);
            grpMultiBoard.Controls.Add(btnTestMb);
            grpMultiBoard.Location = new Point(668, 318);
            grpMultiBoard.Name = "grpMultiBoard";
            grpMultiBoard.Size = new Size(320, 130);
            grpMultiBoard.TabIndex = 20;
            grpMultiBoard.TabStop = false;
            grpMultiBoard.Text = "MULTI BOARD (Modbus TCP)";

            lblMbHost.Location = new Point(16, 28);
            lblMbHost.Name = "lblMbHost";
            lblMbHost.Size = new Size(90, 20);
            lblMbHost.TabIndex = 0;
            lblMbHost.Text = "Host";

            txtMbHost.Location = new Point(110, 26);
            txtMbHost.Name = "txtMbHost";
            txtMbHost.Size = new Size(190, 23);
            txtMbHost.TabIndex = 1;

            lblMbPort.Location = new Point(16, 56);
            lblMbPort.Name = "lblMbPort";
            lblMbPort.Size = new Size(90, 20);
            lblMbPort.TabIndex = 2;
            lblMbPort.Text = "Port";

            numMbPort.Location = new Point(110, 54);
            numMbPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numMbPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numMbPort.Name = "numMbPort";
            numMbPort.Size = new Size(190, 23);
            numMbPort.TabIndex = 3;
            numMbPort.Value = new decimal(new int[] { 13000, 0, 0, 0 });

            lblMbUnitId.Location = new Point(16, 84);
            lblMbUnitId.Name = "lblMbUnitId";
            lblMbUnitId.Size = new Size(90, 20);
            lblMbUnitId.TabIndex = 4;
            lblMbUnitId.Text = "UnitId";

            numMbUnitId.Location = new Point(110, 82);
            numMbUnitId.Maximum = new decimal(new int[] { 247, 0, 0, 0 });
            numMbUnitId.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numMbUnitId.Name = "numMbUnitId";
            numMbUnitId.Size = new Size(190, 23);
            numMbUnitId.TabIndex = 5;
            numMbUnitId.Value = new decimal(new int[] { 1, 0, 0, 0 });

            btnTestMb.Location = new Point(110, 108);
            btnTestMb.Name = "btnTestMb";
            btnTestMb.Size = new Size(190, 22);
            btnTestMb.TabIndex = 6;
            btnTestMb.Text = "통신 테스트(FC03 0~13)";

            // 하단 버튼/로그 (기존 그대로 배치 유지)
            btnReload.Location = new Point(646, 550);
            btnReload.Name = "btnReload";
            btnReload.Size = new Size(120, 34);
            btnReload.TabIndex = 4;
            btnReload.Text = "포트 새로고침";

            btnSave.Location = new Point(772, 550);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(100, 34);
            btnSave.TabIndex = 5;
            btnSave.Text = "저장";

            btnClose.Location = new Point(888, 550);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(100, 34);
            btnClose.TabIndex = 6;
            btnClose.Text = "닫기";

            txtLog.Font = new Font("Consolas", 9F);
            txtLog.Location = new Point(12, 318);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(650, 222);
            txtLog.TabIndex = 3;

            // FormComSetting
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 600);
            Controls.Add(grpBath1);
            Controls.Add(grpBath2);
            Controls.Add(grpUt);
            Controls.Add(grpMultiBoard);
            Controls.Add(txtLog);
            Controls.Add(btnReload);
            Controls.Add(btnSave);
            Controls.Add(btnClose);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormComSetting";
            StartPosition = FormStartPosition.CenterParent;
            Text = "COM Settings";

            grpBath1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numBath1Addr).EndInit();
            grpBath2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numBath2Addr).EndInit();
            grpUt.ResumeLayout(false);
            grpMultiBoard.ResumeLayout(false);
            grpMultiBoard.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numMbPort).EndInit();
            ((System.ComponentModel.ISupportInitialize)numMbUnitId).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
