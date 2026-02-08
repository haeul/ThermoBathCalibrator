using System;
using System.Collections.Generic;
using System.Drawing;
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

        // 연결 상태를 "채널/보드" 기준으로 재해석
        private bool _boardConnected;
        private bool _ch1DataOk;
        private bool _ch2DataOk;

        private int _boardFailCount;
        private int _ch1FailCount;
        private int _ch2FailCount;

        private MultiBoardModbusClient _mb = null!;

        private int _inTick;

        // 기본값: 요청한 IP/Port
        private string _host = "192.168.1.11";
        private int _port = 13000;
        private byte _unitId = 1;

        // ===== CSV Logging (추가) =====
        private readonly object _csvSync = new object();
        private string _csvPath = "";
        private bool _csvHeaderWritten;
        private DateTime _csvDay = DateTime.MinValue;

        // 재연결 과도 시도 방지(최소 변경)
        private long _lastReconnectTick;
        private const int ReconnectCooldownMs = 800;

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

            // ===== CommSettings의 멀티보드 설정을 우선 적용(있으면) =====
            LoadMultiBoardSettingsFromDiskIfAny();

            BuildMultiBoardClient();

            ResetConnectionState();
            UpdateStatusLabels();
            UpdateTopNumbers(double.NaN, double.NaN, double.NaN);

            // CSV 파일 경로 초기화
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
                // 설정 로드 실패 시 기존 기본값 유지
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
        }

        private void BtnComSetting_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new FormComSetting())
                {
                    dlg.ShowDialog(this);
                }

                // 닫힌 뒤 저장된 설정을 다시 읽어서 적용
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

            if (ok1) _bath1OffsetCur = applied;
            if (ok2) _bath2OffsetCur = applied;

            lblOffsetValue.Text = applied.ToString("0.0");

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

            // 외부 온도계 값(채널별) + TJ(외부 온도계 내부 값: reg 13)
            double utCh1 = readOk ? snap.Ch1ExternalThermo : double.NaN;
            double utCh2 = readOk ? snap.Ch2ExternalThermo : double.NaN;
            double utTj = readOk ? snap.Tj : double.NaN; // 맵 수정 반영: 외부 온도계 내부 값 = TJ

            double bath1Pv = readOk ? snap.Ch1Pv : double.NaN;
            double bath2Pv = readOk ? snap.Ch2Pv : double.NaN;

            if (readOk)
            {
                _bath1OffsetCur = snap.Ch1OffsetCur;
                _bath2OffsetCur = snap.Ch2OffsetCur;
            }

            double err1 = (!double.IsNaN(utCh1) && !double.IsNaN(bath1Pv)) ? (utCh1 - bath1Pv) : double.NaN;
            double err2 = (!double.IsNaN(utCh2) && !double.IsNaN(bath2Pv)) ? (utCh2 - bath2Pv) : double.NaN;

            double target1 = (!double.IsNaN(err1)) ? CalculateOffsetTarget(err1) : double.NaN;
            double target2 = (!double.IsNaN(err2)) ? CalculateOffsetTarget(err2) : double.NaN;

            double applied1 = (!double.IsNaN(target1)) ? QuantizeOffset(target1, OffsetStep) : double.NaN;
            double applied2 = (!double.IsNaN(target2)) ? QuantizeOffset(target2, OffsetStep) : double.NaN;

            bool w1 = false;
            bool w2 = false;

            if (!double.IsNaN(applied1) && readOk) w1 = TryWriteChannelOffset(channel: 1, appliedOffset: applied1);
            if (!double.IsNaN(applied2) && readOk) w2 = TryWriteChannelOffset(channel: 2, appliedOffset: applied2);

            if (w1) _bath1OffsetCur = applied1;
            if (w2) _bath2OffsetCur = applied2;

            double bath1SetTemp = (!double.IsNaN(applied1)) ? _bath1Setpoint + applied1 : double.NaN;
            double bath2SetTemp = (!double.IsNaN(applied2)) ? _bath2Setpoint + applied2 : double.NaN;

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

                Bath1OffsetTarget = target1,
                Bath2OffsetTarget = target2,

                Bath1OffsetApplied = applied1,
                Bath2OffsetApplied = applied2,

                Bath1SetTemp = bath1SetTemp,
                Bath2SetTemp = bath2SetTemp
            };

            AppendRowToGrid(row);
            AppendRowToHistory(row);

            // ===== CSV 저장(추가) =====
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

            // 맵 수정 반영: FC03 범위 0~13, count=14 (TJ = reg 13 포함)
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
            // 맵 수정 반영: length >= 14
            if (r == null || r.Length < 14) return default;

            short ch1PvRaw = unchecked((short)r[2]);
            short ch1SvRaw = unchecked((short)r[3]);
            short ch1OffRaw = unchecked((short)r[4]);
            short ch2PvRaw = unchecked((short)r[9]);
            short ch2SvRaw = unchecked((short)r[10]);
            short ch2OffRaw = unchecked((short)r[11]);

            // 채널별 외부 온도계 값: 1/1000℃
            // NOTE: WORD(low)라서 음수 해석 필요 없으므로 ushort -> int
            int ch1ExtRaw = r[5];
            int ch2ExtRaw = r[12];

            // TJ: 외부 온도계 내부 값 (reg 13), 1/1000℃
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
                cmd |= (1 << 1); // bit1: Offset Setting 요청

                ushort svWord = unchecked((ushort)((short)Math.Round(_bath1Setpoint * 10.0, MidpointRounding.AwayFromZero)));

                if (!_mb.TryWriteMultipleRegisters(start: 20, values: new ushort[] { cmd, svWord, offsetWord }, out string err1))
                    return false;

                // cmd clear(기존 그대로)
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

        private double CalculateOffsetTarget(double error)
        {
            if (Math.Abs(error) < 0.05)
                return 0.0;

            double k = 0.8;
            double target = error * k;

            target = Clamp(target, OffsetClampMin, OffsetClampMax);
            return target;
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
            return v.ToString(fmt);
        }

        private void AppendRowToHistory(SampleRow r)
        {
            _history.Add(r);
            if (_history.Count > MaxPoints)
                _history.RemoveAt(0);
        }

        private void UpdateTopNumbers(double ch1, double ch2, double offsetAvg)
        {
            lblCh1Temperature.Text = double.IsNaN(ch1) ? "-" : ch1.ToString("0.000");
            lblCh2Temperature.Text = double.IsNaN(ch2) ? "-" : ch2.ToString("0.000");
            lblOffsetValue.Text = double.IsNaN(offsetAvg) ? "-" : offsetAvg.ToString("0.0");
        }

        private void UpdateStatusLabels()
        {
            lblBath1PortStatus.Text = $"CH1: {(_ch1DataOk ? "OK" : "NG")} (fail={_ch1FailCount})";
            lblBath2PortStatus.Text = $"CH2: {(_ch2DataOk ? "OK" : "NG")} (fail={_ch2FailCount})";
            lblThermoPortStatus.Text = $"BOARD({_host}:{_port}): {(_boardConnected ? "CONNECTED" : "DISCONNECTED")} (fail={_boardFailCount})";

            lblBath1PortStatus.ForeColor = _ch1DataOk ? Color.LimeGreen : Color.OrangeRed;
            lblBath2PortStatus.ForeColor = _ch2DataOk ? Color.LimeGreen : Color.OrangeRed;
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
                g.DrawString(v.ToString("0.000"), axisFont, axisBrush, new PointF(clientRect.Left + 5, y));
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

        // ===== CSV helpers (추가) =====
        private void PrepareCsvPath(DateTime now)
        {
            // 날짜 바뀌면 새 파일로
            DateTime day = now.Date;
            if (_csvDay == day && !string.IsNullOrWhiteSpace(_csvPath)) return;

            _csvDay = day;
            _csvHeaderWritten = false;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data");
            Directory.CreateDirectory(dataDir);

            string fileName = $"thermo_log_{day:yyyyMMdd}.csv";
            _csvPath = Path.Combine(dataDir, fileName);

            // 이미 존재하면 헤더는 있다고 가정(최소 변경)
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

                    var sb = new StringBuilder(256);

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
                // 로깅 실패는 프로그램 흐름을 막지 않음(최소 변경)
            }
        }

        private static string CsvNum(double v, string fmt)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString(fmt);
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

            // TJ (외부 온도계 내부 값) - 1/1000℃
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

            public double Bath1OffsetTarget { get; set; }
            public double Bath2OffsetTarget { get; set; }

            public double Bath1OffsetApplied { get; set; }
            public double Bath2OffsetApplied { get; set; }

            public double Bath1SetTemp { get; set; }
            public double Bath2SetTemp { get; set; }
        }
    }
}
