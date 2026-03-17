using System;
using System.Collections.Generic;

namespace ThermoBathCalibrator.Controller
{
    internal sealed class OffsetAutoController
    {
        // =========================================================
        // TUNING PARAMETERS
        // 현장 조정은 이 구역 숫자만 보면 된다.
        // =========================================================

        // 오차 구간 (°C)
        private const double LOCK_ERR_MAX = 0.010;
        private const double NEAR_ERR_MAX = 0.030;
        private const double APPROACH_ERR_MAX = 0.120;

        // 절대 방치하면 안 되는 오차 기준
        private const double MUST_CORRECT_ERR = 0.030;

        // Near / Approach에서 추가 push 허용 기준
        private const double NEAR_PUSH_ERR_MIN = 0.018;
        private const double APPROACH_PUSH_ERR_MIN = 0.040;

        // 개선량 판단 창 (초)
        private const int IMPROVEMENT_WINDOW_SEC = 60;

        // 기울기 계산 창 (초)
        private const int SLOPE_WINDOW_SEC = 30;

        // 최근 IMPROVEMENT_WINDOW_SEC 동안 이 정도도 개선 안 되면 정체로 판단 (°C)
        private const double MIN_IMPROVEMENT_FAR = 0.020;
        private const double MIN_IMPROVEMENT_APPROACH = 0.012;
        private const double MIN_IMPROVEMENT_NEAR = 0.006;

        // 과속 판단 기준 (°C/min)
        // 목표 쪽으로 이보다 너무 빠르게 가면 brake 고려
        private const double LOCK_OVERSPEED = 0.010;
        private const double NEAR_OVERSPEED = 0.020;
        private const double APPROACH_OVERSPEED = 0.050;
        private const double FAR_OVERSPEED = 0.100;

        // 같은 방향 push 최소 간격 (초)
        private const int FAR_PUSH_INTERVAL_SEC = 30;
        private const int APPROACH_PUSH_INTERVAL_SEC = 45;
        private const int NEAR_PUSH_INTERVAL_SEC = 60;

        // brake 최소 간격 (초)
        private const int LOCK_BRAKE_INTERVAL_SEC = 15;
        private const int NEAR_BRAKE_INTERVAL_SEC = 15;
        private const int APPROACH_BRAKE_INTERVAL_SEC = 20;
        private const int FAR_BRAKE_INTERVAL_SEC = 20;

        // 연속 push 상한
        // 완전 고정 제어용이 아니라 폭주 방지용 안전장치
        private const int FAR_MAX_CONSECUTIVE_PUSH = 4;
        private const int APPROACH_MAX_CONSECUTIVE_PUSH = 3;
        private const int NEAR_MAX_CONSECUTIVE_PUSH = 1;

        // Near에서 목표 근처인데도 안 붙고 정체되면 이 시간 뒤 1회 push 허용
        private const int NEAR_STAGNATION_HOLD_SEC = 45;

        // 내부 히스토리 유지 시간
        private const int HISTORY_KEEP_SEC = 300;

        // read-back mismatch 허용 오차
        private const double OFFSET_MATCH_EPS = 0.05;

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
            Heat, // offset 감소 -> 실제 온도 상승
            Cool  // offset 증가 -> 실제 온도 하강
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

            public DateTime LastPushAt = DateTime.MinValue;
            public DateTime LastBrakeAt = DateTime.MinValue;

            public OffsetDirection LastWriteDirection = OffsetDirection.None;
            public int ConsecutivePushCount = 0;

            public DateTime? NearStagnationSince = null;
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

            st.LastPushAt = DateTime.MinValue;
            st.LastBrakeAt = DateTime.MinValue;

            st.LastWriteDirection = OffsetDirection.None;
            st.ConsecutivePushCount = 0;

            st.NearStagnationSince = null;
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

            // mismatch면 resend하지 말고 readback을 채택
            if (!AreSameOffset(st.CurrentBathOffset, currentOffset, OFFSET_MATCH_EPS))
            {
                traceLog?.Invoke(
                    $"AUTO CH{channel} mismatch local={st.CurrentBathOffset:F3} readback={currentOffset:F3} -> adopt readback");

                st.CurrentBathOffset = currentOffset;
                return currentOffset;
            }

            // err = target - ut
            double localErr = targetTemperature - ut;
            double absErr = Math.Abs(localErr);

            Region region = GetRegion(absErr);

            double slope = ComputeSlopeCPerMin(st, now, SLOPE_WINDOW_SEC);
            double improvement = ComputeImprovement(st, now, targetTemperature, IMPROVEMENT_WINDOW_SEC);
            double towardSlope = GetSignedTowardTargetSlope(localErr, slope);

