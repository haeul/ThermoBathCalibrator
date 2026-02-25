using System;

namespace ThermoBathCalibrator.Controller
{
    internal sealed class OffsetAutoController
    {
        // Smart Bath Control constants (m°C scale)
        private const int DEADBAND_MILLI = 20;            // ±0.02°C
        private const int SLOPE_THRESHOLD_MILLI = 5;      // 0.005°C per tick (1s)
        private const int MIN_ACTION_INTERVAL_MS = 60000; // 60s
        private const int FOLLOW_UP_THRESHOLD_MILLI = 10; // ±0.01°C

        // Fast converge (only when |err| > 0.10°C)
        private const int FAST_BAND_MILLI = 100;              // 0.10°C
        private const int FAST_MIN_ACTION_INTERVAL_MS = 90000; // 30s (fast mode only)
        private const int FAST_SLOPE_OK_MILLI = 20;           // 0.020°C per tick (1s) is "already moving enough"
        private const int FAST_MAX_BURST_WRITES = 6;          // safety cap per "far away" episode

        private enum TempDirection
        {
            Init = 0,
            Up,
            Down
        }

        private sealed class ChannelState
        {
            // Last commanded offset tracked locally for resend mismatch check
            public double CurrentBathOffset = double.NaN;

            // Previous temperature (m°C) for slope calculation
            public int? PrevTempMilli;

            // Previous action direction for follow-up correction
            public TempDirection PrevAction = TempDirection.Init;

            // Last time we considered an action (enforces MIN_ACTION_INTERVAL_MS)
            public DateTime LastActionAt = DateTime.MinValue;

            // Fast mode state (used only when |err| > 0.10°C)
            public DateTime LastFastActionAt = DateTime.MinValue;
            public int FastBurstWrites = 0;
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

        private static void ResetChannel(ChannelState st)
        {
            st.CurrentBathOffset = double.NaN;
            st.PrevTempMilli = null;
            st.PrevAction = TempDirection.Init;
            st.LastActionAt = DateTime.MinValue;

            st.LastFastActionAt = DateTime.MinValue;
            st.FastBurstWrites = 0;
        }

        // 개선 1) 채널 유효성 검사: 1/2 외 값이면 null 반환
        private ChannelState? GetStateOrNull(int channel)
        {
            if (channel == 1) return _ch1;
            if (channel == 2) return _ch2;
            return null;
        }

