using System;

namespace ThermoBathCalibrator.Controller
{
    internal static class OffsetMath
    {
        public static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static double Quantize(double value, double step)
        {
            if (step <= 0)
                return value;

            return Math.Round(
                value / step,
                MidpointRounding.AwayFromZero
            ) * step;
        }
    }
}
