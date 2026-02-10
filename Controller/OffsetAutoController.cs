using System;

namespace ThermoBathCalibrator.Controller
{
    internal sealed class OffsetAutoController
    {
        private sealed class ChannelState
        {
            public DateTime LastWrite = DateTime.MinValue;
            public DateTime LastAdjust = DateTime.MinValue;

            public double PrevUt = double.NaN;
            public double PrevErr = double.NaN;

            public int WrongTrendCount;
            public double LastDeltaApplied;

            public double Aggressiveness = 1.0;
            public DateTime LargeErrorStart = DateTime.MinValue;

            public bool PwmActive;
            public DateTime PwmPeriodStart = DateTime.MinValue;
            public double PwmDutyHigh = 0.5;
            public double PwmOffsetLow = double.NaN;
            public double PwmOffsetHigh = double.NaN;

            // 발산 방지
            public DateTime StartupAt = DateTime.MinValue;

            public bool HasLastCommanded;
            public double LastCommandedOffset = double.NaN;
            public double LastCommandedAbsErr = double.NaN;

            public int WorsenStreak;
            public DateTime FreezeUntil = DateTime.MinValue;
        }

        private readonly OffsetAutoConfig _cfg;

        private readonly ChannelState _ch1 = new ChannelState();
        private readonly ChannelState _ch2 = new ChannelState();


        public OffsetAutoController(OffsetAutoConfig cfg)
        {
            _cfg = cfg;
        }

        public void Reset()
        {
            ResetChannel(_ch1);
            ResetChannel(_ch2);
        }

        private static void ResetChannel(ChannelState s)
        {
            s.LastWrite = DateTime.MinValue;
            s.LastAdjust = DateTime.MinValue;

            s.PrevUt = double.NaN;
            s.PrevErr = double.NaN;
            s.WrongTrendCount = 0;
            s.LastDeltaApplied = 0.0;

            s.Aggressiveness = 1.0;
            s.LargeErrorStart = DateTime.MinValue;

            s.PwmActive = false;
            s.PwmPeriodStart = DateTime.MinValue;
            s.PwmDutyHigh = 0.5;
            s.PwmOffsetLow = double.NaN;
            s.PwmOffsetHigh = double.NaN;

            // 발산 방지
            s.StartupAt = DateTime.MinValue;

            s.HasLastCommanded = false;
            s.LastCommandedOffset = double.NaN;
            s.LastCommandedAbsErr = double.NaN;

            s.WorsenStreak = 0;
            s.FreezeUntil = DateTime.MinValue;
        }

        private ChannelState GetState(int channel)
        {
            return channel == 1 ? _ch1 : _ch2;
        }

