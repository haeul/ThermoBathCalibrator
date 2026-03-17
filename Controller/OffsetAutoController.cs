using System;
using System.Collections.Generic;

namespace ThermoBathCalibrator.Controller
{
    internal sealed class OffsetAutoController
    {
        // =========================================================
        // TUNING PARAMETERS
        // 목표:
        // 1) 25.000도에 느리더라도 안정적으로 수렴
        // 2) 수렴 후 ±0.03 이내 유지 최우선
        // 3) 최소 허용도 ±0.05 이내
        // =========================================================

        // 오차 구간 (°C)
        private const double LOCK_ERR_MAX = 0.030;
        private const double NEAR_ERR_MAX = 0.120;
        private const double APPROACH_ERR_MAX = 0.300;

        // Lock pulse 진입 편향 최소치 (현재는 pulse 비활성화 상태지만 유지)
        private const double LOCK_PULSE_BIAS_MIN = 0.005;

        // 기울기 계산 창 (초)
        private const int SLOPE_MAIN_WINDOW_SEC = 30;
        private const int SLOPE_FAST_WINDOW_SEC = 10;
        private const int IMPROVEMENT_WINDOW_SEC = 90;

        // 구간별 목표 기울기 밴드 (°C/min, 목표 방향 기준 양수값)
        // 안정성 우선이라 상한을 더 낮춤
        private const double FAR_SLOPE_MIN = 0.03;
        private const double FAR_SLOPE_MAX = 0.10;

        private const double APPROACH_SLOPE_MIN = 0.01;
        private const double APPROACH_SLOPE_MAX = 0.05;

        private const double NEAR_SLOPE_MIN = 0.000;
        private const double NEAR_SLOPE_MAX = 0.015;

        private const double LOCK_SLOPE_MAX = 0.008;

        // 평평하다고 볼 기준 (°C/min)
        private const double LOCK_PULSE_FLAT_SLOPE_MAX = 0.010;

        // 기울기 과속 판정 여유치 (°C/min)
        private const double SLOPE_OVERSPEED_MARGIN = 0.005;

        // 최근 오차 개선 최소치 (°C)
        private const double MIN_IMPROVEMENT_IN_WINDOW = 0.03;

        // 같은 방향 write 최소 간격 (초)
        // 안정성 우선이라 Approach/Near는 길게 가져감
        private const int FAR_SAME_DIR_INTERVAL_SEC = 60;
        private const int APPROACH_SAME_DIR_INTERVAL_SEC = 90;
        private const int NEAR_SAME_DIR_INTERVAL_SEC = 180;

        // 반대 방향 감속 write 최소 간격 (초)
        // 과속은 빨리 잡도록 더 짧게
        private const int FAR_BRAKE_INTERVAL_SEC = 20;
        private const int APPROACH_BRAKE_INTERVAL_SEC = 20;
        private const int NEAR_BRAKE_INTERVAL_SEC = 15;
        private const int LOCK_BRAKE_INTERVAL_SEC = 15;

        // 같은 방향 연속 write 허용 횟수
        private const int FAR_MAX_SAME_DIR_WRITES = 2;
        private const int APPROACH_MAX_SAME_DIR_WRITES = 1;
        private const int NEAR_MAX_SAME_DIR_WRITES = 0;

        // Lock pulse
        private const int LOCK_PULSE_STABLE_SEC = 60;
        private const int LOCK_PULSE_HOLD_SEC = 15;
        private const int LOCK_PULSE_COOLDOWN_SEC = 120;

        // Pulse 도중 조건이 틀어졌을 때 조기 복귀 기준
        private const double LOCK_PULSE_ABORT_ERR = 0.025;

        // 내부 히스토리 유지 시간
        private const int HISTORY_KEEP_SEC = 300;

        // read-back mismatch 허용 오차
        private const double OFFSET_MATCH_EPS = 0.05;

        // 임시: lock pulse 비활성화
        private const bool ENABLE_LOCK_PULSE = false;

        // =========================================================
        // INTERNAL TYPES
        // =========================================================

        private enum Region
        {
            Lock = 0,
            Near,
            Approach,
            Far
        }

