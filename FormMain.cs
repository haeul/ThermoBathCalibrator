using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using ThermoBathCalibrator.Controller;

namespace ThermoBathCalibrator
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();

            Text = "ThermoBathCalibrator";

            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;

            btnOffsetApplyCh1.Click += BtnOffsetApplyCh1_Click;
            btnOffsetApplyCh2.Click += BtnOffsetApplyCh2_Click;

            btnComSetting.Click += BtnComSetting_Click;

            nudOffsetCh1.DecimalPlaces = 1;
            nudOffsetCh1.Increment = 0.1M;
            nudOffsetCh1.Minimum = -1.0M;
            nudOffsetCh1.Maximum = 1.0M;
            nudOffsetCh1.Value = 0.0M;

            nudOffsetCh2.DecimalPlaces = 1;
            nudOffsetCh2.Increment = 0.1M;
            nudOffsetCh2.Minimum = -1.0M;
            nudOffsetCh2.Maximum = 1.0M;
            nudOffsetCh2.Value = 0.0M;

            pnlCh1Graph.Paint += PnlCh1Graph_Paint;
            pnlCh2Graph.Paint += PnlCh2Graph_Paint;
            EnableDoubleBuffer(pnlCh1Graph);
            EnableDoubleBuffer(pnlCh2Graph);

            LoadMultiBoardSettingsFromDiskIfAny();
            BuildMultiBoardClient();

            _autoCtrl = new OffsetAutoController(_autoCfg);

            ResetConnectionState();
            UpdateStatusLabels();

            UpdateTopNumbers(double.NaN, double.NaN);
            UpdateOffsetUiFromState();

            PrepareCsvPath(DateTime.Now);
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_running) return;

            PrepareCsvPath(DateTime.Now);

            // 버전2 동기화 플로우: Start 직전 read-back을 필수 게이트로 둔다.
            if (!TrySyncOffsetsFromDevice(reason: "START_BUTTON"))
            {
                MessageBox.Show(
                    "장비 offset read-back 실패로 자동 제어를 시작하지 않습니다. 통신 상태를 확인해 주세요.",
                    "Offset Sync Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                UpdateStatusLabels();
                UpdateOffsetUiFromState();
                return;
            }

            // 동기화된 내부 상태를 UI에 확실히 반영
            UpdateOffsetUiFromState();

            _running = true;
            _workerRunning = true;

            _workerThread = new System.Threading.Thread(WorkerLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            UpdateStatusLabels();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (!_running) return;

            _running = false;
            _workerRunning = false;

            try { _workerThread?.Join(500); } catch { }

            _mb.Disconnect();
            _boardConnected = false;

            UpdateStatusLabels();
        }

        private void BtnOffsetApplyCh1_Click(object sender, EventArgs e)
        {
            double offset = (double)nudOffsetCh1.Value;

            double applied = OffsetMath.Quantize(offset, _autoCfg.OffsetStep);
            applied = OffsetMath.Clamp(applied, _autoCfg.OffsetClampMin, _autoCfg.OffsetClampMax);

            // 버전2: write 성공 직후 내부 상태 즉시 반영은 TryWriteChannelOffset 내부에서 수행된다고 가정
            bool ok = TryWriteChannelOffset(channel: 1, appliedOffset: applied, reason: "MANUAL_APPLY_CH1");
            if (ok)
            {
                UpdateOffsetUiFromState();
            }

            UpdateStatusLabels();
            pnlCh1Graph.Invalidate();
        }

        private void BtnOffsetApplyCh2_Click(object sender, EventArgs e)
        {
            double offset = (double)nudOffsetCh2.Value;

            double applied = OffsetMath.Quantize(offset, _autoCfg.OffsetStep);
            applied = OffsetMath.Clamp(applied, _autoCfg.OffsetClampMin, _autoCfg.OffsetClampMax);

            bool ok = TryWriteChannelOffset(channel: 2, appliedOffset: applied, reason: "MANUAL_APPLY_CH2");
            if (ok)
            {
                UpdateOffsetUiFromState();
            }

            UpdateStatusLabels();
            pnlCh2Graph.Invalidate();
        }

        private void UpdateOffsetUiFromState()
        {
            double ch1, ch2;
            lock (_offsetStateSync)
            {
                ch1 = _bath1OffsetCur;
                ch2 = _bath2OffsetCur;
            }

            lblCh1OffsetValue.Text = double.IsNaN(ch1) ? "-" : ch1.ToString("0.0", CultureInfo.InvariantCulture);
            lblCh2OffsetValue.Text = double.IsNaN(ch2) ? "-" : ch2.ToString("0.0", CultureInfo.InvariantCulture);

            // 사용자가 직접 입력 중이면 Value를 덮지 않음
            if (!nudOffsetCh1.Focused && !double.IsNaN(ch1))
            {
                decimal v = (decimal)ch1;
                if (v < nudOffsetCh1.Minimum) v = nudOffsetCh1.Minimum;
                if (v > nudOffsetCh1.Maximum) v = nudOffsetCh1.Maximum;
                nudOffsetCh1.Value = v;
            }

            if (!nudOffsetCh2.Focused && !double.IsNaN(ch2))
            {
                decimal v = (decimal)ch2;
                if (v < nudOffsetCh2.Minimum) v = nudOffsetCh2.Minimum;
                if (v > nudOffsetCh2.Maximum) v = nudOffsetCh2.Maximum;
                nudOffsetCh2.Value = v;
            }
        }

        private void BtnComSetting_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new FormComSetting())
                {
                    dlg.ShowDialog(this);
                }

                string path = CommSettings.GetDefaultPath();
                var s = CommSettings.LoadOrDefault(path);

                if (s?.MultiBoard != null)
                {
                    string host = (s.MultiBoard.Host ?? "").Trim();
                    int port = s.MultiBoard.Port;
                    int unit = s.MultiBoard.UnitId;

                    ApplyMultiBoardEndpoint(host, port, (byte)Math.Max(1, Math.Min(247, unit)));

                    MessageBox.Show(
                        $"멀티보드 설정 적용: {_host}:{_port} (UnitId={_unitId})",
                        "COM Settings",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "멀티보드 설정을 찾을 수 없습니다. (comm_settings.json 확인)",
                        "COM Settings",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "COM Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTopNumbers(double ch1, double ch2)
        {
            lblCh1Temperature.Text = double.IsNaN(ch1) ? "-" : ch1.ToString("0.000", CultureInfo.InvariantCulture);
            lblCh2Temperature.Text = double.IsNaN(ch2) ? "-" : ch2.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private void UpdateStatusLabels()
        {
            lblThermoPortStatus.Text =
                $"BOARD({_host}:{_port}): {(_boardConnected ? "CONNECTED" : "DISCONNECTED")} (fail={_boardFailCount})";

            lblThermoPortStatus.ForeColor = _boardConnected ? Color.LimeGreen : Color.OrangeRed;
        }
    }
}
