using System;

namespace ThermoBathCalibrator.Controller
{
    internal sealed class OffsetAutoController
    {
        // =========================================================
        // 목표: 25.000°C 근처에서 "안정적으로 붙도록" 헌팅/오버슈트 최소화
        // 제약: offset 최소 단위는 0.1(step) (장비 한계)
        //
        // 구조(우선순위):
        //  1) 입력 유효성/채널 체크
        //  2) offset mismatch resend (내부 상태 vs 장비 읽힘 불일치)
        //  3) Follow-up(사후 보정) : 목표 통과 후 일정 이상 벗어나면 1회 보정
        //  4) slope 계산
        //  5) Fast converge(|err| >= 0.10°C) : 멀리 있으면 빠르게, 단 안전장치 포함
        //  6) Deadband(|err| <= 0.02°C) : 원칙적으로 손대지 않음(안정 우선)
        //  7) Predictive Brake(사전 감속) : 목표 근처에서 "곧 통과" 예상되면 미리 브레이크
        //  8) Near Pulse(근처 파동 붙이기) : 목표 근처에서 미세한 파동을 목표에 더 붙게 "한 번만" 살짝 보정
        //  9) 일반 모드(최소 간격 + slope guard)
        // =========================================================

        // =========================
        // 기본 제어 상수 (m°C 단위)
        // =========================
        private const int DEADBAND_MILLI = 20;              // ±0.02°C
        private const int SLOPE_THRESHOLD_MILLI = 5;        // 0.005°C/s (이미 충분히 움직이면 추가 조치 스킵)
        private const int MIN_ACTION_INTERVAL_MS = 30000;   // 일반 모드 최소 조치 간격(대략 50s)

        // Follow-up(사후 보정): 목표 통과 후 이 정도 벗어나면 1회 보정
        private const int FOLLOW_UP_THRESHOLD_MILLI = 5;   // ±0.005°C

        // =========================
        // Fast converge (|err| >= 0.10°C) 전용
        // =========================
        private const int FAST_BAND_MILLI = 100;               // 0.10°C
        private const int FAST_MIN_ACTION_INTERVAL_MS = 90000; // fast 모드 최소 간격(90s)
        private const int FAST_SLOPE_OK_MILLI = 20;            // 0.020°C/s 이상으로 이미 움직이면 스킵
        private const int FAST_MAX_BURST_WRITES = 6;           // fast 모드 안전 상한(연속 write 제한)

        // =========================
        // Predictive Brake (사전 감속)
        // =========================
        // 목표 근처에서 slope 때문에 곧 통과할 것 같으면, 오차 부호 바뀌기 전에 미리 반대성격 조치를 넣어 과속을 줄임
        private const int PB_ARM_ERR_MILLI = 100;          // ±0.1°C 이내에서만 브레이크 활성
        private const int PB_MIN_SLOPE_MILLI = 1;         // 0.001°C/s 미만 slope는 노이즈로 보고 무시
        private const int PB_HORIZON_SEC = 45;            // 45초 뒤 예측
        private const int PB_COOLDOWN_MS = 10000;         // Predictive Brake 연타 방지(10s)

        // =========================
        // Near Pulse (목표 근처 파동을 더 붙이기)
        // =========================
        // deadband 밖이지만 목표 근처(예: 0.02~0.04 정도)에서,
        // slope 방향으로 "곧 더 멀어질" 기미가 보이면 0.1 step을 1회만 살짝 넣어 파동 중심을 25.000에 붙임
        private const int NP_ARM_ERR_MILLI = 30;           // ±0.03°C 이내에서만 Near Pulse 허용
        private const int NP_MIN_ERR_MILLI = DEADBAND_MILLI; // deadband(0.02) 밖에서만(즉 0.02 < |err| <= 0.04)
        private const int NP_MIN_SLOPE_MILLI = 1;          // 0.001°C/s 이상일 때만 의미 있다고 판단
        private const int NP_COOLDOWN_MS = 30000;          // Near Pulse 연타 방지(30s)

        private enum TempDirection
        {
            Init = 0,
            Up,    // 가열 방향으로 조치했던 상태(논리 플래그)
            Down   // 냉각 방향으로 조치했던 상태(논리 플래그)
        }

