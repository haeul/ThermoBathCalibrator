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

            // UT 보정
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

            double appliedToSend1 = _bath1OffsetCur;
            double appliedToSend2 = _bath2OffsetCur;

            double next1 = _autoCtrl.UpdateAndMaybeWrite(
                channel: 1,
                now: now,
                readOk: readOk,
                ut: utCh1,
                err: err1,
                currentOffset: _bath1OffsetCur,
                tryWriteOffset: (ch, off, reason) => TryWriteChannelOffset(ch, off, reason),
                traceLog: TraceModbus
            );

            if (Math.Abs(next1 - _bath1OffsetCur) > 1e-9)
            {
                _bath1OffsetCur = next1;
                _lastWriteCh1 = now;
            }
            appliedToSend1 = _bath1OffsetCur;

            double next2 = _autoCtrl.UpdateAndMaybeWrite(
                channel: 2,
                now: now,
                readOk: readOk,
                ut: utCh2,
                err: err2,
                currentOffset: _bath2OffsetCur,
                tryWriteOffset: (ch, off, reason) => TryWriteChannelOffset(ch, off, reason),
                traceLog: TraceModbus
            );

            if (Math.Abs(next2 - _bath2OffsetCur) > 1e-9)
            {
                _bath2OffsetCur = next2;
                _lastWriteCh2 = now;
            }
            appliedToSend2 = _bath2OffsetCur;

            double bath1SetTemp = (!double.IsNaN(_bath1OffsetCur)) ? _bath1Setpoint + _bath1OffsetCur : double.NaN;
            double bath2SetTemp = (!double.IsNaN(_bath2OffsetCur)) ? _bath2Setpoint + _bath2OffsetCur : double.NaN;

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

                Bath1OffsetTarget = double.NaN,
                Bath2OffsetTarget = double.NaN,

                Bath1OffsetApplied = appliedToSend1,
                Bath2OffsetApplied = appliedToSend2,

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
