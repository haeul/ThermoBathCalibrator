using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using ThermoBathCalibrator.Controller;

namespace ThermoBathCalibrator
{
    public partial class FormMain : Form
    {
        // =============================
        // Offset control enable flag
        // =============================
        private volatile bool _enableOffsetControl = false;

        public FormMain()
        {
            InitializeComponent();

            Text = "ThermoBathCalibrator";

            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;

            btnOffsetApplyCh1.Click += BtnOffsetApplyCh1_Click;
            btnOffsetApplyCh2.Click += BtnOffsetApplyCh2_Click;

            btnComSetting.Click += BtnComSetting_Click;

            // NEW: offset control checkbox
            chkEnableOffsetControl.CheckedChanged += ChkEnableOffsetControl_CheckedChanged;
            chkEnableOffsetControl.Checked = false; // 기본 OFF (안전)
            _enableOffsetControl = chkEnableOffsetControl.Checked;

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

            // 체크박스 상태에 따라 UI 잠금 반영
            ApplyOffsetControlUiLock();
        }

        // =============================
        // NEW: checkbox event
        // =============================
        private void ChkEnableOffsetControl_CheckedChanged(object sender, EventArgs e)
        {
            _enableOffsetControl = chkEnableOffsetControl.Checked;
            ApplyOffsetControlUiLock();
        }

        private void ApplyOffsetControlUiLock()
        {
            bool on = _enableOffsetControl;

            // 수동 조작 UI도 같이 잠그기
            btnOffsetApplyCh1.Enabled = on;
            btnOffsetApplyCh2.Enabled = on;
            nudOffsetCh1.Enabled = on;
            nudOffsetCh2.Enabled = on;

            // (선택) 화면에서 현재 모드 표시를 하고 싶으면 라벨 색/텍스트 변경도 가능
            // 예: chkEnableOffsetControl.ForeColor = on ? Color.DarkGreen : Color.DarkRed;
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_running) return;

            PrepareCsvPath(DateTime.Now);

            bool enableControl = _enableOffsetControl;

            if (enableControl)
            {
                // 보정 모드: Start 직전 read-back을 "필수 게이트"
                if (!TrySyncOffsetsFromDevice(reason: "START_BUTTON"))
                {
                    MessageBox.Show(
                        "장비 offset read-back 실패로 Offset 보정(쓰기) 모드를 시작하지 않습니다.\r\n" +
                        "통신 상태를 확인하거나, 체크박스를 끄고(모니터링만) Start 하세요.",
                        "Offset Sync Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    UpdateStatusLabels();
                    UpdateOffsetUiFromState();
                    return;
                }
            }
            else
            {
                // 모니터링 모드: 동기화 best-effort (실패해도 시작 허용)
                _ = TrySyncOffsetsFromDevice(reason: "START_BUTTON_MONITOR_ONLY");
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
            if (!_enableOffsetControl)
            {
                MessageBox.Show(
                    "Offset 보정(쓰기) 기능이 꺼져 있습니다.\r\n체크박스를 켠 뒤 다시 시도하세요.",
                    "Offset Control Disabled",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            double offset = (double)nudOffsetCh1.Value;

            double applied = OffsetMath.Quantize(offset, _autoCfg.OffsetStep);
            applied = OffsetMath.Clamp(applied, _autoCfg.OffsetClampMin, _autoCfg.OffsetClampMax);

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
            if (!_enableOffsetControl)
            {
                MessageBox.Show(
                    "Offset 보정(쓰기) 기능이 꺼져 있습니다.\r\n체크박스를 켠 뒤 다시 시도하세요.",
                    "Offset Control Disabled",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

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

        // ===========================================================
        // IMPORTANT: LoopOnceCore() 내부에서 "자동 write" 분기 필요
        // ===========================================================
        //
        // 네가 이미 가진 LoopOnceCore() 코드에서,
        // 아래와 같은 형태로 "enableControl=false면 UpdateAndMaybeWrite 호출 자체를 스킵"해야 함.
        //
        //  bool enableControl = _enableOffsetControl;
        //  if (enableControl) { next1 = _autoCtrl.UpdateAndMaybeWrite(... tryWriteOffset: TryWriteChannelOffset ...); }
        //  else { next1 = currentOffset1; TraceModbus("OFFSET CONTROL DISABLED -> monitoring only (no FC10 write)"); }
        //
        // 이 분기만 들어가면 "Start 모니터링 모드"에서 절대 FC10 write가 나가지 않는다.
    }
}
