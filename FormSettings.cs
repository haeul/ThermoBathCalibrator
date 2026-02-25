using System;
using System.Reflection;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    public partial class FormSettings : Form
    {
        private readonly string _path;
        private readonly Func<int, double, bool> _tryWriteDeviceSv;
        private CommSettings _settings;

        // offset 보정 on/off
        public bool AppliedEnableOffsetControl => chkEnableOffsetControl.Checked;

        public string AppliedHost => txtMbHost.Text.Trim();
        public int AppliedPort => (int)numMbPort.Value;
        public byte AppliedUnitId => (byte)numMbUnitId.Value;

        private const string EnableOffsetPropName = "EnableOffsetControl";

        public FormSettings(
            string currentHost,
            int currentPort,
            byte currentUnitId,
            double ch1Sv,
            double ch2Sv,
            Func<int, double, bool> tryWriteDeviceSv,
            bool enableOffsetControl)
        {
            InitializeComponent();

            _path = CommSettings.GetDefaultPath();
            _settings = CommSettings.LoadOrDefault(_path);
            _tryWriteDeviceSv = tryWriteDeviceSv;

            if (_settings.MultiBoard == null)
                _settings.MultiBoard = MultiBoardTcpSettings.CreateDefault();

            if (!string.IsNullOrWhiteSpace(currentHost)) _settings.MultiBoard.Host = currentHost;
            if (currentPort > 0 && currentPort <= 65535) _settings.MultiBoard.Port = currentPort;
            if (currentUnitId >= 1 && currentUnitId <= 247) _settings.MultiBoard.UnitId = currentUnitId;

            txtMbHost.Text = _settings.MultiBoard.Host;
            numMbPort.Value = Math.Max(numMbPort.Minimum, Math.Min(numMbPort.Maximum, _settings.MultiBoard.Port));
            numMbUnitId.Value = Math.Max(numMbUnitId.Minimum, Math.Min(numMbUnitId.Maximum, _settings.MultiBoard.UnitId));

            nudCh1Sv.Value = Clamp(nudCh1Sv, ch1Sv);
            nudCh2Sv.Value = Clamp(nudCh2Sv, ch2Sv);

            // 체크박스 초기값: 저장값이 있으면 저장값 우선, 없으면 FormMain에서 넘어온 값 사용
            chkEnableOffsetControl.Checked = ReadEnableOffsetControlOrFallback(enableOffsetControl);

            btnSave.Click += BtnSave_Click;
            btnWriteCh1Sv.Click += BtnWriteCh1Sv_Click;
            btnWriteCh2Sv.Click += BtnWriteCh2Sv_Click;
            btnClose.Click += (s, e) => DialogResult = DialogResult.Cancel;
        }

        private bool ReadEnableOffsetControlOrFallback(bool fallback)
        {
            try
            {
                PropertyInfo? prop = _settings.GetType().GetProperty(EnableOffsetPropName, BindingFlags.Instance | BindingFlags.Public);
                if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
                {
                    object? v = prop.GetValue(_settings);
                    if (v is bool b) return b;
                }
            }
            catch
            {
            }
            return fallback;
        }

        private void WriteEnableOffsetControlToSettings(bool value)
        {
            try
            {
                PropertyInfo? prop = _settings.GetType().GetProperty(EnableOffsetPropName, BindingFlags.Instance | BindingFlags.Public);
                if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
                {
                    prop.SetValue(_settings, value);
                }
            }
            catch
            {
            }
        }

        private static decimal Clamp(NumericUpDown nud, double value)
        {
            decimal v = (decimal)value;
            if (v < nud.Minimum) return nud.Minimum;
            if (v > nud.Maximum) return nud.Maximum;
            return v;
        }

        private void BtnWriteCh1Sv_Click(object? sender, EventArgs e)
        {
            WriteDeviceSv(1, (double)nudCh1Sv.Value);
        }

        private void BtnWriteCh2Sv_Click(object? sender, EventArgs e)
        {
            WriteDeviceSv(2, (double)nudCh2Sv.Value);
        }

        private void WriteDeviceSv(int channel, double sv)
        {
            bool ok = _tryWriteDeviceSv?.Invoke(channel, sv) == true;
            MessageBox.Show(
                ok ? $"CH{channel} SV 쓰기 성공 ({sv:0.0}℃)" : $"CH{channel} SV 쓰기 실패",
                "Settings",
                MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            _settings.MultiBoard.Host = txtMbHost.Text.Trim();
            _settings.MultiBoard.Port = (int)numMbPort.Value;
            _settings.MultiBoard.UnitId = (int)numMbUnitId.Value;

            // offset 보정 on/off 저장
            WriteEnableOffsetControlToSettings(chkEnableOffsetControl.Checked);

            _settings.Save(_path);
            DialogResult = DialogResult.OK;
        }
    }
}