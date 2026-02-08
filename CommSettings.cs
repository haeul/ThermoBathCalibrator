using System;
using System.IO;
using System.Text.Json;

namespace ThermoBathCalibrator
{
    public sealed class CommSettings
    {
        public BathPortSettings Bath1 { get; set; } = BathPortSettings.CreateDefault(address: 1);
        public BathPortSettings Bath2 { get; set; } = BathPortSettings.CreateDefault(address: 2);
        public UtOnePortSettings UtOne { get; set; } = UtOnePortSettings.CreateDefault();

        // 추가: 멀티보드(Modbus TCP) 설정
        public MultiBoardTcpSettings MultiBoard { get; set; } = MultiBoardTcpSettings.CreateDefault();

        public static string GetDefaultPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data");
            Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, "comm_settings.json");
        }

        public static CommSettings LoadOrDefault(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return new CommSettings();

                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<CommSettings>(json);
                return loaded ?? new CommSettings();
            }
            catch
            {
                return new CommSettings();
            }
        }

        public void Save(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var opt = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(this, opt);
            File.WriteAllText(path, json);
        }

        public CommSettings Clone()
        {
            string json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<CommSettings>(json) ?? new CommSettings();
        }
    }

    public class SerialPortSettings
    {
        public string PortName { get; set; } = "";
        public int BaudRate { get; set; } = 9600;

        // "None", "Odd", "Even"
        public string Parity { get; set; } = "None";

        public int DataBits { get; set; } = 8;

        // "One", "Two"
        public string StopBits { get; set; } = "One";

        public int ReadTimeoutMs { get; set; } = 800;
        public int WriteTimeoutMs { get; set; } = 800;
    }

    public class BathPortSettings : SerialPortSettings
    {
        public int Address { get; set; } = 1;          // 1~99
        public bool UseBcc { get; set; } = true;       // 장비 설정과 일치
        public int ResponseDelayMs { get; set; } = 2;  // 요청 간 최소 대기(권장 1ms 이상이어서 기본 2ms)

        public static BathPortSettings CreateDefault(int address)
        {
            return new BathPortSettings
            {
                BaudRate = 9600,
                Parity = "None",
                DataBits = 8,
                StopBits = "One",
                ReadTimeoutMs = 800,
                WriteTimeoutMs = 800,

                Address = address,
                UseBcc = true,
                ResponseDelayMs = 2
            };
        }

        public BathPortSettings Clone()
        {
            string json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<BathPortSettings>(json) ?? CreateDefault(Address);
        }
    }

    public class UtOnePortSettings : SerialPortSettings
    {
        // "LF" or "CRLF"
        public string LineTerminator { get; set; } = "LF";

        public static UtOnePortSettings CreateDefault()
        {
            return new UtOnePortSettings
            {
                BaudRate = 38400,
                Parity = "Odd",
                DataBits = 8,
                StopBits = "One",
                ReadTimeoutMs = 800,
                WriteTimeoutMs = 800,
                LineTerminator = "LF"
            };
        }

        public UtOnePortSettings Clone()
        {
            string json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<UtOnePortSettings>(json) ?? CreateDefault();
        }
    }

    // 추가: 멀티보드(Modbus TCP) 설정 클래스
    public sealed class MultiBoardTcpSettings
    {
        public string Host { get; set; } = "192.168.1.11";
        public int Port { get; set; } = 13000;
        public int UnitId { get; set; } = 1;

        public int ConnectTimeoutMs { get; set; } = 1000;
        public int ReadTimeoutMs { get; set; } = 1000;
        public int WriteTimeoutMs { get; set; } = 1000;

        public static MultiBoardTcpSettings CreateDefault()
        {
            return new MultiBoardTcpSettings
            {
                Host = "192.168.1.11",
                Port = 13000,
                UnitId = 1,
                ConnectTimeoutMs = 1000,
                ReadTimeoutMs = 1000,
                WriteTimeoutMs = 1000
            };
        }

        public MultiBoardTcpSettings Clone()
        {
            string json = JsonSerializer.Serialize(this);
            return JsonSerializer.Deserialize<MultiBoardTcpSettings>(json) ?? CreateDefault();
        }
    }
}
