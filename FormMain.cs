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
        // ===== UT-ONE 측정 보정(업체 기준 온도계 대비 오프셋) =====
        // UT-ONE이 0.3도 높게 나오면 +0.3으로 두고, 계산 시 ut에서 빼준다.
        private double _utBiasCh1 = 0.14;
        private double _utBiasCh2 = 0.3;

        private double _bath1Setpoint = 25.0;
        private double _bath2Setpoint = 25.0;

        private const double OffsetStep = 0.1;
        private const double OffsetClampMin = -1.0;
        private const double OffsetClampMax = 1.0;

        private bool _running;

        // ===== 단순 offset 제어용 =====
        private DateTime _lastSimpleAdjustCh1 = DateTime.MinValue;
        private DateTime _lastSimpleAdjustCh2 = DateTime.MinValue;

        private const double TargetTemp = 25.000;
        private const double SimpleStep = 0.1;
        private const double SimpleHoldSeconds = 20.0; // 물 반응 기다리는 시간(최소 유지)

        // ===== 모니터링 그래프: 최근 5분 "시간 기반" 윈도우로 흘러가게 표시 =====
        private const int MaxPoints = 300;
        private readonly List<SampleRow> _history = new List<SampleRow>(MaxPoints);
        private static readonly TimeSpan GraphWindow = TimeSpan.FromMinutes(5);

        private double _bath1OffsetCur;
        private double _bath2OffsetCur;

        private bool _boardConnected;
        private int _boardFailCount;

        private MultiBoardModbusClient _mb = null!;

        private string _host = "192.168.1.11";
        private int _port = 13000;
        private byte _unitId = 1;

        // CSV 로깅
        private readonly object _csvSync = new object();
        private string _csvPath = "";
        private bool _csvHeaderWritten;
        private DateTime _csvDay = DateTime.MinValue;

        private readonly object _traceSync = new object();
        private string _tracePath = "";
        private DateTime _traceDay = DateTime.MinValue;

        private long _lastReconnectTick;
        private const int ReconnectCooldownMs = 800;

        // prevErr는 로그(derr) 계산용
        private double _prevErr1 = double.NaN;
        private double _prevErr2 = double.NaN;

        private DateTime _lastWriteCh1 = DateTime.MinValue;
        private DateTime _lastWriteCh2 = DateTime.MinValue;

        private DateTime _lastEnforceWriteCh1 = DateTime.MinValue;
        private DateTime _lastEnforceWriteCh2 = DateTime.MinValue;
        private double _lastWrittenOffsetCh1 = double.NaN;
        private double _lastWrittenOffsetCh2 = double.NaN;

        // 워커 스레드(통신/로깅)
        private Thread? _workerThread;
        private volatile bool _workerRunning;

        // 직전 정상값(필드 단위) 보정용 스냅샷
        private MultiBoardSnapshot _lastGoodSnap;
        private bool _hasLastGoodSnap;

        // stale(레지스터 갱신 안 됨) 판정용
        private ushort _lastRespCh1;
        private ushort _lastRespCh2;
        private bool _hasLastResp;

        // Grid 성능
        private const int MaxGridRows = 2000;
        private int _scrollEveryN = 5;
        private int _rowAddCount = 0;

        // 그래프 스케일 고정 (요청: 24.5~25.5)
        private const bool UseFixedGraphScale = true;
        private const double FixedGraphMinY = 24.0;
        private const double FixedGraphMaxY = 26.0;

        private const ushort RegReadStart = 0;
        private const ushort RegReadCount = 14;
        private const ushort RegCh1Command = 20;
        private const ushort RegCh2Command = 24;
        private const ushort RegCh1Response = 1;
        private const ushort RegCh2Response = 8;

        private const int AckTimeoutMs = 1500;
        private const int AckPollIntervalMs = 100;
        private const int AckInitialDelayMs = 120;

        private const double OffsetReadbackMismatchEpsilon = 0.049;
        private const double EnforceWriteIntervalSeconds = 1.0;

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
            _mb.Trace = TraceModbus;
        }

        private void ResetConnectionState()
        {
            _boardConnected = false;
            _boardFailCount = 0;

            _prevErr1 = double.NaN;
            _prevErr2 = double.NaN;
            _lastWriteCh1 = DateTime.MinValue;
            _lastWriteCh2 = DateTime.MinValue;

            _lastEnforceWriteCh1 = DateTime.MinValue;
            _lastEnforceWriteCh2 = DateTime.MinValue;
            _lastWrittenOffsetCh1 = double.NaN;
            _lastWrittenOffsetCh2 = double.NaN;

            _hasLastGoodSnap = false;
            _lastGoodSnap = default;

            _hasLastResp = false;
            _lastRespCh1 = 0;
            _lastRespCh2 = 0;

            _lastSimpleAdjustCh1 = DateTime.MinValue;
            _lastSimpleAdjustCh2 = DateTime.MinValue;
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

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_running) return;

            PrepareCsvPath(DateTime.Now);

            _running = true;
            _workerRunning = true;

            _workerThread = new Thread(WorkerLoop);
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

        private void BtnOffsetApply_Click(object sender, EventArgs e)
        {
            // 수동 적용: CH1/CH2 모두 동일 값으로 쓰는 동작(원하면 채널별 UI로 분리 가능)
            double offset = (double)nudOffSet.Value;
            double applied = QuantizeOffset(offset, step: OffsetStep);
            applied = Clamp(applied, OffsetClampMin, OffsetClampMax);

            bool ok1 = TryWriteChannelOffset(channel: 1, appliedOffset: applied, reason: "MANUAL_APPLY");
            bool ok2 = TryWriteChannelOffset(channel: 2, appliedOffset: applied, reason: "MANUAL_APPLY");

            if (ok1)
            {
                _bath1OffsetCur = applied;
                _lastWriteCh1 = DateTime.Now;
                _lastSimpleAdjustCh1 = DateTime.Now; // 수동도 “최근 변경”으로 취급
            }

            if (ok2)
            {
                _bath2OffsetCur = applied;
                _lastWriteCh2 = DateTime.Now;
                _lastSimpleAdjustCh2 = DateTime.Now;
            }

            lblOffsetValue.Text = applied.ToString("0.0", CultureInfo.InvariantCulture);

            UpdateStatusLabels();
            pnlCh1Graph.Invalidate();
            pnlCh2Graph.Invalidate();
        }

        private void WorkerLoop()
        {
            while (_workerRunning)
            {
                DateTime started = DateTime.Now;

                try
                {
                    SampleRow row = LoopOnceCore();

                    // ===== 스킵 기준 적용 =====
                    if (ShouldSkipRow(row))
                    {
                        BeginInvoke(new Action(() =>
                        {
                            UpdateStatusLabels();
                        }));
                    }
                    else
                    {
                        BeginInvoke(new Action(() =>
                        {
                            AppendRowToGrid(row);
                            AppendRowToHistory(row);

                            double offsetAvg = AverageOrNaN(row.Bath1OffsetCur, row.Bath2OffsetCur);
                            UpdateTopNumbers(row.UtCh1, row.UtCh2, offsetAvg);

                            UpdateStatusLabels();
                            pnlCh1Graph.Invalidate();
                            pnlCh2Graph.Invalidate();
                        }));

                        AppendCsvRow(row);
                    }
                }
                catch
                {
                }

                int sleepMs = 1000 - (int)(DateTime.Now - started).TotalMilliseconds;
                if (sleepMs < 1) sleepMs = 1;
                Thread.Sleep(sleepMs);
            }
        }

        private static bool IsMissingOrZero(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return true;
            return Math.Abs(v) < 1e-9;
        }

        private static bool ShouldSkipRow(SampleRow r)
        {
            bool allMissing =
                IsMissingOrZero(r.Bath1Pv) &&
                IsMissingOrZero(r.Bath2Pv) &&
                IsMissingOrZero(r.UtCh1) &&
                IsMissingOrZero(r.UtCh2);

            return allMissing;
        }

        private SampleRow LoopOnceCore()
        {
            DateTime now = DateTime.Now;

            PrepareCsvPath(now);

            bool readOk = TryReadMultiBoard(out MultiBoardSnapshot snap, out bool stale);

            double utCh1Raw = readOk ? snap.Ch1ExternalThermo : double.NaN;
            double utCh2Raw = readOk ? snap.Ch2ExternalThermo : double.NaN;

            // ===== UT 보정(반드시 유지) =====
            double utCh1 = double.IsNaN(utCh1Raw) ? double.NaN : (utCh1Raw - _utBiasCh1);
            double utCh2 = double.IsNaN(utCh2Raw) ? double.NaN : (utCh2Raw - _utBiasCh2);
            double utTj = readOk ? snap.Tj : double.NaN;

            double bath1Pv = readOk ? snap.Ch1Pv : double.NaN;
            double bath2Pv = readOk ? snap.Ch2Pv : double.NaN;

            if (readOk)
            {
                _bath1OffsetCur = snap.Ch1OffsetCur;
                _bath2OffsetCur = snap.Ch2OffsetCur;

                LogOffsetReadAndMismatch(channel: 1, readOffset: snap.Ch1OffsetCur, response: snap.Ch1Response);
                LogOffsetReadAndMismatch(channel: 2, readOffset: snap.Ch2OffsetCur, response: snap.Ch2Response);
            }
            else
            {
                TraceModbus("OFFSET READ SKIP readOk=false (cannot compare readback)");
            }

            // =================================================================================
            // 목표: UT(보정 후)가 25.000에 수렴 (SV=25 고정 + offset으로 조절)
            // err = SP - UT
            //  - err > 0 : UT가 낮음 => 온도를 올려야 함 => offset을 내려야 함
            //  - err < 0 : UT가 높음 => 온도를 내려야 함 => offset을 올려야 함
            // =================================================================================
            double err1 = (!double.IsNaN(utCh1)) ? (_bath1Setpoint - utCh1) : double.NaN;
            double err2 = (!double.IsNaN(utCh2)) ? (_bath2Setpoint - utCh2) : double.NaN;

            double derr1 = (!double.IsNaN(err1) && !double.IsNaN(_prevErr1)) ? (err1 - _prevErr1) : double.NaN;
            double derr2 = (!double.IsNaN(err2) && !double.IsNaN(_prevErr2)) ? (err2 - _prevErr2) : double.NaN;

            _prevErr1 = err1;
            _prevErr2 = err2;

            double err1Ma5 = MovingAverageWithCurrent(_history.Select(h => h.Err1), current: err1, window: 5);
            double err2Ma5 = MovingAverageWithCurrent(_history.Select(h => h.Err2), current: err2, window: 5);

            double err1Std10 = StdDevWithCurrent(_history.Select(h => h.Err1), current: err1, window: 10);
            double err2Std10 = StdDevWithCurrent(_history.Select(h => h.Err2), current: err2, window: 10);

            double lastWriteAgeCh1Sec = (_lastWriteCh1 == DateTime.MinValue) ? double.NaN : (now - _lastWriteCh1).TotalSeconds;
            double lastWriteAgeCh2Sec = (_lastWriteCh2 == DateTime.MinValue) ? double.NaN : (now - _lastWriteCh2).TotalSeconds;

            // ===== 단순 계단(step) 제어 =====
            // - 조건1: UT가 목표에서 벗어났을 때만(±0.001)
            // - 조건2: 마지막 조정 후 최소 SimpleHoldSeconds(기본 20초) 기다린 후
            // - 동작: UT가 높으면 offset +0.1 (온도 내려가기), UT가 낮으면 offset -0.1 (온도 올라가기)
            // - offset은 [-1.0, +1.0] clamp + 0.1 quantize
            double appliedToSend1 = _bath1OffsetCur;
            double appliedToSend2 = _bath2OffsetCur;

            // CH1
            if (readOk && !double.IsNaN(utCh1))
            {
                bool holdElapsed = (now - _lastSimpleAdjustCh1).TotalSeconds >= SimpleHoldSeconds;

                if (holdElapsed)
                {
                    if (utCh1 > TargetTemp + 0.001)
                    {
                        double next = Clamp(_bath1OffsetCur + SimpleStep, OffsetClampMin, OffsetClampMax);
                        next = QuantizeOffset(next, OffsetStep);

                        if (TryWriteChannelOffset(1, next, "AUTO_STEP_CH1"))
                        {
                            _bath1OffsetCur = next;
                            _lastWriteCh1 = now;
                            _lastSimpleAdjustCh1 = now;
                        }
                    }
                    else if (utCh1 < TargetTemp - 0.001)
                    {
                        double next = Clamp(_bath1OffsetCur - SimpleStep, OffsetClampMin, OffsetClampMax);
                        next = QuantizeOffset(next, OffsetStep);

                        if (TryWriteChannelOffset(1, next, "AUTO_STEP_CH1"))
                        {
                            _bath1OffsetCur = next;
                            _lastWriteCh1 = now;
                            _lastSimpleAdjustCh1 = now;
                        }
                    }
                    else
                    {
                        TraceModbus($"OFFSET AUTO SKIP ch=1 reason=in_deadband ut={utCh1.ToString("0.000", CultureInfo.InvariantCulture)} target={TargetTemp.ToString("0.000", CultureInfo.InvariantCulture)}");
                    }
                }
                else
                {
                    double holdLeft = SimpleHoldSeconds - (now - _lastSimpleAdjustCh1).TotalSeconds;
                    TraceModbus($"OFFSET AUTO SKIP ch=1 reason=hold remainSec={Math.Max(0.0, holdLeft).ToString("0.0", CultureInfo.InvariantCulture)}");
                    EnforceLastWrittenOffsetIfNeeded(1, now, snap.Ch1OffsetCur, snap.Ch1Response);
                }
            }
            else
            {
                if (!readOk)
                {
                    TraceModbus("OFFSET AUTO SKIP ch=1 reason=readOk_false");
                }
                else
                {
                    TraceModbus("OFFSET AUTO SKIP ch=1 reason=ut_nan");
                }
            }
            appliedToSend1 = _bath1OffsetCur;

            // CH2
            if (readOk && !double.IsNaN(utCh2))
            {
                bool holdElapsed = (now - _lastSimpleAdjustCh2).TotalSeconds >= SimpleHoldSeconds;

                if (holdElapsed)
                {
                    if (utCh2 > TargetTemp + 0.001)
                    {
                        double next = Clamp(_bath2OffsetCur + SimpleStep, OffsetClampMin, OffsetClampMax);
                        next = QuantizeOffset(next, OffsetStep);

                        if (TryWriteChannelOffset(2, next, "AUTO_STEP_CH2"))
                        {
                            _bath2OffsetCur = next;
                            _lastWriteCh2 = now;
                            _lastSimpleAdjustCh2 = now;
                        }
                    }
                    else if (utCh2 < TargetTemp - 0.001)
                    {
                        double next = Clamp(_bath2OffsetCur - SimpleStep, OffsetClampMin, OffsetClampMax);
                        next = QuantizeOffset(next, OffsetStep);

                        if (TryWriteChannelOffset(2, next, "AUTO_STEP_CH2"))
                        {
                            _bath2OffsetCur = next;
                            _lastWriteCh2 = now;
                            _lastSimpleAdjustCh2 = now;
                        }
                    }
                    else
                    {
                        TraceModbus($"OFFSET AUTO SKIP ch=2 reason=in_deadband ut={utCh2.ToString("0.000", CultureInfo.InvariantCulture)} target={TargetTemp.ToString("0.000", CultureInfo.InvariantCulture)}");
                    }
                }
                else
                {
                    double holdLeft = SimpleHoldSeconds - (now - _lastSimpleAdjustCh2).TotalSeconds;
                    TraceModbus($"OFFSET AUTO SKIP ch=2 reason=hold remainSec={Math.Max(0.0, holdLeft).ToString("0.0", CultureInfo.InvariantCulture)}");
                    EnforceLastWrittenOffsetIfNeeded(2, now, snap.Ch2OffsetCur, snap.Ch2Response);
                }
            }
            else
            {
                if (!readOk)
                {
                    TraceModbus("OFFSET AUTO SKIP ch=2 reason=readOk_false");
                }
                else
                {
                    TraceModbus("OFFSET AUTO SKIP ch=2 reason=ut_nan");
                }
            }
            appliedToSend2 = _bath2OffsetCur;

            // 그래프/로그 표시용 "Set(SP+Off)" (관측용 유지)
            double bath1SetTemp = (!double.IsNaN(_bath1OffsetCur)) ? _bath1Setpoint + _bath1OffsetCur : double.NaN;
            double bath2SetTemp = (!double.IsNaN(_bath2OffsetCur)) ? _bath2Setpoint + _bath2OffsetCur : double.NaN;

            _ = stale;

            // 단순화 버전에서는 target은 의미 없으므로 NaN로 기록(컬럼 구조 유지)
            double target1 = double.NaN;
            double target2 = double.NaN;

            return new SampleRow
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

                ReadOk = readOk,
                BoardConnected = _boardConnected,

                Bath1OffsetTarget = target1,
                Bath2OffsetTarget = target2,

                Bath1OffsetApplied = appliedToSend1,
                Bath2OffsetApplied = appliedToSend2,

                Bath1SetTemp = bath1SetTemp,
                Bath2SetTemp = bath2SetTemp
            };
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

        private bool TryReadMultiBoard(out MultiBoardSnapshot snap, out bool stale)
        {
            snap = default;
            stale = false;

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

            if (!_mb.TryReadHoldingRegisters(start: RegReadStart, count: RegReadCount, out ushort[] regs, out string err))
            {
                _boardConnected = false;
                _boardFailCount++;
                return false;
            }

            _boardConnected = true;

            MultiBoardSnapshot parsed = ParseSnapshot(regs);

            // ===== 레지스터 갱신(stale) 판정 =====
            if (_hasLastResp)
            {
                if (parsed.Ch1Response == _lastRespCh1 && parsed.Ch2Response == _lastRespCh2)
                    stale = true;
            }
            _lastRespCh1 = parsed.Ch1Response;
            _lastRespCh2 = parsed.Ch2Response;
            _hasLastResp = true;

            // ===== 필드 단위 lastGood 보정 =====
            parsed = MergeWithLastGood(parsed);

            snap = parsed;
            return true;
        }

        private MultiBoardSnapshot MergeWithLastGood(MultiBoardSnapshot cur)
        {
            bool curHasAny =
                !double.IsNaN(cur.Ch1Pv) ||
                !double.IsNaN(cur.Ch2Pv) ||
                !double.IsNaN(cur.Ch1ExternalThermo) ||
                !double.IsNaN(cur.Ch2ExternalThermo) ||
                !double.IsNaN(cur.Tj);

            if (!_hasLastGoodSnap)
            {
                if (curHasAny)
                {
                    _lastGoodSnap = cur;
                    _hasLastGoodSnap = true;
                }
                return cur;
            }

            if (double.IsNaN(cur.Ch1Pv)) cur.Ch1Pv = _lastGoodSnap.Ch1Pv;
            else _lastGoodSnap.Ch1Pv = cur.Ch1Pv;

            if (double.IsNaN(cur.Ch2Pv)) cur.Ch2Pv = _lastGoodSnap.Ch2Pv;
            else _lastGoodSnap.Ch2Pv = cur.Ch2Pv;

            if (double.IsNaN(cur.Ch1ExternalThermo)) cur.Ch1ExternalThermo = _lastGoodSnap.Ch1ExternalThermo;
            else _lastGoodSnap.Ch1ExternalThermo = cur.Ch1ExternalThermo;

            if (double.IsNaN(cur.Ch2ExternalThermo)) cur.Ch2ExternalThermo = _lastGoodSnap.Ch2ExternalThermo;
            else _lastGoodSnap.Ch2ExternalThermo = cur.Ch2ExternalThermo;

            if (double.IsNaN(cur.Tj)) cur.Tj = _lastGoodSnap.Tj;
            else _lastGoodSnap.Tj = cur.Tj;

            _lastGoodSnap.Ch1Alive = cur.Ch1Alive;
            _lastGoodSnap.Ch1Response = cur.Ch1Response;
            _lastGoodSnap.Ch1Sv = cur.Ch1Sv;
            _lastGoodSnap.Ch1OffsetCur = cur.Ch1OffsetCur;

            _lastGoodSnap.Ch2Alive = cur.Ch2Alive;
            _lastGoodSnap.Ch2Response = cur.Ch2Response;
            _lastGoodSnap.Ch2Sv = cur.Ch2Sv;
            _lastGoodSnap.Ch2OffsetCur = cur.Ch2OffsetCur;

            return cur;
        }

        private static bool IsPlausibleTemp(double v, double min, double max)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return false;
            return v >= min && v <= max;
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

            double ch1Pv = ch1PvRaw / 10.0;
            double ch2Pv = ch2PvRaw / 10.0;

            double ch1Ext = ch1ExtRaw / 1000.0;
            double ch2Ext = ch2ExtRaw / 1000.0;
            double tj = tjRaw / 1000.0;

            if (!IsPlausibleTemp(ch1Pv, 5, 80)) ch1Pv = double.NaN;
            if (!IsPlausibleTemp(ch2Pv, 5, 80)) ch2Pv = double.NaN;

            if (!IsPlausibleTemp(ch1Ext, 5, 80)) ch1Ext = double.NaN;
            if (!IsPlausibleTemp(ch2Ext, 5, 80)) ch2Ext = double.NaN;

            if (!IsPlausibleTemp(tj, 5, 80)) tj = double.NaN;

            return new MultiBoardSnapshot
            {
                Ch1Alive = r[0],
                Ch1Response = r[1],
                Ch1Pv = ch1Pv,
                Ch1Sv = ch1SvRaw / 10.0,
                Ch1OffsetCur = ch1OffRaw / 10.0,
                Ch1ExternalThermo = ch1Ext,

                Ch2Alive = r[7],
                Ch2Response = r[8],
                Ch2Pv = ch2Pv,
                Ch2Sv = ch2SvRaw / 10.0,
                Ch2OffsetCur = ch2OffRaw / 10.0,
                Ch2ExternalThermo = ch2Ext,

                Tj = tj
            };
        }

        private bool TryWriteChannelOffset(int channel, double appliedOffset, string reason = "UNSPECIFIED")
        {
            if (!_mb.IsConnected)
            {
                _boardConnected = TryConnectWithCooldown();
                if (!_boardConnected) _boardFailCount++;
            }

            if (!_mb.IsConnected)
            {
                _boardConnected = false;
                TraceModbus($"OFFSET WRITE SKIP ch={channel} reason={reason} not_connected");
                return false;
            }

            appliedOffset = Clamp(appliedOffset, OffsetClampMin, OffsetClampMax);

            short raw10 = (short)Math.Round(appliedOffset * 10.0, MidpointRounding.AwayFromZero);
            ushort offsetWord = unchecked((ushort)raw10);

            if (channel == 1)
            {
                ushort cmd = 0;
                cmd |= (1 << 1); // offset write

                ushort svWord = unchecked((ushort)((short)Math.Round(_bath1Setpoint * 10.0, MidpointRounding.AwayFromZero)));

                TraceModbus($"OFFSET WRITE TRY ch=1 reason={reason} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} cmd=0x{cmd:X4} svWord=0x{svWord:X4} offWord=0x{offsetWord:X4}");
                bool ok = TryWriteAndWaitAck(channel: 1, cmd: cmd, svWord: svWord, offsetWord: offsetWord, reason: reason, desiredOffset: appliedOffset, raw10: raw10, out string errWriteAndAck);
                if (ok)
                {
                    _lastWrittenOffsetCh1 = appliedOffset;
                }
                else
                {
                    TraceModbus($"OFFSET WRITE FAIL ch=1 reason={reason} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} err={errWriteAndAck}");
                }

                return ok;
            }

            if (channel == 2)
            {
                ushort cmd = 0;
                cmd |= (1 << 1); // offset write

                ushort svWord = unchecked((ushort)((short)Math.Round(_bath2Setpoint * 10.0, MidpointRounding.AwayFromZero)));

                TraceModbus($"OFFSET WRITE TRY ch=2 reason={reason} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} cmd=0x{cmd:X4} svWord=0x{svWord:X4} offWord=0x{offsetWord:X4}");
                bool ok = TryWriteAndWaitAck(channel: 2, cmd: cmd, svWord: svWord, offsetWord: offsetWord, reason: reason, desiredOffset: appliedOffset, raw10: raw10, out string errWriteAndAck);
                if (ok)
                {
                    _lastWrittenOffsetCh2 = appliedOffset;
                }
                else
                {
                    TraceModbus($"OFFSET WRITE FAIL ch=2 reason={reason} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} err={errWriteAndAck}");
                }

                return ok;
            }

            return false;
        }

        private bool TryWriteAndWaitAck(int channel, ushort cmd, ushort svWord, ushort offsetWord, string reason, double desiredOffset, short raw10, out string error)
        {
            error = string.Empty;

            ushort start = channel == 1 ? RegCh1Command : RegCh2Command;
            ushort[] payload = new ushort[] { cmd, svWord, offsetWord };

            if (!_mb.TryWriteMultipleRegisters(start, payload, out string errWrite))
            {
                error = $"WRITE FAIL: {errWrite}";
                TraceModbus($"OFFSET WRITE FAIL ch={channel} reason={reason} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} start={start} err={errWrite}"); return false;
            }

            if (cmd == 0)
                return true;

            int ackMask = cmd & 0x0003;
            if (ackMask == 0)
                return true;

            Thread.Sleep(AckInitialDelayMs);

            DateTime startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < AckTimeoutMs)
            {
                if (TryReadResponseRegister(channel, out ushort resp, out string errResp))
                {
                    if ((resp & ackMask) == ackMask)
                    {
                        TraceModbus($"OFFSET WRITE OK ch={channel} reason={reason} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} ackResp=0x{resp:X4} ackMask=0x{ackMask:X4}"); return true;
                    }
                }
                else
                {
                    TraceModbus($"ACK READ FAIL ch={channel} reason={reason} err={errResp}");
                }

                Thread.Sleep(AckPollIntervalMs);
            }

            error = $"ACK TIMEOUT ch={channel} reason={reason} mask=0x{ackMask:X4}"; TraceModbus(error);
            return false;
        }

        private bool TryReadResponseRegister(int channel, out ushort response, out string error)
        {
            response = 0;
            error = string.Empty;

            ushort start = channel == 1 ? RegCh1Response : RegCh2Response;
            if (!_mb.TryReadHoldingRegisters(start, 1, out ushort[] regs, out string err))
            {
                error = err;
                return false;
            }

            response = regs.Length > 0 ? regs[0] : (ushort)0;
            return true;
        }

        private void LogOffsetReadAndMismatch(int channel, double readOffset, ushort response)
        {
            double lastWritten = channel == 1 ? _lastWrittenOffsetCh1 : _lastWrittenOffsetCh2;

            string readText = readOffset.ToString("0.0", CultureInfo.InvariantCulture);
            if (double.IsNaN(lastWritten))
            {
                TraceModbus($"OFFSET READ ch={channel} read={readText} ackResp=0x{response:X4} lastWritten=NaN");
                return;
            }

            double diff = Math.Abs(readOffset - lastWritten);
            if (diff > OffsetReadbackMismatchEpsilon)
            {
                TraceModbus($"OFFSET READ MISMATCH ch={channel} read={readText} lastWritten={lastWritten.ToString("0.0", CultureInfo.InvariantCulture)} diff={diff.ToString("0.000", CultureInfo.InvariantCulture)} ackResp=0x{response:X4}");
            }
            else
            {
                TraceModbus($"OFFSET READ ch={channel} read={readText} lastWritten={lastWritten.ToString("0.0", CultureInfo.InvariantCulture)} ackResp=0x{response:X4}");
            }
        }

        private void EnforceLastWrittenOffsetIfNeeded(int channel, DateTime now, double readOffset, ushort response)
        {
            double lastWritten = channel == 1 ? _lastWrittenOffsetCh1 : _lastWrittenOffsetCh2;
            if (double.IsNaN(lastWritten))
            {
                TraceModbus($"OFFSET ENFORCE SKIP ch={channel} reason=no_last_written");
                return;
            }

            double diff = Math.Abs(readOffset - lastWritten);
            if (diff <= OffsetReadbackMismatchEpsilon)
            {
                return;
            }

            DateTime lastEnforce = channel == 1 ? _lastEnforceWriteCh1 : _lastEnforceWriteCh2;
            double sinceEnforceSec = (lastEnforce == DateTime.MinValue) ? double.MaxValue : (now - lastEnforce).TotalSeconds;
            if (sinceEnforceSec < EnforceWriteIntervalSeconds)
            {
                TraceModbus($"OFFSET ENFORCE SKIP ch={channel} reason=throttle sinceSec={sinceEnforceSec.ToString("0.000", CultureInfo.InvariantCulture)} read={readOffset.ToString("0.0", CultureInfo.InvariantCulture)} lastWritten={lastWritten.ToString("0.0", CultureInfo.InvariantCulture)} ackResp=0x{response:X4}");
                return;
            }

            TraceModbus($"OFFSET ENFORCE TRY ch={channel} read={readOffset.ToString("0.0", CultureInfo.InvariantCulture)} lastWritten={lastWritten.ToString("0.0", CultureInfo.InvariantCulture)} diff={diff.ToString("0.000", CultureInfo.InvariantCulture)} ackResp=0x{response:X4}");
            bool ok = TryWriteChannelOffset(channel, lastWritten, reason: "HOLD_ENFORCE");
            if (ok)
            {
                if (channel == 1)
                {
                    _lastEnforceWriteCh1 = now;
                    _bath1OffsetCur = lastWritten;
                    _lastWriteCh1 = now;
                }
                else
                {
                    _lastEnforceWriteCh2 = now;
                    _bath2OffsetCur = lastWritten;
                    _lastWriteCh2 = now;
                }
            }
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

            if (dataGridView1.Rows.Count > MaxGridRows)
            {
                dataGridView1.Rows.RemoveAt(0);
            }

            _rowAddCount++;
            if (_rowAddCount % _scrollEveryN == 0)
            {
                int last = dataGridView1.Rows.Count - 1;
                if (last >= 0)
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

            DateTime last = _history[_history.Count - 1].Timestamp;
            DateTime minT = last - GraphWindow;

            while (_history.Count > 2 && _history[0].Timestamp < minT)
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

            DateTime lastT = _history[_history.Count - 1].Timestamp;
            DateTime minT = lastT - GraphWindow;

            DateTime firstT = _history[0].Timestamp;
            if (firstT > minT) minT = firstT;

            List<(DateTime t, double v)> pv = channel == 1
                ? _history.Select(h => (h.Timestamp, h.Bath1Pv)).ToList()
                : _history.Select(h => (h.Timestamp, h.Bath2Pv)).ToList();

            List<(DateTime t, double v)> ut = channel == 1
                ? _history.Select(h => (h.Timestamp, h.UtCh1)).ToList()
                : _history.Select(h => (h.Timestamp, h.UtCh2)).ToList();

            List<(DateTime t, double v)> setTemp = channel == 1
                ? _history.Select(h => (h.Timestamp, h.Bath1SetTemp)).ToList()
                : _history.Select(h => (h.Timestamp, h.Bath2SetTemp)).ToList();

            double minY, maxY;

            if (UseFixedGraphScale)
            {
                minY = FixedGraphMinY;
                maxY = FixedGraphMaxY;
            }
            else
            {
                var all = new List<double>();

                all.AddRange(pv.Where(x => x.t >= minT && x.t <= lastT).Select(x => x.v).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));
                all.AddRange(ut.Where(x => x.t >= minT && x.t <= lastT).Select(x => x.v).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));
                all.AddRange(setTemp.Where(x => x.t >= minT && x.t <= lastT).Select(x => x.v).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));

                if (all.Count >= 2)
                {
                    double minV = all.Min();
                    double maxV = all.Max();

                    double span = Math.Max(0.05, maxV - minV);
                    double margin = span * 0.2;

                    minY = minV - margin;
                    maxY = maxV + margin;
                }
                else
                {
                    minY = 24.5;
                    maxY = 25.5;
                }
            }

            using var gridPen = new Pen(Color.LightGray, 1);

            double yStep = 0.01;
            int hLines = (int)Math.Round((maxY - minY) / yStep);

            for (int i = 0; i <= hLines; i++)
            {
                float y = plot.Bottom - (float)(i * (plot.Height / (maxY - minY)) * yStep);

                bool isMajor = (i % 10 == 0);

                using var pen = new Pen(
                    isMajor ? Color.LightGray : Color.Gainsboro,
                    isMajor ? 1.5f : 1.0f
                );

                g.DrawLine(pen, plot.Left, y, plot.Right, y);
            }

            using var axisFont = new Font("Segoe UI", 9, FontStyle.Regular);
            using var axisBrush = new SolidBrush(Color.DimGray);

            for (int i = 0; i <= hLines; i++)
            {
                if (i % 10 != 0) continue;

                double v = minY + i * yStep;
                float y = plot.Bottom - (float)((v - minY) / (maxY - minY) * plot.Height) - 7;

                g.DrawString(
                    v.ToString("0.000", CultureInfo.InvariantCulture),
                    axisFont,
                    axisBrush,
                    new PointF(clientRect.Left + 5, y)
                );
            }

            using var axisPen = new Pen(Color.Gray, 1);
            g.DrawRectangle(axisPen, plot);

            using var xFont = new Font("Segoe UI", 8, FontStyle.Regular);
            using var xBrush = new SolidBrush(Color.DimGray);

            DateTime tick = new DateTime(minT.Year, minT.Month, minT.Day, minT.Hour, minT.Minute, 0);
            if (tick < minT) tick = tick.AddMinutes(1);

            while (tick <= lastT)
            {
                float x = XFromTime(plot, minT, lastT, tick);
                g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);

                string label = tick.ToString("HH:mm");
                SizeF sz = g.MeasureString(label, xFont);
                g.DrawString(label, xFont, xBrush, x - sz.Width / 2, plot.Bottom + 6);

                tick = tick.AddMinutes(1);
            }

            using var penPv = new Pen(Color.Blue, 2);
            using var penUt = new Pen(Color.Red, 2);
            using var penSet = new Pen(Color.Green, 2);

            DrawSeriesTime(g, plot, pv, minT, lastT, minY, maxY, penPv);
            DrawSeriesTime(g, plot, ut, minT, lastT, minY, maxY, penUt);
            DrawSeriesTime(g, plot, setTemp, minT, lastT, minY, maxY, penSet);

            using var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
            string title = channel == 1
                ? "CH1: PV / ExternalThermo(UT) / Set(SP+Off)"
                : "CH2: PV / ExternalThermo(UT) / Set(SP+Off)";
            g.DrawString(title, titleFont, Brushes.Black, new PointF(clientRect.Left + 10, clientRect.Top + 8));

            DrawLegend(g, clientRect, channel);
        }

        private static float XFromTime(Rectangle rect, DateTime minT, DateTime maxT, DateTime t)
        {
            double spanSec = (maxT - minT).TotalSeconds;
            if (spanSec <= 0.001) return rect.Right;

            double xRatio = (t - minT).TotalSeconds / spanSec;
            if (xRatio < 0) xRatio = 0;
            if (xRatio > 1) xRatio = 1;

            return rect.Left + (float)(xRatio * rect.Width);
        }

        private void DrawLegend(Graphics g, Rectangle clientRect, int channel)
        {
            using var font = new Font("Segoe UI", 9, FontStyle.Bold);
            int x = clientRect.Right - 240;
            int y = clientRect.Top + 10;

            DrawLegendItem(g, x, y, Color.Blue, channel == 1 ? "CH1 PV" : "CH2 PV", font);
            y += 18;

            DrawLegendItem(g, x, y, Color.Red, channel == 1 ? "CH1 UT(ExtThermo)" : "CH2 UT(ExtThermo)", font);
            y += 18;

            DrawLegendItem(g, x, y, Color.Green, channel == 1 ? "CH1 Set(SP+Off)" : "CH2 Set(SP+Off)", font);
        }

        private void DrawLegendItem(Graphics g, int x, int y, Color c, string text, Font font)
        {
            using var pen = new Pen(c, 3);
            g.DrawLine(pen, x, y + 7, x + 26, y + 7);
            g.DrawString(text, font, Brushes.Black, new PointF(x + 32, y));
        }

        private void DrawSeriesTime(Graphics g, Rectangle rect, List<(DateTime t, double v)> values, DateTime minT, DateTime maxT, double minY, double maxY, Pen pen)
        {
            if (values == null || values.Count < 2) return;

            PointF? prev = null;

            for (int i = 0; i < values.Count; i++)
            {
                DateTime t = values[i].t;
                if (t < minT || t > maxT) continue;

                double v = values[i].v;
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    prev = null;
                    continue;
                }

                float x = XFromTime(rect, minT, maxT, t);
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

        private void PrepareTracePath(DateTime now)
        {
            DateTime day = now.Date;
            if (_traceDay == day && !string.IsNullOrWhiteSpace(_tracePath)) return;

            _traceDay = day;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logDir = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logDir);

            string fileName = $"modbus_trace_{day:yyyyMMdd}.log";
            _tracePath = Path.Combine(logDir, fileName);
        }

        private void TraceModbus(string message)
        {
            try
            {
                lock (_traceSync)
                {
                    PrepareTracePath(DateTime.Now);
                    if (string.IsNullOrWhiteSpace(_tracePath)) return;
                    File.AppendAllText(_tracePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch
            {
            }
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
