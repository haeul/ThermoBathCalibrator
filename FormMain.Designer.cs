using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace ThermoBathCalibrator
{
    public partial class FormMain
    {
        private void PnlCh1Graph_Paint(object sender, PaintEventArgs e)
        {
            DrawGraph(e.Graphics, pnlCh1Graph.ClientRectangle, channel: 1);
        }

        private void PnlCh2Graph_Paint(object sender, PaintEventArgs e)
        {
            DrawGraph(e.Graphics, pnlCh2Graph.ClientRectangle, channel: 2);
        }

        private void DrawGraph(Graphics g, Rectangle clientRect, int channel)
        {
            g.Clear(Color.White);

            using var borderPen = new Pen(Color.Silver, 1);
            g.DrawRectangle(borderPen, new Rectangle(clientRect.Left, clientRect.Top, clientRect.Width - 1, clientRect.Height - 1));

            if (_history == null || _history.Count < 2)
                return;

            // plot area
            Rectangle plot = clientRect;
            plot.Inflate(-60, -46); // 좌/우/상/하 여백 조금 더

            DateTime lastT = _history[_history.Count - 1].Timestamp;
            DateTime minT = lastT - GraphWindow;
            DateTime firstT = _history[0].Timestamp;
            if (firstT > minT) minT = firstT;

            // ===== Temp axis (left) =====
            double minTemp = UseFixedGraphScale ? FixedGraphMinY : 24.8;
            double maxTemp = UseFixedGraphScale ? FixedGraphMaxY : 25.2;

            // ===== ON/OFF axis (right) =====
            double minOnOff = 0.0;
            double maxOnOff = 1.0;

            using var gridMinor = new Pen(Color.Gainsboro, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            using var gridMajor = new Pen(Color.LightGray, 1.2f);
            using var axisPen = new Pen(Color.Gray, 1);

            using var axisFont = new Font("Segoe UI", 8, FontStyle.Regular);
            using var axisBrush = new SolidBrush(Color.DimGray);

            // ===== Horizontal grid: 0.001 dotted lines, label 0.1 only =====
            double minorStep = 0.001; // 1/1000
            double majorLabelStep = 0.1; // 숫자는 1/10 단위만

            int minorCount = (int)Math.Round((maxTemp - minTemp) / minorStep);
            for (int i = 0; i <= minorCount; i++)
            {
                double v = minTemp + (i * minorStep);
                float y = plot.Bottom - (float)((v - minTemp) / (maxTemp - minTemp) * plot.Height);

                // 0.001은 점선
                g.DrawLine(gridMinor, plot.Left, y, plot.Right, y);

                // 라벨은 0.1 간격만 (부동소수 오차 방지)
                if (Math.Abs((v / majorLabelStep) - Math.Round(v / majorLabelStep)) < 1e-6)
                {
                    // 라벨 라인은 조금 더 진하게
                    g.DrawLine(gridMajor, plot.Left, y, plot.Right, y);

                    g.DrawString(v.ToString("0.0", CultureInfo.InvariantCulture), axisFont, axisBrush,
                        new PointF(clientRect.Left + 4, y - 7));
                }
            }

            g.DrawRectangle(axisPen, plot);

            // ===== Right axis labels: ON/OFF only (0 / 1) =====
            for (int i = 0; i <= 1; i++)
            {
                double ov = i;
                float y = plot.Bottom - (float)((ov - minOnOff) / (maxOnOff - minOnOff) * plot.Height);
                string text = ov.ToString("0", CultureInfo.InvariantCulture);
                SizeF sz = g.MeasureString(text, axisFont);
                g.DrawString(text, axisFont, axisBrush, new PointF(clientRect.Right - sz.Width - 6, y - 7));
            }

            // ===== Time vertical grid (every minute) =====
            DateTime tick = new DateTime(minT.Year, minT.Month, minT.Day, minT.Hour, minT.Minute, 0);
            if (tick < minT) tick = tick.AddMinutes(1);

            using var vGrid = new Pen(Color.Gainsboro, 1);
            while (tick <= lastT)
            {
                float x = XFromTime(plot, minT, lastT, tick);
                g.DrawLine(vGrid, x, plot.Top, x, plot.Bottom);

                string label = tick.ToString("HH:mm");
                SizeF sz = g.MeasureString(label, axisFont);
                g.DrawString(label, axisFont, axisBrush, x - sz.Width / 2, plot.Bottom + 8);

                tick = tick.AddMinutes(1);
            }

            // ===== Series: UT (red), Offset ON/OFF (green) =====
            List<(DateTime t, double v)> ut = channel == 1
                ? _history.Select(h => (h.Timestamp, h.UtCh1)).ToList()
                : _history.Select(h => (h.Timestamp, h.UtCh2)).ToList();

            bool showOffsetOnOff = channel == 1
                ? (chkCh1OffsetOnOff != null && chkCh1OffsetOnOff.Checked)
                : (chkCh2OffsetOnOff != null && chkCh2OffsetOnOff.Checked);

            // ON/OFF 데이터는 "Offset 보정 전체 체크"를 기준으로 0/1로 표시
            // (만약 채널별 enable이 따로 있으면 여기만 바꾸면 됨)
            List<(DateTime t, double v)> onoff = _history
                .Select(h => (h.Timestamp, chkEnableOffsetControl != null && chkEnableOffsetControl.Checked ? 1.0 : 0.0))
                .ToList();

            using var penUt = new Pen(Color.Firebrick, 2.2f);
            using var penOnOff = new Pen(Color.SeaGreen, 2.2f);

            DrawSeriesTime(g, plot, ut, minT, lastT, minTemp, maxTemp, penUt);

            if (showOffsetOnOff)
            {
                // ON/OFF는 step 느낌이 나게: 값이 바뀌는 순간 수직/수평으로 그리기
                DrawSeriesTimeStep(g, plot, onoff, minT, lastT, minOnOff, maxOnOff, penOnOff);
            }
        }

        private static float XFromTime(Rectangle rect, DateTime minT, DateTime maxT, DateTime t)
        {
            double spanSec = (maxT - minT).TotalSeconds;
            if (spanSec <= 0.001) return rect.Right;

            double xRatio = (t - minT).TotalSeconds / spanSec;
            if (xRatio < 0) xRatio = 0;
            if (xRatio > 1) xRatio = 1;

            return rect.Left + (float)(xRatio * rect.Width);
        }

        private void DrawSeriesTime(Graphics g, Rectangle rect, List<(DateTime t, double v)> values,
            DateTime minT, DateTime maxT, double minY, double maxY, Pen pen)
        {
            if (values == null || values.Count < 2) return;

            PointF? prev = null;

            for (int i = 0; i < values.Count; i++)
            {
                DateTime t = values[i].t;
                if (t < minT || t > maxT) continue;

                double v = values[i].v;
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    prev = null;
                    continue;
                }

                float x = XFromTime(rect, minT, maxT, t);
                float yRatio = (float)((v - minY) / (maxY - minY));
                float y = rect.Bottom - yRatio * rect.Height;

                var pt = new PointF(x, y);

                if (prev.HasValue)
                    g.DrawLine(pen, prev.Value, pt);

                prev = pt;
            }
        }

        // ON/OFF 같은 계단형 표시용
        private void DrawSeriesTimeStep(Graphics g, Rectangle rect, List<(DateTime t, double v)> values,
            DateTime minT, DateTime maxT, double minY, double maxY, Pen pen)
        {
            if (values == null || values.Count < 2) return;

            PointF? prev = null;
            double? prevV = null;

            for (int i = 0; i < values.Count; i++)
            {
                DateTime t = values[i].t;
                if (t < minT || t > maxT) continue;

                double v = values[i].v;
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    prev = null;
                    prevV = null;
                    continue;
                }

                float x = XFromTime(rect, minT, maxT, t);
                float yRatio = (float)((v - minY) / (maxY - minY));
                float y = rect.Bottom - yRatio * rect.Height;

                var pt = new PointF(x, y);

                if (prev.HasValue && prevV.HasValue)
                {
                    // 수평(이전값 유지)
                    g.DrawLine(pen, prev.Value, new PointF(pt.X, prev.Value.Y));
                    // 수직(값 변경)
                    if (Math.Abs(v - prevV.Value) > 1e-9)
                        g.DrawLine(pen, new PointF(pt.X, prev.Value.Y), pt);
                }

                prev = pt;
                prevV = v;
            }
        }

        private void EnableDoubleBuffer(Control c)
        {
            PropertyInfo prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(c, true, null);
        }
    }
}