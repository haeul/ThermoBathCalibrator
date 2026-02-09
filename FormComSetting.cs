using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    public partial class FormComSetting : Form
    {
        private readonly string _path;
        private CommSettings _settings;

        public CommSettings CurrentSettings => _settings;

        public FormComSetting()
        {
            InitializeComponent();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            _path = CommSettings.GetDefaultPath();
            _settings = CommSettings.LoadOrDefault(_path);

            WireEvents();
            LoadToUi(_settings);
        }

        private void WireEvents()
        {
            btnTestMb.Click -= BtnTestMb_Click;
            btnTestMb.Click += BtnTestMb_Click;

            btnCh1SvSet.Click -= BtnCh1SvSet_Click;
            btnCh1SvSet.Click += BtnCh1SvSet_Click;

            btnCh1OffSet.Click -= BtnCh1OffSet_Click;
            btnCh1OffSet.Click += BtnCh1OffSet_Click;

            btnCh2SvSet.Click -= BtnCh2SvSet_Click;
            btnCh2SvSet.Click += BtnCh2SvSet_Click;

            btnCh2OffSet.Click -= BtnCh2OffSet_Click;
            btnCh2OffSet.Click += BtnCh2OffSet_Click;

            // ✅ READBACK 전용 버튼
            btnReadbackNow.Click -= BtnReadbackNow_Click;
            btnReadbackNow.Click += BtnReadbackNow_Click;

            btnReadbackDelay.Click -= BtnReadbackDelay_Click;
            btnReadbackDelay.Click += BtnReadbackDelay_Click;

            btnSave.Click -= BtnSave_Click;
            btnSave.Click += BtnSave_Click;

            btnClose.Click -= (s, e) => Close();
            btnClose.Click += (s, e) => Close();
        }

        private void BtnTestMb_Click(object? sender, EventArgs e)
        {
            ReadSnapshotAndLog(delayMs: 0);
        }

        private void BtnCh1SvSet_Click(object? sender, EventArgs e)
        {
            WriteCommandOnly(channel: 1, doSv: true, doOffset: false);
        }

        private void BtnCh1OffSet_Click(object? sender, EventArgs e)
        {
            WriteCommandOnly(channel: 1, doSv: false, doOffset: true);
        }

        private void BtnCh2SvSet_Click(object? sender, EventArgs e)
        {
            WriteCommandOnly(channel: 2, doSv: true, doOffset: false);
        }

        private void BtnCh2OffSet_Click(object? sender, EventArgs e)
        {
            WriteCommandOnly(channel: 2, doSv: false, doOffset: true);
        }

        // ✅ READBACK 전용
        private void BtnReadbackNow_Click(object? sender, EventArgs e)
        {
            ReadSnapshotAndLog(delayMs: 0);
        }

        // ✅ READBACK 전용(지연)
        private void BtnReadbackDelay_Click(object? sender, EventArgs e)
        {
            ReadSnapshotAndLog(delayMs: 800);
        }

        /// <summary>
        /// (1) WRITE 전용 테스트: FC10만 수행.
        /// - READBACK 하지 않음(장비 Busy 타이밍으로 TCP 강제 종료되는 케이스 회피)
        /// - cmd clear도 하지 않음(WRITE 연타를 줄여서 안정성↑)
        /// </summary>
        private void WriteCommandOnly(int channel, bool doSv, bool doOffset)
        {
            try
            {
                var s = ReadFromUi();
                var mb = s.MultiBoard;

                using (var c = new MultiBoardModbusClient(mb.Host, mb.Port, (byte)mb.UnitId))
                {
                    if (!c.TryConnect(out string errConn))
                    {
                        Log($"MULTI CONNECT FAIL: {errConn}");
                        return;
                    }

                    // Command bit: bit0=SV, bit1=Offset
                    ushort cmd = 0;
                    if (doSv) cmd |= 1 << 0;
                    if (doOffset) cmd |= 1 << 1;

                    // 값은 1/10℃ 단위로 write
                    short svRaw10 = (short)Math.Round((double)nudTestSv.Value * 10.0, MidpointRounding.AwayFromZero);
                    short offRaw10 = (short)Math.Round((double)nudTestOffset.Value * 10.0, MidpointRounding.AwayFromZero);

                    ushort svWord = unchecked((ushort)svRaw10);
                    ushort offWord = unchecked((ushort)offRaw10);

                    ushort start = (channel == 1) ? (ushort)20 : (ushort)24;
                    ushort[] payload = new ushort[] { cmd, svWord, offWord }; // 20,21,22 or 24,25,26

                    if (!c.TryWriteMultipleRegisters(start, payload, out string errW))
                    {
                        Log($"WRITE FAIL: CH{channel} start={start} err={errW}");
                        return;
                    }

                    Log($"WRITE OK (NO READBACK): CH{channel} cmd=0x{cmd:X4} sv={nudTestSv.Value.ToString("0.0", CultureInfo.InvariantCulture)} off={nudTestOffset.Value.ToString("0.0", CultureInfo.InvariantCulture)}");

                    // ✅ 내가 쓴 영역도 즉시 읽어서 확인(20~22 or 24~26)
                    if (!c.TryReadHoldingRegisters(start, 3, out ushort[] wrRegs, out string errWrRead))
                    {
                        Log($"WRITE-AREA READ FAIL: start={start} err={errWrRead}");
                        return;
                    }

                    Log($"WRITE-AREA READ OK: start={start} => {string.Join(", ", wrRegs)} (cmd, sv, off)");

                }
            }
            catch (Exception ex)
            {
                Log($"CMD TEST EX: {ex.Message}");
            }
        }

        /// <summary>
        /// (2) READBACK 전용: FC03 0~13 스냅샷만 읽어 로그 출력
        /// - delayMs > 0 이면 대기 후 READ (장비 busy 회피용)
        /// </summary>
        private void ReadSnapshotAndLog(int delayMs)
        {
            try
            {
                var s = ReadFromUi();
                var mb = s.MultiBoard;

                if (delayMs > 0)
                {
                    Log($"READBACK WAIT: {delayMs}ms");
                    Thread.Sleep(delayMs);
                }

                using (var c = new MultiBoardModbusClient(mb.Host, mb.Port, (byte)mb.UnitId))
                {
                    if (!c.TryConnect(out string errConn))
                    {
                        Log($"MULTI CONNECT FAIL: {errConn}");
                        return;
                    }

                    if (!c.TryReadHoldingRegisters(0, 14, out ushort[] regs, out string errRead))
                    {
                        Log($"MULTI READ FAIL: {errRead}");
                        return;
                    }

                    Log($"MULTI READ OK: {mb.Host}:{mb.Port} Unit={mb.UnitId}");
                    Log(DecodeFc03Snapshot(regs));
                }
            }
            catch (Exception ex)
            {
                Log($"MULTI TEST EX: {ex.Message}");
            }
        }

        private static string DecodeFc03Snapshot(ushort[] r)
        {
            if (r == null || r.Length < 14)
                return "REG0..13 invalid";

            ushort ch1Alive = r[0];
            ushort ch1Resp = r[1];
            double ch1Pv = ToSigned10(r[2]) / 10.0;
            double ch1Sv = ToSigned10(r[3]) / 10.0;
            double ch1Off = ToSigned10(r[4]) / 10.0;
            double ch1Ext = r[5] / 1000.0;

            ushort ch2Alive = r[7];
            ushort ch2Resp = r[8];
            double ch2Pv = ToSigned10(r[9]) / 10.0;
            double ch2Sv = ToSigned10(r[10]) / 10.0;
            double ch2Off = ToSigned10(r[11]) / 10.0;
            double ch2Ext = r[12] / 1000.0;

            double tj = r[13] / 1000.0;

            string ch1Bits = $"SV={(GetBit(ch1Resp, 0) ? 1 : 0)}, OFF={(GetBit(ch1Resp, 1) ? 1 : 0)}";
            string ch2Bits = $"SV={(GetBit(ch2Resp, 0) ? 1 : 0)}, OFF={(GetBit(ch2Resp, 1) ? 1 : 0)}";

            return
                $"REG0..13 = {string.Join(", ", r.Select(x => x.ToString()))}\r\n" +
                $"CH1 Alive={ch1Alive} Resp=0x{ch1Resp:X4} ({ch1Bits}) PV={ch1Pv:0.0} SV={ch1Sv:0.0} OFF={ch1Off:0.0} EXT={ch1Ext:0.000}\r\n" +
                $"CH2 Alive={ch2Alive} Resp=0x{ch2Resp:X4} ({ch2Bits}) PV={ch2Pv:0.0} SV={ch2Sv:0.0} OFF={ch2Off:0.0} EXT={ch2Ext:0.000}\r\n" +
                $"TJ={tj:0.000}";
        }

        private static short ToSigned10(ushort w) => unchecked((short)w);

        private static bool GetBit(ushort w, int bit) => (w & (1 << bit)) != 0;

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                var s = ReadFromUi();
                s.Save(_path);
                _settings = s;

                Log($"저장 완료: {_path}");
                MessageBox.Show("저장 완료", "COM Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"SAVE EX: {ex.Message}");
                MessageBox.Show(ex.Message, "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadToUi(CommSettings s)
        {
            txtMbHost.Text = (s.MultiBoard.Host ?? "").Trim();
            numMbPort.Value = Clamp(s.MultiBoard.Port, 1, 65535);
            numMbUnitId.Value = Clamp(s.MultiBoard.UnitId, 1, 247);
        }

        private CommSettings ReadFromUi()
        {
            var s = new CommSettings();

            s.MultiBoard.Host = SafeText(txtMbHost.Text, "192.168.1.11");
            s.MultiBoard.Port = (int)numMbPort.Value;
            s.MultiBoard.UnitId = (int)numMbUnitId.Value;

            return s;
        }

        private void Log(string msg)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }

        private static string SafeText(string s, string fallback)
        {
            var t = (s ?? "").Trim();
            return string.IsNullOrWhiteSpace(t) ? fallback : t;
        }

        private static decimal Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