        private enum OffsetDirection
        {
            None = 0,
            Heat, // offset 감소 방향
            Cool  // offset 증가 방향
        }

        private sealed class TempSample
        {
            public DateTime At;
            public double Temp;
        }

        private sealed class ChannelState
        {
            public double CurrentBathOffset = double.NaN;

            public readonly List<TempSample> TempHistory = new List<TempSample>();

            public DateTime LastSameDirectionWriteAt = DateTime.MinValue;
            public DateTime LastBrakeWriteAt = DateTime.MinValue;

            public OffsetDirection LastWriteDirection = OffsetDirection.None;
            public int ConsecutiveSameDirectionWrites = 0;

            public DateTime? LockFlatBiasSince = null;
            public int LockBiasSign = 0;

            public bool LockPulseActive = false;
            public DateTime LockPulseStartedAt = DateTime.MinValue;
            public DateTime LockPulseLastEndedAt = DateTime.MinValue;
            public double LockPulseBaseOffset = double.NaN;
            public OffsetDirection LockPulseDirection = OffsetDirection.None;
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

            st.TempHistory.Clear();

            st.LastSameDirectionWriteAt = DateTime.MinValue;
            st.LastBrakeWriteAt = DateTime.MinValue;

            st.LastWriteDirection = OffsetDirection.None;
            st.ConsecutiveSameDirectionWrites = 0;

            st.LockFlatBiasSince = null;
            st.LockBiasSign = 0;

            st.LockPulseActive = false;
            st.LockPulseStartedAt = DateTime.MinValue;
            st.LockPulseLastEndedAt = DateTime.MinValue;
            st.LockPulseBaseOffset = double.NaN;
            st.LockPulseDirection = OffsetDirection.None;
        }

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
            if (!readOk || double.IsNaN(ut) || double.IsNaN(currentOffset) || double.IsNaN(targetTemperature))
                return currentOffset;

            ChannelState? st = GetStateOrNull(channel);
            if (st == null)
            {
                traceLog?.Invoke($"AUTO invalid channel={channel} -> skip");
                return currentOffset;
            }

            EnsureLocalStateInitialized(st, currentOffset);

            AddTemperatureSample(st, now, ut);

            if (!AreSameOffset(st.CurrentBathOffset, currentOffset, OFFSET_MATCH_EPS))
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

            double localErr = targetTemperature - ut;
            double absErr = Math.Abs(localErr);

            double slope10 = ComputeSlopeCPerMin(st, now, SLOPE_FAST_WINDOW_SEC);
            double slope30 = ComputeSlopeCPerMin(st, now, SLOPE_MAIN_WINDOW_SEC);
            double improvement90 = ComputeImprovement(st, now, targetTemperature, IMPROVEMENT_WINDOW_SEC);

            Region region = GetRegion(absErr);

            traceLog?.Invoke(
                $"AUTO CH{channel} region={region} ut={ut:F3} target={targetTemperature:F3} " +
                $"err={localErr:F3} absErr={absErr:F3} slope10={FormatDouble(slope10)} slope30={FormatDouble(slope30)} " +
                $"impr90={FormatDouble(improvement90)} offset={currentOffset:F3}");

            // =====================================================
            // 1. lock pulse 진행 중이면 일반 제어보다 우선 처리
            // =====================================================
            if (st.LockPulseActive)
            {
                double pulseErr = targetTemperature - ut;
                double pulseAbsErr = Math.Abs(pulseErr);

                bool shouldAbort =
                    pulseAbsErr > LOCK_PULSE_ABORT_ERR ||
                    Math.Sign(pulseErr) != GetDirectionSign(st.LockPulseDirection);

                if (shouldAbort)
                {
                    bool reverted = TryReturnFromLockPulse(
                        channel,
                        now,
                        st,
                        currentOffset,
                        "AUTO_LOCK_PULSE_ABORT_RETURN",
                        tryWriteOffset,
                        traceLog);

                    return reverted ? st.LockPulseBaseOffset : currentOffset;
                }

                if ((now - st.LockPulseStartedAt).TotalSeconds >= LOCK_PULSE_HOLD_SEC)
                {
                    bool reverted = TryReturnFromLockPulse(
                        channel,
                        now,
                        st,
                        currentOffset,
                        "AUTO_LOCK_PULSE_END_RETURN",
                        tryWriteOffset,
                        traceLog);

                    return reverted ? st.LockPulseBaseOffset : currentOffset;
                }

                traceLog?.Invoke($"AUTO CH{channel} lock pulse holding dir={st.LockPulseDirection} base={st.LockPulseBaseOffset:F3}");
                return currentOffset;
            }

