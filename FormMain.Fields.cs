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

        private double _bath1FineTarget = double.NaN;
        private double _bath2FineTarget = double.NaN;
        private double _trackedCoarseSvCh1 = double.NaN;
        private double _trackedCoarseSvCh2 = double.NaN;

        private bool _isAdminAuthenticated;
        private const string AdminPassword = "1234";

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
        private const double FixedGraphMinY = 24.8;
        private const double FixedGraphMaxY = 25.2;
        private const double FixedOffsetMinY = -1.0;
        private const double FixedOffsetMaxY = 1.0;

        // 일별 통계(CSV 기반, Ch1/Ch2 독립)
        private DateTime _todayStatsDay = DateTime.MinValue;
        private long _todayStatsCsvLength;
        private bool _todayStatsInitialized;
        private DailyChannelStats _todayStatsCh1;
        private DailyChannelStats _todayStatsCh2;

        // Grid 1분 표시 제어
        private DateTime _lastGridDisplayedMinute = DateTime.MinValue;
        
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

        // AUTO/MANUAL 동시 offset write 시퀀스(0->값->2->wait->0) 충돌 방지
        private readonly object _offsetWriteSequenceSync = new object();

        // FIELD PATCH START
        private volatile bool _inWriteSequence;
        private DateTime? _manualHoldUntilCh1;
        private DateTime? _manualHoldUntilCh2;
        // FIELD PATCH END

        private const double OffsetReadbackMismatchEpsilon = 0.049;
        private const double EnforceWriteIntervalSeconds = 1.0;

        // offset 적용 상태를 1초간 상태바에 표시
        private readonly object _offsetStatusSync = new object();
        private DateTime _offsetApplyStatusUntilUtc = DateTime.MinValue;
        private string _offsetApplyStatusText = string.Empty;
        private System.Drawing.Color _offsetApplyStatusColor = System.Drawing.Color.DeepSkyBlue;
        private System.Windows.Forms.Timer? _offsetStatusTimer;

        // 온도 이탈 경보(전체 화면 점멸)
        private const double TempAlarmThresholdC = 0.1;
        private bool _isTempAlarmActive;
        private string _tempAlarmStatusText = string.Empty;
        private bool _isAlarmFlashOn;
        private System.Windows.Forms.Timer? _alarmFlashTimer;
        private readonly System.Collections.Generic.Dictionary<System.Windows.Forms.Control, System.Drawing.Color> _normalBackColors = new System.Collections.Generic.Dictionary<System.Windows.Forms.Control, System.Drawing.Color>();
    }
}
