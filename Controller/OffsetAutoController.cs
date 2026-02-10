using System;

namespace ThermoBathCalibrator.Controller
{
    internal sealed class OffsetAutoController
    {
        private sealed class ChannelState
        {
            public DateTime LastWrite = DateTime.MinValue;
            public DateTime LastAdjust = DateTime.MinValue;

            public bool PwmActive;
            public DateTime PwmPeriodStart = DateTime.MinValue;
            public double PwmDutyLow = 0.5;
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
            s.PwmActive = false;
            s.PwmPeriodStart = DateTime.MinValue;
            s.PwmDutyLow = 0.5;
        }

        private ChannelState GetState(int channel)
        {
            return channel == 1 ? _ch1 : _ch2;
        }

        /// <summary>
        /// 자동 오프셋 계산 + 필요 시 write 수행
        /// </summary>
        public double UpdateAndMaybeWrite(
            int channel,
            DateTime now,
            bool readOk,
            double ut,
            double err,
            double currentOffset,
            Func<int, double, string, bool> tryWriteOffset)
        {
            if (!readOk)
                return currentOffset;

            if (double.IsNaN(ut) || double.IsNaN(err))
                return currentOffset;

            ChannelState st = GetState(channel);
            double absErr = Math.Abs(err);

            // =========================
            // 1) PWM 미세 제어 영역
            // =========================
            currentOffset = RunPwm(
                channel,
                now,
                err,
                absErr,
                currentOffset,
                st,
                tryWriteOffset
            );

            if (st.PwmActive)
                return currentOffset;

            // =========================
            // 2) Coarse step 제어
            // =========================
            (double step, double hold) = GetStepAndHold(absErr);

            if (step <= 0.0)
                return currentOffset;

            if ((now - st.LastAdjust).TotalSeconds < hold)
                return currentOffset;

            bool errPositive = err > 0.0;

            // 기존 FormMain 로직 유지
            // err > 0 : UT 낮음 → offset 감소
            // err < 0 : UT 높음 → offset 증가
            double next = currentOffset + (errPositive ? -step : step);

            next = OffsetMath.Clamp(next, _cfg.OffsetClampMin, _cfg.OffsetClampMax);
            next = OffsetMath.Quantize(next, _cfg.OffsetStep);

            if (Math.Abs(next - currentOffset) < 1e-9)
                return currentOffset;

            if ((now - st.LastWrite).TotalSeconds < _cfg.MinAutoWriteIntervalSeconds)
                return currentOffset;

            if (!tryWriteOffset(channel, next, $"AUTO_STEP_CH{channel}"))
                return currentOffset;

            st.LastWrite = now;
            st.LastAdjust = now;

            return next;
        }

        private double RunPwm(
            int channel,
            DateTime now,
            double err,
            double absErr,
            double currentOffset,
            ChannelState st,
            Func<int, double, string, bool> tryWriteOffset)
        {
            if (!st.PwmActive && absErr <= _cfg.FinePwmEnterErr)
            {
                st.PwmActive = true;
                st.PwmPeriodStart = now;
            }
            else if (st.PwmActive && absErr >= _cfg.FinePwmExitErr)
            {
                st.PwmActive = false;
            }

            if (!st.PwmActive)
                return currentOffset;

            bool errPositive = err > 0.0;

            double neighbor = currentOffset + (errPositive ? -_cfg.OffsetStep : _cfg.OffsetStep);
            neighbor = OffsetMath.Clamp(neighbor, _cfg.OffsetClampMin, _cfg.OffsetClampMax);
            neighbor = OffsetMath.Quantize(neighbor, _cfg.OffsetStep);
            
            if (Math.Abs(neighbor - currentOffset) < 1e-9)
                return currentOffset;

            double low = Math.Min(currentOffset, neighbor);
            double high = Math.Max(currentOffset, neighbor);

            st.PwmDutyLow += _cfg.FinePwmDutyGain * err;
            st.PwmDutyLow = OffsetMath.Clamp(
                st.PwmDutyLow,
                _cfg.FinePwmMinDuty,
                _cfg.FinePwmMaxDuty
            );

            double elapsed = (now - st.PwmPeriodStart).TotalSeconds;
            if (elapsed >= _cfg.FinePwmPeriodSec)
            {
                st.PwmPeriodStart = now;
                elapsed = 0.0;
            }

            double desired =
                elapsed < st.PwmDutyLow * _cfg.FinePwmPeriodSec
                ? low
                : high;

            if ((now - st.LastWrite).TotalSeconds < _cfg.MinAutoWriteIntervalSeconds)
                return currentOffset;

            if (Math.Abs(desired - currentOffset) < 1e-9)
                return currentOffset;

            if (!tryWriteOffset(channel, desired, $"PWM_FINE_CH{channel}"))
                return currentOffset;

            st.LastWrite = now;
            return desired;
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
    }
}
