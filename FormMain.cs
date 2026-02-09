using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    public partial class FormMain : Form
    {
        private double _bath1Setpoint = 25.0;
        private double _bath2Setpoint = 25.0;

        private const double OffsetStep = 0.1;
        private const double OffsetClampMin = -1.0;
        private const double OffsetClampMax = 1.0;

        private readonly System.Windows.Forms.Timer _loopTimer;
        private bool _running;

        private const int MaxPoints = 300;
        private readonly List<SampleRow> _history = new List<SampleRow>(MaxPoints);

        private double _bath1OffsetCur;
        private double _bath2OffsetCur;

        private bool _boardConnected;
        private bool _ch1DataOk;
        private bool _ch2DataOk;

        private int _boardFailCount;
        private int _ch1FailCount;
        private int _ch2FailCount;

        private MultiBoardModbusClient _mb = null!;

        private int _inTick;

        private string _host = "192.168.1.11";
        private int _port = 13000;
        private byte _unitId = 1;

        // CSV 로깅
        private readonly object _csvSync = new object();
        private string _csvPath = "";
        private bool _csvHeaderWritten;
        private DateTime _csvDay = DateTime.MinValue;

        private long _lastReconnectTick;
        private const int ReconnectCooldownMs = 800;

        // 제어 안정화 상태

        // 적분(I) 항
        private double _iTerm1 = 0.0;
        private double _iTerm2 = 0.0;

        // 미분(D) 항용 이전 error
        private double _prevErr1 = double.NaN;
        private double _prevErr2 = double.NaN;

        // 채널별 마지막 write 시각(hold/지연 반응 학습용)
        private DateTime _lastWriteCh1 = DateTime.MinValue;
        private DateTime _lastWriteCh2 = DateTime.MinValue;

        public FormMain()
        {
            InitializeComponent();

            Text = "ThermoBathCalibrator";

            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
            btnOffsetApply.Click += BtnOffsetApply_Click;
            btnComSetting.Click += BtnComSetting_Click;

            nudOffSet.DecimalPlaces = 1;
            nudOffSet.Increment = 0.1M;
            nudOffSet.Minimum = -1.0M;
            nudOffSet.Maximum = 1.0M;
            nudOffSet.Value = 0.0M;

            pnlCh1Graph.Paint += PnlCh1Graph_Paint;
            pnlCh2Graph.Paint += PnlCh2Graph_Paint;
            EnableDoubleBuffer(pnlCh1Graph);
            EnableDoubleBuffer(pnlCh2Graph);

            _loopTimer = new System.Windows.Forms.Timer();
            _loopTimer.Interval = 1000;
            _loopTimer.Tick += LoopTimer_Tick;

            LoadMultiBoardSettingsFromDiskIfAny();

            BuildMultiBoardClient();

            ResetConnectionState();
            UpdateStatusLabels();
            UpdateTopNumbers(double.NaN, double.NaN, double.NaN);

            PrepareCsvPath(DateTime.Now);
        }

        private void LoadMultiBoardSettingsFromDiskIfAny()
        {
            try
            {
                string path = CommSettings.GetDefaultPath();
                var s = CommSettings.LoadOrDefault(path);

                if (s?.MultiBoard != null)
                {
                    string host = (s.MultiBoard.Host ?? "").Trim();
                    int port = s.MultiBoard.Port;
                    int unit = s.MultiBoard.UnitId;

                    if (!string.IsNullOrWhiteSpace(host)) _host = host;
                    if (port > 0 && port <= 65535) _port = port;
                    if (unit >= 1 && unit <= 247) _unitId = (byte)unit;
                }
            }
            catch
            {
                // 설정 로드 실패 시 기본값 유지
            }
        }

        private void ApplyMultiBoardEndpoint(string host, int port, byte unitId)
        {
            _host = (host ?? "").Trim();
            if (string.IsNullOrWhiteSpace(_host)) _host = "192.168.1.11";

            _port = (port > 0 && port <= 65535) ? port : 13000;
            _unitId = (unitId >= 1 && unitId <= 247) ? unitId : (byte)1;

            try { _mb?.Disconnect(); } catch { }

            BuildMultiBoardClient();
            ResetConnectionState();
            UpdateStatusLabels();
        }

        private void BuildMultiBoardClient()
        {
            _mb = new MultiBoardModbusClient(_host, _port, _unitId);
        }

        private void ResetConnectionState()
        {
            _boardConnected = false;
            _ch1DataOk = false;
            _ch2DataOk = false;

            _boardFailCount = 0;
            _ch1FailCount = 0;
            _ch2FailCount = 0;

            _iTerm1 = 0.0;
            _iTerm2 = 0.0;
            _prevErr1 = double.NaN;
            _prevErr2 = double.NaN;
            _lastWriteCh1 = DateTime.MinValue;
            _lastWriteCh2 = DateTime.MinValue;
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
                    MessageBox.Show($"멀티보드 설정 적용: {_host}:{_port} (UnitId={_unitId})",
                        "COM Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("멀티보드 설정을 찾을 수 없습니다. (comm_settings.json 확인)",
                        "COM Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "COM Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_running) return;

            PrepareCsvPath(DateTime.Now);

            if (!_mb.IsConnected)
            {
                _boardConnected = TryConnectWithCooldown();
                if (!_boardConnected) _boardFailCount++;
            }

            _running = true;
            _loopTimer.Start();

            UpdateStatusLabels();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (!_running) return;

            _running = false;
            _loopTimer.Stop();

            _mb.Disconnect();
            _boardConnected = false;

            UpdateStatusLabels();
        }

        private void BtnOffsetApply_Click(object sender, EventArgs e)
        {
            double offset = (double)nudOffSet.Value;
            double applied = QuantizeOffset(offset, step: OffsetStep);

            bool ok1 = TryWriteChannelOffset(channel: 1, appliedOffset: applied);
            bool ok2 = TryWriteChannelOffset(channel: 2, appliedOffset: applied);

            if (ok1)
            {
                _bath1OffsetCur = applied;
                _lastWriteCh1 = DateTime.Now;
            }
            if (ok2)
            {
                _bath2OffsetCur = applied;
                _lastWriteCh2 = DateTime.Now;
            }

            lblOffsetValue.Text = applied.ToString("0.0", CultureInfo.InvariantCulture);

            UpdateStatusLabels();
            pnlCh1Graph.Invalidate();
            pnlCh2Graph.Invalidate();
        }

        private void LoopTimer_Tick(object sender, EventArgs e)
        {
            if (!_running) return;

            if (Interlocked.Exchange(ref _inTick, 1) == 1) return;

            try { LoopOnce(); }
            finally { Interlocked.Exchange(ref _inTick, 0); }
        }

        private void LoopOnce()
        {
            DateTime now = DateTime.Now;

            PrepareCsvPath(now);

            bool readOk = TryReadMultiBoard(out MultiBoardSnapshot snap);

            // ut_ch1 / ut_ch2: UT-ONE 외부 온도계 측정값(채널별)
            double utCh1 = readOk ? snap.Ch1ExternalThermo : double.NaN;
            double utCh2 = readOk ? snap.Ch2ExternalThermo : double.NaN;

            // ut_tj: UT-ONE 내부 온도(TJ)
            double utTj = readOk ? snap.Tj : double.NaN;

            // bath1_pv / bath2_pv: 항온조 현재 온도(PV)
            double bath1Pv = readOk ? snap.Ch1Pv : double.NaN;
            double bath2Pv = readOk ? snap.Ch2Pv : double.NaN;

            if (readOk)
            {
                // bath1_offset_cur / bath2_offset_cur: 장비에 이미 적용되어 있는 offset(레지스터 값)
                _bath1OffsetCur = snap.Ch1OffsetCur;
                _bath2OffsetCur = snap.Ch2OffsetCur;
            }

            // err1 = ut_ch1 - bath1_pv, err2 = ut_ch2 - bath2_pv
            double err1 = (!double.IsNaN(utCh1) && !double.IsNaN(bath1Pv)) ? (utCh1 - bath1Pv) : double.NaN;
            double err2 = (!double.IsNaN(utCh2) && !double.IsNaN(bath2Pv)) ? (utCh2 - bath2Pv) : double.NaN;

            // derr1 = err1 - prev_err1, derr2 = err2 - prev_err2
            double derr1 = (!double.IsNaN(err1) && !double.IsNaN(_prevErr1)) ? (err1 - _prevErr1) : double.NaN;
            double derr2 = (!double.IsNaN(err2) && !double.IsNaN(_prevErr2)) ? (err2 - _prevErr2) : double.NaN;

            // err1_ma5 / err2_ma5: 최근 5초 이동평균(유효값만)
            double err1Ma5 = MovingAverageWithCurrent(_history.Select(h => h.Err1), current: err1, window: 5);
            double err2Ma5 = MovingAverageWithCurrent(_history.Select(h => h.Err2), current: err2, window: 5);

            // err1_std10 / err2_std10: 최근 10초 표준편차(유효값만)
            double err1Std10 = StdDevWithCurrent(_history.Select(h => h.Err1), current: err1, window: 10);
            double err2Std10 = StdDevWithCurrent(_history.Select(h => h.Err2), current: err2, window: 10);

            // last_write_age_ch1_sec / last_write_age_ch2_sec
            // 채널별 마지막 write 이후 경과 시간(초). hold/지연 반응을 채널별로 학습시키려면 분리하는 편이 좋다.
            double lastWriteAgeCh1Sec = (_lastWriteCh1 == DateTime.MinValue) ? double.NaN : (now - _lastWriteCh1).TotalSeconds;
            double lastWriteAgeCh2Sec = (_lastWriteCh2 == DateTime.MinValue) ? double.NaN : (now - _lastWriteCh2).TotalSeconds;

            bool readOkFlag = readOk;
            bool boardConnectedFlag = _boardConnected;

            // bath1_offset_target / bath2_offset_target: 제어 로직이 계산한 목표 offset(연속값)
            double target1 = (!double.IsNaN(err1)) ? CalculateOffsetTarget(channel: 1, error: err1) : double.NaN;
            double target2 = (!double.IsNaN(err2)) ? CalculateOffsetTarget(channel: 2, error: err2) : double.NaN;

            // step(0.1) 반영된 "원래 쓰고 싶은 값"
            double desiredApplied1 = (!double.IsNaN(target1)) ? QuantizeOffset(target1, OffsetStep) : double.NaN;
            double desiredApplied2 = (!double.IsNaN(target2)) ? QuantizeOffset(target2, OffsetStep) : double.NaN;

            bool w1 = false;
            bool w2 = false;

            double appliedToSend1 = desiredApplied1;
            double appliedToSend2 = desiredApplied2;

            if (readOk && !double.IsNaN(desiredApplied1))
            {
                if (TryApplyOffsetWithPolicy(channel: 1, now: now, err: err1, desiredAppliedOffset: desiredApplied1, ref appliedToSend1))
                {
                    w1 = TryWriteChannelOffset(channel: 1, appliedOffset: appliedToSend1);
                    if (w1)
                    {
                        _bath1OffsetCur = appliedToSend1;
                        _lastWriteCh1 = now;
                    }
                }
                else
                {
                    appliedToSend1 = _bath1OffsetCur;
                }
            }
            else
            {
                appliedToSend1 = _bath1OffsetCur;
            }

            if (readOk && !double.IsNaN(desiredApplied2))
            {
                if (TryApplyOffsetWithPolicy(channel: 2, now: now, err: err2, desiredAppliedOffset: desiredApplied2, ref appliedToSend2))
                {
                    w2 = TryWriteChannelOffset(channel: 2, appliedOffset: appliedToSend2);
                    if (w2)
                    {
                        _bath2OffsetCur = appliedToSend2;
                        _lastWriteCh2 = now;
                    }
                }
                else
                {
                    appliedToSend2 = _bath2OffsetCur;
                }
            }
            else
            {
                appliedToSend2 = _bath2OffsetCur;
            }

            // bath1_set_temp = bath1_setpoint + bath1_offset_cur, bath2_set_temp = bath2_setpoint + bath2_offset_cur
            double bath1SetTemp = (!double.IsNaN(_bath1OffsetCur)) ? _bath1Setpoint + _bath1OffsetCur : double.NaN;
            double bath2SetTemp = (!double.IsNaN(_bath2OffsetCur)) ? _bath2Setpoint + _bath2OffsetCur : double.NaN;

            double offsetAvg = AverageOrNaN(_bath1OffsetCur, _bath2OffsetCur);
            UpdateTopNumbers(utCh1, utCh2, offsetAvg);

            var row = new SampleRow
            {
                Timestamp = now,

                UtCh1 = utCh1,
                UtCh2 = utCh2,
                UtTj = utTj,

                Bath1Pv = bath1Pv,
                Bath2Pv = bath2Pv,

                Err1 = err1,
                Err2 = err2,

                Bath1OffsetCur = _bath1OffsetCur,
                Bath2OffsetCur = _bath2OffsetCur,

                Derr1 = derr1,
                Derr2 = derr2,

                Err1Ma5 = err1Ma5,
                Err2Ma5 = err2Ma5,

                Err1Std10 = err1Std10,
                Err2Std10 = err2Std10,

                LastWriteAgeCh1Sec = lastWriteAgeCh1Sec,
                LastWriteAgeCh2Sec = lastWriteAgeCh2Sec,

                ReadOk = readOkFlag,
                BoardConnected = boardConnectedFlag,

                Bath1OffsetTarget = target1,
                Bath2OffsetTarget = target2,

                Bath1OffsetApplied = appliedToSend1,
                Bath2OffsetApplied = appliedToSend2,

                Bath1SetTemp = bath1SetTemp,
                Bath2SetTemp = bath2SetTemp
            };

            AppendRowToGrid(row);
            AppendRowToHistory(row);

            AppendCsvRow(row);

            UpdateStatusLabels();

            pnlCh1Graph.Invalidate();
            pnlCh2Graph.Invalidate();
        }

        private bool TryConnectWithCooldown()
        {
            long now = Environment.TickCount64;
            long elapsed = now - _lastReconnectTick;
            if (elapsed < ReconnectCooldownMs)
                return false;

            _lastReconnectTick = now;
            return _mb.TryConnect(out _);
        }

        private bool TryReadMultiBoard(out MultiBoardSnapshot snap)
        {
            snap = default;

            if (!_mb.IsConnected)
            {
                _boardConnected = TryConnectWithCooldown();
                if (!_boardConnected) _boardFailCount++;
            }

            if (!_mb.IsConnected)
            {
                _boardConnected = false;
                _ch1DataOk = false;
                _ch2DataOk = false;

                _ch1FailCount++;
                _ch2FailCount++;
                return false;
            }

            if (!_mb.TryReadHoldingRegisters(start: 0, count: 14, out ushort[] regs, out string err))
            {
                _boardConnected = false;
                _boardFailCount++;

                _ch1DataOk = false;
                _ch2DataOk = false;
                _ch1FailCount++;
                _ch2FailCount++;

                return false;
            }

            _boardConnected = true;

            snap = ParseSnapshot(regs);

            _ch1DataOk = true;
            _ch2DataOk = true;

            return true;
        }

        private static MultiBoardSnapshot ParseSnapshot(ushort[] r)
        {
            if (r == null || r.Length < 14) return default;

            short ch1PvRaw = unchecked((short)r[2]);
            short ch1SvRaw = unchecked((short)r[3]);
            short ch1OffRaw = unchecked((short)r[4]);
            short ch2PvRaw = unchecked((short)r[9]);
            short ch2SvRaw = unchecked((short)r[10]);
            short ch2OffRaw = unchecked((short)r[11]);

            int ch1ExtRaw = r[5];
            int ch2ExtRaw = r[12];

            int tjRaw = r[13];

            return new MultiBoardSnapshot
            {
                Ch1Alive = r[0],
                Ch1Response = r[1],
                Ch1Pv = ch1PvRaw / 10.0,
                Ch1Sv = ch1SvRaw / 10.0,
                Ch1OffsetCur = ch1OffRaw / 10.0,
                Ch1ExternalThermo = ch1ExtRaw / 1000.0,

                Ch2Alive = r[7],
                Ch2Response = r[8],
                Ch2Pv = ch2PvRaw / 10.0,
                Ch2Sv = ch2SvRaw / 10.0,
                Ch2OffsetCur = ch2OffRaw / 10.0,
                Ch2ExternalThermo = ch2ExtRaw / 1000.0,

                Tj = tjRaw / 1000.0
            };
        }

        private bool TryWriteChannelOffset(int channel, double appliedOffset)
        {
            if (!_mb.IsConnected)
            {
                _boardConnected = TryConnectWithCooldown();
                if (!_boardConnected) _boardFailCount++;
            }

            if (!_mb.IsConnected)
            {
                _boardConnected = false;
                return false;
            }

            short raw10 = (short)Math.Round(appliedOffset * 10.0, MidpointRounding.AwayFromZero);
            ushort offsetWord = unchecked((ushort)raw10);

            if (channel == 1)
            {
                ushort cmd = 0;
                cmd |= (1 << 1);

                ushort svWord = unchecked((ushort)((short)Math.Round(_bath1Setpoint * 10.0, MidpointRounding.AwayFromZero)));

                if (!_mb.TryWriteMultipleRegisters(start: 20, values: new ushort[] { cmd, svWord, offsetWord }, out string err1))
                    return false;

                _mb.TryWriteMultipleRegisters(start: 20, values: new ushort[] { 0 }, out _);
                return true;
            }

            if (channel == 2)
            {
                ushort cmd = 0;
                cmd |= (1 << 1);

                ushort svWord = unchecked((ushort)((short)Math.Round(_bath2Setpoint * 10.0, MidpointRounding.AwayFromZero)));

                if (!_mb.TryWriteMultipleRegisters(start: 24, values: new ushort[] { cmd, svWord, offsetWord }, out string err1))
                    return false;

                _mb.TryWriteMultipleRegisters(start: 24, values: new ushort[] { 0 }, out _);
                return true;
            }

            return false;
        }

        private static double AverageOrNaN(double a, double b)
        {
            bool aOk = !double.IsNaN(a) && !double.IsInfinity(a);
            bool bOk = !double.IsNaN(b) && !double.IsInfinity(b);

            if (aOk && bOk) return (a + b) / 2.0;
            if (aOk) return a;
            if (bOk) return b;
            return double.NaN;
        }

        private double CalculateOffsetTarget(int channel, double error)
        {
            if (Math.Abs(error) < 0.05)
                return 0.0;

            double absErr = Math.Abs(error);

            double kBase;
            if (absErr >= 0.5) kBase = 1.5;
            else if (absErr >= 0.2) kBase = 1.0;
            else kBase = 0.5;

            double k = kBase + 0.5;

            double p = error * k;

            const bool EnableIntegral = true;
            const double Ki = 0.05;
            const double IClamp = 0.3;

            double i = (channel == 1) ? _iTerm1 : _iTerm2;

            if (EnableIntegral)
            {
                double predicted = p + i;

                bool atMax = predicted >= OffsetClampMax;
                bool atMin = predicted <= OffsetClampMin;
                bool pushingUp = error > 0;
                bool pushingDown = error < 0;

                bool blockIntegrate = (atMax && pushingUp) || (atMin && pushingDown);

                if (!blockIntegrate)
                {
                    i += error * Ki;
                    i = Clamp(i, -IClamp, IClamp);
                }
            }

            const bool EnableDerivative = false;
            const double Kd = 0.0;

            double d = 0.0;
            if (EnableDerivative)
            {
                double prev = (channel == 1) ? _prevErr1 : _prevErr2;
                if (!double.IsNaN(prev))
                {
                    double derr = error - prev;
                    d = -Kd * derr;
                }
            }

            if (channel == 1) _prevErr1 = error;
            else _prevErr2 = error;

            if (channel == 1) _iTerm1 = i;
            else _iTerm2 = i;

            double target = p + i + d;
            target = Clamp(target, OffsetClampMin, OffsetClampMax);
            return target;
        }

        private bool TryApplyOffsetWithPolicy(int channel, DateTime now, double err, double desiredAppliedOffset, ref double appliedToSendOffset)
        {
            const double MaxDeltaPerWrite = 0.2;
            const double MinChangeToWrite = 0.1;

            double absErr = Math.Abs(err);
            double holdSeconds;
            if (absErr >= 0.5) holdSeconds = 2.0;
            else if (absErr >= 0.2) holdSeconds = 3.0;
            else holdSeconds = 10.0;

            double curOffset = (channel == 1) ? _bath1OffsetCur : _bath2OffsetCur;
            DateTime lastWrite = (channel == 1) ? _lastWriteCh1 : _lastWriteCh2;

            if (lastWrite != DateTime.MinValue)
            {
                double elapsed = (now - lastWrite).TotalSeconds;
                if (elapsed < holdSeconds)
                {
                    appliedToSendOffset = curOffset;
                    return false;
                }
            }

            double delta = desiredAppliedOffset - curOffset;
            if (Math.Abs(delta) > MaxDeltaPerWrite)
            {
                desiredAppliedOffset = curOffset + Math.Sign(delta) * MaxDeltaPerWrite;
                desiredAppliedOffset = Clamp(desiredAppliedOffset, OffsetClampMin, OffsetClampMax);
                desiredAppliedOffset = QuantizeOffset(desiredAppliedOffset, OffsetStep);
            }

            if (Math.Abs(desiredAppliedOffset - curOffset) < (MinChangeToWrite - 1e-9))
            {
                appliedToSendOffset = curOffset;
                return false;
            }

            appliedToSendOffset = desiredAppliedOffset;
            return true;
        }

        private double QuantizeOffset(double value, double step)
        {
            if (step <= 0) return value;
            return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private void AppendRowToGrid(SampleRow r)
        {
            string fmtTemp = "0.000";
            string ts = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

            dataGridView1.Rows.Add(
                ts,
                ToCell(r.UtCh1, fmtTemp),
                ToCell(r.UtCh2, fmtTemp),
                ToCell(r.UtTj, fmtTemp),
                ToCell(r.Bath1Pv, fmtTemp),
                ToCell(r.Bath2Pv, fmtTemp),
                ToCell(r.Err1, fmtTemp),
                ToCell(r.Err2, fmtTemp),
                ToCell(r.Bath1SetTemp, fmtTemp),
                ToCell(r.Bath2SetTemp, fmtTemp)
            );

            if (dataGridView1.Rows.Count > 0)
            {
                int last = dataGridView1.Rows.Count - 1;
                dataGridView1.FirstDisplayedScrollingRowIndex = last;
            }
        }

        private static string ToCell(double v, string fmt)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "-";
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }

        private void AppendRowToHistory(SampleRow r)
        {
            _history.Add(r);
            if (_history.Count > MaxPoints)
                _history.RemoveAt(0);
        }

        private void UpdateTopNumbers(double ch1, double ch2, double offsetAvg)
        {
            lblCh1Temperature.Text = double.IsNaN(ch1) ? "-" : ch1.ToString("0.000", CultureInfo.InvariantCulture);
            lblCh2Temperature.Text = double.IsNaN(ch2) ? "-" : ch2.ToString("0.000", CultureInfo.InvariantCulture);
            lblOffsetValue.Text = double.IsNaN(offsetAvg) ? "-" : offsetAvg.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private void UpdateStatusLabels()
        {
            lblThermoPortStatus.Text =
                $"BOARD({_host}:{_port}): {(_boardConnected ? "CONNECTED" : "DISCONNECTED")} (fail={_boardFailCount})";

            lblThermoPortStatus.ForeColor = _boardConnected ? Color.LimeGreen : Color.OrangeRed;
        }

        private void PnlCh1Graph_Paint(object sender, PaintEventArgs e)
        {
            DrawGraph(e.Graphics, pnlCh1Graph.ClientRectangle, channel: 1);
        }

        private void PnlCh2Graph_Paint(object sender, PaintEventArgs e)
        {
            DrawGraph(e.Graphics, pnlCh2Graph.ClientRectangle, channel: 2);
        }

        private void DrawGraph(Graphics g, Rectangle clientRect, int channel)
        {
            g.Clear(Color.White);

            using var borderPen = new Pen(Color.DarkGray, 1);
            g.DrawRectangle(borderPen, new Rectangle(clientRect.Left, clientRect.Top, clientRect.Width - 1, clientRect.Height - 1));

            if (_history.Count < 2)
            {
                using var f = new Font("Segoe UI", 12, FontStyle.Bold);
                g.DrawString("No data", f, Brushes.Gray, new PointF(10, 10));
                return;
            }

            Rectangle plot = clientRect;
            plot.Inflate(-55, -40);

            using (var bgBrush = new SolidBrush(Color.FromArgb(248, 248, 248)))
            {
                g.FillRectangle(bgBrush, plot);
            }

            List<double> pv = channel == 1 ? _history.Select(h => h.Bath1Pv).ToList()
                                           : _history.Select(h => h.Bath2Pv).ToList();

            List<double> ut = channel == 1 ? _history.Select(h => h.UtCh1).ToList()
                                           : _history.Select(h => h.UtCh2).ToList();

            List<double> setTemp = channel == 1 ? _history.Select(h => h.Bath1SetTemp).ToList()
                                                : _history.Select(h => h.Bath2SetTemp).ToList();

            double center = 25.0;
            double span = 0.5;

            double minY = center - span;
            double maxY = center + span;

            using var gridPen = new Pen(Color.LightGray, 1);
            int hLines = 5;
            for (int i = 0; i <= hLines; i++)
            {
                float y = plot.Top + i * (plot.Height / (float)hLines);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            }

            using var axisFont = new Font("Segoe UI", 9, FontStyle.Regular);
            using var axisBrush = new SolidBrush(Color.DimGray);

            for (int i = 0; i <= hLines; i++)
            {
                double v = maxY - i * ((maxY - minY) / hLines);
                float y = plot.Top + i * (plot.Height / (float)hLines) - 7;
                g.DrawString(v.ToString("0.000", CultureInfo.InvariantCulture), axisFont, axisBrush, new PointF(clientRect.Left + 5, y));
            }

            using var axisPen = new Pen(Color.Gray, 1);
            g.DrawRectangle(axisPen, plot);

            using var xFont = new Font("Segoe UI", 8, FontStyle.Regular);
            using var xBrush = new SolidBrush(Color.DimGray);

            int step = 60;
            for (int i = 0; i < _history.Count; i += step)
            {
                float x = plot.Left + (float)(i * (plot.Width / (double)(_history.Count - 1)));
                g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);

                string t = _history[i].Timestamp.ToString("HH:mm");
                SizeF sz = g.MeasureString(t, xFont);
                g.DrawString(t, xFont, xBrush, x - sz.Width / 2, plot.Bottom + 6);
            }

            using var penPv = new Pen(Color.Blue, 2);
            using var penUt = new Pen(Color.Red, 2);
            using var penSet = new Pen(Color.Green, 2);

            DrawSeries(g, plot, pv, minY, maxY, penPv);
            DrawSeries(g, plot, ut, minY, maxY, penUt);
            DrawSeries(g, plot, setTemp, minY, maxY, penSet);

            using var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
            string title = channel == 1
                ? "CH1: PV / ExternalThermo / Set(SP+Off)"
                : "CH2: PV / ExternalThermo / Set(SP+Off)";
            g.DrawString(title, titleFont, Brushes.Black, new PointF(clientRect.Left + 10, clientRect.Top + 8));

            DrawLegend(g, clientRect, channel);
        }

        private void DrawLegend(Graphics g, Rectangle clientRect, int channel)
        {
            using var font = new Font("Segoe UI", 9, FontStyle.Bold);
            int x = clientRect.Right - 240;
            int y = clientRect.Top + 10;

            DrawLegendItem(g, x, y, Color.Blue, channel == 1 ? "CH1 PV" : "CH2 PV", font);
            y += 18;

            DrawLegendItem(g, x, y, Color.Red, channel == 1 ? "CH1 ExtThermo" : "CH2 ExtThermo", font);
            y += 18;

            DrawLegendItem(g, x, y, Color.Green, channel == 1 ? "CH1 Set" : "CH2 Set", font);
        }

        private void DrawLegendItem(Graphics g, int x, int y, Color c, string text, Font font)
        {
            using var pen = new Pen(c, 3);
            g.DrawLine(pen, x, y + 7, x + 26, y + 7);
            g.DrawString(text, font, Brushes.Black, new PointF(x + 32, y));
        }

        private void DrawSeries(Graphics g, Rectangle rect, List<double> values, double minY, double maxY, Pen pen)
        {
            if (values == null || values.Count < 2) return;

            PointF? prev = null;

            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];

                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    prev = null;
                    continue;
                }

                float x = rect.Left + (float)(i * (rect.Width / (double)(values.Count - 1)));
                float yRatio = (float)((v - minY) / (maxY - minY));
                float y = rect.Bottom - yRatio * rect.Height;

                var pt = new PointF(x, y);

                if (prev.HasValue)
                    g.DrawLine(pen, prev.Value, pt);

                prev = pt;
            }
        }

        private void EnableDoubleBuffer(Control c)
        {
            PropertyInfo prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(c, true, null);
        }

        private void PrepareCsvPath(DateTime now)
        {
            DateTime day = now.Date;
            if (_csvDay == day && !string.IsNullOrWhiteSpace(_csvPath)) return;

            _csvDay = day;
            _csvHeaderWritten = false;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data");
            Directory.CreateDirectory(dataDir);

            string fileName = $"thermo_log_{day:yyyyMMdd}.csv";
            _csvPath = Path.Combine(dataDir, fileName);

            if (File.Exists(_csvPath) && new FileInfo(_csvPath).Length > 0)
                _csvHeaderWritten = true;
        }

        private void AppendCsvRow(SampleRow r)
        {
            try
            {
                lock (_csvSync)
                {
                    if (string.IsNullOrWhiteSpace(_csvPath))
                        PrepareCsvPath(r.Timestamp);

                    bool needHeader = !_csvHeaderWritten;

                    var sb = new StringBuilder(512);

                    if (needHeader)
                    {
                        sb.AppendLine(string.Join(",",
                            "timestamp",
                            "ut_ch1",
                            "ut_ch2",
                            "ut_tj",
                            "bath1_pv",
                            "bath2_pv",
                            "err1",
                            "err2",
                            "bath1_offset_cur",
                            "bath2_offset_cur",
                            "derr1",
                            "derr2",
                            "err1_ma5",
                            "err2_ma5",
                            "err1_std10",
                            "err2_std10",
                            "last_write_age_ch1_sec",
                            "last_write_age_ch2_sec",
                            "read_ok",
                            "board_connected",
                            "bath1_offset_target",
                            "bath2_offset_target",
                            "bath1_offset_applied",
                            "bath2_offset_applied",
                            "bath1_set_temp",
                            "bath2_set_temp"
                        ));
                        _csvHeaderWritten = true;
                    }

                    sb.AppendLine(string.Join(",",
                        r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        CsvNum(r.UtCh1, "0.000"),
                        CsvNum(r.UtCh2, "0.000"),
                        CsvNum(r.UtTj, "0.000"),
                        CsvNum(r.Bath1Pv, "0.000"),
                        CsvNum(r.Bath2Pv, "0.000"),
                        CsvNum(r.Err1, "0.000"),
                        CsvNum(r.Err2, "0.000"),
                        CsvNum(r.Bath1OffsetCur, "0.0"),
                        CsvNum(r.Bath2OffsetCur, "0.0"),
                        CsvNum(r.Derr1, "0.000"),
                        CsvNum(r.Derr2, "0.000"),
                        CsvNum(r.Err1Ma5, "0.000"),
                        CsvNum(r.Err2Ma5, "0.000"),
                        CsvNum(r.Err1Std10, "0.000"),
                        CsvNum(r.Err2Std10, "0.000"),
                        CsvNum(r.LastWriteAgeCh1Sec, "0.0"),
                        CsvNum(r.LastWriteAgeCh2Sec, "0.0"),
                        r.ReadOk ? "1" : "0",
                        r.BoardConnected ? "1" : "0",
                        CsvNum(r.Bath1OffsetTarget, "0.000"),
                        CsvNum(r.Bath2OffsetTarget, "0.000"),
                        CsvNum(r.Bath1OffsetApplied, "0.0"),
                        CsvNum(r.Bath2OffsetApplied, "0.0"),
                        CsvNum(r.Bath1SetTemp, "0.000"),
                        CsvNum(r.Bath2SetTemp, "0.000")
                    ));

                    File.AppendAllText(_csvPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // 로깅 실패는 프로그램 흐름을 막지 않음
            }
        }

        private static string CsvNum(double v, string fmt)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }

        private static double MovingAverageWithCurrent(IEnumerable<double> pastValues, double current, int window)
        {
            var list = new List<double>(window);
            if (!double.IsNaN(current) && !double.IsInfinity(current))
                list.Add(current);

            foreach (var v in pastValues.Reverse())
            {
                if (list.Count >= window) break;
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                list.Add(v);
            }

            if (list.Count == 0) return double.NaN;
            return list.Average();
        }

        private static double StdDevWithCurrent(IEnumerable<double> pastValues, double current, int window)
        {
            var list = new List<double>(window);
            if (!double.IsNaN(current) && !double.IsInfinity(current))
                list.Add(current);

            foreach (var v in pastValues.Reverse())
            {
                if (list.Count >= window) break;
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                list.Add(v);
            }

            if (list.Count < 2) return double.NaN;

            double mean = list.Average();
            double var = 0.0;
            for (int i = 0; i < list.Count; i++)
            {
                double d = list[i] - mean;
                var += d * d;
            }
            var /= (list.Count - 1);
            return Math.Sqrt(var);
        }

        private struct MultiBoardSnapshot
        {
            public ushort Ch1Alive;
            public ushort Ch1Response;
            public double Ch1Pv;
            public double Ch1Sv;
            public double Ch1OffsetCur;
            public double Ch1ExternalThermo;

            public ushort Ch2Alive;
            public ushort Ch2Response;
            public double Ch2Pv;
            public double Ch2Sv;
            public double Ch2OffsetCur;
            public double Ch2ExternalThermo;

            public double Tj;
        }

        private sealed class SampleRow
        {
            public DateTime Timestamp { get; set; }

            public double UtCh1 { get; set; }
            public double UtCh2 { get; set; }
            public double UtTj { get; set; }

            public double Bath1Pv { get; set; }
            public double Bath2Pv { get; set; }

            public double Err1 { get; set; }
            public double Err2 { get; set; }

            public double Bath1OffsetCur { get; set; }
            public double Bath2OffsetCur { get; set; }

            public double Derr1 { get; set; }
            public double Derr2 { get; set; }

            public double Err1Ma5 { get; set; }
            public double Err2Ma5 { get; set; }

            public double Err1Std10 { get; set; }
            public double Err2Std10 { get; set; }

            public double LastWriteAgeCh1Sec { get; set; }
            public double LastWriteAgeCh2Sec { get; set; }

            public bool ReadOk { get; set; }
            public bool BoardConnected { get; set; }

            public double Bath1OffsetTarget { get; set; }
            public double Bath2OffsetTarget { get; set; }

            public double Bath1OffsetApplied { get; set; }
            public double Bath2OffsetApplied { get; set; }

            public double Bath1SetTemp { get; set; }
            public double Bath2SetTemp { get; set; }
        }
    }
}
