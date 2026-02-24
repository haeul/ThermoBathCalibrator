using System.Globalization;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        private void AppendRowToGrid(SampleRow r)
        {
            string fmtTemp = "0.000";
            string ts = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

            dataGridView1.Rows.Add(
                ts,
                ToCell(r.UtCh1, fmtTemp),
                ToCell(r.UtCh2, fmtTemp),
                ToCell(r.Max, fmtTemp),
                ToCell(r.Min, fmtTemp),
                ToCell(r.Average, fmtTemp),
                ToCell(r.Err1, fmtTemp),
                ToCell(r.Err2, fmtTemp),
                ToCell(r.Bath1OffsetCur, "0.0"),
                ToCell(r.Bath2OffsetCur, "0.0")
            );

            if (dataGridView1.Rows.Count > MaxGridRows)
            {
                dataGridView1.Rows.RemoveAt(0);
            }

            _rowAddCount++;
            if (_rowAddCount % _scrollEveryN == 0)
            {
                int last = dataGridView1.Rows.Count - 1;
                if (last >= 0)
                    dataGridView1.FirstDisplayedScrollingRowIndex = last;
            }
        }

        private static string ToCell(double v, string fmt)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "-";
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }
    }
}
