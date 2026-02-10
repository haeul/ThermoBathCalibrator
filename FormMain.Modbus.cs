using System;
using System.Globalization;
using System.Threading;
using ThermoBathCalibrator.Controller;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        private void LoadMultiBoardSettingsFromDiskIfAny()
        {
            try
            {
                string path = CommSettings.GetDefaultPath();
                var s = CommSettings.LoadOrDefault(path);

                if (s?.MultiBoard != null)
                {
                    string host = (s.MultiBoard.Host ?? "").Trim();
                    int port = s.MultiBoard.Port;
                    int unit = s.MultiBoard.UnitId;

                    if (!string.IsNullOrWhiteSpace(host)) _host = host;
                    if (port > 0 && port <= 65535) _port = port;
                    if (unit >= 1 && unit <= 247) _unitId = (byte)unit;
                }
            }
            catch
            {
            }
        }

        private void ApplyMultiBoardEndpoint(string host, int port, byte unitId)
        {
            _host = (host ?? "").Trim();
            if (string.IsNullOrWhiteSpace(_host)) _host = "192.168.1.11";

            _port = (port > 0 && port <= 65535) ? port : 13000;
            _unitId = (unitId >= 1 && unitId <= 247) ? unitId : (byte)1;

            try { _mb?.Disconnect(); } catch { }

            BuildMultiBoardClient();
            ResetConnectionState();
            UpdateStatusLabels();
        }

        private void BuildMultiBoardClient()
        {
            _mb = new MultiBoardModbusClient(_host, _port, _unitId);
            _mb.Trace = TraceModbus;
        }

        private void ResetConnectionState()
        {
            _boardConnected = false;
            _boardFailCount = 0;

            _prevErr1 = double.NaN;
            _prevErr2 = double.NaN;
            _lastWriteCh1 = DateTime.MinValue;
            _lastWriteCh2 = DateTime.MinValue;

            _lastEnforceWriteCh1 = DateTime.MinValue;
            _lastEnforceWriteCh2 = DateTime.MinValue;
            _lastWrittenOffsetCh1 = double.NaN;
            _lastWrittenOffsetCh2 = double.NaN;

            _hasLastGoodSnap = false;
            _lastGoodSnap = default;

            _hasLastResp = false;
            _lastRespCh1 = 0;
            _lastRespCh2 = 0;

            _autoCtrl.Reset();
        }

        private bool TryConnectWithCooldown()
        {
            long now = Environment.TickCount64;
            long elapsed = now - _lastReconnectTick;
            if (elapsed < ReconnectCooldownMs)
                return false;

            _lastReconnectTick = now;
            return _mb.TryConnect(out _);
        }

        private bool TryReadMultiBoard(out MultiBoardSnapshot snap, out bool stale)
        {
            snap = default;
            stale = false;

            if (!_mb.IsConnected)
            {
                _boardConnected = TryConnectWithCooldown();
                if (!_boardConnected) _boardFailCount++;
            }

            if (!_mb.IsConnected)
            {
                _boardConnected = false;
                return false;
            }

            if (!_mb.TryReadHoldingRegisters(start: RegReadStart, count: RegReadCount, out ushort[] regs, out string err))
            {
                _boardConnected = false;
                _boardFailCount++;
                return false;
            }

            _boardConnected = true;

            MultiBoardSnapshot parsed = ParseSnapshot(regs);

            if (_hasLastResp)
            {
                if (parsed.Ch1Response == _lastRespCh1 && parsed.Ch2Response == _lastRespCh2)
                    stale = true;
            }
            _lastRespCh1 = parsed.Ch1Response;
            _lastRespCh2 = parsed.Ch2Response;
            _hasLastResp = true;

            parsed = MergeWithLastGood(parsed);

            snap = parsed;
            return true;
        }

        private MultiBoardSnapshot MergeWithLastGood(MultiBoardSnapshot cur)
        {
            bool curHasAny =
                !double.IsNaN(cur.Ch1Pv) ||
                !double.IsNaN(cur.Ch2Pv) ||
                !double.IsNaN(cur.Ch1ExternalThermo) ||
                !double.IsNaN(cur.Ch2ExternalThermo) ||
                !double.IsNaN(cur.Tj);

            if (!_hasLastGoodSnap)
            {
                if (curHasAny)
                {
                    _lastGoodSnap = cur;
                    _hasLastGoodSnap = true;
                }
                return cur;
            }

            if (double.IsNaN(cur.Ch1Pv)) cur.Ch1Pv = _lastGoodSnap.Ch1Pv;
            else _lastGoodSnap.Ch1Pv = cur.Ch1Pv;

            if (double.IsNaN(cur.Ch2Pv)) cur.Ch2Pv = _lastGoodSnap.Ch2Pv;
            else _lastGoodSnap.Ch2Pv = cur.Ch2Pv;

            if (double.IsNaN(cur.Ch1ExternalThermo)) cur.Ch1ExternalThermo = _lastGoodSnap.Ch1ExternalThermo;
            else _lastGoodSnap.Ch1ExternalThermo = cur.Ch1ExternalThermo;

            if (double.IsNaN(cur.Ch2ExternalThermo)) cur.Ch2ExternalThermo = _lastGoodSnap.Ch2ExternalThermo;
            else _lastGoodSnap.Ch2ExternalThermo = cur.Ch2ExternalThermo;

            if (double.IsNaN(cur.Tj)) cur.Tj = _lastGoodSnap.Tj;
            else _lastGoodSnap.Tj = cur.Tj;

            _lastGoodSnap.Ch1Alive = cur.Ch1Alive;
            _lastGoodSnap.Ch1Response = cur.Ch1Response;
            _lastGoodSnap.Ch1Sv = cur.Ch1Sv;
            _lastGoodSnap.Ch1OffsetCur = cur.Ch1OffsetCur;

            _lastGoodSnap.Ch2Alive = cur.Ch2Alive;
            _lastGoodSnap.Ch2Response = cur.Ch2Response;
            _lastGoodSnap.Ch2Sv = cur.Ch2Sv;
            _lastGoodSnap.Ch2OffsetCur = cur.Ch2OffsetCur;

            return cur;
        }

        private static bool IsPlausibleTemp(double v, double min, double max)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return false;
            return v >= min && v <= max;
        }

        private static MultiBoardSnapshot ParseSnapshot(ushort[] r)
        {
            if (r == null || r.Length < 14) return default;

            short ch1PvRaw = unchecked((short)r[2]);
            short ch1SvRaw = unchecked((short)r[3]);
            short ch1OffRaw = unchecked((short)r[4]);

            short ch2PvRaw = unchecked((short)r[9]);
            short ch2SvRaw = unchecked((short)r[10]);
            short ch2OffRaw = unchecked((short)r[11]);

            int ch1ExtRaw = r[5];
            int ch2ExtRaw = r[12];
            int tjRaw = r[13];

            double ch1Pv = ch1PvRaw / 10.0;
            double ch2Pv = ch2PvRaw / 10.0;

            double ch1Ext = ch1ExtRaw / 1000.0;
            double ch2Ext = ch2ExtRaw / 1000.0;
            double tj = tjRaw / 1000.0;

            if (!IsPlausibleTemp(ch1Pv, 5, 80)) ch1Pv = double.NaN;
            if (!IsPlausibleTemp(ch2Pv, 5, 80)) ch2Pv = double.NaN;

            if (!IsPlausibleTemp(ch1Ext, 5, 80)) ch1Ext = double.NaN;
            if (!IsPlausibleTemp(ch2Ext, 5, 80)) ch2Ext = double.NaN;

            if (!IsPlausibleTemp(tj, 5, 80)) tj = double.NaN;

            return new MultiBoardSnapshot
            {
                Ch1Alive = r[0],
                Ch1Response = r[1],
                Ch1Pv = ch1Pv,
                Ch1Sv = ch1SvRaw / 10.0,
                Ch1OffsetCur = ch1OffRaw / 10.0,
                Ch1ExternalThermo = ch1Ext,

                Ch2Alive = r[7],
                Ch2Response = r[8],
                Ch2Pv = ch2Pv,
                Ch2Sv = ch2SvRaw / 10.0,
                Ch2OffsetCur = ch2OffRaw / 10.0,
                Ch2ExternalThermo = ch2Ext,

                Tj = tj
            };
        }

        private bool TryReadOffsetFromDevice(int channel, out double offset)
        {
            offset = double.NaN;

            if (!_mb.IsConnected)
            {
                _boardConnected = TryConnectWithCooldown();
                if (!_boardConnected) _boardFailCount++;
            }

            if (!_mb.IsConnected)
            {
                _boardConnected = false;
                TraceModbus($"OFFSET READBACK FAIL ch={channel} reason=not_connected");
                return false;
            }

            ushort start = channel == 1 ? RegCh1OffsetCur : RegCh2OffsetCur;
            if (!_mb.TryReadHoldingRegisters(start, 1, out ushort[] regs, out string err))
            {
                _boardConnected = false;
                _boardFailCount++;
                TraceModbus($"OFFSET READBACK FAIL ch={channel} reason=read_register err={err}");
                return false;
            }

            _boardConnected = true;

            if (regs == null || regs.Length < 1)
            {
                TraceModbus($"OFFSET READBACK FAIL ch={channel} reason=empty_response");
                return false;
            }

            short raw10 = unchecked((short)regs[0]);
            offset = raw10 / 10.0;
            TraceModbus($"OFFSET READBACK OK ch={channel} read={offset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10}");
            return true;
        }

        private bool TrySyncOffsetsFromDevice(string reason)
        {
            bool ok1 = TryReadOffsetFromDevice(channel: 1, out double ch1Offset);
            bool ok2 = TryReadOffsetFromDevice(channel: 2, out double ch2Offset);

            if (!ok1 || !ok2)
            {
                TraceModbus($"OFFSET SYNC FAIL reason={reason} ch1Ok={ok1} ch2Ok={ok2}");
                return false;
            }

            lock (_offsetStateSync)
            {
                _bath1OffsetCur = ch1Offset;
                _bath2OffsetCur = ch2Offset;
            }

            _lastWrittenOffsetCh1 = ch1Offset;
            _lastWrittenOffsetCh2 = ch2Offset;

            TraceModbus($"OFFSET SYNC OK reason={reason} ch1={ch1Offset.ToString("0.0", CultureInfo.InvariantCulture)} ch2={ch2Offset.ToString("0.0", CultureInfo.InvariantCulture)}");
            BeginInvoke(new Action(() => UpdateOffsetUiFromState()));
            return true;
        }

        private bool TryWriteChannelOffset(int channel, double appliedOffset, string reason = "UNSPECIFIED")
        {
            if (!_mb.IsConnected)
            {
                _boardConnected = TryConnectWithCooldown();
                if (!_boardConnected) _boardFailCount++;
            }

            if (!_mb.IsConnected)
            {
                _boardConnected = false;
                TraceModbus($"OFFSET WRITE SKIP ch={channel} reason={reason} not_connected");
                return false;
            }

            appliedOffset = OffsetMath.Clamp(appliedOffset, _autoCfg.OffsetClampMin, _autoCfg.OffsetClampMax);

            short raw10 = (short)Math.Round(appliedOffset * 10.0, MidpointRounding.AwayFromZero);
            ushort offsetWord = unchecked((ushort)raw10);

            if (channel == 1)
            {
                ushort cmd = 0;
                cmd |= (1 << 1);

                ushort svWord = unchecked((ushort)((short)Math.Round(_bath1Setpoint * 10.0, MidpointRounding.AwayFromZero)));

                TraceModbus($"OFFSET WRITE TRY ch=1 reason={reason} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} cmd=0x{cmd:X4} svWord=0x{svWord:X4} offWord=0x{offsetWord:X4}");
                bool ok = TryWriteAndWaitAck(channel: 1, cmd: cmd, svWord: svWord, offsetWord: offsetWord, reason: reason, desiredOffset: appliedOffset, raw10: raw10, out string errWriteAndAck);
                if (ok)
                {
                    if (TryReadbackAfterWrite(channel: 1, desiredOffset: appliedOffset, out double readback))
                    {
                        lock (_offsetStateSync)
                        {
                            _bath1OffsetCur = readback;
                        }
                        _lastWrittenOffsetCh1 = readback;
                    }
                    else
                    {
                        _lastWrittenOffsetCh1 = appliedOffset;
                    }

                    _lastWriteCh1 = DateTime.Now;
                    BeginInvoke(new Action(() => UpdateOffsetUiFromState()));
                }
                else
                {
                    TraceModbus($"OFFSET WRITE FAIL ch=1 reason={reason} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} err={errWriteAndAck}");
                }
                return ok;
            }

            if (channel == 2)
            {
                ushort cmd = 0;
                cmd |= (1 << 1);

                ushort svWord = unchecked((ushort)((short)Math.Round(_bath2Setpoint * 10.0, MidpointRounding.AwayFromZero)));

                TraceModbus($"OFFSET WRITE TRY ch=2 reason={reason} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} cmd=0x{cmd:X4} svWord=0x{svWord:X4} offWord=0x{offsetWord:X4}");
                bool ok = TryWriteAndWaitAck(channel: 2, cmd: cmd, svWord: svWord, offsetWord: offsetWord, reason: reason, desiredOffset: appliedOffset, raw10: raw10, out string errWriteAndAck);
                if (ok)
                {
                    if (TryReadbackAfterWrite(channel: 2, desiredOffset: appliedOffset, out double readback))
                    {
                        lock (_offsetStateSync)
                        {
                            _bath2OffsetCur = readback;
                        }
                        _lastWrittenOffsetCh2 = readback;
                    }
                    else
                    {
                        _lastWrittenOffsetCh2 = appliedOffset;
                    }

                    _lastWriteCh2 = DateTime.Now;
                    BeginInvoke(new Action(() => UpdateOffsetUiFromState()));
                }
                else
                {
                    TraceModbus($"OFFSET WRITE FAIL ch=2 reason={reason} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} err={errWriteAndAck}");
                }
                return ok;
            }

            return false;
        }

        private bool TryWriteAndWaitAck(int channel, ushort cmd, ushort svWord, ushort offsetWord, string reason, double desiredOffset, short raw10, out string error)
        {
            error = string.Empty;

            ushort start = channel == 1 ? RegCh1Command : RegCh2Command;
            ushort[] payload = new ushort[] { cmd, svWord, offsetWord };

            // ✅ 중요: clear는 "command만 0"으로 내리고, SV/Offset은 유지(0,0,0 금지)
            _ = TryClearCommandWord(channel, svWord, offsetWord, reason: reason);

            ushort beforeResp = 0;
            bool hasBeforeResp = TryReadResponseRegister(channel, out beforeResp, out string errBeforeResp);
            if (!hasBeforeResp)
                TraceModbus($"ACK PRE-READ FAIL ch={channel} reason={reason} err={errBeforeResp}");

            if (!_mb.TryWriteMultipleRegisters(start, payload, out string errWrite))
            {
                error = $"WRITE FAIL: {errWrite}";
                TraceModbus($"OFFSET WRITE FAIL ch={channel} reason={reason} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} start={start} err={errWrite}");
                return false;
            }

            int ackMask = cmd & 0x0003;
            if (ackMask == 0)
                return true;

            Thread.Sleep(AckInitialDelayMs);

            DateTime startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < AckTimeoutMs)
            {
                if (TryReadResponseRegister(channel, out ushort resp, out string errResp))
                {
                    bool ackSet = (resp & ackMask) == ackMask;
                    bool staleAck = hasBeforeResp && ((beforeResp & ackMask) == ackMask) && resp == beforeResp;

                    if (ackSet && !staleAck)
                    {
                        TraceModbus($"OFFSET WRITE OK ch={channel} reason={reason} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} ackResp=0x{resp:X4} ackMask=0x{ackMask:X4}");
                        return true;
                    }

                    if (staleAck)
                        TraceModbus($"OFFSET WRITE ACK STALE ch={channel} reason={reason} before=0x{beforeResp:X4} now=0x{resp:X4} ackMask=0x{ackMask:X4}");
                }
                else
                {
                    TraceModbus($"ACK READ FAIL ch={channel} reason={reason} err={errResp}");
                }

                Thread.Sleep(AckPollIntervalMs);
            }

            error = $"ACK TIMEOUT ch={channel} reason={reason} mask=0x{ackMask:X4}";
            TraceModbus(error);
            return false;
        }

        // ✅ 수정본: 0,0,0 금지. Command만 0으로 내리고 SV/Offset은 유지.
        private bool TryClearCommandWord(int channel, ushort svWord, ushort offsetWord, string reason)
        {
            ushort start = channel == 1 ? RegCh1Command : RegCh2Command;

            // command=0, sv/offset 유지 (장비가 파라미터를 레벨로 읽는 경우도 안전)
            ushort[] clearPayload = new ushort[] { 0, svWord, offsetWord };

            bool ok = _mb.TryWriteMultipleRegisters(start, clearPayload, out string errClear);
            if (!ok)
            {
                TraceModbus($"OFFSET CMD CLEAR FAIL ch={channel} reason={reason} start={start} err={errClear}");
                return false;
            }

            TraceModbus($"OFFSET CMD CLEAR OK ch={channel} reason={reason} start={start} keepSV=0x{svWord:X4} keepOFF=0x{offsetWord:X4}");
            return true;
        }

        private bool TryReadbackAfterWrite(int channel, double desiredOffset, out double readback)
        {
            readback = double.NaN;
            DateTime begin = DateTime.UtcNow;

            while ((DateTime.UtcNow - begin).TotalMilliseconds < AckTimeoutMs)
            {
                if (TryReadOffsetFromDevice(channel, out readback))
                {
                    double diff = Math.Abs(readback - desiredOffset);
                    if (diff <= OffsetReadbackMismatchEpsilon)
                    {
                        TraceModbus($"OFFSET WRITE VERIFY OK ch={channel} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} read={readback.ToString("0.0", CultureInfo.InvariantCulture)} diff={diff.ToString("0.000", CultureInfo.InvariantCulture)}");
                        return true;
                    }

                    TraceModbus($"OFFSET WRITE VERIFY WAIT ch={channel} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} read={readback.ToString("0.0", CultureInfo.InvariantCulture)} diff={diff.ToString("0.000", CultureInfo.InvariantCulture)}");
                }

                Thread.Sleep(AckPollIntervalMs);
            }

            TraceModbus($"OFFSET WRITE VERIFY TIMEOUT ch={channel} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} read={readback.ToString("0.0", CultureInfo.InvariantCulture)}");
            return false;
        }

        private bool TryReadResponseRegister(int channel, out ushort response, out string error)
        {
            response = 0;
            error = string.Empty;

            ushort start = channel == 1 ? RegCh1Response : RegCh2Response;
            if (!_mb.TryReadHoldingRegisters(start, 1, out ushort[] regs, out string err))
            {
                error = err;
                return false;
            }

            response = regs.Length > 0 ? regs[0] : (ushort)0;
            return true;
        }

        private void LogOffsetReadAndMismatch(int channel, double readOffset, ushort response)
        {
            double lastWritten = channel == 1 ? _lastWrittenOffsetCh1 : _lastWrittenOffsetCh2;

            string readText = readOffset.ToString("0.0", CultureInfo.InvariantCulture);
            if (double.IsNaN(lastWritten))
            {
                TraceModbus($"OFFSET READ ch={channel} read={readText} ackResp=0x{response:X4} lastWritten=NaN");
                return;
            }

            double diff = Math.Abs(readOffset - lastWritten);
            if (diff > OffsetReadbackMismatchEpsilon)
            {
                TraceModbus($"OFFSET READ MISMATCH ch={channel} read={readText} lastWritten={lastWritten.ToString("0.0", CultureInfo.InvariantCulture)} diff={diff.ToString("0.000", CultureInfo.InvariantCulture)} ackResp=0x{response:X4}");
            }
            else
            {
                TraceModbus($"OFFSET READ ch={channel} read={readText} lastWritten={lastWritten.ToString("0.0", CultureInfo.InvariantCulture)} ackResp=0x{response:X4}");
            }
        }
    }
}