            traceLog?.Invoke(
                $"AUTO CH{channel} region={region} ut={ut:F3} target={targetTemperature:F3} " +
                $"err={localErr:F3} absErr={absErr:F3} slope={FormatDouble(slope)} " +
                $"towardSlope={FormatDouble(towardSlope)} improvement={FormatDouble(improvement)} offset={currentOffset:F3}");

            // 1. Lock 구간
            if (region == Region.Lock)
            {
                st.NearStagnationSince = null;

                if (IsOverspeed(region, towardSlope) && CanBrakeWrite(now, st, region))
                {
                    OffsetDirection brakeDir = GetBrakeDirection(localErr);
                    double next = ApplyDirectionToOffset(currentOffset, brakeDir);

                    if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                    {
                        bool ok = TryWriteAndConfirm(channel, st, next, "AUTO_LOCK_BRAKE", tryWriteOffset, traceLog);
                        if (ok)
                        {
                            MarkBrake(now, st, brakeDir);
                            return next;
                        }
                    }
                }

                return currentOffset;
            }

            // 2. Lock 밖에서는 기본적으로 0.03 초과면 반드시 수렴 동작이 있어야 함
            bool mustCorrect = absErr > MUST_CORRECT_ERR;

            // 3. 과속이면 brake 우선
            if (IsOverspeed(region, towardSlope) && CanBrakeWrite(now, st, region))
            {
                OffsetDirection brakeDir = GetBrakeDirection(localErr);
                double next = ApplyDirectionToOffset(currentOffset, brakeDir);

                if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                {
                    bool ok = TryWriteAndConfirm(channel, st, next, $"AUTO_{region}_BRAKE", tryWriteOffset, traceLog);
                    if (ok)
                    {
                        MarkBrake(now, st, brakeDir);
                        return next;
                    }
                }

                return currentOffset;
            }

            // 4. Far 구간
            if (region == Region.Far)
            {
                bool stagnating = double.IsNaN(improvement) || improvement < MIN_IMPROVEMENT_FAR;
                bool needPush = mustCorrect && stagnating;

                if (needPush &&
                    CanPushWrite(now, st, region) &&
                    st.ConsecutivePushCount < FAR_MAX_CONSECUTIVE_PUSH)
                {
                    OffsetDirection dir = GetTowardTargetDirection(localErr);
                    double next = ApplyDirectionToOffset(currentOffset, dir);

                    if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                    {
                        bool ok = TryWriteAndConfirm(
                            channel,
                            st,
                            next,
                            stagnating ? "AUTO_FAR_STAGNATION_PUSH" : "AUTO_FAR_PUSH",
                            tryWriteOffset,
                            traceLog);

                        if (ok)
                        {
                            MarkPush(now, st, dir);
                            return next;
                        }
                    }
                }

                return currentOffset;
            }

            // 5. Approach 구간
            if (region == Region.Approach)
            {
                bool stagnating = double.IsNaN(improvement) || improvement < MIN_IMPROVEMENT_APPROACH;
                bool needPush = absErr >= APPROACH_PUSH_ERR_MIN && (mustCorrect || stagnating);

                if (needPush &&
                    CanPushWrite(now, st, region) &&
                    st.ConsecutivePushCount < APPROACH_MAX_CONSECUTIVE_PUSH)
                {
                    OffsetDirection dir = GetTowardTargetDirection(localErr);
                    double next = ApplyDirectionToOffset(currentOffset, dir);

                    if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                    {
                        bool ok = TryWriteAndConfirm(
                            channel,
                            st,
                            next,
                            stagnating ? "AUTO_APPROACH_STAGNATION_PUSH" : "AUTO_APPROACH_PUSH",
                            tryWriteOffset,
                            traceLog);

                        if (ok)
                        {
                            MarkPush(now, st, dir);
                            return next;
                        }
                    }
                }

                return currentOffset;
            }

