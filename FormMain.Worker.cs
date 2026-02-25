using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        private void WorkerLoop()
        {
            while (_workerRunning)
            {
                DateTime started = DateTime.Now;

                try
                {
                    SampleRow row = LoopOnceCore();

                    // CSV는 항상 남김 (통신 실패도 기록해야 나중에 원인분석 가능)
                    AppendCsvRow(row);

                    bool skipRow = ShouldSkipRow(row);

                    BeginInvoke(new Action(() =>
                    {
                        if (!skipRow)
                        {
                            AppendRowToGridByMinute(row);
                            AppendRowToHistory(row);
                            UpdateTopNumbers(row.UtCh1, row.UtCh2);
                        }

                        UpdateAlarmState(row.UtCh1, row.UtCh2);
                        UpdateOffsetUiFromState();
                        UpdateStatusLabels();

                        if (!skipRow)
                        {
                            pnlCh1Graph.Invalidate();
                            pnlCh2Graph.Invalidate();
                        }
                    }));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
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

            // UT 보정
            double utCh1 = double.IsNaN(utCh1Raw) ? double.NaN : (utCh1Raw - _utBiasCh1);
            double utCh2 = double.IsNaN(utCh2Raw) ? double.NaN : (utCh2Raw - _utBiasCh2);
            double utTj = readOk ? snap.Tj : double.NaN;

            double bath1Pv = readOk ? snap.Ch1Pv : double.NaN;
            double bath2Pv = readOk ? snap.Ch2Pv : double.NaN;

            EnsureTodayStatsReady(now);
            UpdateTodayStatsIfNeeded(now);

            (double max, double min, double avg) ch1Stats = GetTodayStatsSummaryWithCurrent(_todayStatsCh1, utCh1);
            (double max, double min, double avg) ch2Stats = GetTodayStatsSummaryWithCurrent(_todayStatsCh2, utCh2);

            if (readOk)
            {
                _bath1Setpoint = snap.Ch1Sv;
                _bath2Setpoint = snap.Ch2Sv;

                if (double.IsNaN(_bath1FineTarget)) _bath1FineTarget = _bath1Setpoint;
                if (double.IsNaN(_bath2FineTarget)) _bath2FineTarget = _bath2Setpoint;

                _trackedCoarseSvCh1 = _bath1Setpoint;
                _trackedCoarseSvCh2 = _bath2Setpoint;
            }

            // ===== read-back 기반 cur 갱신(정본) =====
            if (readOk)
            {
                lock (_offsetStateSync)
                {
                    _bath1OffsetCur = snap.Ch1OffsetCur;
                    _bath2OffsetCur = snap.Ch2OffsetCur;
                }
                LogOffsetReadAndMismatch(channel: 1, readOffset: snap.Ch1OffsetCur, response: snap.Ch1Response);
                LogOffsetReadAndMismatch(channel: 2, readOffset: snap.Ch2OffsetCur, response: snap.Ch2Response);
            }
            else
            {
                TraceModbus("OFFSET READ SKIP readOk=false (cannot compare readback)");
            }

            double target1 = !double.IsNaN(_bath1FineTarget) ? _bath1FineTarget : _bath1Setpoint;
            double target2 = !double.IsNaN(_bath2FineTarget) ? _bath2FineTarget : _bath2Setpoint;

            // err = Target - UT
            double err1 = (!double.IsNaN(utCh1)) ? (target1 - utCh1) : double.NaN;
            double err2 = (!double.IsNaN(utCh2)) ? (target2 - utCh2) : double.NaN;

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

            double currentOffset1;
            double currentOffset2;
            lock (_offsetStateSync)
            {
                currentOffset1 = _bath1OffsetCur;
                currentOffset2 = _bath2OffsetCur;
            }

            // ===== 체크박스로 "쓰기/보정" 기능 완전 차단 =====
            bool enableControl = _enableOffsetControl;

            double next1 = currentOffset1;
            double next2 = currentOffset2;

            if (enableControl)
            {
                // FIELD PATCH START
                bool holdCh1 = _manualHoldUntilCh1.HasValue && now < _manualHoldUntilCh1.Value;
                bool holdCh2 = _manualHoldUntilCh2.HasValue && now < _manualHoldUntilCh2.Value;

                // ON: 자동 제어 계산 + 필요 시 write(FC10) 가능
                if (holdCh1)
                {
                    TraceModbus($"AUTO WRITE SKIP ch=1 reason=manual_hold until={_manualHoldUntilCh1.Value:O}");
                    next1 = currentOffset1;
                }
                else
                {
                    next1 = _autoCtrl.UpdateAndMaybeWrite(
                        channel: 1,
                        now: now,
                        readOk: readOk,
                        ut: utCh1,
                        err: err1,
                        currentOffset: currentOffset1,
                        targetTemperature: target1,
                        tryWriteOffset: (ch, off, reason) => TryWriteChannelOffset(ch, off, reason),
                        traceLog: TraceModbus
                    );
                }

                if (holdCh2)
                {
                    TraceModbus($"AUTO WRITE SKIP ch=2 reason=manual_hold until={_manualHoldUntilCh2.Value:O}");
                    next2 = currentOffset2;
                }
                else
                {
                    next2 = _autoCtrl.UpdateAndMaybeWrite(
                        channel: 2,
                        now: now,
                        readOk: readOk,
                        ut: utCh2,
                        err: err2,
                        currentOffset: currentOffset2,
                        targetTemperature: target2,
                        tryWriteOffset: (ch, off, reason) => TryWriteChannelOffset(ch, off, reason),
                        traceLog: TraceModbus
                    );
                }
                // FIELD PATCH END
            }
            else
            {
                // OFF: 모니터링/수집만. 절대 write 호출 금지.
                // next는 "명령값" 의미가 없으니 현재 offset을 그대로 기록.
                TraceModbus("OFFSET CONTROL DISABLED -> monitoring only (no FC10 write)");
                next1 = currentOffset1;
                next2 = currentOffset2;
            }

            // ===== cur는 read-back만 정본 =====
            // (중요) 기존 코드에 있던 아래 블록은 제거:
            // if (Math.Abs(next - current) > ...) { _bathOffsetCur = next; _lastWrite = now; }
            // -> 이건 cur을 요청값(next)으로 오염시키는 동작임.

            double finalOffset1;
            double finalOffset2;
            lock (_offsetStateSync)
            {
                finalOffset1 = _bath1OffsetCur;
                finalOffset2 = _bath2OffsetCur;
            }

            double bath1SetTemp = (!double.IsNaN(finalOffset1)) ? target1 + finalOffset1 : double.NaN;
            double bath2SetTemp = (!double.IsNaN(finalOffset2)) ? target2 + finalOffset2 : double.NaN;

            _ = stale;

            return new SampleRow
            {
                Timestamp = now,

                UtCh1 = utCh1,
                UtCh2 = utCh2,
                UtTj = utTj,

                Max1 = ch1Stats.max,
                Max2 = ch2Stats.max,
                Min1 = ch1Stats.min,
                Min2 = ch2Stats.min,
                Average1 = ch1Stats.avg,
                Average2 = ch2Stats.avg,

                Bath1Pv = bath1Pv,
                Bath2Pv = bath2Pv,

                Err1 = err1,
                Err2 = err2,

                // cur = 장비 read-back(정본)
                Bath1OffsetCur = finalOffset1,
                Bath2OffsetCur = finalOffset2,

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

                Bath1OffsetTarget = double.NaN,
                Bath2OffsetTarget = double.NaN,

                // applied = 이번 tick에서 "계산/명령한 값(next)" (체크 OFF면 current와 동일)
                Bath1OffsetApplied = next1,
                Bath2OffsetApplied = next2,

                Bath1SetTemp = bath1SetTemp,
                Bath2SetTemp = bath2SetTemp,

                DailyMax = MaxOrNaN(ch1Stats.max, ch2Stats.max),
                DailyMin = MinOrNaN(ch1Stats.min, ch2Stats.min),
                DailyAverage = AverageOrNaN(ch1Stats.avg, ch2Stats.avg)
            };
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

        private void AppendRowToGridByMinute(SampleRow r)
        {
            DateTime minute = new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, r.Timestamp.Minute, 0);
            if (_lastGridDisplayedMinute == minute)
                return;

            _lastGridDisplayedMinute = minute;
            AppendRowToGrid(r);
        }

        private void EnsureTodayStatsReady(DateTime now)
        {
            DateTime day = now.Date;
            if (_todayStatsInitialized && _todayStatsDay == day)
                return;

            _todayStatsDay = day;
            _todayStatsCsvLength = 0;
            _todayStatsInitialized = true;
            _todayStatsCh1.Reset();
            _todayStatsCh2.Reset();

            try
            {
                if (string.IsNullOrWhiteSpace(_csvPath) || !System.IO.File.Exists(_csvPath))
                    return;

                using (var fs = new System.IO.FileStream(_csvPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var sr = new System.IO.StreamReader(fs, System.Text.Encoding.UTF8, true))
                {
                    string? line;
                    bool first = true;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (first)
                        {
                            first = false;
                            continue;
                        }

                        TryAccumulateTodayStatsLine(line, day, ref _todayStatsCh1, ref _todayStatsCh2);
                    }

                    _todayStatsCsvLength = fs.Length;
                }
            }
            catch
            {
            }
        }

        private void UpdateTodayStatsIfNeeded(DateTime now)
        {
            DateTime day = now.Date;
            if (_todayStatsDay != day)
                EnsureTodayStatsReady(now);

            try
            {
                if (!string.IsNullOrWhiteSpace(_csvPath) && System.IO.File.Exists(_csvPath))
                {
                    using (var fs = new System.IO.FileStream(_csvPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                    {
                        long len = fs.Length;
                        if (len >= _todayStatsCsvLength)
                        {
                            fs.Seek(_todayStatsCsvLength, System.IO.SeekOrigin.Begin);
                            using (var sr = new System.IO.StreamReader(fs, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true))
                            {
                                string? line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    if (string.IsNullOrWhiteSpace(line)) continue;
                                    TryAccumulateTodayStatsLine(line, day, ref _todayStatsCh1, ref _todayStatsCh2);
                                }
                            }
                            _todayStatsCsvLength = fs.Length;
                        }
                    }
                }
            }
            catch
            {
            }

        }

        private static void TryAccumulateTodayStatsLine(string line, DateTime day, ref DailyChannelStats ch1, ref DailyChannelStats ch2)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("timestamp", StringComparison.OrdinalIgnoreCase))
                return;

            string[] cells = line.Split(',');
            if (cells.Length < 3)
                return;

            if (!DateTime.TryParseExact(cells[0].Trim(), "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime ts))
                return;

            if (ts.Date != day)
                return;

            if (double.TryParse(cells[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ut1))
                ch1.Add(ut1);

            if (double.TryParse(cells[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ut2))
                ch2.Add(ut2);
        }


        private static (double max, double min, double avg) GetTodayStatsSummaryWithCurrent(DailyChannelStats stats, double current)
        {
            DailyChannelStats merged = stats;
            merged.Add(current);
            return GetTodayStatsSummary(merged);
        }

        private static (double max, double min, double avg) GetTodayStatsSummary(DailyChannelStats stats)
        {
            if (stats.Count <= 0)
                return (double.NaN, double.NaN, double.NaN);

            return (stats.Max, stats.Min, stats.Average);
        }
        private static double MaxOrNaN(double a, double b)
        {
            bool aOk = !double.IsNaN(a) && !double.IsInfinity(a);
            bool bOk = !double.IsNaN(b) && !double.IsInfinity(b);

            if (aOk && bOk) return Math.Max(a, b);
            if (aOk) return a;
            if (bOk) return b;
            return double.NaN;
        }

        private static double MinOrNaN(double a, double b)
        {
            bool aOk = !double.IsNaN(a) && !double.IsInfinity(a);
            bool bOk = !double.IsNaN(b) && !double.IsInfinity(b);

            if (aOk && bOk) return Math.Min(a, b);
            if (aOk) return a;
            if (bOk) return b;
            return double.NaN;
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
    }
}