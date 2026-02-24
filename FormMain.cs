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
            lblHeader.DoubleClick += LblHeader_DoubleClick;

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

            _offsetStatusTimer = new System.Windows.Forms.Timer();
            _offsetStatusTimer.Interval = 100;
            _offsetStatusTimer.Tick += (s, e) =>
            {
                bool stillVisible;
                lock (_offsetStatusSync)
                {
                    stillVisible = DateTime.UtcNow <= _offsetApplyStatusUntilUtc;
                }

                if (!stillVisible)
                {
                    _offsetStatusTimer?.Stop();
                }

                UpdateStatusLabels();
            };

            _alarmFlashTimer = new System.Windows.Forms.Timer();
            _alarmFlashTimer.Interval = 350;
            _alarmFlashTimer.Tick += (s, e) => ToggleAlarmFlash();

            CacheNormalBackColors(this);

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
                using (var dlg = new FormSettings(_host, _port, _unitId, _bath1Setpoint, _bath2Setpoint, TryWriteChannelSvCoarseFromSettings))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    ApplyMultiBoardEndpoint(dlg.AppliedHost, dlg.AppliedPort, dlg.AppliedUnitId);
                }

                MessageBox.Show(
                    $"멀티보드 설정 적용: {_host}:{_port} (UnitId={_unitId})",
                    "Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "COM Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LblHeader_DoubleClick(object? sender, EventArgs e)
        {
            if (!EnsureAdminAuthenticated())
                return;

            using (var dlg = new FormAdminSettings(_utBiasCh1, _utBiasCh2, _bath1FineTarget, _bath2FineTarget))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                _utBiasCh1 = dlg.UtBiasCh1;
                _utBiasCh2 = dlg.UtBiasCh2;

                _bath1FineTarget = dlg.SetpointCh1;
                _bath2FineTarget = dlg.SetpointCh2;

                UpdateFineTargetAndMaybeWriteCoarse(1, _bath1FineTarget, "ADMIN_FINE_TARGET");
                UpdateFineTargetAndMaybeWriteCoarse(2, _bath2FineTarget, "ADMIN_FINE_TARGET");

                double offset1;
                double offset2;
                lock (_offsetStateSync)
                {
                    offset1 = _bath1OffsetCur;
                    offset2 = _bath2OffsetCur;
                }

                _ = TryWriteChannelOffset(1, offset1, "ADMIN_SETPOINT_APPLY");
                _ = TryWriteChannelOffset(2, offset2, "ADMIN_SETPOINT_APPLY");

                _autoCfg.TargetTemperature = AverageOrNaN(_bath1FineTarget, _bath2FineTarget);
                UpdateStatusLabels();
            }
        }

        private bool EnsureAdminAuthenticated()
        {
            if (_isAdminAuthenticated)
                return true;

            using (var prompt = new Form())
            using (var txt = new TextBox())
            using (var lbl = new Label())
            using (var btnOk = new Button())
            using (var btnCancel = new Button())
            {
                prompt.Text = "Admin Login";
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MinimizeBox = false;
                prompt.MaximizeBox = false;
                prompt.ClientSize = new Size(320, 130);

                lbl.Text = "관리자 비밀번호";
                lbl.AutoSize = false;
                lbl.TextAlign = ContentAlignment.MiddleLeft;
                lbl.SetBounds(12, 12, 296, 24);

                txt.PasswordChar = '*';
                txt.SetBounds(12, 40, 296, 31);

                btnOk.Text = "OK";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.SetBounds(152, 84, 75, 30);

                btnCancel.Text = "Cancel";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.SetBounds(233, 84, 75, 30);

                prompt.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                if (prompt.ShowDialog(this) != DialogResult.OK)
                    return false;

                if (string.Equals(txt.Text?.Trim(), AdminPassword, StringComparison.Ordinal))
                {
                    _isAdminAuthenticated = true;
                    return true;
                }
            }

            MessageBox.Show("비밀번호가 올바르지 않습니다.", "Admin Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private void UpdateAlarmState(double ch1, double ch2)
        {
            bool ch1Alarm = !double.IsNaN(ch1) && Math.Abs(ch1 - _bath1Setpoint) >= TempAlarmThresholdC;
            bool ch2Alarm = !double.IsNaN(ch2) && Math.Abs(ch2 - _bath2Setpoint) >= TempAlarmThresholdC;
            bool active = ch1Alarm || ch2Alarm;

            if (active == _isTempAlarmActive)
                return;

            _isTempAlarmActive = active;

            if (_isTempAlarmActive)
            {
                _isAlarmFlashOn = false;
                _alarmFlashTimer?.Start();
                ToggleAlarmFlash();
            }
            else
            {
                _alarmFlashTimer?.Stop();
                RestoreNormalBackColors();
            }
        }

        private void ToggleAlarmFlash()
        {
            _isAlarmFlashOn = !_isAlarmFlashOn;
            ApplyAlarmBackColor(_isAlarmFlashOn ? Color.Red : Color.White);
        }

        private void CacheNormalBackColors(Control root)
        {
            if (!_normalBackColors.ContainsKey(root))
                _normalBackColors[root] = root.BackColor;

            foreach (Control child in root.Controls)
            {
                CacheNormalBackColors(child);
            }
        }

        private void ApplyAlarmBackColor(Color color)
        {
            SetBackColorRecursive(this, color);
            Invalidate(true);
        }

        private void RestoreNormalBackColors()
        {
            foreach (var kv in _normalBackColors)
            {
                kv.Key.BackColor = kv.Value;
            }
            Invalidate(true);
        }

        private static void SetBackColorRecursive(Control root, Color color)
        {
            root.BackColor = color;
            foreach (Control child in root.Controls)
            {
                SetBackColorRecursive(child, color);
            }
        }

        private void UpdateTopNumbers(double ch1, double ch2)
        {
            lblCh1Temperature.Text = double.IsNaN(ch1) ? "-" : ch1.ToString("0.000", CultureInfo.InvariantCulture);
            lblCh2Temperature.Text = double.IsNaN(ch2) ? "-" : ch2.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private void ShowOffsetApplyStatus(int channel, double offset, bool success)
        {
            string msg = success
                ? $"CH{channel} Offset {offset.ToString("0.0", CultureInfo.InvariantCulture)} 적용"
                : $"CH{channel} Offset {offset.ToString("0.0", CultureInfo.InvariantCulture)} 적용 실패";

            Color color = success ? Color.DeepSkyBlue : Color.OrangeRed;

            lock (_offsetStatusSync)
            {
                _offsetApplyStatusText = msg;
                _offsetApplyStatusColor = color;
                _offsetApplyStatusUntilUtc = DateTime.UtcNow.AddSeconds(1);
            }

            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    _offsetStatusTimer?.Stop();
                    _offsetStatusTimer?.Start();
                    UpdateStatusLabels();
                }));
            }
        }

        private void UpdateStatusLabels()
        {
            bool showOffsetStatus;
            string offsetStatusText;
            Color offsetStatusColor;
            lock (_offsetStatusSync)
            {
                showOffsetStatus = DateTime.UtcNow <= _offsetApplyStatusUntilUtc && !string.IsNullOrWhiteSpace(_offsetApplyStatusText);
                offsetStatusText = _offsetApplyStatusText;
                offsetStatusColor = _offsetApplyStatusColor;
            }

            if (showOffsetStatus)
            {
                lblThermoPortStatus.Text = offsetStatusText;
                lblThermoPortStatus.ForeColor = offsetStatusColor;
                return;
            }

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