            // =====================================================
            // 2. Lock 구간
            // =====================================================
            if (region == Region.Lock)
            {
                HandleLockBiasTracking(now, st, localErr, slope30);

                // Lock 구간에서는 유지 최우선, 과속만 브레이크
                if (CanApplyBrakeInLock(localErr, slope30))
                {
                    OffsetDirection brakeDir = GetBrakeDirection(localErr);
                    if (CanBrakeWrite(now, st, Region.Lock))
                    {
                        double next = ApplyDirectionToOffset(currentOffset, brakeDir);
                        if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                        {
                            bool ok = TryWriteAndConfirm(
                                channel,
                                st,
                                next,
                                "AUTO_LOCK_BRAKE",
                                tryWriteOffset,
                                traceLog);

                            if (ok)
                            {
                                st.LastBrakeWriteAt = now;
                                st.LastWriteDirection = brakeDir;
                                st.ConsecutiveSameDirectionWrites = 0;
                                return next;
                            }
                        }
                    }
                }

                if (ENABLE_LOCK_PULSE && ShouldStartLockPulse(now, st, localErr, slope30))
                {
                    OffsetDirection pulseDir = GetTowardTargetDirection(localErr);
                    double baseOffset = currentOffset;
                    double pulseOffset = ApplyDirectionToOffset(baseOffset, pulseDir);

                    if (!AreSameOffset(pulseOffset, baseOffset, OFFSET_MATCH_EPS))
                    {
                        bool ok = TryWriteAndConfirm(
                            channel,
                            st,
                            pulseOffset,
                            "AUTO_LOCK_PULSE_START",
                            tryWriteOffset,
                            traceLog);

                        if (ok)
                        {
                            st.LockPulseActive = true;
                            st.LockPulseStartedAt = now;
                            st.LockPulseBaseOffset = baseOffset;
                            st.LockPulseDirection = pulseDir;
                            st.LastWriteDirection = pulseDir;
                            st.ConsecutiveSameDirectionWrites = 0;

                            traceLog?.Invoke(
                                $"AUTO CH{channel} lock pulse start dir={pulseDir} base={baseOffset:F3} pulse={pulseOffset:F3}");

                            return pulseOffset;
                        }
                    }
                }

                return currentOffset;
            }

            // Lock이 아니면 pulse bias 추적 상태는 해제
            st.LockFlatBiasSince = null;
            st.LockBiasSign = 0;

            // =====================================================
            // 3. Near / Approach / Far 구간
            // =====================================================
            double signedTowardSlope = GetSignedTowardTargetSlope(localErr, slope30);
            double slopeMin = GetSlopeMin(region);
            double slopeMax = GetSlopeMax(region);

            bool improvingEnough = !double.IsNaN(improvement90) && improvement90 >= MIN_IMPROVEMENT_IN_WINDOW;
            bool stagnating = !double.IsNaN(improvement90) && improvement90 < MIN_IMPROVEMENT_IN_WINDOW;

            bool inTowardBand =
                !double.IsNaN(signedTowardSlope) &&
                signedTowardSlope >= slopeMin &&
                signedTowardSlope <= slopeMax;

            bool overspeed =
                !double.IsNaN(signedTowardSlope) &&
                signedTowardSlope > (slopeMax + SLOPE_OVERSPEED_MARGIN);

            bool insufficientToward =
                double.IsNaN(signedTowardSlope) ||
                signedTowardSlope < slopeMin;

