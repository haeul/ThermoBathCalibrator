using System;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        private struct MultiBoardSnapshot
        {
            public ushort Ch1Alive;
            public ushort Ch1Response;
            public double Ch1Pv;
            public double Ch1Sv;
            public double Ch1OffsetCur;
            public double Ch1ExternalThermo;

            public ushort Ch2Alive;
            public ushort Ch2Response;
            public double Ch2Pv;
            public double Ch2Sv;
            public double Ch2OffsetCur;
            public double Ch2ExternalThermo;

            public double Tj;
        }

        private struct DailyChannelStats
        {
            public int Count;
            public double Sum;
            public double Min;
            public double Max;

            public void Reset()
            {
                Count = 0;
                Sum = 0.0;
                Min = double.NaN;
                Max = double.NaN;
            }

            public void Add(double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;

                if (Count == 0)
                {
                    Min = value;
                    Max = value;
                }
                else
                {
                    if (value < Min) Min = value;
                    if (value > Max) Max = value;
                }

                Sum += value;
                Count++;
            }

            public double Average => Count > 0 ? (Sum / Count) : double.NaN;
        }

        private sealed class SampleRow
        {
            public DateTime Timestamp { get; set; }

            public double UtCh1 { get; set; }
            public double UtCh2 { get; set; }
            public double UtTj { get; set; }

            public double Max1 { get; set; }
            public double Max2 { get; set; }
            public double Min1 { get; set; }
            public double Min2 { get; set; }
            public double Average1 { get; set; }
            public double Average2 { get; set; }

            public double Bath1Pv { get; set; }
            public double Bath2Pv { get; set; }

            public double Err1 { get; set; }
            public double Err2 { get; set; }

            public double Bath1OffsetCur { get; set; }
            public double Bath2OffsetCur { get; set; }

            public double Derr1 { get; set; }
            public double Derr2 { get; set; }

            public double Err1Ma5 { get; set; }
            public double Err2Ma5 { get; set; }

            public double Err1Std10 { get; set; }
            public double Err2Std10 { get; set; }

            public double LastWriteAgeCh1Sec { get; set; }
            public double LastWriteAgeCh2Sec { get; set; }

            public bool ReadOk { get; set; }
            public bool BoardConnected { get; set; }

            public double Bath1OffsetTarget { get; set; }
            public double Bath2OffsetTarget { get; set; }

            public double Bath1OffsetApplied { get; set; }
            public double Bath2OffsetApplied { get; set; }

            public double Bath1SetTemp { get; set; }
            public double Bath2SetTemp { get; set; }

            public double DailyMax { get; set; }
            public double DailyMin { get; set; }
            public double DailyAverage { get; set; }
        }
    }
}