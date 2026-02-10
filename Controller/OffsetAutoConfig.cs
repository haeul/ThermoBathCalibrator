namespace ThermoBathCalibrator.Controller
{
    internal sealed class OffsetAutoConfig
    {
        // ===== Offset 기본 =====
        public double OffsetStep { get; set; } = 0.1;
        public double OffsetClampMin { get; set; } = -1.0;
        public double OffsetClampMax { get; set; } = 1.0;

        public double MinAutoWriteIntervalSeconds { get; set; } = 1.0;

        // ===== abs(error) 기준 =====
        public double AutoErrStepHigh { get; set; } = 0.30;
        public double AutoErrStepMid { get; set; } = 0.10;
        public double AutoErrStepLow { get; set; } = 0.03;
        public double AutoErrDeadband { get; set; } = 0.01;

        // ===== step 크기 =====
        public double AutoStepHigh { get; set; } = 0.3;   // ← 여기 0.5, 0.6으로 키워도 됨
        public double AutoStepMid { get; set; } = 0.2;
        public double AutoStepLow { get; set; } = 0.1;
        public double AutoStepFine { get; set; } = 0.1;

        // ===== hold 시간 =====
        public double AutoHoldHighSeconds { get; set; } = 30.0;
        public double AutoHoldMidSeconds { get; set; } = 20.0;
        public double AutoHoldLowSeconds { get; set; } = 10.0;
        public double AutoHoldFineSeconds { get; set; } = 5.0;

        // ===== PWM 미세 제어 =====
        public double FinePwmEnterErr { get; set; } = 0.05;
        public double FinePwmExitErr { get; set; } = 0.12;

        public double FinePwmPeriodSec { get; set; } = 20.0;
        public double FinePwmDutyGain { get; set; } = 0.8;

        public double FinePwmMinDuty { get; set; } = 0.10;
        public double FinePwmMaxDuty { get; set; } = 0.90;
    }
}