        private sealed class ChannelState
        {
            // 내부적으로 "내가 마지막으로 썼다고 믿는 offset"
            public double CurrentBathOffset = double.NaN;

            // slope 계산용(이전 온도)
            public int? PrevTempMilli;

            // Follow-up 보정용(이전에 어떤 방향 조치를 했는지)
            public TempDirection PrevAction = TempDirection.Init;

            // 일반 모드 최소 간격 제어용
            public DateTime LastActionAt = DateTime.MinValue;

            // fast 모드 상태
            public DateTime LastFastActionAt = DateTime.MinValue;
            public int FastBurstWrites = 0;

            // Predictive Brake 쿨다운
            public DateTime LastPredictiveBrakeAt = DateTime.MinValue;

            // Near Pulse 쿨다운
            public DateTime LastNearPulseAt = DateTime.MinValue;
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

            st.LastPredictiveBrakeAt = DateTime.MinValue;
            st.LastNearPulseAt = DateTime.MinValue;
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
            // 1) 입력 유효성
            if (!readOk || double.IsNaN(ut) || double.IsNaN(err))
                return currentOffset;

            ChannelState? st = GetStateOrNull(channel);
            if (st == null)
            {
                traceLog?.Invoke($"AUTO invalid channel={channel} -> skip");
                return currentOffset;
            }

            // 2) 최초 진입시 내부 offset 동기화
            EnsureLocalOffsetInitialized(st, currentOffset);

            int currentTempMilli = ToMilli(ut);
            int targetTempMilli = ToMilli(targetTemperature);

            // 3) offset mismatch resend
            // 내부가 믿는 offset과 장비에서 읽힌 offset이 다르면 내부값으로 재전송해 동기화 시도
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

            // 4) Follow-up correction(사후 보정)
            // 목표를 통과한 뒤(오버슈트/언더슈트) ±0.01°C 이상 벗어나면 1번만 보정
            if (st.PrevAction == TempDirection.Up && currentTempMilli > targetTempMilli + FOLLOW_UP_THRESHOLD_MILLI)
            {
                // Up 상태에서 목표 위로 벗어남 -> 냉각 성격 보정( offset +step )
                double next = QuantizeClamp(currentOffset + _cfg.OffsetStep);
                st.PrevAction = TempDirection.Init;

                bool ok = TryWriteAndConfirm(channel, st, next, "AUTO_UP_OVERSHOOT", tryWriteOffset, traceLog);
                return ok ? next : currentOffset;
            }

            if (st.PrevAction == TempDirection.Down && currentTempMilli < targetTempMilli - FOLLOW_UP_THRESHOLD_MILLI)
            {
                // Down 상태에서 목표 아래로 벗어남 -> 가열 성격 보정( offset -step )
                double next = QuantizeClamp(currentOffset - _cfg.OffsetStep);
                st.PrevAction = TempDirection.Init;

                bool ok = TryWriteAndConfirm(channel, st, next, "AUTO_DOWN_OVERSHOOT", tryWriteOffset, traceLog);
                return ok ? next : currentOffset;
            }

            // 5) prev temp 초기화(첫 tick은 slope 계산 불가)
            if (!st.PrevTempMilli.HasValue)
            {
                st.PrevTempMilli = currentTempMilli;
                return currentOffset;
            }

            // slope(1초당 온도 변화, m°C)
            int slopeMilli = currentTempMilli - st.PrevTempMilli.Value;

            // error 정의(현재 - 목표). 현재가 목표보다 높으면 +
            int errorMilli = currentTempMilli - targetTempMilli;

            // prev 갱신
            st.PrevTempMilli = currentTempMilli;

            // =========================================
            // (A) Fast converge (|err| >= 0.10°C)
            // =========================================
            if (Math.Abs(errorMilli) >= FAST_BAND_MILLI)
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
                // far episode 종료 -> fast 상태 리셋
                st.FastBurstWrites = 0;
                st.LastFastActionAt = DateTime.MinValue;
            }

