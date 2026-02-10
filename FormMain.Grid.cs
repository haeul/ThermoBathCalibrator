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
                ToCell(r.UtTj, fmtTemp),
                ToCell(r.Bath1Pv, fmtTemp),
                ToCell(r.Bath2Pv, fmtTemp),
                ToCell(r.Err1, fmtTemp),
                ToCell(r.Err2, fmtTemp),
                ToCell(r.Bath1SetTemp, fmtTemp),
                ToCell(r.Bath2SetTemp, fmtTemp)
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
