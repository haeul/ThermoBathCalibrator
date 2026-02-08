using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace ThermoBathCalibrator
{
    public sealed class HebcBathClient
    {
        private readonly BathPortSettings _cfg;
        private readonly object _sync = new object();
        private SerialPort? _sp;
        private long _lastReqTick;

        private const string ID_PV1 = "PV1"; // PV read
        private const string ID_PVS = "PVS"; // Offset R/W
        private const string ID_STR = "STR"; // Save

        public HebcBathClient(BathPortSettings cfg)
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
                        Encoding = Encoding.ASCII
                    };

                    _sp.Open();

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

        public bool TryReadPv1(out double pv, out string error)
        {
            pv = double.NaN;
            error = "";

            if (!TryExchange(readOrWrite: 'R', id: ID_PV1, numericData: "00000", out var resp, out error))
                return false;

            if (!TryParseReadResponse(resp, out int raw10, out error))
                return false;

            pv = raw10 / 10.0;
            return true;
        }

        public bool TryWritePvsOffset(double offsetC, out string error)
        {
            error = "";

            int raw10 = (int)Math.Round(offsetC * 10.0, MidpointRounding.AwayFromZero);
            string num = FormatNumeric5(raw10);

            if (!TryExchange(readOrWrite: 'W', id: ID_PVS, numericData: num, out var resp, out error))
                return false;

            return TryParseWriteAck(resp, out error);
        }

        public bool TrySaveStr(out string error)
        {
            error = "";

            if (!TryExchange(readOrWrite: 'W', id: ID_STR, numericData: "00000", out var resp, out error))
                return false;

            return TryParseWriteAck(resp, out error);
        }

        private bool TryExchange(char readOrWrite, string id, string numericData, out byte[] response, out string error)
        {
            lock (_sync)
            {
                response = Array.Empty<byte>();
                error = "";

                if (_sp == null || !_sp.IsOpen)
                {
                    error = "SerialPort is not open.";
                    return false;
                }

                try
                {
                    EnforceInterRequestDelay();

                    _sp.DiscardInBuffer();

                    byte[] req = BuildRequest((byte)readOrWrite, id, numericData);
                    _sp.Write(req, 0, req.Length);

                    // 응답은 ETX(0x03)까지 읽고,
                    // UseBcc=true면 추가 1바이트(BCC)까지 읽는다.
                    var buf = new List<byte>(64);
                    var sw = Stopwatch.StartNew();

                    while (sw.ElapsedMilliseconds < _cfg.ReadTimeoutMs)
                    {
                        int b = _sp.ReadByte();
                        if (b < 0) continue;

                        buf.Add((byte)b);

                        if ((byte)b == 0x03)
                        {
                            if (_cfg.UseBcc)
                            {
                                int bcc = _sp.ReadByte();
                                if (bcc >= 0) buf.Add((byte)bcc);
                            }
                            break;
                        }
                    }

                    response = buf.ToArray();
                    _lastReqTick = Environment.TickCount64;

                    if (response.Length < 5)
                    {
                        error = "Response too short.";
                        return false;
                    }

                    return true;
                }
                catch (TimeoutException)
                {
                    error = "Timeout while waiting for response.";
                    return false;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }

        private void EnforceInterRequestDelay()
        {
            int delay = _cfg.ResponseDelayMs;
            if (delay <= 0) return;

            long now = Environment.TickCount64;
            long elapsed = now - _lastReqTick;
            if (elapsed < delay)
            {
                int wait = (int)(delay - elapsed);
                if (wait > 0) Thread.Sleep(wait);
            }
        }

        private byte[] BuildRequest(byte rwAscii, string id3, string numeric5)
        {
            // STX(0x02) + Address(2 ASCII) + R/W + ID(3 ASCII) + Numeric(5 ASCII) + ETX(0x03) + (optional) BCC
            string addr2 = _cfg.Address.ToString("00", CultureInfo.InvariantCulture);

            var core = new List<byte>(16);

            core.Add(0x02);
            core.Add((byte)addr2[0]);
            core.Add((byte)addr2[1]);
            core.Add(rwAscii);

            if (id3.Length != 3) throw new ArgumentException("Identifier must be 3 chars.", nameof(id3));
            core.Add((byte)id3[0]);
            core.Add((byte)id3[1]);
            core.Add((byte)id3[2]);

            if (numeric5.Length != 5) throw new ArgumentException("Numeric data must be 5 chars.", nameof(numeric5));
            for (int i = 0; i < 5; i++)
                core.Add((byte)numeric5[i]);

            core.Add(0x03);

            if (_cfg.UseBcc)
            {
                byte bcc = 0x00;
                for (int i = 0; i < core.Count; i++)
                    bcc ^= core[i];

                core.Add(bcc);
            }

            return core.ToArray();
        }

        private bool TryParseWriteAck(byte[] response, out string error)
        {
            error = "";

            // 기본 형태: STX + Addr(2) + ACK/NAK + ETX (+ BCC)
            if (response.Length < 5)
            {
                error = "ACK response too short.";
                return false;
            }

            if (response[0] != 0x02)
            {
                error = "Missing STX.";
                return false;
            }

            int etxIndex = Array.IndexOf(response, (byte)0x03);
            if (etxIndex < 0)
            {
                error = "Missing ETX.";
                return false;
            }

            if (_cfg.UseBcc)
            {
                if (etxIndex + 1 >= response.Length)
                {
                    error = "Missing BCC.";
                    return false;
                }

                if (!VerifyBcc(response, etxIndex))
                {
                    error = "BCC check failed.";
                    return false;
                }
            }

            // STX(0) Addr(1,2) Code(3)
            if (response.Length <= 3)
            {
                error = "ACK code not found.";
                return false;
            }

            byte code = response[3];

            if (code == 0x06) return true;

            if (code == 0x15)
            {
                error = "NAK received from bath.";
                return false;
            }

            error = $"Unexpected code: 0x{code:X2}";
            return false;
        }

        private bool TryParseReadResponse(byte[] response, out int numericRaw10, out string error)
        {
            numericRaw10 = 0;
            error = "";

            // STX + Addr(2) + Numeric(5 ASCII) + ETX (+ BCC)
            if (response.Length < 9)
            {
                error = "Read response too short.";
                return false;
            }

            if (response[0] != 0x02)
            {
                error = "Missing STX.";
                return false;
            }

            int etxIndex = Array.IndexOf(response, (byte)0x03);
            if (etxIndex < 0)
            {
                error = "Missing ETX.";
                return false;
            }

            if (_cfg.UseBcc)
            {
                if (etxIndex + 1 >= response.Length)
                {
                    error = "Missing BCC.";
                    return false;
                }

                if (!VerifyBcc(response, etxIndex))
                {
                    error = "BCC check failed.";
                    return false;
                }
            }

            // numeric: index 3..7
            if (etxIndex < 3 + 5)
            {
                error = "Numeric field not found.";
                return false;
            }

            string numStr = Encoding.ASCII.GetString(response, 3, 5);

            if (!TryParseNumeric5(numStr, out numericRaw10))
            {
                error = $"Invalid numeric data: {numStr}";
                return false;
            }

            return true;
        }

        private bool VerifyBcc(byte[] response, int etxIndex)
        {
            // XOR from STX(0) to ETX(etxIndex) inclusive == BCC(etxIndex+1)
            byte bcc = 0x00;
            for (int i = 0; i <= etxIndex; i++)
                bcc ^= response[i];

            byte expected = response[etxIndex + 1];
            return bcc == expected;
        }

        private static string FormatNumeric5(int value)
        {
            // 5자리, 음수는 첫 글자 '-' + 4자리 절댓값
            if (value < 0)
            {
                int abs = Math.Abs(value);
                return "-" + abs.ToString("0000", CultureInfo.InvariantCulture);
            }

            return value.ToString("00000", CultureInfo.InvariantCulture);
        }

        private static bool TryParseNumeric5(string s, out int value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(s) || s.Length != 5)
                return false;

            if (s[0] == '-')
            {
                if (!int.TryParse(s.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int abs))
                    return false;

                value = -abs;
                return true;
            }

            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
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