            // 6. Near 구간
            if (region == Region.Near)
            {
                bool stagnating = double.IsNaN(improvement) || improvement < MIN_IMPROVEMENT_NEAR;

                if (absErr >= NEAR_PUSH_ERR_MIN && stagnating)
                {
                    if (!st.NearStagnationSince.HasValue)
                    {
                        st.NearStagnationSince = now;
                    }
                    else if ((now - st.NearStagnationSince.Value).TotalSeconds >= NEAR_STAGNATION_HOLD_SEC)
                    {
                        if (CanPushWrite(now, st, region) &&
                            st.ConsecutivePushCount < NEAR_MAX_CONSECUTIVE_PUSH)
                        {
                            OffsetDirection dir = GetTowardTargetDirection(localErr);
                            double next = ApplyDirectionToOffset(currentOffset, dir);

                            if (!AreSameOffset(next, currentOffset, OFFSET_MATCH_EPS))
                            {
                                bool ok = TryWriteAndConfirm(
                                    channel,
                                    st,
                                    next,
                                    "AUTO_NEAR_STAGNATION_PUSH",
                                    tryWriteOffset,
                                    traceLog);

                                if (ok)
                                {
                                    MarkPush(now, st, dir);
                                    st.NearStagnationSince = null;
                                    return next;
                                }
                            }
                        }
                    }
                }
                else
                {
                    st.NearStagnationSince = null;
                }

                return currentOffset;
            }

            return currentOffset;
        }

        private static Region GetRegion(double absErr)
        {
            if (absErr <= LOCK_ERR_MAX) return Region.Lock;
            if (absErr <= NEAR_ERR_MAX) return Region.Near;
            if (absErr <= APPROACH_ERR_MAX) return Region.Approach;
            return Region.Far;
        }

        private static bool IsOverspeed(Region region, double towardSlope)
        {
            if (double.IsNaN(towardSlope))
                return false;

            switch (region)
            {
                case Region.Lock:
                    return towardSlope > LOCK_OVERSPEED;
                case Region.Near:
                    return towardSlope > NEAR_OVERSPEED;
                case Region.Approach:
                    return towardSlope > APPROACH_OVERSPEED;
                case Region.Far:
                    return towardSlope > FAR_OVERSPEED;
                default:
                    return false;
            }
        }

        private static int GetPushIntervalSec(Region region)
        {
            switch (region)
            {
                case Region.Far:
                    return FAR_PUSH_INTERVAL_SEC;
                case Region.Approach:
                    return APPROACH_PUSH_INTERVAL_SEC;
                case Region.Near:
                    return NEAR_PUSH_INTERVAL_SEC;
                default:
                    return int.MaxValue;
            }
        }

        private static int GetBrakeIntervalSec(Region region)
        {
            switch (region)
            {
                case Region.Lock:
                    return LOCK_BRAKE_INTERVAL_SEC;
                case Region.Near:
                    return NEAR_BRAKE_INTERVAL_SEC;
                case Region.Approach:
                    return APPROACH_BRAKE_INTERVAL_SEC;
                case Region.Far:
                    return FAR_BRAKE_INTERVAL_SEC;
                default:
                    return int.MaxValue;
            }
        }

        private static bool CanPushWrite(DateTime now, ChannelState st, Region region)
        {
            if (st.LastPushAt == DateTime.MinValue)
                return true;

            return (now - st.LastPushAt).TotalSeconds >= GetPushIntervalSec(region);
        }

        private static bool CanBrakeWrite(DateTime now, ChannelState st, Region region)
        {
            if (st.LastBrakeAt == DateTime.MinValue)
                return true;

            return (now - st.LastBrakeAt).TotalSeconds >= GetBrakeIntervalSec(region);
        }

        // err = target - ut
        private static OffsetDirection GetTowardTargetDirection(double err)
        {
            if (err > 0.0)
                return OffsetDirection.Heat; // offset 감소

            if (err < 0.0)
                return OffsetDirection.Cool; // offset 증가

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

        private static double GetSignedTowardTargetSlope(double err, double slopeCPerMin)
        {
            if (double.IsNaN(slopeCPerMin) || err == 0.0)
                return double.NaN;

            return slopeCPerMin * Math.Sign(err);
        }

        private static void MarkPush(DateTime now, ChannelState st, OffsetDirection dir)
        {
            if (st.LastWriteDirection == dir)
                st.ConsecutivePushCount++;
            else
                st.ConsecutivePushCount = 1;

            st.LastWriteDirection = dir;
            st.LastPushAt = now;
        }

        private static void MarkBrake(DateTime now, ChannelState st, OffsetDirection dir)
        {
            st.LastBrakeAt = now;
            st.LastWriteDirection = dir;
            st.ConsecutivePushCount = 0;
        }

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

        private void EnsureLocalStateInitialized(ChannelState st, double currentOffset)
        {
            if (!double.IsNaN(st.CurrentBathOffset))
                return;

            st.CurrentBathOffset = currentOffset;
            st.TempHistory.Clear();
            st.LastPushAt = DateTime.MinValue;
            st.LastBrakeAt = DateTime.MinValue;
            st.LastWriteDirection = OffsetDirection.None;
            st.ConsecutivePushCount = 0;
            st.NearStagnationSince = null;
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