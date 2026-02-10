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

            using var borderPen = new Pen(Color.DarkGray, 1);
            g.DrawRectangle(borderPen, new Rectangle(clientRect.Left, clientRect.Top, clientRect.Width - 1, clientRect.Height - 1));

            if (_history.Count < 2)
            {
                using var f = new Font("Segoe UI", 12, FontStyle.Bold);
                g.DrawString("No data", f, Brushes.Gray, new PointF(10, 10));
                return;
            }

            Rectangle plot = clientRect;
            plot.Inflate(-55, -40);

            using (var bgBrush = new SolidBrush(Color.FromArgb(248, 248, 248)))
            {
                g.FillRectangle(bgBrush, plot);
            }

            DateTime lastT = _history[_history.Count - 1].Timestamp;
            DateTime minT = lastT - GraphWindow;

            DateTime firstT = _history[0].Timestamp;
            if (firstT > minT) minT = firstT;

            List<(DateTime t, double v)> pv = channel == 1
                ? _history.Select(h => (h.Timestamp, h.Bath1Pv)).ToList()
                : _history.Select(h => (h.Timestamp, h.Bath2Pv)).ToList();

            List<(DateTime t, double v)> ut = channel == 1
                ? _history.Select(h => (h.Timestamp, h.UtCh1)).ToList()
                : _history.Select(h => (h.Timestamp, h.UtCh2)).ToList();

            List<(DateTime t, double v)> setTemp = channel == 1
                ? _history.Select(h => (h.Timestamp, h.Bath1SetTemp)).ToList()
                : _history.Select(h => (h.Timestamp, h.Bath2SetTemp)).ToList();

            double minY;
            double maxY;

            if (UseFixedGraphScale)
            {
                minY = FixedGraphMinY;
                maxY = FixedGraphMaxY;
            }
            else
            {
                var all = new List<double>();
                all.AddRange(pv.Where(x => x.t >= minT && x.t <= lastT).Select(x => x.v).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));
                all.AddRange(ut.Where(x => x.t >= minT && x.t <= lastT).Select(x => x.v).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));
                all.AddRange(setTemp.Where(x => x.t >= minT && x.t <= lastT).Select(x => x.v).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)));

                if (all.Count >= 2)
                {
                    double minV = all.Min();
                    double maxV = all.Max();

                    double span = Math.Max(0.05, maxV - minV);
                    double margin = span * 0.2;

                    minY = minV - margin;
                    maxY = maxV + margin;
                }
                else
                {
                    minY = 24.5;
                    maxY = 25.5;
                }
            }

            using var gridPen = new Pen(Color.LightGray, 1);

            double yStep = 0.01;
            int hLines = (int)Math.Round((maxY - minY) / yStep);

            for (int i = 0; i <= hLines; i++)
            {
                float y = plot.Bottom - (float)(i * (plot.Height / (maxY - minY)) * yStep);

                bool isMajor = (i % 10 == 0);

                using var pen = new Pen(
                    isMajor ? Color.LightGray : Color.Gainsboro,
                    isMajor ? 1.5f : 1.0f
                );

                g.DrawLine(pen, plot.Left, y, plot.Right, y);
            }

            using var axisFont = new Font("Segoe UI", 9, FontStyle.Regular);
            using var axisBrush = new SolidBrush(Color.DimGray);

            for (int i = 0; i <= hLines; i++)
            {
                if (i % 10 != 0) continue;

                double v = minY + i * yStep;
                float y = plot.Bottom - (float)((v - minY) / (maxY - minY) * plot.Height) - 7;

                g.DrawString(
                    v.ToString("0.000", CultureInfo.InvariantCulture),
                    axisFont,
                    axisBrush,
                    new PointF(clientRect.Left + 5, y)
                );
            }

            using var axisPen = new Pen(Color.Gray, 1);
            g.DrawRectangle(axisPen, plot);

            using var xFont = new Font("Segoe UI", 8, FontStyle.Regular);
            using var xBrush = new SolidBrush(Color.DimGray);

            DateTime tick = new DateTime(minT.Year, minT.Month, minT.Day, minT.Hour, minT.Minute, 0);
            if (tick < minT) tick = tick.AddMinutes(1);

            while (tick <= lastT)
            {
                float x = XFromTime(plot, minT, lastT, tick);
                g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);

                string label = tick.ToString("HH:mm");
                SizeF sz = g.MeasureString(label, xFont);
                g.DrawString(label, xFont, xBrush, x - sz.Width / 2, plot.Bottom + 6);

                tick = tick.AddMinutes(1);
            }

            using var penPv = new Pen(Color.Blue, 2);
            using var penUt = new Pen(Color.Red, 2);
            using var penSet = new Pen(Color.Green, 2);

            DrawSeriesTime(g, plot, pv, minT, lastT, minY, maxY, penPv);
            DrawSeriesTime(g, plot, ut, minT, lastT, minY, maxY, penUt);
            DrawSeriesTime(g, plot, setTemp, minT, lastT, minY, maxY, penSet);

            using var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
            string title = channel == 1
                ? "CH1: PV / ExternalThermo(UT) / Set(SP+Off)"
                : "CH2: PV / ExternalThermo(UT) / Set(SP+Off)";
            g.DrawString(title, titleFont, Brushes.Black, new PointF(clientRect.Left + 10, clientRect.Top + 8));

            DrawLegend(g, clientRect, channel);
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

        private void DrawLegend(Graphics g, Rectangle clientRect, int channel)
        {
            using var font = new Font("Segoe UI", 9, FontStyle.Bold);
            int x = clientRect.Right - 240;
            int y = clientRect.Top + 10;

            DrawLegendItem(g, x, y, Color.Blue, channel == 1 ? "CH1 PV" : "CH2 PV", font);
            y += 18;

            DrawLegendItem(g, x, y, Color.Red, channel == 1 ? "CH1 UT(ExtThermo)" : "CH2 UT(ExtThermo)", font);
            y += 18;

            DrawLegendItem(g, x, y, Color.Green, channel == 1 ? "CH1 Set(SP+Off)" : "CH2 Set(SP+Off)", font);
        }

        private void DrawLegendItem(Graphics g, int x, int y, Color c, string text, Font font)
        {
            using var pen = new Pen(c, 3);
            g.DrawLine(pen, x, y + 7, x + 26, y + 7);
            g.DrawString(text, font, Brushes.Black, new PointF(x + 32, y));
        }

        private void DrawSeriesTime(Graphics g, Rectangle rect, List<(DateTime t, double v)> values, DateTime minT, DateTime maxT, double minY, double maxY, Pen pen)
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

        private void EnableDoubleBuffer(Control c)
        {
            PropertyInfo prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(c, true, null);
        }
    }
}