        public double UpdateAndMaybeWrite(
            int channel,
            DateTime now,
            bool readOk,
            double ut,
            double err,
            double currentOffset,
            Func<int, double, string, bool> tryWriteOffset,
            Action<string>? traceLog = null)
        {
            if (!readOk || double.IsNaN(ut) || double.IsNaN(err))
                return currentOffset;

            ChannelState st = GetState(channel);

            if (st.StartupAt == DateTime.MinValue)
                st.StartupAt = now;

            // 1) Startup warmup: no writes
            if ((now - st.StartupAt).TotalSeconds < _cfg.StartupWarmupSeconds)
            {
                st.PrevErr = err;
                st.PrevUt = ut;
                traceLog?.Invoke($"AUTO CTRL CH{channel} safety=WARMUP skipWrite sec={(now - st.StartupAt).TotalSeconds:F1} UT={utFmt(ut)} err={err:F4}");
                return currentOffset;
            }

            // 2) Freeze window: no writes
            if (now < st.FreezeUntil)
            {
                st.PrevErr = err;
                st.PrevUt = ut;
                traceLog?.Invoke($"AUTO CTRL CH{channel} safety=FREEZE skipWrite remain={(st.FreezeUntil - now).TotalSeconds:F1}s UT={utFmt(ut)} err={err:F4}");
                return currentOffset;
            }

            double absErr = Math.Abs(err);

            if (TryHandleWorseningRollback(channel, now, err, currentOffset, st, tryWriteOffset, traceLog))
            {
                st.PrevErr = err;
                st.PrevUt = ut;
                return currentOffset;
            }

            double dErr = (!double.IsNaN(st.PrevErr)) ? (err - st.PrevErr) : double.NaN;
            double dUt = (!double.IsNaN(st.PrevUt)) ? (ut - st.PrevUt) : double.NaN;

            bool overshoot = !double.IsNaN(st.PrevErr)
                && Math.Sign(st.PrevErr) != 0
                && Math.Sign(err) != 0
                && Math.Sign(st.PrevErr) != Math.Sign(err);

            // For this system: err = target - UT.
            // err > 0 means UT is low and we need heating, so offset should go down.
            // err < 0 means UT is high and we need cooling, so offset should go up.
            int desiredDirection = -Math.Sign(err); // +1 => offset up(cool), -1 => offset down(heat)

            UpdateStallAggressiveness(now, absErr, st);
            UpdateWrongTrendGuard(absErr, st);

            if (overshoot)
            {
                st.PwmActive = true;
                st.PwmPeriodStart = now;
                st.Aggressiveness = Math.Max(0.5, st.Aggressiveness * 0.7);
            }

            bool pwmAllowed = absErr <= _cfg.FinePwmEnterErr || (st.PwmActive && absErr <= _cfg.FinePwmExitErr) || overshoot;

            if (pwmAllowed)
            {
                currentOffset = RunPwm(
                    channel,
                    now,
                    err,
                    currentOffset,
                    st,
                    tryWriteOffset,
                    traceLog,
                    absErr,
                    dErr,
                    dUt,
                    overshoot);

                if (st.PwmActive)
                {
                    st.PrevErr = err;
                    st.PrevUt = ut;
                    return currentOffset;
                }
            }

            st.PwmActive = false;

            currentOffset = RunCoarse(
                channel,
                now,
                ut,
                err,
                currentOffset,
                desiredDirection,
                st,
                tryWriteOffset,
                traceLog,
                absErr,
                dErr,
                dUt,
                overshoot);

            st.PrevErr = err;
            st.PrevUt = ut;
            return currentOffset;
        }

        private bool TryHandleWorseningRollback(
    int channel,
    DateTime now,
    double err,
    double currentOffset,
    ChannelState st,
    Func<int, double, string, bool> tryWriteOffset,
    Action<string>? traceLog)
        {
            if (!st.HasLastCommanded)
                return false;

            // 최근에 쓴 뒤에는 관찰 시간 동안만 악화를 체크한다
            double sinceWrite = (st.LastWrite == DateTime.MinValue) ? double.MaxValue : (now - st.LastWrite).TotalSeconds;
            if (sinceWrite > _cfg.PostWriteObserveSeconds)
            {
                st.WorsenStreak = 0;
                return false;
            }

            double absErr = Math.Abs(err);
            if (!double.IsNaN(st.LastCommandedAbsErr) && absErr > st.LastCommandedAbsErr + _cfg.WorsenEpsilon)
                st.WorsenStreak++;
            else
                st.WorsenStreak = 0;

            if (st.WorsenStreak < _cfg.WorsenStreakTrigger)
                return false;

            // 롤백 목표: 마지막 커맨드 offset에서 한 칸(0.1) 반대 방향으로 되돌린다
            double delta = currentOffset - st.LastCommandedOffset;
            double rollback = st.LastCommandedOffset - Math.Sign(delta) * _cfg.OffsetStep;
            rollback = OffsetMath.Clamp(rollback, _cfg.OffsetClampMin, _cfg.OffsetClampMax);
            rollback = OffsetMath.Quantize(rollback, _cfg.OffsetStep);

            // 너무 자주 쓰지 않도록 최소 간격 준수
            if ((now - st.LastWrite).TotalSeconds < _cfg.MinAutoWriteIntervalSeconds)
            {
                traceLog?.Invoke($"AUTO CTRL CH{channel} safety=ROLLBACK armed but rate-limited UT=? err={err:F4} cur={currentOffset:F3}");
                return true;
            }

            bool ok = tryWriteOffset(channel, rollback, $"AUTO_SAFETY_ROLLBACK_CH{channel}");
            traceLog?.Invoke($"AUTO CTRL CH{channel} safety=ROLLBACK worsenStreak={st.WorsenStreak} sinceWrite={sinceWrite:F1}s lastCmd={st.LastCommandedOffset:F3} cur={currentOffset:F3} rollback={rollback:F3} ok={ok}");

            // 롤백이 성공하든 실패하든, 일단 더 건드리면 위험하니까 프리즈
            st.FreezeUntil = now.AddSeconds(_cfg.RollbackFreezeSeconds);
            st.WorsenStreak = 0;

            if (ok)
            {
                st.LastWrite = now;
                st.LastAdjust = now;
                st.LastDeltaApplied = rollback - currentOffset;

                // 롤백도 "마지막 커맨드"로 갱신
                st.LastCommandedOffset = rollback;
                st.LastCommandedAbsErr = absErr;
            }

            return true;
        }

