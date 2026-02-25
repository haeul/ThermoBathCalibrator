using System;
using System.Globalization;
using System.Threading;
using ThermoBathCalibrator.Controller;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        // SV/Offset 분리 쓰기 구분용
        private enum WriteKind
        {
            SvOnly,
            OffsetOnly
        }

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
            if (string.IsNullOrWhiteSpace(_host)) _host = "192.168.0.41";

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

            _bath1FineTarget = _bath1Setpoint;
            _bath2FineTarget = _bath2Setpoint;
            _trackedCoarseSvCh1 = _bath1Setpoint;
            _trackedCoarseSvCh2 = _bath2Setpoint;
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

            // FIELD PATCH START
            if (_inWriteSequence)
            {
                TraceModbus("READ POLL SKIP reason=in_write_sequence");
                return false;
            }
            // FIELD PATCH END

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

        // 변경: TryWriteChannelOffset에서 "Offset만" 쓰도록 분리 호출
        // FIELD PATCH START
        private bool TryWriteChannelOffset(int channel, double appliedOffset, string reason = "UNSPECIFIED")
        {
            lock (_offsetWriteSequenceSync)
            {
                _inWriteSequence = true;
                try
                {
                    if (!_mb.IsConnected)
                    {
                        _boardConnected = TryConnectWithCooldown();
                        if (!_boardConnected) _boardFailCount++;
                    }

                    if (!_mb.IsConnected)
                    {
                        _boardConnected = false;
                        TraceModbus($"[WRITE RESULT] FAIL reason=not_connected src={reason} channel={channel}");
                        ShowOffsetApplyStatus(channel: channel, offset: appliedOffset, success: false);
                        return false;
                    }

                    appliedOffset = OffsetMath.Clamp(appliedOffset, _autoCfg.OffsetClampMin, _autoCfg.OffsetClampMax);

                    short raw10 = (short)Math.Round(appliedOffset * 10.0, MidpointRounding.AwayFromZero);
                    ushort offsetWord = unchecked((ushort)raw10);

                    double currentRead = channel == 1 ? _bath1OffsetCur : _bath2OffsetCur;
                    string src = reason != null && reason.StartsWith("MANUAL", StringComparison.OrdinalIgnoreCase) ? "MANUAL" : "AUTO";
                    TraceModbus($"[WRITE START] src={src} channel={channel} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} currentRead={currentRead.ToString("0.0", CultureInfo.InvariantCulture)} thread={Thread.CurrentThread.ManagedThreadId}");
                    TraceModbus("[INFO] ACK disabled - using readback verification only");

                    if (!TryWriteOffsetSequenceNoAck(channel, offsetWord, reason, raw10, out string sequenceErr))
                    {
                        TraceModbus($"[WRITE RESULT] FAIL reason={sequenceErr} src={src} channel={channel}");
                        ShowOffsetApplyStatus(channel: channel, offset: appliedOffset, success: false);
                        return false;
                    }

                    bool verified = TryReadbackAfterWrite(channel: channel, desiredOffset: appliedOffset, out double readback);
                    TraceModbus($"[WRITE VERIFY] readback={readback.ToString("0.0", CultureInfo.InvariantCulture)} success={verified.ToString().ToLowerInvariant()}");

                    if (!verified)
                    {
                        if (channel == 1) _lastWrittenOffsetCh1 = appliedOffset;
                        else _lastWrittenOffsetCh2 = appliedOffset;

                        ShowOffsetApplyStatus(channel: channel, offset: appliedOffset, success: false);
                        TraceModbus($"[WRITE RESULT] FAIL reason=readback_mismatch src={src} channel={channel} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)}");
                        return false;
                    }

                    lock (_offsetStateSync)
                    {
                        if (channel == 1) _bath1OffsetCur = readback;
                        else _bath2OffsetCur = readback;
                    }

                    if (channel == 1)
                    {
                        _lastWrittenOffsetCh1 = readback;
                        _lastWriteCh1 = DateTime.Now;
                    }
                    else
                    {
                        _lastWrittenOffsetCh2 = readback;
                        _lastWriteCh2 = DateTime.Now;
                    }

                    if (src == "MANUAL")
                    {
                        DateTime holdUntil = DateTime.Now.AddSeconds(60);
                        if (channel == 1) _manualHoldUntilCh1 = holdUntil;
                        else _manualHoldUntilCh2 = holdUntil;
                        TraceModbus($"MANUAL HOLD SET ch={channel} until={holdUntil:O}");
                    }

                    ShowOffsetApplyStatus(channel: channel, offset: readback, success: true);
                    BeginInvoke(new Action(() => UpdateOffsetUiFromState()));
                    TraceModbus($"[WRITE RESULT] SUCCESS reason=readback_verified src={src} channel={channel}");
                    return true;
                }
                finally
                {
                    _inWriteSequence = false;
                }
            }
        }
        // FIELD PATCH END

        // SV/Offset 분리 + cmd 펄스(0→cmd→0) 보장
        // FIELD PATCH START
        private bool TryWriteOffsetSequenceNoAck(
            int channel,
            ushort offsetWord,
            string reason,
            short raw10,
            out string error)
        {
            error = string.Empty;

            ushort cmdStart = channel == 1 ? RegCh1Command : RegCh2Command;
            ushort offReg = (ushort)(cmdStart + 2);
            const ushort cmd = 0x0002;

            if (!TryWriteSingleRegister(cmdStart, 0, out string errClear))
            {
                error = $"CMD_CLEAR_FAIL:{errClear}";
                return false;
            }

            if (!TryWriteSingleRegister(offReg, offsetWord, out string errOffWrite))
            {
                error = $"OFFSET_WRITE_FAIL:{errOffWrite}";
                return false;
            }
            TraceModbus($"OFF WRITE OK ch={channel} reason={reason} reg={offReg} off=0x{offsetWord:X4} raw10={raw10}");

            if (!TryWriteSingleRegister(cmdStart, cmd, out string errCmdUp))
            {
                error = $"CMD_UP_FAIL:{errCmdUp}";
                return false;
            }

            const int CmdPulseHoldMs = 120;
            Thread.Sleep(CmdPulseHoldMs);

            if (!TryWriteSingleRegister(cmdStart, 0, out string errCmdDown))
            {
                error = $"CMD_DOWN_FAIL:{errCmdDown}";
                return false;
            }

            return true;
        }

        private bool TryWriteSvSequenceNoAck(
            int channel,
            ushort svWord,
            string reason,
            short raw10,
            out string error)
        {
            error = string.Empty;

            ushort cmdStart = channel == 1 ? RegCh1Command : RegCh2Command;
            ushort svReg = (ushort)(cmdStart + 1);
            const ushort cmd = 0x0001;

            if (!TryWriteSingleRegister(cmdStart, 0, out string errClear))
            {
                error = $"CMD_CLEAR_FAIL:{errClear}";
                return false;
            }

            if (!TryWriteSingleRegister(svReg, svWord, out string errSvWrite))
            {
                error = $"SV_WRITE_FAIL:{errSvWrite}";
                return false;
            }
            TraceModbus($"SV WRITE OK ch={channel} reason={reason} reg={svReg} sv=0x{svWord:X4} raw10={raw10}");

            if (!TryWriteSingleRegister(cmdStart, cmd, out string errCmdUp))
            {
                error = $"CMD_UP_FAIL:{errCmdUp}";
                return false;
            }

            const int CmdPulseHoldMs = 120;
            Thread.Sleep(CmdPulseHoldMs);

            if (!TryWriteSingleRegister(cmdStart, 0, out string errCmdDown))
            {
                error = $"CMD_DOWN_FAIL:{errCmdDown}";
                return false;
            }

            return true;
        }

        private bool TryWriteChannelSvCoarseInternal(int channel, double coarseSv, string reason)
        {
            lock (_offsetWriteSequenceSync)
            {
                _inWriteSequence = true;
                try
                {
                    if (!_mb.IsConnected)
                    {
                        _boardConnected = TryConnectWithCooldown();
                        if (!_boardConnected) _boardFailCount++;
                    }

                    if (!_mb.IsConnected)
                    {
                        _boardConnected = false;
                        TraceModbus($"[SV WRITE RESULT] FAIL reason=not_connected src={reason} channel={channel}");
                        return false;
                    }

                    short raw10 = (short)Math.Round(coarseSv * 10.0, MidpointRounding.AwayFromZero);
                    ushort svWord = unchecked((ushort)raw10);

                    TraceModbus($"[SV WRITE START] src={reason} channel={channel} desired={coarseSv.ToString("0.0", CultureInfo.InvariantCulture)} thread={Thread.CurrentThread.ManagedThreadId}");
                    TraceModbus("[INFO] ACK disabled - using readback verification only");

                    if (!TryWriteSvSequenceNoAck(channel, svWord, reason, raw10, out string sequenceErr))
                    {
                        TraceModbus($"[SV WRITE RESULT] FAIL reason={sequenceErr} src={reason} channel={channel}");
                        return false;
                    }

                    if (channel == 1)
                    {
                        _bath1Setpoint = coarseSv;
                        _trackedCoarseSvCh1 = coarseSv;
                    }
                    else
                    {
                        _bath2Setpoint = coarseSv;
                        _trackedCoarseSvCh2 = coarseSv;
                    }

                    TraceModbus($"[SV WRITE RESULT] SUCCESS src={reason} channel={channel} coarse={coarseSv.ToString("0.0", CultureInfo.InvariantCulture)}");
                    return true;
                }
                finally
                {
                    _inWriteSequence = false;
                }
            }
        }

        private bool TryWriteChannelSvCoarseFromSettings(int channel, double coarseSv)
        {
            coarseSv = Math.Round(coarseSv, 1, MidpointRounding.AwayFromZero);
            return TryWriteChannelSvCoarseInternal(channel, coarseSv, "SETTINGS_DIRECT_COARSE");
        }

        private static double ToCoarseSv(double fineTarget)
        {
            decimal scaled = decimal.Truncate((decimal)fineTarget * 10m);
            return (double)(scaled / 10m);
        }

        private void UpdateFineTargetAndMaybeWriteCoarse(int channel, double fineTarget, string reason)
        {
            double coarse = ToCoarseSv(fineTarget);
            bool needWrite;

            if (channel == 1)
            {
                double prevCoarse = double.IsNaN(_trackedCoarseSvCh1) ? _bath1Setpoint : _trackedCoarseSvCh1;
                _bath1FineTarget = fineTarget;
                _autoCfg.TargetTemperature = AverageOrNaN(_bath1FineTarget, _bath2FineTarget);
                needWrite = double.IsNaN(prevCoarse) || Math.Abs(prevCoarse - coarse) > 0.049;
            }
            else
            {
                double prevCoarse = double.IsNaN(_trackedCoarseSvCh2) ? _bath2Setpoint : _trackedCoarseSvCh2;
                _bath2FineTarget = fineTarget;
                _autoCfg.TargetTemperature = AverageOrNaN(_bath1FineTarget, _bath2FineTarget);
                needWrite = double.IsNaN(prevCoarse) || Math.Abs(prevCoarse - coarse) > 0.049;
            }

            if (!needWrite)
                return;

            _ = TryWriteChannelSvCoarseInternal(channel, coarse, reason);
        }

        private bool TryWriteAndWaitAck_Split(
            int channel,
            WriteKind kind,
            ushort svWord,
            ushort offsetWord,
            string reason,
            double desiredOffset,
            short raw10,
            out string error)
        {
            TraceModbus("[INFO] ACK disabled - using readback verification only");
            error = "ACK_DISABLED";
            return false;
        }
        // FIELD PATCH END

        // (참고) 기존 TryWriteAndWaitAck는 유지해도 되고, 사용 안 하면 그대로 둬도 됨.
        // private bool TryWriteAndWaitAck(...) { ... }

        private bool TryWriteAndWaitAck(int channel, ushort cmd, ushort svWord, ushort offsetWord, string reason, double desiredOffset, short raw10, out string error)
        {
            TraceModbus("[INFO] ACK disabled - using readback verification only");
            error = "ACK_DISABLED";
            return false;
        }

        private bool TryWriteSingleRegister(ushort start, ushort value, out string error)
        {
            error = string.Empty;

            // FC10 count=1로 통일
            ushort[] payload = new ushort[] { value };
            if (!_mb.IsConnected)
            {
                error = "Not connected.";
                TraceModbus($"FC10 TRY start={start} values=[{value}] fail reason=not_connected");
                return false;
            }

            TraceModbus($"FC10 TRY start={start} values=[{value}]");
            bool ok = _mb.TryWriteMultipleRegisters(start, payload, out string err);
            if (!ok)
            {
                error = err;
                TraceModbus($"FC10 FAIL start={start} values=[{value}] err={err}");
                return false;
            }

            return true;
        }

        private bool TryReadCommandRegister(int channel, out ushort command, out string error)
        {
            command = 0;
            error = string.Empty;

            ushort cmdStart = channel == 1 ? RegCh1Command : RegCh2Command;
            if (!_mb.TryReadHoldingRegisters(cmdStart, 1, out ushort[] regs, out string err))
            {
                error = err;
                return false;
            }

            command = regs.Length > 0 ? regs[0] : (ushort)0;
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