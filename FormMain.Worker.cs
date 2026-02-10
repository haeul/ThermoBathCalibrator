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

                    if (ShouldSkipRow(row))
                    {
                        BeginInvoke(new Action(() =>
                        {
                            UpdateOffsetUiFromState();
                            UpdateStatusLabels();
                        }));
                    }
                    else
                    {
                        BeginInvoke(new Action(() =>
                        {
                            AppendRowToGrid(row);
                            AppendRowToHistory(row);
                            UpdateTopNumbers(row.UtCh1, row.UtCh2);
                            UpdateOffsetUiFromState();
                            UpdateStatusLabels();
                            pnlCh1Graph.Invalidate();
                            pnlCh2Graph.Invalidate();
                        }));
                    }

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

            // err = Setpoint - UT
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
                // ON: 자동 제어 계산 + 필요 시 write(FC10) 가능
                next1 = _autoCtrl.UpdateAndMaybeWrite(
                    channel: 1,
                    now: now,
                    readOk: readOk,
                    ut: utCh1,
                    err: err1,
                    currentOffset: currentOffset1,
                    tryWriteOffset: (ch, off, reason) => TryWriteChannelOffset(ch, off, reason),
                    traceLog: TraceModbus
                );

                next2 = _autoCtrl.UpdateAndMaybeWrite(
                    channel: 2,
                    now: now,
                    readOk: readOk,
                    ut: utCh2,
                    err: err2,
                    currentOffset: currentOffset2,
                    tryWriteOffset: (ch, off, reason) => TryWriteChannelOffset(ch, off, reason),
                    traceLog: TraceModbus
                );
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

            double bath1SetTemp = (!double.IsNaN(finalOffset1)) ? _bath1Setpoint + finalOffset1 : double.NaN;
            double bath2SetTemp = (!double.IsNaN(finalOffset2)) ? _bath2Setpoint + finalOffset2 : double.NaN;

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
                Bath2SetTemp = bath2SetTemp
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
