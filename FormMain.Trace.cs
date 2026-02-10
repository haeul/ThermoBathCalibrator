using System;
using System.IO;
using System.Text;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        private void PrepareTracePath(DateTime now)
        {
            DateTime day = now.Date;
            if (_traceDay == day && !string.IsNullOrWhiteSpace(_tracePath)) return;

            _traceDay = day;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logDir = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logDir);

            string fileName = $"modbus_trace_{day:yyyyMMdd}.log";
            _tracePath = Path.Combine(logDir, fileName);
        }

        private void TraceModbus(string message)
        {
            try
            {
                lock (_traceSync)
                {
                    PrepareTracePath(DateTime.Now);
                    if (string.IsNullOrWhiteSpace(_tracePath)) return;
                    File.AppendAllText(_tracePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
