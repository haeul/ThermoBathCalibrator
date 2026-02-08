using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    public partial class FormComSetting : Form
    {
        private readonly string _path;
        private CommSettings _settings;

        public CommSettings CurrentSettings => _settings;

        public FormComSetting()
        {
            InitializeComponent();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            WireEvents();

            _path = CommSettings.GetDefaultPath();
            _settings = CommSettings.LoadOrDefault(_path);

            InitFixedCombos();
            WireEvents();
            RefreshComPorts();

            LoadToUi(_settings);
        }

        private void WireEvents()
        {
            btnOpenBath1.Click -= BtnOpenBath1_Click;
            btnOpenBath1.Click += BtnOpenBath1_Click;
            btnOpenBath2.Click -= BtnOpenBath2_Click;
            btnOpenBath2.Click += BtnOpenBath2_Click;
            btnOpenUt.Click -= BtnOpenUt_Click;
            btnOpenUt.Click += BtnOpenUt_Click;

            btnTestBath1.Click -= BtnTestBath1_Click;
            btnTestBath1.Click += BtnTestBath1_Click;
            btnTestBath2.Click -= BtnTestBath2_Click;
            btnTestBath2.Click += BtnTestBath2_Click;
            btnTestUt.Click -= BtnTestUt_Click;
            btnTestUt.Click += BtnTestUt_Click;

            // 추가: 멀티보드 테스트
            if (btnTestMb != null)
            {
                btnTestMb.Click -= BtnTestMb_Click;
                btnTestMb.Click += BtnTestMb_Click;
            }

            btnReload.Click -= BtnReload_Click;
            btnReload.Click += BtnReload_Click;
            btnSave.Click -= BtnSave_Click;
            btnSave.Click += BtnSave_Click;
            btnClose.Click -= (s, e) => this.Close();
            btnClose.Click += (s, e) => this.Close();
        }

        private void BtnOpenBath1_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();
                var bath = new HebcBathClient(s.Bath1);

                if (!bath.TryOpen(out string err))
                {
                    Log($"BATH1 OPEN FAIL: {err}");
                    return;
                }

                Log("BATH1 OPEN OK");
                bath.Close();
            }
            catch (Exception ex)
            {
                Log($"BATH1 OPEN EX: {ex.Message}");
            }
        }

        private void BtnOpenBath2_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();
                var bath = new HebcBathClient(s.Bath2);

                if (!bath.TryOpen(out string err))
                {
                    Log($"BATH2 OPEN FAIL: {err}");
                    return;
                }

                Log("BATH2 OPEN OK");
                bath.Close();
            }
            catch (Exception ex)
            {
                Log($"BATH2 OPEN EX: {ex.Message}");
            }
        }

        private void BtnOpenUt_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();
                var ut = new UtOneClient(s.UtOne);

                if (!ut.TryOpen(out string err))
                {
                    Log($"UT-ONE OPEN FAIL: {err}");
                    return;
                }

                Log("UT-ONE OPEN OK");
                ut.Close();
            }
            catch (Exception ex)
            {
                Log($"UT-ONE OPEN EX: {ex.Message}");
            }
        }

        private void BtnTestBath1_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();

                var bath = new HebcBathClient(s.Bath1);
                if (!bath.TryOpen(out string errOpen))
                {
                    Log($"BATH1 OPEN FAIL: {errOpen}");
                    return;
                }

                if (bath.TryReadPv1(out double pv, out string errRead))
                    Log($"BATH1 PV1 = {pv:0.000}");
                else
                    Log($"BATH1 READ FAIL: {errRead}");

                bath.Close();
            }
            catch (Exception ex)
            {
                Log($"BATH1 TEST EX: {ex.Message}");
            }
        }

        private void BtnTestBath2_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();

                var bath = new HebcBathClient(s.Bath2);
                if (!bath.TryOpen(out string errOpen))
                {
                    Log($"BATH2 OPEN FAIL: {errOpen}");
                    return;
                }

                if (bath.TryReadPv1(out double pv, out string errRead))
                    Log($"BATH2 PV1 = {pv:0.000}");
                else
                    Log($"BATH2 READ FAIL: {errRead}");

                bath.Close();
            }
            catch (Exception ex)
            {
                Log($"BATH2 TEST EX: {ex.Message}");
            }
        }

        private void BtnTestUt_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();

                var ut = new UtOneClient(s.UtOne);
                if (!ut.TryOpen(out string errOpen))
                {
                    Log($"UT-ONE OPEN FAIL: {errOpen}");
                    return;
                }

                if (ut.TryReadMt(out string payload, out string errRead))
                    Log($"UT-ONE MT? => {payload}");
                else
                    Log($"UT-ONE MT? FAIL: {errRead}");

                ut.Close();
            }
            catch (Exception ex)
            {
                Log($"UT-ONE TEST EX: {ex.Message}");
            }
        }

        // 멀티보드 Modbus TCP 테스트(FC03 0~13 읽기, TJ 포함)
        private void BtnTestMb_Click(object? sender, EventArgs e)
        {
            MultiBoardModbusClient? c = null;

            try
            {
                var s = ReadFromUi();
                var mb = s.MultiBoard;

                c = new MultiBoardModbusClient(mb.Host, mb.Port, (byte)mb.UnitId);

                if (!c.TryConnect(out string errConn))
                {
                    Log($"MULTI CONNECT FAIL: {errConn}");
                    return;
                }

                // 맵 수정 반영: 0~13 => count=14
                if (!c.TryReadHoldingRegisters(0, 14, out ushort[] regs, out string errRead))
                {
                    Log($"MULTI READ FAIL: {errRead}");
                    return;
                }

                Log($"MULTI READ OK: {mb.Host}:{mb.Port} Unit={mb.UnitId}");
                Log($"REG0..13 = {string.Join(", ", regs.Select(x => x.ToString()))}");
            }
            catch (Exception ex)
            {
                Log($"MULTI TEST EX: {ex.Message}");
            }
            finally
            {
                try { c?.Disconnect(); } catch { }
            }
        }

        private void BtnReload_Click(object? sender, EventArgs e)
        {
            try
            {
                RefreshPortCombosKeepSelection();
                Log("포트 목록 새로고침 완료");

                _settings = CommSettings.LoadOrDefault(_path);
                LoadToUi(_settings);
                Log("설정 파일 Reload 완료");
            }
            catch (Exception ex)
            {
                Log($"RELOAD EX: {ex.Message}");
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();
                s.Save(_path);
                _settings = s;

                Log($"저장 완료: {_path}");
                MessageBox.Show("저장 완료", "COM Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"SAVE EX: {ex.Message}");
                MessageBox.Show(ex.Message, "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshPortCombosKeepSelection()
        {
            string b1 = cmbBath1Port.Text;
            string b2 = cmbBath2Port.Text;
            string ut = cmbUtPort.Text;

            string[] ports = SerialPort.GetPortNames()
                                       .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                       .ToArray();

            RebindCombo(cmbBath1Port, ports, b1);
            RebindCombo(cmbBath2Port, ports, b2);
            RebindCombo(cmbUtPort, ports, ut);
        }

        private static void RebindCombo(ComboBox cmb, string[] items, string keep)
        {
            cmb.BeginUpdate();
            try
            {
                cmb.Items.Clear();
                cmb.Items.AddRange(items);

                if (!string.IsNullOrWhiteSpace(keep) && items.Contains(keep))
                    cmb.SelectedItem = keep;
                else if (items.Length > 0)
                    cmb.SelectedIndex = 0;
                else
                    cmb.Text = "";
            }
            finally
            {
                cmb.EndUpdate();
            }
        }

        private void InitFixedCombos()
        {
            ComboBox[] ports = { cmbBath1Port, cmbBath2Port, cmbUtPort };
            foreach (var cb in ports)
                cb.DropDownStyle = ComboBoxStyle.DropDownList;

            var hebcBauds = new object[] { "1200", "2400", "4800", "9600", "19200" };
            cmbBath1Baud.Items.Clear();
            cmbBath2Baud.Items.Clear();
            cmbBath1Baud.Items.AddRange(hebcBauds);
            cmbBath2Baud.Items.AddRange(hebcBauds);

            var utBauds = new object[] { "1200", "2400", "4800", "9600", "19200", "38400" };
            cmbUtBaud.Items.Clear();
            cmbUtBaud.Items.AddRange(utBauds);

            var parities = new object[] { "None", "Odd", "Even" };
            cmbBath1Parity.Items.Clear();
            cmbBath2Parity.Items.Clear();
            cmbUtParity.Items.Clear();
            cmbBath1Parity.Items.AddRange(parities);
            cmbBath2Parity.Items.AddRange(parities);
            cmbUtParity.Items.AddRange(parities);

            var databitsBath = new object[] { "7", "8" };
            cmbBath1DataBits.Items.Clear();
            cmbBath2DataBits.Items.Clear();
            cmbBath1DataBits.Items.AddRange(databitsBath);
            cmbBath2DataBits.Items.AddRange(databitsBath);

            var databitsUt = new object[] { "8" };
            cmbUtDataBits.Items.Clear();
            cmbUtDataBits.Items.AddRange(databitsUt);

            var stopbits = new object[] { "One", "Two" };
            cmbBath1StopBits.Items.Clear();
            cmbBath2StopBits.Items.Clear();
            cmbUtStopBits.Items.Clear();
            cmbBath1StopBits.Items.AddRange(stopbits);
            cmbBath2StopBits.Items.AddRange(stopbits);
            cmbUtStopBits.Items.AddRange(stopbits);

            cmbUtTerm.Items.Clear();
            cmbUtTerm.Items.AddRange(new object[] { "LF", "CRLF" });
        }

        private void RefreshComPorts()
        {
            var ports = SerialPort.GetPortNames()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            RefreshPortCombo(cmbBath1Port, ports);
            RefreshPortCombo(cmbBath2Port, ports);
            RefreshPortCombo(cmbUtPort, ports);
        }

        private static void RefreshPortCombo(ComboBox cmb, string[] ports)
        {
            string prev = cmb.SelectedItem as string ?? cmb.Text;

            cmb.BeginUpdate();
            cmb.Items.Clear();
            cmb.Items.AddRange(ports);
            cmb.EndUpdate();

            if (!string.IsNullOrWhiteSpace(prev) && ports.Contains(prev))
                cmb.SelectedItem = prev;
            else if (ports.Length > 0)
                cmb.SelectedIndex = 0;
        }

        private void LoadToUi(CommSettings s)
        {
            SetComboText(cmbBath1Port, s.Bath1.PortName);
            SetComboText(cmbBath1Baud, s.Bath1.BaudRate.ToString());
            SetComboText(cmbBath1Parity, s.Bath1.Parity);
            SetComboText(cmbBath1DataBits, s.Bath1.DataBits.ToString());
            SetComboText(cmbBath1StopBits, s.Bath1.StopBits);
            numBath1Addr.Value = Clamp(s.Bath1.Address, 1, 99);
            chkBath1Bcc.Checked = s.Bath1.UseBcc;

            SetComboText(cmbBath2Port, s.Bath2.PortName);
            SetComboText(cmbBath2Baud, s.Bath2.BaudRate.ToString());
            SetComboText(cmbBath2Parity, s.Bath2.Parity);
            SetComboText(cmbBath2DataBits, s.Bath2.DataBits.ToString());
            SetComboText(cmbBath2StopBits, s.Bath2.StopBits);
            numBath2Addr.Value = Clamp(s.Bath2.Address, 1, 99);
            chkBath2Bcc.Checked = s.Bath2.UseBcc;

            SetComboText(cmbUtPort, s.UtOne.PortName);
            SetComboText(cmbUtBaud, s.UtOne.BaudRate.ToString());
            SetComboText(cmbUtParity, s.UtOne.Parity);
            SetComboText(cmbUtDataBits, s.UtOne.DataBits.ToString());
            SetComboText(cmbUtStopBits, s.UtOne.StopBits);
            SetComboText(cmbUtTerm, s.UtOne.LineTerminator);

            // 추가: 멀티보드 UI 반영
            if (txtMbHost != null) txtMbHost.Text = (s.MultiBoard.Host ?? "").Trim();
            if (numMbPort != null) numMbPort.Value = Clamp(s.MultiBoard.Port, 1, 65535);
            if (numMbUnitId != null) numMbUnitId.Value = Clamp(s.MultiBoard.UnitId, 1, 247);
        }

        private CommSettings ReadFromUi()
        {
            var s = new CommSettings();

            s.Bath1.PortName = GetComboText(cmbBath1Port);
            s.Bath1.BaudRate = ParseInt(GetComboText(cmbBath1Baud), 9600);
            s.Bath1.Parity = SafeText(GetComboText(cmbBath1Parity), "None");
            s.Bath1.DataBits = ParseInt(GetComboText(cmbBath1DataBits), 8);
            s.Bath1.StopBits = SafeText(GetComboText(cmbBath1StopBits), "One");
            s.Bath1.Address = (int)numBath1Addr.Value;
            s.Bath1.UseBcc = chkBath1Bcc.Checked;

            s.Bath2.PortName = GetComboText(cmbBath2Port);
            s.Bath2.BaudRate = ParseInt(GetComboText(cmbBath2Baud), 9600);
            s.Bath2.Parity = SafeText(GetComboText(cmbBath2Parity), "None");
            s.Bath2.DataBits = ParseInt(GetComboText(cmbBath2DataBits), 8);
            s.Bath2.StopBits = SafeText(GetComboText(cmbBath2StopBits), "One");
            s.Bath2.Address = (int)numBath2Addr.Value;
            s.Bath2.UseBcc = chkBath2Bcc.Checked;

            s.UtOne.PortName = GetComboText(cmbUtPort);
            s.UtOne.BaudRate = ParseInt(GetComboText(cmbUtBaud), 38400);
            s.UtOne.Parity = SafeText(GetComboText(cmbUtParity), "Odd");
            s.UtOne.DataBits = ParseInt(GetComboText(cmbUtDataBits), 8);
            s.UtOne.StopBits = SafeText(GetComboText(cmbUtStopBits), "One");
            s.UtOne.LineTerminator = SafeText(GetComboText(cmbUtTerm), "LF");

            // 추가: 멀티보드 설정 읽기
            if (txtMbHost != null)
                s.MultiBoard.Host = SafeText(txtMbHost.Text, "192.168.1.11");
            if (numMbPort != null)
                s.MultiBoard.Port = (int)numMbPort.Value;
            if (numMbUnitId != null)
                s.MultiBoard.UnitId = (int)numMbUnitId.Value;

            return s;
        }

        private void Log(string msg)
        {
            if (txtLog == null) return;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }

        private static void SetComboText(ComboBox cmb, string value)
        {
            if (cmb == null) return;
            value = (value ?? "").Trim();

            if (cmb.Items.Count > 0 && cmb.Items.Cast<object>().Any(i => string.Equals(i?.ToString(), value, StringComparison.OrdinalIgnoreCase)))
                cmb.SelectedItem = cmb.Items.Cast<object>().First(i => string.Equals(i?.ToString(), value, StringComparison.OrdinalIgnoreCase));
            else
                cmb.Text = value;
        }

        private static string GetComboText(ComboBox cmb) => (cmb.SelectedItem?.ToString() ?? cmb.Text ?? "").Trim();

        private static int ParseInt(string s, int fallback) => int.TryParse(s, out int v) ? v : fallback;

        private static string SafeText(string s, string fallback)
        {
            var t = (s ?? "").Trim();
            return string.IsNullOrWhiteSpace(t) ? fallback : t;
        }

        private static decimal Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
