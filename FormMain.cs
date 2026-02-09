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

        private bool _running;

        private const int MaxPoints = 300;
        private readonly List<SampleRow> _history = new List<SampleRow>(MaxPoints);

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

        // 제어 안정화 상태
        private double _iTerm1 = 0.0;
        private double _iTerm2 = 0.0;

        private double _prevErr1 = double.NaN;
        private double _prevErr2 = double.NaN;

        private DateTime _lastWriteCh1 = DateTime.MinValue;
        private DateTime _lastWriteCh2 = DateTime.MinValue;

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
        private const double FixedGraphMinY = 24.5;
        private const double FixedGraphMaxY = 25.5;

        private const ushort RegReadStart = 0;
        private const ushort RegReadCount = 14;
        private const ushort RegCh1Command = 20;
        private const ushort RegCh2Command = 24;
        private const ushort RegCh1Response = 1;
        private const ushort RegCh2Response = 8;

        private const int AckTimeoutMs = 1500;
        private const int AckPollIntervalMs = 100;
        private const int AckInitialDelayMs = 120;
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

            _iTerm1 = 0.0;
            _iTerm2 = 0.0;
            _prevErr1 = double.NaN;
            _prevErr2 = double.NaN;
            _lastWriteCh1 = DateTime.MinValue;
            _lastWriteCh2 = DateTime.MinValue;

            _hasLastGoodSnap = false;
            _lastGoodSnap = default;

            _hasLastResp = false;
            _lastRespCh1 = 0;
            _lastRespCh2 = 0;
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

        private void WorkerLoop()
        {
            while (_workerRunning)
            {
                DateTime started = DateTime.Now;

                try
                {
                    SampleRow row = LoopOnceCore();

                    // ===== 스킵 기준 적용 =====
                    // 기준: Bath1Pv, Bath2Pv, UtCh1, UtCh2 모두 값이 없거나( NaN ) / 0이면 기록하지 않음
                    if (ShouldSkipRow(row))
                    {
                        // 상태 라벨 정도는 갱신(통신 끊김/복구 표시용)
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
            // 핵심 4개(요청 기준): bath1_pv, bath2_pv, ut_ch1, ut_ch2
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

            double utCh1 = readOk ? snap.Ch1ExternalThermo : double.NaN;
            double utCh2 = readOk ? snap.Ch2ExternalThermo : double.NaN;
            double utTj = readOk ? snap.Tj : double.NaN;

            double bath1Pv = readOk ? snap.Ch1Pv : double.NaN;
            double bath2Pv = readOk ? snap.Ch2Pv : double.NaN;

            if (readOk)
            {
                _bath1OffsetCur = snap.Ch1OffsetCur;
                _bath2OffsetCur = snap.Ch2OffsetCur;
            }

            double err1 = (!double.IsNaN(utCh1) && !double.IsNaN(bath1Pv)) ? (utCh1 - bath1Pv) : double.NaN;
            double err2 = (!double.IsNaN(utCh2) && !double.IsNaN(bath2Pv)) ? (utCh2 - bath2Pv) : double.NaN;

            double derr1 = (!double.IsNaN(err1) && !double.IsNaN(_prevErr1)) ? (err1 - _prevErr1) : double.NaN;
            double derr2 = (!double.IsNaN(err2) && !double.IsNaN(_prevErr2)) ? (err2 - _prevErr2) : double.NaN;

            double err1Ma5 = MovingAverageWithCurrent(_history.Select(h => h.Err1), current: err1, window: 5);
            double err2Ma5 = MovingAverageWithCurrent(_history.Select(h => h.Err2), current: err2, window: 5);

            double err1Std10 = StdDevWithCurrent(_history.Select(h => h.Err1), current: err1, window: 10);
            double err2Std10 = StdDevWithCurrent(_history.Select(h => h.Err2), current: err2, window: 10);

            double lastWriteAgeCh1Sec = (_lastWriteCh1 == DateTime.MinValue) ? double.NaN : (now - _lastWriteCh1).TotalSeconds;
            double lastWriteAgeCh2Sec = (_lastWriteCh2 == DateTime.MinValue) ? double.NaN : (now - _lastWriteCh2).TotalSeconds;

            double target1 = (!double.IsNaN(err1)) ? CalculateOffsetTarget(channel: 1, error: err1) : double.NaN;
            double target2 = (!double.IsNaN(err2)) ? CalculateOffsetTarget(channel: 2, error: err2) : double.NaN;

            double desiredApplied1 = (!double.IsNaN(target1)) ? QuantizeOffset(target1, OffsetStep) : double.NaN;
            double desiredApplied2 = (!double.IsNaN(target2)) ? QuantizeOffset(target2, OffsetStep) : double.NaN;

            double appliedToSend1 = desiredApplied1;
            double appliedToSend2 = desiredApplied2;

            if (readOk && !double.IsNaN(desiredApplied1))
            {
                if (TryApplyOffsetWithPolicy(channel: 1, now: now, err: err1, desiredAppliedOffset: desiredApplied1, ref appliedToSend1))
                {
                    bool w1 = TryWriteChannelOffset(channel: 1, appliedOffset: appliedToSend1);
                    if (w1)
                    {
                        _bath1OffsetCur = appliedToSend1;
                        _lastWriteCh1 = now;
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
            }
            else
            {
                appliedToSend1 = _bath1OffsetCur;
            }

            if (readOk && !double.IsNaN(desiredApplied2))
            {
                if (TryApplyOffsetWithPolicy(channel: 2, now: now, err: err2, desiredAppliedOffset: desiredApplied2, ref appliedToSend2))
                {
                    bool w2 = TryWriteChannelOffset(channel: 2, appliedOffset: appliedToSend2);
                    if (w2)
                    {
                        _bath2OffsetCur = appliedToSend2;
                        _lastWriteCh2 = now;
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
            }
            else
            {
                appliedToSend2 = _bath2OffsetCur;
            }

            double bath1SetTemp = (!double.IsNaN(_bath1OffsetCur)) ? _bath1Setpoint + _bath1OffsetCur : double.NaN;
            double bath2SetTemp = (!double.IsNaN(_bath2OffsetCur)) ? _bath2Setpoint + _bath2OffsetCur : double.NaN;

            // stale 프레임이면(갱신 안 됨) 핵심값이 다 비정상일 때 스킵될 가능성이 큼.
            // 필요하면 SampleRow에 Stale 필드를 추가해도 됨(현재는 로깅에만 영향).
            _ = stale;

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
            // Ch1Response=r[1], Ch2Response=r[8]을 "갱신 카운터/플래그"로 가정.
            // 동일하면 갱신이 안 된 프레임일 수 있음.
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
            // "유효"의 기준은 ParseSnapshot에서 이미 NaN 처리된 값이냐 아니냐로 단순화
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

            // cur 값이 NaN이면 lastGood으로 채우고,
            // cur 값이 유효하면 lastGood을 갱신한다.
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

            // offset/SV/flag 같은 값들은 일단 최신 우선(원하면 이것도 동일 패턴으로 보정 가능)
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

            // 현장 온도 범위에 맞게 조정 가능 (지금은 안전하게 5~80으로 둠)
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

                return TryWriteAndWaitAck(channel: 1, cmd: cmd, svWord: svWord, offsetWord: offsetWord, out _);

            }

            if (channel == 2)
            {
                ushort cmd = 0;
                cmd |= (1 << 1);

                ushort svWord = unchecked((ushort)((short)Math.Round(_bath2Setpoint * 10.0, MidpointRounding.AwayFromZero)));

                return TryWriteAndWaitAck(channel: 2, cmd: cmd, svWord: svWord, offsetWord: offsetWord, out _);
            }

            return false;
        }

        private bool TryWriteAndWaitAck(int channel, ushort cmd, ushort svWord, ushort offsetWord, out string error)
        {
            error = string.Empty;

            ushort start = channel == 1 ? RegCh1Command : RegCh2Command;
            ushort[] payload = new ushort[] { cmd, svWord, offsetWord };

            if (!_mb.TryWriteMultipleRegisters(start, payload, out string errWrite))
            {
                error = $"WRITE FAIL: {errWrite}";
                TraceModbus($"WRITE FAIL ch={channel} start={start} err={errWrite}");
                return false;
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
                        TraceModbus($"ACK OK ch={channel} resp=0x{resp:X4} mask=0x{ackMask:X4}");
                        return true;
                    }
                }
                else
                {
                    TraceModbus($"ACK READ FAIL ch={channel} err={errResp}");
                }

                Thread.Sleep(AckPollIntervalMs);
            }

            error = $"ACK TIMEOUT ch={channel} mask=0x{ackMask:X4}";
            TraceModbus(error);
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

            double minY, maxY;

            if (UseFixedGraphScale)
            {
                minY = FixedGraphMinY;
                maxY = FixedGraphMaxY;
            }
            else
            {
                var all = new List<double>();
                all.AddRange(pv.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));
                all.AddRange(ut.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));
                all.AddRange(setTemp.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));

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
            int hLines = 10;
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
