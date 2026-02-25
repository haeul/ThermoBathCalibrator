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

            // overlay가 그래프 위로 올라오게
            pnlCh1GraphOverlay.BringToFront();
            pnlCh2GraphOverlay.BringToFront();

            // Dock 순서 강제: Left(Offset 라벨) -> Left(체크박스) -> Fill(ON/OFF 라벨)
            pnlCh1GraphOverlay.Controls.SetChildIndex(lblCh1GraphOffset, 2);
            pnlCh1GraphOverlay.Controls.SetChildIndex(chkShowOffsetCh1, 1);
            pnlCh1GraphOverlay.Controls.SetChildIndex(lblCh1GraphOffsetState, 0);

            pnlCh2GraphOverlay.Controls.SetChildIndex(lblCh2GraphOffset, 2);
            pnlCh2GraphOverlay.Controls.SetChildIndex(chkShowOffsetCh2, 1);
            pnlCh2GraphOverlay.Controls.SetChildIndex(lblCh2GraphOffsetState, 0);

            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;

            btnOffsetApplyCh1.Click += BtnOffsetApplyCh1_Click;
            btnOffsetApplyCh2.Click += BtnOffsetApplyCh2_Click;

            btnComSetting.Click += BtnComSetting_Click;
            lblHeader.DoubleClick += LblHeader_DoubleClick;

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

            chkShowOffsetCh1.CheckedChanged += (_, __) => pnlCh1Graph.Invalidate();
            chkShowOffsetCh2.CheckedChanged += (_, __) => pnlCh2Graph.Invalidate();

            LoadMultiBoardSettingsFromDiskIfAny();

            // 저장된 설정을 읽어서 _enableOffsetControl 초기화
            _enableOffsetControl = CommSettings.LoadOrDefault(CommSettings.GetDefaultPath()).EnableOffsetControl;

            ApplyOffsetControlUiLock(); BuildMultiBoardClient();

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

            //_alarmFlashTimer = new System.Windows.Forms.Timer();
            //_alarmFlashTimer.Interval = 350;
            //_alarmFlashTimer.Tick += (s, e) => ToggleAlarmFlash();

            CacheNormalBackColors(this);

            // 체크박스 상태에 따라 UI 잠금 반영
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
                    "Offset 보정(쓰기) 기능이 꺼져 있습니다.\r\n설정에서 Offset 보정 체크 후 다시 시도하세요.",
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
                    "Offset 보정(쓰기) 기능이 꺼져 있습니다.\r\n설정에서 Offset 보정 체크 후 다시 시도하세요.",
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
                bool before = _enableOffsetControl;

                using (var dlg = new FormSettings(
                    _host, _port, _unitId,
                    _bath1Setpoint, _bath2Setpoint,
                    TryWriteChannelSvCoarseFromSettings,
                    _enableOffsetControl
                ))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    ApplyMultiBoardEndpoint(dlg.AppliedHost, dlg.AppliedPort, dlg.AppliedUnitId);

                    _enableOffsetControl = dlg.AppliedEnableOffsetControl;
                    ApplyOffsetControlUiLock();

                    // 실행 중에 ON -> OFF로 바뀌면 자동 보정 상태를 초기화해두는 게 안전
                    if (_running && before && !_enableOffsetControl)
                    {
                        try { _autoCtrl?.Reset(); } catch { }
                    }

                    UpdateStatusLabels();
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
                dlg.FormClosed += (_, __) => _isAdminAuthenticated = false;

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

        //private void UpdateAlarmState(double ch1, double ch2)
        //{
        //    double ch1Low = _bath1Setpoint - TempAlarmThresholdC;
        //    double ch1High = _bath1Setpoint + TempAlarmThresholdC;
        //    double ch2Low = _bath2Setpoint - TempAlarmThresholdC;
        //    double ch2High = _bath2Setpoint + TempAlarmThresholdC;

        //    bool ch1Under = !double.IsNaN(ch1) && ch1 < ch1Low;
        //    bool ch1Over = !double.IsNaN(ch1) && ch1 > ch1High;
        //    bool ch2Under = !double.IsNaN(ch2) && ch2 < ch2Low;
        //    bool ch2Over = !double.IsNaN(ch2) && ch2 > ch2High;

        //    _tempAlarmStatusText = BuildAlarmStatusText(ch1Over, ch1Under, ch2Over, ch2Under);

        //    bool active = !string.IsNullOrWhiteSpace(_tempAlarmStatusText);
        //    if (active != _isTempAlarmActive)
        //    {
        //        _isTempAlarmActive = active;

        //        if (_isTempAlarmActive)
        //        {
        //            _isAlarmFlashOn = false;
        //            _alarmFlashTimer?.Start();
        //            ToggleAlarmFlash();
        //        }
        //        else
        //        {
        //            _alarmFlashTimer?.Stop();
        //            RestoreNormalBackColors();
        //        }
        //    }
        //}

        //private static string BuildAlarmStatusText(bool ch1Over, bool ch1Under, bool ch2Over, bool ch2Under)
        //{
        //    string ch1 = ch1Over ? "항온조1 온도 초과" : (ch1Under ? "항온조1 온도 미달" : string.Empty);
        //    string ch2 = ch2Over ? "항온조2 온도 초과" : (ch2Under ? "항온조2 온도 미달" : string.Empty);

        //    if (!string.IsNullOrWhiteSpace(ch1) && !string.IsNullOrWhiteSpace(ch2))
        //        return ch1 + " / " + ch2;

        //    if (!string.IsNullOrWhiteSpace(ch1))
        //        return ch1;

        //    return ch2;
        //}

        //private void ToggleAlarmFlash()
        //{
        //    _isAlarmFlashOn = !_isAlarmFlashOn;
        //    ApplyAlarmBackColor(_isAlarmFlashOn ? Color.OrangeRed : Color.White);
        //}

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
            SetBackColorRecursive(this, color, skip: lblThermoPortStatus);
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

        private static void SetBackColorRecursive(Control root, Color color, Control skip)
        {
            if (!ReferenceEquals(root, skip))
                root.BackColor = color;

            foreach (Control child in root.Controls)
            {
                SetBackColorRecursive(child, color, skip);
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

            if (_isTempAlarmActive && !string.IsNullOrWhiteSpace(_tempAlarmStatusText))
            {
                lblThermoPortStatus.Text = _tempAlarmStatusText;
                lblThermoPortStatus.ForeColor = Color.OrangeRed;
                return;
            }

            lblThermoPortStatus.Text =
                $"BOARD({_host}:{_port}): {(_boardConnected ? "CONNECTED" : "DISCONNECTED")} (fail={_boardFailCount})";

            lblThermoPortStatus.ForeColor = _boardConnected ? Color.LimeGreen : Color.OrangeRed;
        }

        // ===========================================================
        // IMPORTANT
        // ===========================================================
        // 자동 보정이 실행 중에 OFF로 바뀌었을 때도 즉시 멈추려면,
        // LoopOnceCore() 또는 WorkerLoop 내부에서 매 tick마다 _enableOffsetControl을 읽어서
        // UpdateAndMaybeWrite 호출을 스킵해야 한다.
    }
}