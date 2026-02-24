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

            Rectangle plot = new Rectangle(clientRect.Left + 48, clientRect.Top + 14, clientRect.Width - 108, clientRect.Height - 44);
            if (plot.Width < 20 || plot.Height < 20)
                return;

            DateTime lastT = DateTime.Now;
            DateTime minT = lastT - GraphWindow;
            if (_history.Count > 0)
            {
                lastT = _history[_history.Count - 1].Timestamp;
                minT = lastT - GraphWindow;
                DateTime firstT = _history[0].Timestamp;
                if (firstT > minT) minT = firstT;
            }

            double minTemp = FixedGraphMinY;
            double maxTemp = FixedGraphMaxY;
            double minOffset = FixedOffsetMinY;
            double maxOffset = FixedOffsetMaxY;

            using var gridMinor = new Pen(Color.Gainsboro, 1);
            using var gridMajor = new Pen(Color.LightGray, 1.2f);
            using var axisPen = new Pen(Color.Gray, 1);
            using var axisFont = new Font("Segoe UI", 8, FontStyle.Regular);
            using var axisBrush = new SolidBrush(Color.DimGray);

            // ===== 요구사항 반영 =====
            // - 격자: 0.001 (1/1000)
            // - 라벨/굵은선: 0.01 (1/100)
            const double minorStep = 0.01;
            const double majorStep = 0.01;

            int minorCount = (int)Math.Round((maxTemp - minTemp) / minorStep);
            if (minorCount < 1) minorCount = 1;

            for (int i = 0; i <= minorCount; i++)
            {
                double v = minTemp + (i * minorStep);
                float y = plot.Bottom - (float)((v - minTemp) / (maxTemp - minTemp) * plot.Height);

                bool isMajor = Math.Abs((v / majorStep) - Math.Round(v / majorStep)) < 1e-6;
                g.DrawLine(isMajor ? gridMajor : gridMinor, plot.Left, y, plot.Right, y);

                // 라벨은 0.01 간격으로만 찍고, 표기는 소수 2자리
                if (isMajor)
                {
                    g.DrawString(v.ToString("0.00", CultureInfo.InvariantCulture), axisFont, axisBrush,
                        new PointF(clientRect.Left + 4, y - 7));
                }
            }

            g.DrawRectangle(axisPen, plot);

            // 우측 offset 축 (라벨 - 제거는 DrawRightOffsetAxis에서 처리)
            DrawRightOffsetAxis(g, axisFont, axisBrush, plot, clientRect.Right - 42, minOffset, maxOffset);

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

            if (_history.Count < 2)
                return;

            // ===== Series: UT (red), Offset (green) =====
            List<(DateTime t, double v)> ut = channel == 1
                ? _history.Select(h => (h.Timestamp, h.UtCh1)).ToList()
                : _history.Select(h => (h.Timestamp, h.UtCh2)).ToList();

            List<(DateTime t, double v)> offsets = channel == 1
                ? _history.Select(h => (h.Timestamp, h.Bath1OffsetCur)).ToList()
                : _history.Select(h => (h.Timestamp, h.Bath2OffsetCur)).ToList();

            bool showOffset = channel == 1 ? chkShowOffsetCh1.Checked : chkShowOffsetCh2.Checked;

            using var penUt = new Pen(Color.Firebrick, 2.0f);
            using var penOffset = new Pen(Color.SeaGreen, 2.0f);

            DrawSeriesTime(g, plot, ut, minT, lastT, minTemp, maxTemp, penUt);

            if (showOffset)
                DrawSeriesTime(g, plot, offsets, minT, lastT, minOffset, maxOffset, penOffset);
        }
        private void DrawRightOffsetAxis(Graphics g, Font axisFont, Brush axisBrush, Rectangle plot, int axisX, double minOffset, double maxOffset)
        {
            const double step = 0.1;

            int count = (int)Math.Round((maxOffset - minOffset) / step);
            if (count < 1) count = 1;

            for (int i = 0; i <= count; i++)
            {
                double offset = minOffset + i * step;

                float y = plot.Bottom - (float)((offset - minOffset) / (maxOffset - minOffset) * plot.Height);
                int tickLen = (i % 2 == 0) ? 6 : 4;
                g.DrawLine(Pens.Gray, axisX, y, axisX + tickLen, y);

                // 표시만 절대값(마이너스 제거)
                string text = Math.Abs(offset).ToString("0.0", CultureInfo.InvariantCulture);
                g.DrawString(text, axisFont, axisBrush, new PointF(axisX + tickLen + 2, y - 7));
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

                if (v < minY) v = minY;
                if (v > maxY) v = maxY;

                float x = XFromTime(rect, minT, maxT, t);
                float yRatio = (float)((v - minY) / (maxY - minY));
                float y = rect.Bottom - yRatio * rect.Height;

                var pt = new PointF(x, y);

                if (prev.HasValue)
                    g.DrawLine(pen, prev.Value, pt);

                prev = pt;
            }
        }

        private void EnableDoubleBuffer(Control c)
        {
            PropertyInfo prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(c, true, null);
        }
    }
}