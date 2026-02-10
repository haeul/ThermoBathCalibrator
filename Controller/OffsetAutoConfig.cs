namespace ThermoBathCalibrator.Controller
{
    internal sealed class OffsetAutoConfig
    {
        // Offset quantization and clamp
        public double OffsetStep { get; set; } = 0.1;
        public double OffsetClampMin { get; set; } = -1.0;
        public double OffsetClampMax { get; set; } = 1.0;

        // Target and write pacing
        public double TargetTemperature { get; set; } = 25.0;
        public double MinAutoWriteIntervalSeconds { get; set; } = 1.0;

        // Coarse control zones by absolute error
        public double AutoErrStepHigh { get; set; } = 0.30;
        public double AutoErrStepMid { get; set; } = 0.12;
        public double AutoErrStepLow { get; set; } = 0.04;
        public double AutoErrDeadband { get; set; } = 0.010;

        // Coarse control step sizes
        public double AutoStepHigh { get; set; } = 0.4;
        public double AutoStepMid { get; set; } = 0.2;
        public double AutoStepLow { get; set; } = 0.1;
        public double AutoStepFine { get; set; } = 0.1;

        // Coarse hold times
        public double AutoHoldHighSeconds { get; set; } = 24.0;
        public double AutoHoldMidSeconds { get; set; } = 14.0;
        public double AutoHoldLowSeconds { get; set; } = 8.0;
        public double AutoHoldFineSeconds { get; set; } = 5.0;

        // Overshoot and trend guard
        public double ErrorTrendEpsilon { get; set; } = 0.001;
        public int WrongTrendTriggerSamples { get; set; } = 3;
        public double WrongTrendAggressivenessScale { get; set; } = 0.6;
        public int ReverseStepTicksOnWrongTrend { get; set; } = 1;

        // Stall logic for large and persistent error
        public double StallErrorThreshold { get; set; } = 0.10;
        public double StallTimeSeconds { get; set; } = 90.0;
        public double StallAggressivenessMultiplier { get; set; } = 1.5;
        public double MaxAggressivenessMultiplier { get; set; } = 3.0;

        // PWM fine control
        public double FinePwmEnterErr { get; set; } = 0.020;
        public double FinePwmExitErr { get; set; } = 0.060;
        public double FinePwmPeriodSec { get; set; } = 18.0;
        public double FinePwmDutyGainPerError { get; set; } = 6.0;
        public double FinePwmMinDutyHigh { get; set; } = 0.10;
        public double FinePwmMaxDutyHigh { get; set; } = 0.90;

        // 발산 방지
        // Safety: startup warmup (no write)
        public double StartupWarmupSeconds { get; set; } = 25.0;

        // Safety: after a write, observe before reacting
        public double PostWriteObserveSeconds { get; set; } = 20.0;

        // Safety: worsening detection (abs(err) increasing)
        public int WorsenStreakTrigger { get; set; } = 3;
        public double WorsenEpsilon { get; set; } = 0.002;

        // Safety: rollback + freeze
        public double RollbackFreezeSeconds { get; set; } = 60.0;

    }
}