            // =========================================
            // (B) Deadband(±0.02°C): 안정성 최우선 -> 기본적으로 손대지 않음
            // =========================================
            if (Math.Abs(errorMilli) <= DEADBAND_MILLI)
            {
                st.PrevAction = TempDirection.Init;
                return currentOffset;
            }

            // =========================================
            // (C) Predictive Brake: 목표 근처에서 "곧 통과"하면 미리 감속
            // =========================================
            if (ShouldApplyPredictiveBrake(now, st, errorMilli, slopeMilli))
            {
                // 단순 선형 예측(15초 뒤)
                int predictedMilli = currentTempMilli + (slopeMilli * PB_HORIZON_SEC);

                // 현재는 목표보다 낮음(error<0), 상승 중(slope>0), 예측상 목표를 넘어갈 것 같으면 -> 냉각 브레이크( +step )
                if (errorMilli < 0 && slopeMilli > 0 && predictedMilli >= targetTempMilli + DEADBAND_MILLI)
                {
                    double next = QuantizeClamp(currentOffset + _cfg.OffsetStep);

                    if (!AreSameOffset(next, currentOffset, _cfg.OffsetStep))
                    {
                        bool ok = TryWriteAndConfirm(channel, st, next, "AUTO_PREDICTIVE_BRAKE_COOL", tryWriteOffset, traceLog);
                        if (ok)
                        {
                            st.LastPredictiveBrakeAt = now;
                            st.LastActionAt = now;      // 일반 모드도 잠깐 쉬게 해서 연타 방지
                            st.PrevAction = TempDirection.Down;
                            return next;
                        }
                    }
                }

                // 현재는 목표보다 높음(error>0), 하강 중(slope<0), 예측상 목표 아래로 내려갈 것 같으면 -> 가열 브레이크( -step )
                if (errorMilli > 0 && slopeMilli < 0 && predictedMilli <= targetTempMilli - DEADBAND_MILLI)
                {
                    double next = QuantizeClamp(currentOffset - _cfg.OffsetStep);

                    if (!AreSameOffset(next, currentOffset, _cfg.OffsetStep))
                    {
                        bool ok = TryWriteAndConfirm(channel, st, next, "AUTO_PREDICTIVE_BRAKE_HEAT", tryWriteOffset, traceLog);
                        if (ok)
                        {
                            st.LastPredictiveBrakeAt = now;
                            st.LastActionAt = now;
                            st.PrevAction = TempDirection.Up;
                            return next;
                        }
                    }
                }
            }

            // =========================================
            // (D) Near Pulse: 목표 근처 파동을 25.000쪽으로 더 붙이는 "한 번" 보정
            // =========================================
            // - deadband 밖(>0.02)인데 0.04 이내일 때만
            // - 기울기 방향상 곧 목표를 더 지나치거나 멀어질 것 같으면 0.1step 1회
            // - 연타 방지 쿨다운
            if (ShouldApplyNearPulse(now, st, errorMilli, slopeMilli))
            {
                // 조건 1) 목표보다 낮은데(error<0) 계속 올라가고(slope>0) 있어 "곧 통과" 또는 "파동이 커질" 기미
                // -> 약한 냉각(+step)로 파동 중심을 목표 근처로 당김
                if (errorMilli < 0 && slopeMilli > 0)
                {
                    double next = QuantizeClamp(currentOffset + _cfg.OffsetStep);
                    if (!AreSameOffset(next, currentOffset, _cfg.OffsetStep))
                    {
                        bool ok = TryWriteAndConfirm(channel, st, next, "AUTO_NEAR_PULSE_COOL", tryWriteOffset, traceLog);
                        if (ok)
                        {
                            st.LastNearPulseAt = now;
                            st.LastActionAt = now;      // 연속 write 방지
                            st.PrevAction = TempDirection.Down;
                            return next;
                        }
                    }
                }

                // 조건 2) 목표보다 높은데(error>0) 계속 내려가고(slope<0) 있어 "곧 아래로 과하게 내려갈" 기미
                // -> 약한 가열(-step)로 파동 중심을 목표 근처로 당김
                if (errorMilli > 0 && slopeMilli < 0)
                {
                    double next = QuantizeClamp(currentOffset - _cfg.OffsetStep);
                    if (!AreSameOffset(next, currentOffset, _cfg.OffsetStep))
                    {
                        bool ok = TryWriteAndConfirm(channel, st, next, "AUTO_NEAR_PULSE_HEAT", tryWriteOffset, traceLog);
                        if (ok)
                        {
                            st.LastNearPulseAt = now;
                            st.LastActionAt = now;
                            st.PrevAction = TempDirection.Up;
                            return next;
                        }
                    }
                }
            }