            traceLog?.Invoke(
                $"AUTO CH{channel} region={region} towardSlope={FormatDouble(signedTowardSlope)} " +
                $"band=[{slopeMin:F3},{slopeMax:F3}] improvingEnough={improvingEnough} stagnating={stagnating}");

            // 3-1. 과속이면 반대 방향 감속
            if (overspeed)
            {
                OffsetDirection brakeDir = GetBrakeDirection(localErr);

                if (CanBrakeWrite(now, st, region))
                {
                    double next = ApplyDirectionToOffset(currentOffset, brakeDir);

                    if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                    {
                        bool ok = TryWriteAndConfirm(
                            channel,
                            st,
                            next,
                            $"AUTO_{region}_BRAKE",
                            tryWriteOffset,
                            traceLog);

                        if (ok)
                        {
                            st.LastBrakeWriteAt = now;
                            st.LastWriteDirection = brakeDir;
                            st.ConsecutiveSameDirectionWrites = 0;
                            return next;
                        }
                    }
                }

                return currentOffset;
            }

            // 3-2. Far 구간에서만 정체 상태면 같은 방향으로 한 번 더 밀어줌
            if (region == Region.Far && stagnating)
            {
                OffsetDirection towardDir = GetTowardTargetDirection(localErr);

                if (towardDir != OffsetDirection.None &&
                    CanSameDirectionWrite(now, st, region) &&
                    st.ConsecutiveSameDirectionWrites < GetMaxSameDirectionWrites(region))
                {
                    double next = ApplyDirectionToOffset(currentOffset, towardDir);

                    if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                    {
                        bool ok = TryWriteAndConfirm(
                            channel,
                            st,
                            next,
                            "AUTO_FAR_STAGNATION_PUSH",
                            tryWriteOffset,
                            traceLog);

                        if (ok)
                        {
                            UpdateSameDirectionWriteState(now, st, towardDir);
                            return next;
                        }
                    }
                }
            }

            // 3-3. 목표 방향으로 충분히 움직이고 있고 개선도도 좋으면 유지
            if (inTowardBand && improvingEnough)
                return currentOffset;

            // 3-4. Near 구간은 유지/브레이크 우선, same-direction push는 금지
            if (region == Region.Near)
            {
                return currentOffset;
            }

            // 3-5. Approach 구간은 same-direction push를 아주 제한적으로만 허용
            if (region == Region.Approach)
            {
                // 안정성 우선: 오차가 0.08 이내로 내려오면 더 이상 same-direction push 안 함
                if (absErr <= 0.080)
                    return currentOffset;

                if (insufficientToward &&
                    CanSameDirectionWrite(now, st, region) &&
                    st.ConsecutiveSameDirectionWrites < GetMaxSameDirectionWrites(region))
                {
                    OffsetDirection towardDir = GetTowardTargetDirection(localErr);
                    double next = ApplyDirectionToOffset(currentOffset, towardDir);

                    if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                    {
                        bool ok = TryWriteAndConfirm(
                            channel,
                            st,
                            next,
                            "AUTO_APPROACH_PUSH",
                            tryWriteOffset,
                            traceLog);

                        if (ok)
                        {
                            UpdateSameDirectionWriteState(now, st, towardDir);
                            return next;
                        }
                    }
                }

                return currentOffset;
            }

            // 3-6. Far 구간 일반 보정
            if (insufficientToward)
            {
                OffsetDirection towardDir = GetTowardTargetDirection(localErr);

                if (CanSameDirectionWrite(now, st, region) &&
                    st.ConsecutiveSameDirectionWrites < GetMaxSameDirectionWrites(region))
                {
                    double next = ApplyDirectionToOffset(currentOffset, towardDir);

                    if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                    {
                        bool ok = TryWriteAndConfirm(
                            channel,
                            st,
                            next,
                            "AUTO_FAR_PUSH",
                            tryWriteOffset,
                            traceLog);

                        if (ok)
                        {
                            UpdateSameDirectionWriteState(now, st, towardDir);
                            return next;
                        }
                    }
                }
            }

            return currentOffset;
        }

        // =========================================================
        // LOCK PULSE
        // =========================================================

