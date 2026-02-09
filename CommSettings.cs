using System;
using System.IO;
using System.Text.Json;

namespace ThermoBathCalibrator
{
    public sealed class CommSettings
    {
        // 멀티보드(Modbus TCP) 설정만 유지
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

                // System.Text.Json은 기본적으로 "알 수 없는 필드"는 무시하므로
                // 기존 파일에 Bath1/Bath2/UtOne이 있어도 문제 없이 로드됨.
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

    // 멀티보드(Modbus TCP) 설정 클래스
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
