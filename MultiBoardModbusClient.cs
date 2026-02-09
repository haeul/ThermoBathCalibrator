using NModbus;
using System;
using System.Net.Sockets;
using System.Diagnostics;

namespace ThermoBathCalibrator
{
    public sealed class MultiBoardModbusClient : IDisposable
    {
        private TcpClient? _tcp;
        private IModbusMaster? _master;
        private readonly object _sync = new object();

        public Action<string>? Trace { get; set; }

        public string Host { get; private set; }
        public int Port { get; private set; }
        public byte UnitId { get; private set; }

        public bool IsConnected =>
            _tcp != null && _tcp.Connected && _master != null;

        public MultiBoardModbusClient(string host, int port, byte unitId = 1)
        {
            Host = host;
            Port = port;
            UnitId = unitId;
        }

        public void UpdateEndpoint(string host, int port, byte unitId = 1)
        {
            Host = host;
            Port = port;
            UnitId = unitId;
        }

        public bool TryConnect(out string error)
        {
            error = string.Empty;

            try
            {
                lock (_sync)
                {
                    DisconnectCore();

                    _tcp = new TcpClient();
                    _tcp.NoDelay = true;
                    _tcp.ReceiveTimeout = 1000;
                    _tcp.SendTimeout = 1000;

                    var sw = Stopwatch.StartNew();
                    _tcp.Connect(Host, Port);
                    sw.Stop();

                    var factory = new ModbusFactory();
                    _master = factory.CreateMaster(_tcp);

                    Trace?.Invoke($"CONNECT OK {Host}:{Port} Unit={UnitId} {sw.ElapsedMilliseconds}ms");
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Trace?.Invoke($"CONNECT FAIL {Host}:{Port} Unit={UnitId} err={ex.GetType().Name}:{ex.Message}");
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            lock (_sync)
            {
                DisconnectCore();
            }
        }

        private void DisconnectCore()
        {
            try { _master = null; } catch { }

            try
            {
                if (_tcp != null)
                {
                    _tcp.Close();
                    _tcp.Dispose();
                }
            }
            catch { }
            finally
            {
                _tcp = null;
            }
        }

        public bool TryReadHoldingRegisters(ushort start, ushort count, out ushort[] regs, out string error)
        {
            regs = Array.Empty<ushort>();
            error = string.Empty;

            if (!IsConnected)
            {
                error = "Not connected.";
                return false;
            }

            try
            {
                lock (_sync)
                {
                    var sw = Stopwatch.StartNew();
                    // NModbus는 ushort[] 반환
                    regs = _master!.ReadHoldingRegisters(UnitId, start, count);
                    sw.Stop();
                    Trace?.Invoke($"FC03 start={start} count={count} ok {sw.ElapsedMilliseconds}ms");
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Trace?.Invoke($"FC03 start={start} count={count} fail err={ex.GetType().Name}:{ex.Message}");
                return false;
            }
        }

        public bool TryWriteMultipleRegisters(ushort start, ushort[] values, out string error)
        {
            error = string.Empty;

            if (!IsConnected)
            {
                error = "Not connected.";
                return false;
            }

            try
            {
                lock(_sync)
                {
                    var sw = Stopwatch.StartNew();
                    _master!.WriteMultipleRegisters(UnitId, start, values);
                    sw.Stop();
                    Trace?.Invoke($"FC10 start={start} values=[{string.Join(", ", values)}] ok {sw.ElapsedMilliseconds}ms");
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Trace?.Invoke($"FC10 start={start} values=[{string.Join(", ", values)}] fail err={ex.GetType().Name}:{ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
