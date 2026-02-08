using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace ThermoBathCalibrator
{
    public sealed class UtOneClient
    {
        private readonly UtOnePortSettings _cfg;
        private readonly object _sync = new object();
        private SerialPort? _sp;

        private const string CmdReadMt = "MT?";

        public UtOneClient(UtOnePortSettings cfg)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        public bool IsOpen => _sp != null && _sp.IsOpen;

        public bool TryOpen(out string error)
        {
            lock (_sync)
            {
                error = "";
                try
                {
                    if (_sp != null && _sp.IsOpen) return true;

                    _sp?.Dispose();
                    _sp = new SerialPort
                    {
                        PortName = _cfg.PortName,
                        BaudRate = _cfg.BaudRate,
                        Parity = ParseParity(_cfg.Parity),
                        DataBits = _cfg.DataBits,
                        StopBits = ParseStopBits(_cfg.StopBits),
                        ReadTimeout = _cfg.ReadTimeoutMs,
                        WriteTimeout = _cfg.WriteTimeoutMs,
                        Encoding = Encoding.ASCII,
                        NewLine = "\n"
                    };

                    _sp.Open();

                    // USB-Serial 조합에 따라 Open 직후 첫 요청이 불안정할 수 있어 짧은 안정 시간
                    Thread.Sleep(80);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    SafeCloseNoThrow();
                    return false;
                }
            }
        }

        public void Close()
        {
            lock (_sync)
            {
                SafeCloseNoThrow();
            }
        }

        private void SafeCloseNoThrow()
        {
            try
            {
                if (_sp != null)
                {
                    if (_sp.IsOpen) _sp.Close();
                    _sp.Dispose();
                    _sp = null;
                }
            }
            catch
            {
                _sp = null;
            }
        }

        public bool TryReadMt(out string responsePayload, out string error)
        {
            lock (_sync)
            {
                responsePayload = "";
                error = "";

                if (_sp == null || !_sp.IsOpen)
                {
                    error = "SerialPort is not open.";
                    return false;
                }

                try
                {
                    _sp.DiscardInBuffer();

                    string term = (_cfg.LineTerminator ?? "LF").Equals("CRLF", StringComparison.OrdinalIgnoreCase)
                        ? "\r\n"
                        : "\n";

                    _sp.Write(CmdReadMt + term);

                    var sb = new StringBuilder();
                    var sw = Stopwatch.StartNew();

                    while (sw.ElapsedMilliseconds < _cfg.ReadTimeoutMs)
                    {
                        string chunk = _sp.ReadExisting();
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            sb.Append(chunk);
                            string all = sb.ToString();

                            // MT? 응답은 C01/C02/TJ 같은 토큰이 포함되는 경우가 많아 이를 기준으로 컷
                            if (all.Contains("TJ", StringComparison.OrdinalIgnoreCase) ||
                                all.Contains("C01", StringComparison.OrdinalIgnoreCase) ||
                                all.Contains("C1", StringComparison.OrdinalIgnoreCase))
                                break;
                        }

                        Thread.Sleep(10);
                    }

                    string raw = sb.ToString().Trim();

                    // 일부 모드에서 "OK"가 앞에 붙는 경우가 있어 제거
                    if (raw.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                        raw = raw.Substring(2).Trim();

                    responsePayload = raw;
                    return !string.IsNullOrWhiteSpace(responsePayload);
                }
                catch (TimeoutException)
                {
                    error = "Timeout while reading MT response.";
                    return false;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }

        private static Parity ParseParity(string s)
        {
            return s switch
            {
                "Odd" => Parity.Odd,
                "Even" => Parity.Even,
                _ => Parity.None
            };
        }

        private static StopBits ParseStopBits(string s)
        {
            return s switch
            {
                "Two" => StopBits.Two,
                _ => StopBits.One
            };
        }
    }
}
