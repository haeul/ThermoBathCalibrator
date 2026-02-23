using System;
using System.Globalization;
using System.Linq;
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

        // WRITE QUEUE PATCH START
        private bool TryWriteChannelOffset(int channel, double appliedOffset, string reason = "UNSPECIFIED")
        {
            string source = reason != null && reason.IndexOf("MANUAL", StringComparison.OrdinalIgnoreCase) >= 0
                ? "MANUAL"
                : "AUTO";

            if (source == "AUTO")
            {
                bool existsSameChannel = _writeQueue.Any(q => q.Channel == channel);
                if (existsSameChannel)
                {
                    TraceModbus($"[QUEUE ENQUEUE] src=AUTO ch={channel} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} skip=duplicate");
                    return true;
                }
            }

            if (source == "MANUAL")
            {
                RemovePendingAutoRequestsForChannel(channel);
            }

            var request = new OffsetWriteRequest
            {
                Channel = channel,
                DesiredOffset = appliedOffset,
                Source = source,
                RequestedAt = DateTime.Now
            };

            _writeQueue.Enqueue(request);
            _writeSignal.Set();
            TraceModbus($"[QUEUE ENQUEUE] src={request.Source} ch={request.Channel} desired={request.DesiredOffset.ToString("0.0", CultureInfo.InvariantCulture)}");
            return true;
        }

        private void RemovePendingAutoRequestsForChannel(int channel)
        {
            if (_writeQueue.IsEmpty) return;

            var kept = new System.Collections.Generic.List<OffsetWriteRequest>();
            while (_writeQueue.TryDequeue(out OffsetWriteRequest pending))
            {
                bool remove = pending.Channel == channel && string.Equals(pending.Source, "AUTO", StringComparison.OrdinalIgnoreCase);
                if (remove)
                {
                    TraceModbus($"[QUEUE ENQUEUE] src=MANUAL ch={channel} droppedPendingAutoAt={pending.RequestedAt:O}");
                    continue;
                }

                kept.Add(pending);
            }

            foreach (OffsetWriteRequest req in kept)
                _writeQueue.Enqueue(req);
        }

        private bool TryDequeueAndExecuteWriteRequest()
        {
            if (!_writeQueue.TryDequeue(out OffsetWriteRequest request))
                return false;

            TraceModbus($"[QUEUE DEQUEUE] src={request.Source} ch={request.Channel} desired={request.DesiredOffset.ToString("0.0", CultureInfo.InvariantCulture)}");
            return ExecuteOffsetWriteRequest(request);
        }

        private bool ExecuteOffsetWriteRequest(OffsetWriteRequest request)
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
                        TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} desired={request.DesiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} result=skip_not_connected");
                        ShowOffsetApplyStatus(channel: request.Channel, offset: request.DesiredOffset, success: false);
                        return false;
                    }

                    double appliedOffset = OffsetMath.Clamp(request.DesiredOffset, _autoCfg.OffsetClampMin, _autoCfg.OffsetClampMax);
                    short raw10 = (short)Math.Round(appliedOffset * 10.0, MidpointRounding.AwayFromZero);
                    ushort offsetWord = unchecked((ushort)raw10);
                    ushort cmdStart = request.Channel == 1 ? RegCh1Command : RegCh2Command;
                    ushort offReg = (ushort)(cmdStart + 2);
                    const int holdMs = 120;

                    TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} step=cmd_clear");
                    if (!TryWriteSingleRegister(cmdStart, 0, out string errClear))
                    {
                        TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} step=cmd_clear fail err={errClear}");
                        ShowOffsetApplyStatus(channel: request.Channel, offset: appliedOffset, success: false);
                        return false;
                    }

                    TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} step=offset_write reg={offReg}");
                    if (!TryWriteSingleRegister(offReg, offsetWord, out string errOffWrite))
                    {
                        TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} step=offset_write fail err={errOffWrite}");
                        ShowOffsetApplyStatus(channel: request.Channel, offset: appliedOffset, success: false);
                        return false;
                    }

                    TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} step=cmd_up");
                    if (!TryWriteSingleRegister(cmdStart, 0x0002, out string errCmdUp))
                    {
                        TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} step=cmd_up fail err={errCmdUp}");
                        ShowOffsetApplyStatus(channel: request.Channel, offset: appliedOffset, success: false);
                        return false;
                    }

                    Thread.Sleep(holdMs);

                    TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} step=cmd_down holdMs={holdMs}");
                    if (!TryWriteSingleRegister(cmdStart, 0, out string errCmdDown))
                    {
                        TraceModbus($"[WRITE EXECUTE] src={request.Source} ch={request.Channel} step=cmd_down fail err={errCmdDown}");
                        ShowOffsetApplyStatus(channel: request.Channel, offset: appliedOffset, success: false);
                        return false;
                    }

                    bool verified = TryReadbackAfterWrite(channel: request.Channel, desiredOffset: appliedOffset, out double readback);
                    bool success = verified && Math.Abs(readback - appliedOffset) <= OffsetReadbackMismatchEpsilon;
                    TraceModbus($"[WRITE VERIFY] src={request.Source} ch={request.Channel} desired={appliedOffset.ToString("0.0", CultureInfo.InvariantCulture)} readback={readback.ToString("0.0", CultureInfo.InvariantCulture)} success={success}");

                    if (success)
                    {
                        lock (_offsetStateSync)
                        {
                            if (request.Channel == 1) _bath1OffsetCur = readback;
                            else if (request.Channel == 2) _bath2OffsetCur = readback;
                        }

                        if (request.Channel == 1)
                        {
                            _lastWrittenOffsetCh1 = readback;
                            _lastWriteCh1 = DateTime.Now;
                        }
                        else if (request.Channel == 2)
                        {
                            _lastWrittenOffsetCh2 = readback;
                            _lastWriteCh2 = DateTime.Now;
                        }

                        ShowOffsetApplyStatus(channel: request.Channel, offset: readback, success: true);
                        BeginInvoke(new Action(() => UpdateOffsetUiFromState()));
                        return true;
                    }

                    if (request.Channel == 1) _lastWrittenOffsetCh1 = appliedOffset;
                    else if (request.Channel == 2) _lastWrittenOffsetCh2 = appliedOffset;

                    ShowOffsetApplyStatus(channel: request.Channel, offset: appliedOffset, success: false);
                    return false;
                }
                finally
                {
                    _inWriteSequence = false;
                }
            }
        }
        // WRITE QUEUE PATCH END
        // SV/Offset 분리 + cmd 펄스(0→cmd→0) 보장
        // - OffsetOnly면 offReg만 write
        // - SvOnly면 svReg만 write
        // - Command는 항상 0->cmd->0으로 내려준다 (로그/FC10으로 확인 가능)
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
            error = string.Empty;

            ushort cmdStart = channel == 1 ? RegCh1Command : RegCh2Command;

            // 문서 기준 레이아웃(연속):
            // [Command] [SV Setting Value] [Offset Setting Value]
            ushort svReg = (ushort)(cmdStart + 1);
            ushort offReg = (ushort)(cmdStart + 2);

            ushort cmd = 0;
            if (kind == WriteKind.SvOnly) cmd = 0x0001;      // bit0
            if (kind == WriteKind.OffsetOnly) cmd = 0x0002;  // bit1

            int ackMask = cmd & 0x0003;
            if (ackMask == 0)
            {
                TraceModbus($"OFFSET WRITE SKIP ACK ch={channel} reason={reason} kind={kind} cmd=0x{cmd:X4}");
                return true;
            }

            // 0) response pre-read (stale 판단/진단용)
            ushort beforeResp = 0;
            bool hasBeforeResp = TryReadResponseRegister(channel, out beforeResp, out string errBeforeResp);
            if (!hasBeforeResp)
                TraceModbus($"ACK PRE-READ FAIL ch={channel} reason={reason} err={errBeforeResp}");
            else
                TraceModbus($"ACK PRE-READ ch={channel} reason={reason} before=0x{beforeResp:X4}");

            // 1) Command 0으로 클리어
            if (!TryWriteSingleRegister(cmdStart, 0, out string errClear))
            {
                error = $"CMD CLEAR FAIL: {errClear}";
                TraceModbus($"OFFSET CMD CLEAR FAIL ch={channel} reason={reason} reg={cmdStart} err={errClear}");
                return false;
            }
            TraceModbus($"OFFSET CMD CLEAR OK ch={channel} reason={reason} reg={cmdStart} value=0x0000");

            // 2) 값 write (SV만 또는 Offset만)
            if (kind == WriteKind.SvOnly)
            {
                if (!TryWriteSingleRegister(svReg, svWord, out string errSvWrite))
                {
                    error = $"SV WRITE FAIL: {errSvWrite}";
                    TraceModbus($"SV WRITE FAIL ch={channel} reason={reason} reg={svReg} sv=0x{svWord:X4} err={errSvWrite}");
                    return false;
                }
                TraceModbus($"SV WRITE OK ch={channel} reason={reason} reg={svReg} sv=0x{svWord:X4}");
            }
            else // OffsetOnly
            {
                if (!TryWriteSingleRegister(offReg, offsetWord, out string errOffWrite))
                {
                    error = $"OFF WRITE FAIL: {errOffWrite}";
                    TraceModbus($"OFF WRITE FAIL ch={channel} reason={reason} reg={offReg} off=0x{offsetWord:X4} err={errOffWrite}");
                    return false;
                }
                TraceModbus($"OFF WRITE OK ch={channel} reason={reason} reg={offReg} off=0x{offsetWord:X4} raw10={raw10}");
            }

            // 3) Command 올리기 (트리거)
            if (!TryWriteSingleRegister(cmdStart, cmd, out string errCmdUp))
            {
                error = $"CMD UP FAIL: {errCmdUp}";
                TraceModbus($"OFFSET CMD UP FAIL ch={channel} reason={reason} reg={cmdStart} cmd=0x{cmd:X4} err={errCmdUp}");
                return false;
            }
            TraceModbus($"OFFSET CMD UP OK ch={channel} reason={reason} reg={cmdStart} cmd=0x{cmd:X4} kind={kind}");

            // 3.5) cmd 펄스 마무리: cmd -> 0 (여기서 반드시 0으로 내림)
            const int CmdPulseHoldMs = 120; // 필요 시 50~200ms 조절
            Thread.Sleep(CmdPulseHoldMs);

            TraceModbus($"OFFSET CMD DOWN TRY ch={channel} reason={reason} reg={cmdStart} value=0x0000 holdMs={CmdPulseHoldMs}");

            if (!TryWriteSingleRegister(cmdStart, 0, out string errCmdDown))
            {
                error = $"CMD DOWN FAIL: {errCmdDown}";
                TraceModbus($"OFFSET CMD DOWN FAIL ch={channel} reason={reason} reg={cmdStart} err={errCmdDown}");
                return false;
            }
            TraceModbus($"OFFSET CMD DOWN OK ch={channel} reason={reason} reg={cmdStart} value=0x0000 holdMs={CmdPulseHoldMs}");

            if (TryReadCommandRegister(channel, out ushort cmdReadback, out string errCmdReadback))
                TraceModbus($"OFFSET CMD DOWN READBACK ch={channel} reason={reason} reg={cmdStart} value=0x{cmdReadback:X4} isZero={(cmdReadback == 0)}");
            else
                TraceModbus($"OFFSET CMD DOWN READBACK FAIL ch={channel} reason={reason} reg={cmdStart} err={errCmdReadback}");

            // 4) ACK 대기
            Thread.Sleep(AckInitialDelayMs);

            DateTime startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < AckTimeoutMs)
            {
                if (TryReadResponseRegister(channel, out ushort resp, out string errResp))
                {
                    bool ackSet = (resp & ackMask) == ackMask;
                    bool staleAck = hasBeforeResp && ((beforeResp & ackMask) == ackMask) && resp == beforeResp;

                    if (ackSet)
                    {
                        if (staleAck)
                        {
                            if (_allowStaleAck)
                            {
                                TraceModbus($"OFFSET WRITE ACK STALE-ACCEPT ch={channel} reason={reason} before=0x{beforeResp:X4} now=0x{resp:X4} ackMask=0x{ackMask:X4}");
                            }
                            else
                            {
                                TraceModbus($"OFFSET WRITE ACK STALE-REJECT ch={channel} reason={reason} before=0x{beforeResp:X4} now=0x{resp:X4} ackMask=0x{ackMask:X4}");
                                Thread.Sleep(AckPollIntervalMs);
                                continue;
                            }
                        }

                        TraceModbus($"OFFSET WRITE ACK OK ch={channel} reason={reason} kind={kind} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} ackResp=0x{resp:X4} ackMask=0x{ackMask:X4}");
                        return true;
                    }
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

        // (참고) 기존 TryWriteAndWaitAck는 유지해도 되고, 사용 안 하면 그대로 둬도 됨.
        // private bool TryWriteAndWaitAck(...) { ... }

        private bool TryWriteAndWaitAck(int channel, ushort cmd, ushort svWord, ushort offsetWord, string reason, double desiredOffset, short raw10, out string error)
        {
            error = string.Empty;

            ushort cmdStart = channel == 1 ? RegCh1Command : RegCh2Command;

            // 문서 기준 레이아웃(연속):
            // [Command] [SV Setting Value] [Offset Setting Value]
            ushort svStart = (ushort)(cmdStart + 1);
            ushort offStart = (ushort)(cmdStart + 2);

            int ackMask = cmd & 0x0003;
            if (ackMask == 0)
            {
                TraceModbus($"OFFSET WRITE SKIP ACK ch={channel} reason={reason} cmd=0x{cmd:X4}");
                return true;
            }

            // 0) response pre-read (stale 판단용)
            ushort beforeResp = 0;
            bool hasBeforeResp = TryReadResponseRegister(channel, out beforeResp, out string errBeforeResp);
            if (!hasBeforeResp)
                TraceModbus($"ACK PRE-READ FAIL ch={channel} reason={reason} err={errBeforeResp}");

            // 1) Command만 0으로 내리기
            if (!TryWriteSingleRegister(cmdStart, 0, out string errClear))
            {
                error = $"CMD CLEAR FAIL: {errClear}";
                TraceModbus($"OFFSET CMD CLEAR FAIL ch={channel} reason={reason} reg={cmdStart} err={errClear}");
                return false;
            }
            TraceModbus($"OFFSET CMD CLEAR OK ch={channel} reason={reason} reg={cmdStart} value=0x0000");

            // 2) 값 먼저 세팅 (SV/Offset)
            ushort[] valuesPayload = new ushort[] { svWord, offsetWord };
            if (!_mb.TryWriteMultipleRegisters(svStart, valuesPayload, out string errValueWrite))
            {
                error = $"VALUE WRITE FAIL: {errValueWrite}";
                TraceModbus($"OFFSET VALUE WRITE FAIL ch={channel} reason={reason} svReg={svStart} offReg={offStart} sv=0x{svWord:X4} off=0x{offsetWord:X4} err={errValueWrite}");
                return false;
            }
            TraceModbus($"OFFSET VALUE WRITE OK ch={channel} reason={reason} svReg={svStart} offReg={offStart} sv=0x{svWord:X4} off=0x{offsetWord:X4}");

            // 3) Command 올리기 (트리거)
            if (!TryWriteSingleRegister(cmdStart, cmd, out string errCmdWrite))
            {
                error = $"CMD WRITE FAIL: {errCmdWrite}";
                TraceModbus($"OFFSET CMD WRITE FAIL ch={channel} reason={reason} reg={cmdStart} cmd=0x{cmd:X4} err={errCmdWrite}");
                return false;
            }
            TraceModbus($"OFFSET CMD WRITE OK ch={channel} reason={reason} reg={cmdStart} cmd=0x{cmd:X4}");

            // 3.5) 펄스 마무리(2 -> 0)
            const int CmdPulseHoldMs = 120; // 필요 시 50~200ms 범위로 조절
            Thread.Sleep(CmdPulseHoldMs);

            TraceModbus($"OFFSET CMD DOWN TRY ch={channel} reason={reason} reg={cmdStart} value=0x0000 holdMs={CmdPulseHoldMs}");

            if (!TryWriteSingleRegister(cmdStart, 0, out string errCmdDown))
            {
                error = $"CMD DOWN FAIL: {errCmdDown}";
                TraceModbus($"OFFSET CMD DOWN FAIL ch={channel} reason={reason} reg={cmdStart} err={errCmdDown}");
                return false;
            }
            TraceModbus($"OFFSET CMD DOWN OK ch={channel} reason={reason} reg={cmdStart} value=0x0000 holdMs={CmdPulseHoldMs}");

            if (TryReadCommandRegister(channel, out ushort cmdReadback, out string errCmdReadback))
                TraceModbus($"OFFSET CMD DOWN READBACK ch={channel} reason={reason} reg={cmdStart} value=0x{cmdReadback:X4} isZero={(cmdReadback == 0)}");
            else
                TraceModbus($"OFFSET CMD DOWN READBACK FAIL ch={channel} reason={reason} reg={cmdStart} err={errCmdReadback}");

            // 4) ACK 대기
            Thread.Sleep(AckInitialDelayMs);

            DateTime startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < AckTimeoutMs)
            {
                if (TryReadResponseRegister(channel, out ushort resp, out string errResp))
                {
                    bool ackSet = (resp & ackMask) == ackMask;
                    bool staleAck = hasBeforeResp && ((beforeResp & ackMask) == ackMask) && resp == beforeResp;

                    if (ackSet)
                    {
                        if (staleAck)
                        {
                            if (_allowStaleAck)
                            {
                                TraceModbus($"OFFSET WRITE ACK STALE-ACCEPT ch={channel} reason={reason} before=0x{beforeResp:X4} now=0x{resp:X4} ackMask=0x{ackMask:X4}");
                            }
                            else
                            {
                                TraceModbus($"OFFSET WRITE ACK STALE-REJECT ch={channel} reason={reason} before=0x{beforeResp:X4} now=0x{resp:X4} ackMask=0x{ackMask:X4}");
                                Thread.Sleep(AckPollIntervalMs);
                                continue;
                            }
                        }
                        TraceModbus($"OFFSET WRITE ACK OK ch={channel} reason={reason} desired={desiredOffset.ToString("0.0", CultureInfo.InvariantCulture)} raw10={raw10} ackResp=0x{resp:X4} ackMask=0x{ackMask:X4}");
                        return true;
                    }
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