        private static void HandleLockBiasTracking(
            DateTime now,
            ChannelState st,
            double err,
            double slope30)
        {
            int sign = Math.Sign(err);
            bool flatEnough = !double.IsNaN(slope30) && Math.Abs(slope30) <= LOCK_PULSE_FLAT_SLOPE_MAX;
            bool biasedEnough = Math.Abs(err) >= LOCK_PULSE_BIAS_MIN && Math.Abs(err) <= LOCK_ERR_MAX;

            if (sign == 0 || !flatEnough || !biasedEnough)
            {
                st.LockFlatBiasSince = null;
                st.LockBiasSign = 0;
                return;
            }

            if (!st.LockFlatBiasSince.HasValue || st.LockBiasSign != sign)
            {
                st.LockFlatBiasSince = now;
                st.LockBiasSign = sign;
            }
        }

        private static bool ShouldStartLockPulse(
            DateTime now,
            ChannelState st,
            double err,
            double slope30)
        {
            if (Math.Abs(err) > LOCK_ERR_MAX)
                return false;

            if (Math.Abs(err) < LOCK_PULSE_BIAS_MIN)
                return false;

            if (double.IsNaN(slope30) || Math.Abs(slope30) > LOCK_PULSE_FLAT_SLOPE_MAX)
                return false;

            if (!st.LockFlatBiasSince.HasValue)
                return false;

            if ((now - st.LockFlatBiasSince.Value).TotalSeconds < LOCK_PULSE_STABLE_SEC)
                return false;

            if (st.LockPulseLastEndedAt != DateTime.MinValue &&
                (now - st.LockPulseLastEndedAt).TotalSeconds < LOCK_PULSE_COOLDOWN_SEC)
                return false;

            return true;
        }

        private bool TryReturnFromLockPulse(
            int channel,
            DateTime now,
            ChannelState st,
            double currentOffset,
            string reason,
            Func<int, double, string, bool> tryWriteOffset,
            Action<string>? traceLog)
        {
            if (double.IsNaN(st.LockPulseBaseOffset))
            {
                st.LockPulseActive = false;
                st.LockPulseDirection = OffsetDirection.None;
                st.LockPulseStartedAt = DateTime.MinValue;
                st.LockPulseLastEndedAt = now;
                return false;
            }

            double baseOffset = QuantizeClamp(st.LockPulseBaseOffset);

            if (AreSameOffset(baseOffset, currentOffset, OFFSET_MATCH_EPS))
            {
                st.CurrentBathOffset = baseOffset;
                st.LockPulseActive = false;
                st.LockPulseDirection = OffsetDirection.None;
                st.LockPulseStartedAt = DateTime.MinValue;
                st.LockPulseLastEndedAt = now;
                return true;
            }

            bool ok = TryWriteAndConfirm(channel, st, baseOffset, reason, tryWriteOffset, traceLog);

            if (ok)
            {
                st.LockPulseActive = false;
                st.LockPulseDirection = OffsetDirection.None;
                st.LockPulseStartedAt = DateTime.MinValue;
                st.LockPulseLastEndedAt = now;
                st.LastWriteDirection = OffsetDirection.None;
                st.ConsecutiveSameDirectionWrites = 0;
            }

            return ok;
        }

        // =========================================================
        // REGION / DECISION
        // =========================================================

        private static Region GetRegion(double absErr)
        {
            if (absErr < LOCK_ERR_MAX) return Region.Lock;
            if (absErr < NEAR_ERR_MAX) return Region.Near;
            if (absErr < APPROACH_ERR_MAX) return Region.Approach;
            return Region.Far;
        }

        private static double GetSlopeMin(Region region)
        {
            switch (region)
            {
                case Region.Far:
                    return FAR_SLOPE_MIN;
                case Region.Approach:
                    return APPROACH_SLOPE_MIN;
                case Region.Near:
                    return NEAR_SLOPE_MIN;
                default:
                    return 0.0;
            }
        }

        private static double GetSlopeMax(Region region)
        {
            switch (region)
            {
                case Region.Far:
                    return FAR_SLOPE_MAX;
                case Region.Approach:
                    return APPROACH_SLOPE_MAX;
                case Region.Near:
                    return NEAR_SLOPE_MAX;
                default:
                    return LOCK_SLOPE_MAX;
            }
        }

