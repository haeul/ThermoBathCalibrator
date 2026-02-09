using System;
using System.ComponentModel;
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

            _path = CommSettings.GetDefaultPath();
            _settings = CommSettings.LoadOrDefault(_path);

            WireEvents();
            LoadToUi(_settings);
        }

        private void WireEvents()
        {
            btnTestMb.Click -= BtnTestMb_Click;
            btnTestMb.Click += BtnTestMb_Click;

            btnSave.Click -= BtnSave_Click;
            btnSave.Click += BtnSave_Click;

            btnClose.Click -= (s, e) => Close();
            btnClose.Click += (s, e) => Close();
        }

        private void BtnTestMb_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();
                var mb = s.MultiBoard;

                using (var c = new MultiBoardModbusClient(mb.Host, mb.Port, (byte)mb.UnitId))
                {
                    if (!c.TryConnect(out string errConn))
                    {
                        Log($"MULTI CONNECT FAIL: {errConn}");
                        return;
                    }

                    // 맵 수정 반영: FC03 0~13 (count=14)
                    if (!c.TryReadHoldingRegisters(0, 14, out ushort[] regs, out string errRead))
                    {
                        Log($"MULTI READ FAIL: {errRead}");
                        return;
                    }

                    Log($"MULTI READ OK: {mb.Host}:{mb.Port} Unit={mb.UnitId}");
                    Log($"REG0..13 = {string.Join(", ", regs.Select(x => x.ToString()))}");
                }
            }
            catch (Exception ex)
            {
                Log($"MULTI TEST EX: {ex.Message}");
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

        private void LoadToUi(CommSettings s)
        {
            txtMbHost.Text = (s.MultiBoard.Host ?? "").Trim();
            numMbPort.Value = Clamp(s.MultiBoard.Port, 1, 65535);
            numMbUnitId.Value = Clamp(s.MultiBoard.UnitId, 1, 247);
        }

        private CommSettings ReadFromUi()
        {
            var s = new CommSettings();

            s.MultiBoard.Host = SafeText(txtMbHost.Text, "192.168.1.11");
            s.MultiBoard.Port = (int)numMbPort.Value;
            s.MultiBoard.UnitId = (int)numMbUnitId.Value;

            return s;
        }

        private void Log(string msg)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }

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