        public double UpdateAndMaybeWrite(
            int channel,
            DateTime now,
            bool readOk,
            double ut,
            double err,
            double currentOffset,
            double targetTemperature,
            Func<int, double, string, bool> tryWriteOffset,
            Action<string>? traceLog = null)
        {
            if (!readOk || double.IsNaN(ut) || double.IsNaN(err))
                return currentOffset;

            ChannelState? st = GetStateOrNull(channel);
            if (st == null)
            {
                traceLog?.Invoke($"AUTO SMART invalid channel={channel} -> skip");
                return currentOffset;
            }

            EnsureLocalOffsetInitialized(st, currentOffset);

            int currentTempMilli = ToMilli(ut);
            int targetTempMilli = ToMilli(targetTemperature);

            // 개선 2) offset 비교 허용오차 완화: OffsetStep의 절반을 기준으로 비교
            // (double 오차로 인한 불필요 mismatch resend 방지)
            if (!AreSameOffset(st.CurrentBathOffset, currentOffset, _cfg.OffsetStep))
            {
                bool resent = TryWriteAndConfirm(
                    channel,
                    st,
                    st.CurrentBathOffset,
                    "AUTO_RESEND_MISMATCH",
                    tryWriteOffset,
                    traceLog);

                return resent ? st.CurrentBathOffset : currentOffset;
            }

            // Follow-up correction based on previous action (+/-0.01°C crossing)
            if (st.PrevAction == TempDirection.Up && currentTempMilli > targetTempMilli + FOLLOW_UP_THRESHOLD_MILLI)
            {
                double next = QuantizeClamp(currentOffset + _cfg.OffsetStep);
                st.PrevAction = TempDirection.Init;

                bool ok = TryWriteAndConfirm(
                    channel,
                    st,
                    next,
                    "AUTO_UP_OVERSHOOT",
                    tryWriteOffset,
                    traceLog);

                return ok ? next : currentOffset;
            }

            if (st.PrevAction == TempDirection.Down && currentTempMilli < targetTempMilli - FOLLOW_UP_THRESHOLD_MILLI)
            {
                double next = QuantizeClamp(currentOffset - _cfg.OffsetStep);
                st.PrevAction = TempDirection.Init;

                bool ok = TryWriteAndConfirm(
                    channel,
                    st,
                    next,
                    "AUTO_DOWN_OVERSHOOT",
                    tryWriteOffset,
                    traceLog);

                return ok ? next : currentOffset;
            }

            // Initialize prev temp for slope
            if (!st.PrevTempMilli.HasValue)
            {
                st.PrevTempMilli = currentTempMilli;
                return currentOffset;
            }

            // Fast converge path (only when far away: |err| > 0.10°C)
            // 기존 로직은 그대로 두고, 큰 오차일 때만 별도 간격/가드로 빠르게 수렴
            int errorMilliFastCheck = currentTempMilli - targetTempMilli;
            if (Math.Abs(errorMilliFastCheck) >= FAST_BAND_MILLI)
            {
                return FastConvergeWhenFar(
                    channel,
                    now,
                    st,
                    currentTempMilli,
                    targetTempMilli,
                    currentOffset,
                    tryWriteOffset,
                    traceLog);
            }
            else
            {
                // far episode가 끝나면 fast burst 카운트를 리셋
                st.FastBurstWrites = 0;
                st.LastFastActionAt = DateTime.MinValue;
            }

            // Enforce minimum action interval (60s)
            if (st.LastActionAt != DateTime.MinValue &&
                (now - st.LastActionAt).TotalMilliseconds < MIN_ACTION_INTERVAL_MS)
            {
                return currentOffset;
            }

            int slopeMilli = currentTempMilli - st.PrevTempMilli.Value;
            int errorMilli = currentTempMilli - targetTempMilli;

            st.PrevTempMilli = currentTempMilli;

            // Deadband: update time only
            if (Math.Abs(errorMilli) <= DEADBAND_MILLI)
            {
                st.LastActionAt = now;
                st.PrevAction = TempDirection.Init;
                return currentOffset;
            }

            bool isAction = false;
            double targetOffset = currentOffset;

            // Too hot -> offset up (cooling side)
            if (errorMilli > DEADBAND_MILLI)
            {
                st.LastActionAt = now;

                // If already cooling fast enough, skip; otherwise apply offset up
                if (slopeMilli > -SLOPE_THRESHOLD_MILLI)
                {
                    targetOffset = QuantizeClamp(currentOffset + _cfg.OffsetStep);
                    st.PrevAction = TempDirection.Down;
                    isAction = !AreSameOffset(targetOffset, currentOffset, _cfg.OffsetStep);
                }
            }
            // Too cold -> offset down (heating side)
            else if (errorMilli < -DEADBAND_MILLI)
            {
                st.LastActionAt = now;

                // If already heating fast enough, skip; otherwise apply offset down
                if (slopeMilli < SLOPE_THRESHOLD_MILLI)
                {
                    targetOffset = QuantizeClamp(currentOffset - _cfg.OffsetStep);
                    st.PrevAction = TempDirection.Up;
                    isAction = !AreSameOffset(targetOffset, currentOffset, _cfg.OffsetStep);
                }
            }

            if (!isAction)
                return currentOffset;

            bool writeOk = TryWriteAndConfirm(
                channel,
                st,
                targetOffset,
                "AUTO_SMART_CTRL",
                tryWriteOffset,
                traceLog);

            return writeOk ? targetOffset : currentOffset;
        }

