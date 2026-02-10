using System;
using System.Collections.Generic;
using ThermoBathCalibrator.Controller;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        // UT-ONE 측정 보정(업체 기준 온도계 대비 오프셋)
        private double _utBiasCh1 = 0.14;
        private double _utBiasCh2 = 0.3;

        private double _bath1Setpoint = 25.0;
        private double _bath2Setpoint = 25.0;

        private bool _running;

        // 자동제어 모듈 (분리됨)
        private readonly OffsetAutoConfig _autoCfg = new OffsetAutoConfig();
        private readonly OffsetAutoController _autoCtrl;

        // 그래프 데이터
        private const int MaxPoints = 300;
        private readonly List<SampleRow> _history = new List<SampleRow>(MaxPoints);
        private static readonly TimeSpan GraphWindow = TimeSpan.FromMinutes(5);

        private double _bath1OffsetCur;
        private double _bath2OffsetCur;
        private readonly object _offsetStateSync = new object();

        // 보드 연결 상태
        private bool _boardConnected;
        private int _boardFailCount;

        private MultiBoardModbusClient _mb = null!;

        private string _host = "192.168.1.11";
        private int _port = 13000;
        private byte _unitId = 1;

        // CSV 로깅
        private readonly object _csvSync = new object();
        private string _csvPath = "";
        private bool _csvHeaderWritten;
        private DateTime _csvDay = DateTime.MinValue;

        // Trace 로깅
        private readonly object _traceSync = new object();
        private string _tracePath = "";
        private DateTime _traceDay = DateTime.MinValue;

        // 재연결 쿨다운
        private long _lastReconnectTick;
        private const int ReconnectCooldownMs = 800;

        // 에러 로그용
        private double _prevErr1 = double.NaN;
        private double _prevErr2 = double.NaN;

        private DateTime _lastWriteCh1 = DateTime.MinValue;
        private DateTime _lastWriteCh2 = DateTime.MinValue;

        // 마지막으로 쓴 offset 추적 + readback 비교/강제 유지용
        private DateTime _lastEnforceWriteCh1 = DateTime.MinValue;
        private DateTime _lastEnforceWriteCh2 = DateTime.MinValue;
        private double _lastWrittenOffsetCh1 = double.NaN;
        private double _lastWrittenOffsetCh2 = double.NaN;

        // 워커 스레드
        private System.Threading.Thread? _workerThread;
        private volatile bool _workerRunning;

        // lastGood 스냅샷
        private MultiBoardSnapshot _lastGoodSnap;
        private bool _hasLastGoodSnap;

        // stale 판정
        private ushort _lastRespCh1;
        private ushort _lastRespCh2;
        private bool _hasLastResp;

        // Grid 성능
        private const int MaxGridRows = 2000;
        private int _scrollEveryN = 5;
        private int _rowAddCount = 0;

        // 그래프 스케일
        private const bool UseFixedGraphScale = true;
        private const double FixedGraphMinY = 24.0;
        private const double FixedGraphMaxY = 26.0;

        // Modbus 레지스터
        private const ushort RegReadStart = 0;
        private const ushort RegReadCount = 14;
        private const ushort RegCh1Command = 20;
        private const ushort RegCh2Command = 24;
        private const ushort RegCh1Response = 1;
        private const ushort RegCh2Response = 8;
        private const ushort RegCh1OffsetCur = 4;
        private const ushort RegCh2OffsetCur = 11;

        private const int AckTimeoutMs = 1500;
        private const int AckPollIntervalMs = 100;
        private const int AckInitialDelayMs = 120;

        private const double OffsetReadbackMismatchEpsilon = 0.049;
        private const double EnforceWriteIntervalSeconds = 1.0;
    }
}
