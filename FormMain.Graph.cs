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
            DrawGraph(e.Graphics, pnlCh1Graph.ClientRectangle, 1);
        }

        private void PnlCh2Graph_Paint(object sender, PaintEventArgs e)
        {
            DrawGraph(e.Graphics, pnlCh2Graph.ClientRectangle, 2);
        }

        // double 비교용
        private static bool IsClose(double a, double b, double eps = 1e-9)
        {
            return Math.Abs(a - b) < eps;
        }

        private void DrawGraph(Graphics g, Rectangle clientRect, int channel)
        {
            g.Clear(Color.White);

            if (clientRect.Width < 50 || clientRect.Height < 50)
                return;

            int leftMargin = 55;
            int rightMargin = 65;
            int topMargin = 10;
            int bottomMargin = 30;

            Rectangle plot = new Rectangle(
                clientRect.Left + leftMargin,
                clientRect.Top + topMargin,
                clientRect.Width - leftMargin - rightMargin,
                clientRect.Height - topMargin - bottomMargin);

            if (plot.Width <= 0 || plot.Height <= 0)
                return;

            double minTemp = FixedGraphMinY;
            double maxTemp = FixedGraphMaxY;

            using var borderPen = new Pen(Color.Silver, 1);
            using var gridPen = new Pen(Color.Gainsboro, 1);

            using var axisFont = new Font("Segoe UI", 8);
            using var axisFontBold = new Font("Segoe UI", 8, FontStyle.Bold);

            using var axisBrush = new SolidBrush(Color.DimGray);
            using var axisBrushStrong = new SolidBrush(Color.Black);

            using var targetPen = new Pen(Color.Black, 1.8f);
            using var limitPen = new Pen(Color.DimGray, 1.8f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };

            g.DrawRectangle(borderPen, plot);

            // ===== Y grid 0.01 step =====
            const double tempStep = 0.01;
            int count = (int)Math.Round((maxTemp - minTemp) / tempStep);

            // 강조 기준
            const double targetTemp = 25.00;
            const double limitLow = 24.90;
            const double limitHigh = 25.10;

            // [LEFT LABEL STEP]
            // 좌측 온도 라벨은 0.02마다 표시 (0.01 step 기준으로 2칸마다)
            int labelEvery = 2;

            for (int i = 0; i <= count; i++)
            {
                double v = minTemp + i * tempStep;
                float y = plot.Bottom - (float)((v - minTemp) / (maxTemp - minTemp) * plot.Height);

                bool isTarget = IsClose(v, targetTemp);
                bool isLimit = IsClose(v, limitLow) || IsClose(v, limitHigh);

                Pen linePen = gridPen;
                if (isTarget) linePen = targetPen;
                else if (isLimit) linePen = limitPen;

                g.DrawLine(linePen, plot.Left, y, plot.Right, y);

                // 라벨은 0.02마다 찍되, target/limit은 무조건 찍음
                bool drawLabel = (i % labelEvery == 0) || isTarget || isLimit;
                if (!drawLabel)
                    continue;

                Font font = (isTarget || isLimit) ? axisFontBold : axisFont;
                Brush brush = (isTarget || isLimit) ? axisBrushStrong : axisBrush;

                g.DrawString(
                    v.ToString("0.00", CultureInfo.InvariantCulture),
                    font,
                    brush,
                    new PointF(clientRect.Left + 5, y - 7));
            }

            //// ===== Right Offset Axis (-1.0 ~ 1.0, 0.1 step) =====
            //DrawRightOffsetAxis(g, axisFont, axisFontBold, axisBrush, axisBrushStrong, plot, plot.Right + 5,
            //    FixedOffsetMinY, FixedOffsetMaxY);

            // ===== Time axis =====
            if (_history == null || _history.Count < 2)
                return;

            DateTime lastT = _history.Last().Timestamp;
            DateTime minT = lastT - GraphWindow;

            var utSeries = channel == 1
                ? _history.Select(h => (h.Timestamp, h.UtCh1)).ToList()
                : _history.Select(h => (h.Timestamp, h.UtCh2)).ToList();

            var offsetSeries = channel == 1
                ? _history.Select(h => (h.Timestamp, h.Bath1OffsetCur)).ToList()
                : _history.Select(h => (h.Timestamp, h.Bath2OffsetCur)).ToList();

            using var penUt = new Pen(Color.Firebrick, 2f);
            using var penOffset = new Pen(Color.SeaGreen, 2f);

            DrawSeriesTime(g, plot, utSeries, minT, lastT, minTemp, maxTemp, penUt);

            bool showOffset = channel == 1
                ? chkShowOffsetCh1.Checked
                : chkShowOffsetCh2.Checked;

            if (showOffset)
            {
                DrawSeriesTime(g, plot, offsetSeries, minT, lastT,
                    FixedOffsetMinY, FixedOffsetMaxY, penOffset);
            }
        }

        private void DrawRightOffsetAxis(
            Graphics g,
            Font axisFont,
            Font axisFontBold,
            Brush axisBrush,
            Brush axisBrushStrong,
            Rectangle plot,
            int axisX,
            double minOffset,
            double maxOffset)
        {
            const double defaultOff = 0.0;
            const double step = 0.1;

            int count = (int)Math.Round((maxOffset - minOffset) / step);

            for (int i = 0; i <= count; i++)
            {
                double offset = minOffset + i * step;

                float y = plot.Bottom - (float)((offset - minOffset) /
                          (maxOffset - minOffset) * plot.Height);

                g.DrawLine(Pens.Gray, axisX, y, axisX + 6, y);

                bool isDefault = IsClose(offset, defaultOff);

                Font font = isDefault ? axisFontBold : axisFont;
                Brush brush = isDefault ? axisBrushStrong : axisBrush;

                g.DrawString(
                    offset.ToString("0.00", CultureInfo.InvariantCulture),
                    font,
                    brush,
                    new PointF(axisX + 8, y - 7));
            }
        }

        private static float XFromTime(Rectangle rect, DateTime minT, DateTime maxT, DateTime t)
        {
            double span = (maxT - minT).TotalSeconds;
            if (span <= 0.001)
                return rect.Left;

            double ratio = (t - minT).TotalSeconds / span;
            ratio = Math.Max(0, Math.Min(1, ratio));

            return rect.Left + (float)(ratio * rect.Width);
        }

        private void DrawSeriesTime(
            Graphics g,
            Rectangle rect,
            List<(DateTime t, double v)> values,
            DateTime minT,
            DateTime maxT,
            double minY,
            double maxY,
            Pen pen)
        {
            if (values == null || values.Count < 2)
                return;

            PointF? prev = null;

            foreach (var item in values)
            {
                if (item.t < minT || item.t > maxT)
                    continue;

                double v = Math.Max(minY, Math.Min(maxY, item.v));

                float x = XFromTime(rect, minT, maxT, item.t);
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
            var prop = typeof(Control).GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(c, true, null);
        }
    }
}