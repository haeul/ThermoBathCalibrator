using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Windows.Forms;
using ThermoBathCalibrator.Controller;

namespace ThermoBathCalibrator
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();

            Text = "ThermoBathCalibrator";

            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
            btnOffsetApply.Click += BtnOffsetApply_Click;
            btnComSetting.Click += BtnComSetting_Click;

            nudOffSet.DecimalPlaces = 1;
            nudOffSet.Increment = 0.1M;
            nudOffSet.Minimum = -1.0M;
            nudOffSet.Maximum = 1.0M;
            nudOffSet.Value = 0.0M;

            pnlCh1Graph.Paint += PnlCh1Graph_Paint;
            pnlCh2Graph.Paint += PnlCh2Graph_Paint;
            EnableDoubleBuffer(pnlCh1Graph);
            EnableDoubleBuffer(pnlCh2Graph);

            LoadMultiBoardSettingsFromDiskIfAny();
            BuildMultiBoardClient();

            _autoCtrl = new OffsetAutoController(_autoCfg);

            ResetConnectionState();
            UpdateStatusLabels();
            UpdateTopNumbers(double.NaN, double.NaN, double.NaN);

            PrepareCsvPath(DateTime.Now);
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_running) return;

            PrepareCsvPath(DateTime.Now);

            _running = true;
            _workerRunning = true;

            _workerThread = new System.Threading.Thread(WorkerLoop);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            UpdateStatusLabels();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (!_running) return;

            _running = false;
            _workerRunning = false;

            try { _workerThread?.Join(500); } catch { }

            _mb.Disconnect();
            _boardConnected = false;

            UpdateStatusLabels();
        }

        private void BtnOffsetApply_Click(object sender, EventArgs e)
        {
            double offset = (double)nudOffSet.Value;

            double applied = OffsetMath.Quantize(offset, _autoCfg.OffsetStep);
            applied = OffsetMath.Clamp(applied, _autoCfg.OffsetClampMin, _autoCfg.OffsetClampMax);

            bool ok1 = TryWriteChannelOffset(channel: 1, appliedOffset: applied, reason: "MANUAL_APPLY");
            bool ok2 = TryWriteChannelOffset(channel: 2, appliedOffset: applied, reason: "MANUAL_APPLY");

            if (ok1)
            {
                _bath1OffsetCur = applied;
                _lastWriteCh1 = DateTime.Now;
            }

            if (ok2)
            {
                _bath2OffsetCur = applied;
                _lastWriteCh2 = DateTime.Now;
            }

            lblOffsetValue.Text = applied.ToString("0.0", CultureInfo.InvariantCulture);

            UpdateStatusLabels();
            pnlCh1Graph.Invalidate();
            pnlCh2Graph.Invalidate();
        }

        private void BtnComSetting_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new FormComSetting())
                {
                    dlg.ShowDialog(this);
                }

                string path = CommSettings.GetDefaultPath();
                var s = CommSettings.LoadOrDefault(path);

                if (s?.MultiBoard != null)
                {
                    string host = (s.MultiBoard.Host ?? "").Trim();
                    int port = s.MultiBoard.Port;
                    int unit = s.MultiBoard.UnitId;

                    ApplyMultiBoardEndpoint(host, port, (byte)Math.Max(1, Math.Min(247, unit)));

                    MessageBox.Show(
                        $"멀티보드 설정 적용: {_host}:{_port} (UnitId={_unitId})",
                        "COM Settings",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "멀티보드 설정을 찾을 수 없습니다. (comm_settings.json 확인)",
                        "COM Settings",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "COM Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTopNumbers(double ch1, double ch2, double offsetAvg)
        {
            lblCh1Temperature.Text = double.IsNaN(ch1) ? "-" : ch1.ToString("0.000", CultureInfo.InvariantCulture);
            lblCh2Temperature.Text = double.IsNaN(ch2) ? "-" : ch2.ToString("0.000", CultureInfo.InvariantCulture);
            lblOffsetValue.Text = double.IsNaN(offsetAvg) ? "-" : offsetAvg.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private void UpdateStatusLabels()
        {
            lblThermoPortStatus.Text =
                $"BOARD({_host}:{_port}): {(_boardConnected ? "CONNECTED" : "DISCONNECTED")} (fail={_boardFailCount})";

            lblThermoPortStatus.ForeColor = _boardConnected ? Color.LimeGreen : Color.OrangeRed;
        }
    }
}
