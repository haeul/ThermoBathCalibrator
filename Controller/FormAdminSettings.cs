using System;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    public partial class FormAdminSettings : Form
    {
        // offset 보정 on/off
        public bool AppliedEnableOffsetControl => chkEnableOffsetControl.Checked;


        public double UtBiasCh1 => (double)nudUtBiasCh1.Value;
        public double UtBiasCh2 => (double)nudUtBiasCh2.Value;
        public double SetpointCh1 => (double)nudSetpointCh1.Value;
        public double SetpointCh2 => (double)nudSetpointCh2.Value;

        public FormAdminSettings(double utBiasCh1, double utBiasCh2, double setpointCh1, double setpointCh2, bool enableOffsetControl)
        {
            InitializeComponent();

            nudUtBiasCh1.Value = ClampToNumeric(nudUtBiasCh1, utBiasCh1);
            nudUtBiasCh2.Value = ClampToNumeric(nudUtBiasCh2, utBiasCh2);
            nudSetpointCh1.Value = ClampToNumeric(nudSetpointCh1, setpointCh1);
            nudSetpointCh2.Value = ClampToNumeric(nudSetpointCh2, setpointCh2);

<<<<<<< HEAD
            // 체크박스 초기값: 저장값이 있으면 저장값 우선, 없으면 FormMain에서 넘어온 값 사용
            // chkEnableOffsetControl.Checked = ReadEnableOffsetControlOrFallback(enableOffsetControl);
            chkEnableOffsetControl.Checked = true;
=======
            chkEnableOffsetControl.Checked = enableOffsetControl;
>>>>>>> Offset-UI
        }

        private static decimal ClampToNumeric(NumericUpDown nud, double value)
        {
            decimal v = (decimal)value;
            if (v < nud.Minimum) return nud.Minimum;
            if (v > nud.Maximum) return nud.Maximum;
            return v;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}