        private static int GetSameDirectionIntervalSec(Region region)
        {
            switch (region)
            {
                case Region.Far:
                    return FAR_SAME_DIR_INTERVAL_SEC;
                case Region.Approach:
                    return APPROACH_SAME_DIR_INTERVAL_SEC;
                case Region.Near:
                    return NEAR_SAME_DIR_INTERVAL_SEC;
                default:
                    return int.MaxValue;
            }
        }

        private static int GetBrakeIntervalSec(Region region)
        {
            switch (region)
            {
                case Region.Far:
                    return FAR_BRAKE_INTERVAL_SEC;
                case Region.Approach:
                    return APPROACH_BRAKE_INTERVAL_SEC;
                case Region.Near:
                    return NEAR_BRAKE_INTERVAL_SEC;
                default:
                    return LOCK_BRAKE_INTERVAL_SEC;
            }
        }

        private static int GetMaxSameDirectionWrites(Region region)
        {
            switch (region)
            {
                case Region.Far:
                    return FAR_MAX_SAME_DIR_WRITES;
                case Region.Approach:
                    return APPROACH_MAX_SAME_DIR_WRITES;
                case Region.Near:
                    return NEAR_MAX_SAME_DIR_WRITES;
                default:
                    return 0;
            }
        }

        private static bool CanSameDirectionWrite(DateTime now, ChannelState st, Region region)
        {
            if (st.LastSameDirectionWriteAt == DateTime.MinValue)
                return true;

            return (now - st.LastSameDirectionWriteAt).TotalSeconds >= GetSameDirectionIntervalSec(region);
        }

        private static bool CanBrakeWrite(DateTime now, ChannelState st, Region region)
        {
            if (st.LastBrakeWriteAt == DateTime.MinValue)
                return true;

            return (now - st.LastBrakeWriteAt).TotalSeconds >= GetBrakeIntervalSec(region);
        }

        private static bool CanApplyBrakeInLock(double err, double slope30)
        {
            if (double.IsNaN(slope30))
                return false;

            if (err > 0.0)
            {
                return slope30 > (LOCK_SLOPE_MAX + SLOPE_OVERSPEED_MARGIN);
            }

            if (err < 0.0)
            {
                return slope30 < -(LOCK_SLOPE_MAX + SLOPE_OVERSPEED_MARGIN);
            }

            return false;
        }

        private static OffsetDirection GetTowardTargetDirection(double err)
        {
            if (err > 0.0)
                return OffsetDirection.Heat;

            if (err < 0.0)
                return OffsetDirection.Cool;

            return OffsetDirection.None;
        }

        private static OffsetDirection GetBrakeDirection(double err)
        {
            if (err > 0.0)
                return OffsetDirection.Cool;

            if (err < 0.0)
                return OffsetDirection.Heat;

            return OffsetDirection.None;
        }

        private static int GetDirectionSign(OffsetDirection dir)
        {
            switch (dir)
            {
                case OffsetDirection.Heat:
                    return 1;
                case OffsetDirection.Cool:
                    return -1;
                default:
                    return 0;
            }
        }

        private static double GetSignedTowardTargetSlope(double err, double slopeCPerMin)
        {
            if (double.IsNaN(slopeCPerMin) || err == 0.0)
                return double.NaN;

            return slopeCPerMin * Math.Sign(err);
        }

        private void UpdateSameDirectionWriteState(DateTime now, ChannelState st, OffsetDirection dir)
        {
            if (st.LastWriteDirection == dir)
            {
                st.ConsecutiveSameDirectionWrites++;
            }
            else
            {
                st.ConsecutiveSameDirectionWrites = 1;
            }

            st.LastWriteDirection = dir;
            st.LastSameDirectionWriteAt = now;
        }

        // =========================================================
        // HISTORY / SLOPE / IMPROVEMENT
        // =========================================================