            // =========================================
            // (E) 일반 모드 최소 간격(너무 자주 쓰지 않기)
            // =========================================
            if (st.LastActionAt != DateTime.MinValue &&
                (now - st.LastActionAt).TotalMilliseconds < MIN_ACTION_INTERVAL_MS)
            {
                return currentOffset;
            }

            // =========================================
            // (F) 일반 모드 제어(기존 핵심 로직 유지)
            // =========================================
            bool isAction = false;
            double targetOffset = currentOffset;

            // Too hot (현재가 목표보다 높음: error>0) -> 냉각: offset +step
            if (errorMilli > DEADBAND_MILLI)
            {
                st.LastActionAt = now;

                // 이미 충분히 내려가는 중이면(음수 slope가 충분히 큼) 기다림
                // slopeMilli > -threshold 이면 "내려가는 속도가 충분하지 않다" -> 냉각 offset 적용
                if (slopeMilli > -SLOPE_THRESHOLD_MILLI)
                {
                    targetOffset = QuantizeClamp(currentOffset + _cfg.OffsetStep);
                    st.PrevAction = TempDirection.Down;
                    isAction = !AreSameOffset(targetOffset, currentOffset, _cfg.OffsetStep);
                }
            }
            // Too cold (현재가 목표보다 낮음: error<0) -> 가열: offset -step
            else if (errorMilli < -DEADBAND_MILLI)
            {
                st.LastActionAt = now;

                // 이미 충분히 올라가는 중이면(양수 slope가 충분히 큼) 기다림
                // slopeMilli < threshold 이면 "올라가는 속도가 충분하지 않다" -> 가열 offset 적용
                if (slopeMilli < SLOPE_THRESHOLD_MILLI)
                {
                    targetOffset = QuantizeClamp(currentOffset - _cfg.OffsetStep);
                    st.PrevAction = TempDirection.Up;
                    isAction = !AreSameOffset(targetOffset, currentOffset, _cfg.OffsetStep);
                }
            }

            if (!isAction)
                return currentOffset;

            bool writeOk = TryWriteAndConfirm(channel, st, targetOffset, "AUTO_SMART_CTRL", tryWriteOffset, traceLog);
            return writeOk ? targetOffset : currentOffset;
        }

        // =========================================================
        // Predictive Brake 조건
        // =========================================================
        private static bool ShouldApplyPredictiveBrake(
            DateTime now,
            ChannelState st,
            int errorMilli,
            int slopeMilli)
        {
            // 목표 근처에서만
            if (Math.Abs(errorMilli) > PB_ARM_ERR_MILLI)
                return false;

            // slope가 너무 작으면 노이즈
            if (Math.Abs(slopeMilli) < PB_MIN_SLOPE_MILLI)
                return false;

            // deadband는 위에서 이미 return 되지만 안전장치로 방어
            if (Math.Abs(errorMilli) <= DEADBAND_MILLI)
                return false;

            // 쿨다운
            if (st.LastPredictiveBrakeAt != DateTime.MinValue &&
                (now - st.LastPredictiveBrakeAt).TotalMilliseconds < PB_COOLDOWN_MS)
                return false;

            return true;
        }