        private double RunCoarse(
            int channel,
            DateTime now,
            double ut,
            double err,
            double currentOffset,
            int desiredDirection,
            ChannelState st,
            Func<int, double, string, bool> tryWriteOffset,
            Action<string>? traceLog,
            double absErr,
            double dErr,
            double dUt,
            bool overshoot)
        {
            (double baseStep, double baseHold) = GetStepAndHold(absErr);
            if (baseStep <= 0.0)
            {
                traceLog?.Invoke($"AUTO CTRL CH{channel} mode=COARSE hold=DEADBAND UT={utFmt(ut)} err={err:F4} dErr={dErrFmt(dErr)} dUT={dUtFmt(dUt)} offset={currentOffset:F3}");
                return currentOffset;
            }

            double effectiveAggressiveness = st.Aggressiveness;
            double step = OffsetMath.Quantize(baseStep * effectiveAggressiveness, _cfg.OffsetStep);
            step = Math.Max(_cfg.OffsetStep, step);
            double hold = Math.Max(_cfg.MinAutoWriteIntervalSeconds, baseHold / Math.Max(1.0, effectiveAggressiveness));

            // Wrong-trend guard: if error keeps worsening, soften and reverse one quantum.
            bool shouldReverse = st.WrongTrendCount >= _cfg.WrongTrendTriggerSamples && Math.Abs(st.LastDeltaApplied) > 1e-9;
            if (shouldReverse)
            {
                double reverse = -Math.Sign(st.LastDeltaApplied) * _cfg.OffsetStep * _cfg.ReverseStepTicksOnWrongTrend;
                double reverseTarget = OffsetMath.Quantize(OffsetMath.Clamp(currentOffset + reverse, _cfg.OffsetClampMin, _cfg.OffsetClampMax), _cfg.OffsetStep);

                if ((now - st.LastWrite).TotalSeconds >= _cfg.MinAutoWriteIntervalSeconds && Math.Abs(reverseTarget - currentOffset) > 1e-9)
                {
                    bool ok = tryWriteOffset(channel, reverseTarget, $"AUTO_GUARD_REVERSE_CH{channel}");
                    traceLog?.Invoke($"AUTO CTRL CH{channel} mode=COARSE guard=WRONG_TREND action=REVERSE UT={utFmt(ut)} err={err:F4} dErr={dErrFmt(dErr)} dUT={dUtFmt(dUt)} next={reverseTarget:F3} ok={ok}");
                    if (ok)
                    {
                        st.HasLastCommanded = true;
                        st.LastCommandedOffset = reverseTarget;
                        st.LastCommandedAbsErr = Math.Abs(err);

                        st.LastWrite = now;
                        st.LastAdjust = now;
                        st.LastDeltaApplied = reverseTarget - currentOffset;
                        st.WrongTrendCount = 0;
                        st.Aggressiveness = Math.Max(0.5, st.Aggressiveness * _cfg.WrongTrendAggressivenessScale);
                        return reverseTarget;
                    }
                }
            }

            if ((now - st.LastAdjust).TotalSeconds < hold)
            {
                traceLog?.Invoke($"AUTO CTRL CH{channel} mode=COARSE action=WAIT_HOLD UT={utFmt(ut)} err={err:F4} dErr={dErrFmt(dErr)} dUT={dUtFmt(dUt)} hold={hold:F1}s");
                return currentOffset;
            }

            double next = currentOffset + (desiredDirection * step);
            next = OffsetMath.Clamp(next, _cfg.OffsetClampMin, _cfg.OffsetClampMax);
            next = OffsetMath.Quantize(next, _cfg.OffsetStep);

            if (Math.Abs(next - currentOffset) < 1e-9)
            {
                traceLog?.Invoke($"AUTO CTRL CH{channel} mode=COARSE action=NO_MOVE_CLAMP UT={utFmt(ut)} err={err:F4} next={next:F3}");
                return currentOffset;
            }

            if ((now - st.LastWrite).TotalSeconds < _cfg.MinAutoWriteIntervalSeconds)
            {
                traceLog?.Invoke($"AUTO CTRL CH{channel} mode=COARSE action=WAIT_RATE_LIMIT UT={utFmt(ut)} err={err:F4} minInt={_cfg.MinAutoWriteIntervalSeconds:F1}s");
                return currentOffset;
            }

            bool writeOk = tryWriteOffset(channel, next, $"AUTO_COARSE_CH{channel}");
            traceLog?.Invoke($"AUTO CTRL CH{channel} mode=COARSE UT={utFmt(ut)} err={err:F4} dErr={dErrFmt(dErr)} dUT={dUtFmt(dUt)} step={step:F2} hold={hold:F1}s aggr={effectiveAggressiveness:F2} overshoot={overshoot} next={next:F3} ok={writeOk}");

            if (!writeOk)
                return currentOffset;

            // 마지막 커맨드 기록(롤백/발산감지용)
            st.HasLastCommanded = true;
            st.LastCommandedOffset = next;
            st.LastCommandedAbsErr = Math.Abs(err);

            st.LastWrite = now;
            st.LastAdjust = now;
            st.LastDeltaApplied = next - currentOffset;
            return next;
        }