        private static void AddTemperatureSample(ChannelState st, DateTime now, double temp)
        {
            st.TempHistory.Add(new TempSample
            {
                At = now,
                Temp = temp
            });

            DateTime minKeep = now.AddSeconds(-HISTORY_KEEP_SEC);

            while (st.TempHistory.Count > 0 && st.TempHistory[0].At < minKeep)
                st.TempHistory.RemoveAt(0);
        }

        private static double ComputeSlopeCPerMin(ChannelState st, DateTime now, int windowSec)
        {
            if (st.TempHistory.Count < 2)
                return double.NaN;

            DateTime targetAt = now.AddSeconds(-windowSec);

            TempSample? refSample = null;
            for (int i = st.TempHistory.Count - 1; i >= 0; i--)
            {
                if (st.TempHistory[i].At <= targetAt)
                {
                    refSample = st.TempHistory[i];
                    break;
                }
            }

            if (refSample == null)
                return double.NaN;

            TempSample last = st.TempHistory[st.TempHistory.Count - 1];
            double dtMin = (last.At - refSample.At).TotalMinutes;

            if (dtMin <= 0.0)
                return double.NaN;

            return (last.Temp - refSample.Temp) / dtMin;
        }

        private static double ComputeImprovement(ChannelState st, DateTime now, double targetTemperature, int windowSec)
        {
            if (st.TempHistory.Count < 2)
                return double.NaN;

            DateTime targetAt = now.AddSeconds(-windowSec);

            TempSample? refSample = null;
            for (int i = st.TempHistory.Count - 1; i >= 0; i--)
            {
                if (st.TempHistory[i].At <= targetAt)
                {
                    refSample = st.TempHistory[i];
                    break;
                }
            }

            if (refSample == null)
                return double.NaN;

            TempSample last = st.TempHistory[st.TempHistory.Count - 1];

            double prevAbsErr = Math.Abs(targetTemperature - refSample.Temp);
            double nowAbsErr = Math.Abs(targetTemperature - last.Temp);

            return prevAbsErr - nowAbsErr;
        }

        // =========================================================
        // OFFSET WRITE / UTIL
        // =========================================================

        private void EnsureLocalStateInitialized(ChannelState st, double currentOffset)
        {
            if (!double.IsNaN(st.CurrentBathOffset))
                return;

            st.CurrentBathOffset = currentOffset;
            st.TempHistory.Clear();
            st.LastSameDirectionWriteAt = DateTime.MinValue;
            st.LastBrakeWriteAt = DateTime.MinValue;
            st.LastWriteDirection = OffsetDirection.None;
            st.ConsecutiveSameDirectionWrites = 0;
            st.LockFlatBiasSince = null;
            st.LockBiasSign = 0;
            st.LockPulseActive = false;
            st.LockPulseStartedAt = DateTime.MinValue;
            st.LockPulseLastEndedAt = DateTime.MinValue;
            st.LockPulseBaseOffset = currentOffset;
            st.LockPulseDirection = OffsetDirection.None;
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
                traceLog?.Invoke($"AUTO CH{channel} write=OK reason={reason} target={targetOffset:F3}");
            }
            else
            {
                traceLog?.Invoke($"AUTO CH{channel} write=FAIL reason={reason} target={targetOffset:F3}");
            }

            return ok;
        }

        private double ApplyDirectionToOffset(double currentOffset, OffsetDirection dir)
        {
            switch (dir)
            {
                case OffsetDirection.Heat:
                    return QuantizeClamp(currentOffset - _cfg.OffsetStep);

                case OffsetDirection.Cool:
                    return QuantizeClamp(currentOffset + _cfg.OffsetStep);

                default:
                    return QuantizeClamp(currentOffset);
            }
        }

        private double QuantizeClamp(double offset)
        {
            double clamped = OffsetMath.Clamp(offset, _cfg.OffsetClampMin, _cfg.OffsetClampMax);
            return OffsetMath.Quantize(clamped, _cfg.OffsetStep);
        }

        private static bool AreSameOffset(double a, double b, double eps)
        {
            return Math.Abs(a - b) <= eps;
        }

        private static string FormatDouble(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                return "NaN";

            return v.ToString("F4");
        }
    }
}