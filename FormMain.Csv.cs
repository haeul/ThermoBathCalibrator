using System;
using System.IO;
using System.Text;
using System.Globalization;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        private void PrepareCsvPath(DateTime now)
        {
            DateTime day = now.Date;
            if (_csvDay == day && !string.IsNullOrWhiteSpace(_csvPath)) return;

            _csvDay = day;
            _csvHeaderWritten = false;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data");
            Directory.CreateDirectory(dataDir);

            string fileName = $"thermo_log_{day:yyyyMMdd}.csv";
            _csvPath = Path.Combine(dataDir, fileName);

            if (File.Exists(_csvPath) && new FileInfo(_csvPath).Length > 0)
                _csvHeaderWritten = true;
        }

        private void AppendCsvRow(SampleRow r)
        {
            try
            {
                lock (_csvSync)
                {
                    if (string.IsNullOrWhiteSpace(_csvPath))
                        PrepareCsvPath(r.Timestamp);

                    bool needHeader = !_csvHeaderWritten;

                    var sb = new StringBuilder(512);

                    if (needHeader)
                    {
                        sb.AppendLine(string.Join(",",
                            "timestamp",
                            "ut_ch1",
                            "ut_ch2",
                            "ut_tj",
                            "bath1_pv",
                            "bath2_pv",
                            "err1",
                            "err2",
                            "bath1_offset_cur",
                            "bath2_offset_cur",
                            "derr1",
                            "derr2",
                            "err1_ma5",
                            "err2_ma5",
                            "err1_std10",
                            "err2_std10",
                            "last_write_age_ch1_sec",
                            "last_write_age_ch2_sec",
                            "read_ok",
                            "board_connected",
                            "bath1_offset_target",
                            "bath2_offset_target",
                            "bath1_offset_applied",
                            "bath2_offset_applied",
                            "bath1_set_temp",
                            "bath2_set_temp"
                        ));

                        _csvHeaderWritten = true;
                    }

                    sb.AppendLine(string.Join(",",
                        r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        CsvNum(r.UtCh1, "0.000"),
                        CsvNum(r.UtCh2, "0.000"),
                        CsvNum(r.UtTj, "0.000"),
                        CsvNum(r.Bath1Pv, "0.000"),
                        CsvNum(r.Bath2Pv, "0.000"),
                        CsvNum(r.Err1, "0.000"),
                        CsvNum(r.Err2, "0.000"),
                        CsvNum(r.Bath1OffsetCur, "0.0"),
                        CsvNum(r.Bath2OffsetCur, "0.0"),
                        CsvNum(r.Derr1, "0.000"),
                        CsvNum(r.Derr2, "0.000"),
                        CsvNum(r.Err1Ma5, "0.000"),
                        CsvNum(r.Err2Ma5, "0.000"),
                        CsvNum(r.Err1Std10, "0.000"),
                        CsvNum(r.Err2Std10, "0.000"),
                        CsvNum(r.LastWriteAgeCh1Sec, "0.0"),
                        CsvNum(r.LastWriteAgeCh2Sec, "0.0"),
                        r.ReadOk ? "1" : "0",
                        r.BoardConnected ? "1" : "0",
                        CsvNum(r.Bath1OffsetTarget, "0.000"),
                        CsvNum(r.Bath2OffsetTarget, "0.000"),
                        CsvNum(r.Bath1OffsetApplied, "0.0"),
                        CsvNum(r.Bath2OffsetApplied, "0.0"),
                        CsvNum(r.Bath1SetTemp, "0.000"),
                        CsvNum(r.Bath2SetTemp, "0.000")
                    ));

                    File.AppendAllText(_csvPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        private static string CsvNum(double v, string fmt)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }
    }
}
