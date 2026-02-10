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

        private sealed class SampleRow
        {
            public DateTime Timestamp { get; set; }

            public double UtCh1 { get; set; }
            public double UtCh2 { get; set; }
            public double UtTj { get; set; }

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
        }
    }
}