        private double RunPwm(
            int channel,
            DateTime now,
            double err,
            double currentOffset,
            ChannelState st,
            Func<int, double, string, bool> tryWriteOffset,
            Action<string>? traceLog,
            double absErr,
            double dErr,
            double dUt,
            bool overshoot)
        {
            st.PwmActive = true;

            int desiredDirection = -Math.Sign(err);
            double neighbor = currentOffset + (desiredDirection * _cfg.OffsetStep);
            neighbor = OffsetMath.Clamp(neighbor, _cfg.OffsetClampMin, _cfg.OffsetClampMax);
            neighbor = OffsetMath.Quantize(neighbor, _cfg.OffsetStep);

            if (Math.Abs(neighbor - currentOffset) < 1e-9)
            {
                st.PwmActive = false;
                traceLog?.Invoke($"AUTO CTRL CH{channel} mode=PWM action=NO_NEIGHBOR UT={utFmt(st.PrevUt)} err={err:F4} offset={currentOffset:F3}");
                return currentOffset;
            }

            st.PwmOffsetLow = Math.Min(currentOffset, neighbor);
            st.PwmOffsetHigh = Math.Max(currentOffset, neighbor);

            double dutyHigh = 0.5 + (-err * _cfg.FinePwmDutyGainPerError);
            dutyHigh = OffsetMath.Clamp(dutyHigh, _cfg.FinePwmMinDutyHigh, _cfg.FinePwmMaxDutyHigh);
            st.PwmDutyHigh = dutyHigh;

            double elapsed = (now - st.PwmPeriodStart).TotalSeconds;
            if (st.PwmPeriodStart == DateTime.MinValue || elapsed >= _cfg.FinePwmPeriodSec)
            {
                st.PwmPeriodStart = now;
                elapsed = 0.0;
            }

            double targetOffset = elapsed < (st.PwmDutyHigh * _cfg.FinePwmPeriodSec)
                ? st.PwmOffsetHigh
                : st.PwmOffsetLow;

            if ((now - st.LastWrite).TotalSeconds < _cfg.MinAutoWriteIntervalSeconds)
            {
                traceLog?.Invoke($"AUTO CTRL CH{channel} mode=PWM action=WAIT_RATE_LIMIT UT={utFmt(st.PrevUt)} err={err:F4} dErr={dErrFmt(dErr)} dutyHigh={st.PwmDutyHigh:F2}");
                return currentOffset;
            }

            if (Math.Abs(targetOffset - currentOffset) < 1e-9)
            {
                traceLog?.Invoke($"AUTO CTRL CH{channel} mode=PWM action=HOLD UT={utFmt(st.PrevUt)} err={err:F4} dErr={dErrFmt(dErr)} dUT={dUtFmt(dUt)} low={st.PwmOffsetLow:F3} high={st.PwmOffsetHigh:F3} dutyHigh={st.PwmDutyHigh:F2} overshoot={overshoot}");
                return currentOffset;
            }

            bool ok = tryWriteOffset(channel, targetOffset, $"AUTO_PWM_CH{channel}");
            traceLog?.Invoke($"AUTO CTRL CH{channel} mode=PWM UT={utFmt(st.PrevUt)} err={err:F4} dErr={dErrFmt(dErr)} dUT={dUtFmt(dUt)} low={st.PwmOffsetLow:F3} high={st.PwmOffsetHigh:F3} dutyHigh={st.PwmDutyHigh:F2} selected={targetOffset:F3} overshoot={overshoot} ok={ok}");

            if (!ok)
                return currentOffset;

            // 마지막 커맨드 기록(롤백/발산감지용)
            st.HasLastCommanded = true;
            st.LastCommandedOffset = targetOffset;
            st.LastCommandedAbsErr = Math.Abs(err);

            st.LastWrite = now;
            st.LastDeltaApplied = targetOffset - currentOffset;
            return targetOffset;
        }