        // =========================================================
        // Near Pulse 조건
        // =========================================================
        private static bool ShouldApplyNearPulse(
            DateTime now,
            ChannelState st,
            int errorMilli,
            int slopeMilli)
        {
            int absErr = Math.Abs(errorMilli);

            // deadband 밖이지만 목표 근처에서만
            if (absErr <= NP_MIN_ERR_MILLI)   // <= 0.02면 deadband 취급
                return false;

            if (absErr > NP_ARM_ERR_MILLI)    // > 0.04면 의미 없음(오히려 일반/브레이크가 처리)
                return false;

            // slope가 너무 작으면 노이즈
            if (Math.Abs(slopeMilli) < NP_MIN_SLOPE_MILLI)
                return false;

            // 쿨다운: 근처에서 0.1 step을 연타하면 파동이 커짐
            if (st.LastNearPulseAt != DateTime.MinValue &&
                (now - st.LastNearPulseAt).TotalMilliseconds < NP_COOLDOWN_MS)
                return false;

            return true;
        }

        // =========================================================
        // Fast converge (멀리 있을 때)
        // =========================================================
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
            // fast에서는 slope 갱신이 의미 있어야 하므로 계속 업데이트
            int slopeMilli = currentTempMilli - st.PrevTempMilli!.Value;
            int errorMilli = currentTempMilli - targetTempMilli;

            st.PrevTempMilli = currentTempMilli;

            // 안전 상한
            if (st.FastBurstWrites >= FAST_MAX_BURST_WRITES)
            {
                traceLog?.Invoke($"AUTO FAST CH{channel} burst cap reached -> hold (writes={st.FastBurstWrites})");
                return currentOffset;
            }

            // 최소 간격
            if (st.LastFastActionAt != DateTime.MinValue &&
                (now - st.LastFastActionAt).TotalMilliseconds < FAST_MIN_ACTION_INTERVAL_MS)
            {
                return currentOffset;
            }

            double targetOffset = currentOffset;
            bool wantWrite = false;

            // Too hot -> cool: offset +step
            if (errorMilli > FAST_BAND_MILLI)
            {
                // 이미 충분히 내려가는 중이면 스킵
                if (slopeMilli > -FAST_SLOPE_OK_MILLI)
                {
                    targetOffset = QuantizeClamp(currentOffset + _cfg.OffsetStep);
                    wantWrite = !AreSameOffset(targetOffset, currentOffset, _cfg.OffsetStep);
                    st.PrevAction = TempDirection.Down;
                }
            }
            // Too cold -> heat: offset -step
            else if (errorMilli < -FAST_BAND_MILLI)
            {
                // 이미 충분히 올라가는 중이면 스킵
                if (slopeMilli < FAST_SLOPE_OK_MILLI)
                {
                    targetOffset = QuantizeClamp(currentOffset - _cfg.OffsetStep);
                    wantWrite = !AreSameOffset(targetOffset, currentOffset, _cfg.OffsetStep);
                    st.PrevAction = TempDirection.Up;
                }
            }

            if (!wantWrite)
                return currentOffset;

            bool ok = TryWriteAndConfirm(channel, st, targetOffset, "AUTO_FAST_FAR_CONVERGE", tryWriteOffset, traceLog);

            if (ok)
            {
                st.LastFastActionAt = now;
                st.FastBurstWrites++;

                // fast에서 write했으면 일반 모드도 잠깐 쉬게(헌팅 방지)
                st.LastActionAt = now;
            }

            return ok ? targetOffset : currentOffset;
        }

        // =========================================================
        // 초기화/쓰기/유틸
        // =========================================================
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

            st.LastPredictiveBrakeAt = DateTime.MinValue;
            st.LastNearPulseAt = DateTime.MinValue;
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

        private double QuantizeClamp(double offset)
        {
            // 장비/프로그램 설정 범위로 clamp 후 step(0.1) 단위로 quantize
            double clamped = OffsetMath.Clamp(offset, _cfg.OffsetClampMin, _cfg.OffsetClampMax);
            return OffsetMath.Quantize(clamped, _cfg.OffsetStep);
        }

        private static int ToMilli(double tempC)
        {
            return (int)Math.Round(tempC * 1000.0, MidpointRounding.AwayFromZero);
        }

        // step의 절반(0.05) 정도를 허용오차로 사용(불필요한 mismatch resend 방지)
        private static bool AreSameOffset(double a, double b, double offsetStep)
        {
            double eps = Math.Max(1e-6, Math.Abs(offsetStep) * 0.5);
            return Math.Abs(a - b) <= eps;
        }
    }
}