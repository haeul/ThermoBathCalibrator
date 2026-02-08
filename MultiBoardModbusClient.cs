using NModbus;
using System;
using System.Net.Sockets;

namespace ThermoBathCalibrator
{
    public sealed class MultiBoardModbusClient : IDisposable
    {
        private TcpClient? _tcp;
        private IModbusMaster? _master;

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
                Disconnect();

                _tcp = new TcpClient();
                _tcp.NoDelay = true;
                _tcp.ReceiveTimeout = 1000;
                _tcp.SendTimeout = 1000;

                _tcp.Connect(Host, Port);

                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_tcp);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
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
                // NModbus는 ushort[] 반환
                regs = _master!.ReadHoldingRegisters(UnitId, start, count);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
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
                _master!.WriteMultipleRegisters(UnitId, start, values);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