        private void UpdateWrongTrendGuard(double absErr, ChannelState st)
        {
            if (double.IsNaN(st.PrevErr))
            {
                st.WrongTrendCount = 0;
                return;
            }

            double prevAbsErr = Math.Abs(st.PrevErr);
            if (absErr > prevAbsErr + _cfg.ErrorTrendEpsilon)
                st.WrongTrendCount++;
            else
                st.WrongTrendCount = 0;

            if (st.WrongTrendCount >= _cfg.WrongTrendTriggerSamples)
                st.Aggressiveness = Math.Max(0.5, st.Aggressiveness * _cfg.WrongTrendAggressivenessScale);
        }

        private void UpdateStallAggressiveness(DateTime now, double absErr, ChannelState st)
        {
            if (absErr >= _cfg.StallErrorThreshold)
            {
                if (st.LargeErrorStart == DateTime.MinValue)
                    st.LargeErrorStart = now;

                if ((now - st.LargeErrorStart).TotalSeconds >= _cfg.StallTimeSeconds)
                {
                    st.Aggressiveness = Math.Min(
                        _cfg.MaxAggressivenessMultiplier,
                        st.Aggressiveness * _cfg.StallAggressivenessMultiplier);
                    st.LargeErrorStart = now;
                }
            }
            else
            {
                st.LargeErrorStart = DateTime.MinValue;
                st.Aggressiveness = Math.Max(1.0, st.Aggressiveness * 0.9);
            }
        }

        private (double step, double holdSec) GetStepAndHold(double absErr)
        {
            if (absErr >= _cfg.AutoErrStepHigh)
                return (_cfg.AutoStepHigh, _cfg.AutoHoldHighSeconds);

            if (absErr >= _cfg.AutoErrStepMid)
                return (_cfg.AutoStepMid, _cfg.AutoHoldMidSeconds);

            if (absErr >= _cfg.AutoErrStepLow)
                return (_cfg.AutoStepLow, _cfg.AutoHoldLowSeconds);

            if (absErr >= _cfg.AutoErrDeadband)
                return (_cfg.AutoStepFine, _cfg.AutoHoldFineSeconds);

            return (0.0, _cfg.AutoHoldFineSeconds);
        }

        private static string utFmt(double ut)
        {
            return double.IsNaN(ut) ? "NaN" : ut.ToString("F4");
        }

        private static string dErrFmt(double dErr)
        {
            return double.IsNaN(dErr) ? "NaN" : dErr.ToString("F4");
        }

        private static string dUtFmt(double dUt)
        {
            return double.IsNaN(dUt) ? "NaN" : dUt.ToString("F4");
        }
    }
}