        private double FastConvergeWhenFar(
            int channel,
            DateTime now,
            ChannelState st,
            int currentTempMilli,
            int targetTempMilli,
            double currentOffset,
            Func<int, double, string, bool> tryWriteOffset,
            Action<string>? traceLog)
        {
            int slopeMilli = currentTempMilli - st.PrevTempMilli!.Value;
            int errorMilli = currentTempMilli - targetTempMilli;

            // Fast mode also updates prev temp every tick to keep slope meaningful
            st.PrevTempMilli = currentTempMilli;

            // Safety cap: do not keep pushing forever in fast mode
            if (st.FastBurstWrites >= FAST_MAX_BURST_WRITES)
            {
                traceLog?.Invoke($"AUTO FAST CH{channel} burst cap reached -> hold (writes={st.FastBurstWrites})");
                return currentOffset;
            }

            // Fast mode interval (separate from normal 60s)
            if (st.LastFastActionAt != DateTime.MinValue &&
                (now - st.LastFastActionAt).TotalMilliseconds < FAST_MIN_ACTION_INTERVAL_MS)
            {
                return currentOffset;
            }

            double targetOffset = currentOffset;
            bool wantWrite = false;

            // Too hot (above target) -> cool: offset +step
            if (errorMilli > FAST_BAND_MILLI)
            {
                // If already cooling fast enough, skip (avoid overshoot)
                if (slopeMilli > -FAST_SLOPE_OK_MILLI)
                {
                    targetOffset = QuantizeClamp(currentOffset + _cfg.OffsetStep);
                    wantWrite = !AreSameOffset(targetOffset, currentOffset, _cfg.OffsetStep);
                    st.PrevAction = TempDirection.Down;
                }
            }
            // Too cold (below target) -> heat: offset -step
            else if (errorMilli < -FAST_BAND_MILLI)
            {
                // If already heating fast enough, skip (avoid overshoot)
                if (slopeMilli < FAST_SLOPE_OK_MILLI)
                {
                    targetOffset = QuantizeClamp(currentOffset - _cfg.OffsetStep);
                    wantWrite = !AreSameOffset(targetOffset, currentOffset, _cfg.OffsetStep);
                    st.PrevAction = TempDirection.Up;
                }
            }

            if (!wantWrite)
                return currentOffset;

            bool ok = TryWriteAndConfirm(
                channel,
                st,
                targetOffset,
                "AUTO_FAST_FAR_CONVERGE",
                tryWriteOffset,
                traceLog);

            if (ok)
            {
                st.LastFastActionAt = now;
                st.FastBurstWrites++;

                // 안정성: fast mode에서 write 했으면 normal mode도 잠깐 쉬게 만들어
                // (큰 오차에서 빠르게 붙이고, 근처로 오면 기존 60s 규칙으로 급격한 헌팅 방지)
                st.LastActionAt = now;
            }

            return ok ? targetOffset : currentOffset;
        }

        private void EnsureLocalOffsetInitialized(ChannelState st, double currentOffset)
        {
            if (!double.IsNaN(st.CurrentBathOffset))
                return;

            st.CurrentBathOffset = currentOffset;
            st.PrevTempMilli = null;
            st.PrevAction = TempDirection.Init;
            st.LastActionAt = DateTime.MinValue;

            st.LastFastActionAt = DateTime.MinValue;
            st.FastBurstWrites = 0;
        }

        private bool TryWriteAndConfirm(
            int channel,
            ChannelState st,
            double targetOffset,
            string reason,
            Func<int, double, string, bool> tryWriteOffset,
            Action<string>? traceLog)
        {
            bool ok = tryWriteOffset(channel, targetOffset, reason);
            if (ok)
            {
                st.CurrentBathOffset = targetOffset;
                traceLog?.Invoke($"AUTO SMART CH{channel} write=OK reason={reason} target={targetOffset:F3}");
            }
            else
            {
                traceLog?.Invoke($"AUTO SMART CH{channel} write=FAIL reason={reason} target={targetOffset:F3}");
            }
            return ok;
        }

        private double QuantizeClamp(double offset)
        {
            double clamped = OffsetMath.Clamp(offset, _cfg.OffsetClampMin, _cfg.OffsetClampMax);
            return OffsetMath.Quantize(clamped, _cfg.OffsetStep);
        }

        private static int ToMilli(double tempC)
        {
            return (int)Math.Round(tempC * 1000.0, MidpointRounding.AwayFromZero);
        }

        // 개선 2) 비교 허용오차: OffsetStep의 절반(최소 epsilon은 아주 작게 보장)
        private static bool AreSameOffset(double a, double b, double offsetStep)
        {
            double eps = Math.Max(1e-6, Math.Abs(offsetStep) * 0.5);
            return Math.Abs(a - b) <= eps;
        }
    }
}