using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Forge.Services;

namespace Forge.Controls
{
    /// <summary>Hand-drawn line chart of final price versus quantity, with the live quantity marked.</summary>
    public sealed class CurveChart : FrameworkElement
    {
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(CurveData), typeof(CurveChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public CurveData? Data
        {
            get => (CurveData?)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        private const double Left = 58, Right = 18, Top = 22, Bottom = 36;

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            dc.DrawRectangle(Theme.Panel, null, new Rect(0, 0, w, h));
            if (w < 120 || h < 90) return;

            var data = Data;
            if (data == null || data.Points.Count < 2)
            {
                Label(dc, "Add a Volume Tiers rule to see the discount curve.", w / 2, h / 2 - 8, Theme.Muted, 13, TextAlignment.Center);
                return;
            }

            var plot = new Rect(Left, Top, w - Left - Right, h - Top - Bottom);
            double minX = data.Points[0].Quantity, maxX = data.Points[^1].Quantity;
            double minY = data.Points.Min(p => p.Price), maxY = data.Points.Max(p => p.Price);
            if (maxY - minY < 1e-9) { minY -= 1; maxY += 1; }
            else
            {
                double pad = (maxY - minY) * 0.08;
                minY = Math.Max(minY - pad, minY >= 0 ? 0 : minY - pad);
                maxY += pad;
            }

            double X(double q) => plot.Left + (q - minX) / (maxX - minX) * plot.Width;
            double Y(double p) => plot.Bottom - (p - minY) / (maxY - minY) * plot.Height;

            var grid = new Pen(Theme.Border, 1);
            for (int i = 0; i <= 4; i++)
            {
                double value = minY + (maxY - minY) * i / 4;
                double y = Y(value);
                dc.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
                Label(dc, "$" + value.ToString("N2", CultureInfo.CurrentCulture), plot.Left - 6, y - 8, Theme.Muted, 11, TextAlignment.Right);
            }
            for (int i = 0; i <= 4; i++)
            {
                double q = minX + (maxX - minX) * i / 4;
                Label(dc, q.ToString("0.#", CultureInfo.CurrentCulture), X(q), plot.Bottom + 6, Theme.Muted, 11, TextAlignment.Center);
            }
            Label(dc, data.QuantityKey, plot.Left + plot.Width / 2, h - 18, Theme.Muted, 11, TextAlignment.Center);
            Label(dc, "Final price", 4, 2, Theme.Muted, 11, TextAlignment.Left);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(X(data.Points[0].Quantity), Y(data.Points[0].Price)), false, false);
                foreach (var p in data.Points.Skip(1))
                    ctx.LineTo(new Point(X(p.Quantity), Y(p.Price)), true, true);
            }
            geo.Freeze();
            dc.DrawGeometry(null, new Pen(Theme.Accent, 2.2) { LineJoin = PenLineJoin.Round }, geo);

            if (data.CurrentQuantity >= minX && data.CurrentQuantity <= maxX)
            {
                var nearest = data.Points.MinBy(p => Math.Abs(p.Quantity - data.CurrentQuantity))!;
                double cx = X(data.CurrentQuantity), cy = Y(nearest.Price);
                dc.DrawLine(new Pen(Theme.Warn, 1) { DashStyle = DashStyles.Dash }, new Point(cx, plot.Top), new Point(cx, plot.Bottom));
                dc.DrawEllipse(Theme.Warn, new Pen(Theme.Panel, 2), new Point(cx, cy), 5, 5);
                Label(dc, $"{data.CurrentQuantity:0.#} → ${nearest.Price:N2}", Math.Min(cx, plot.Right - 60), plot.Top - 18, Theme.Warn, 11, TextAlignment.Center);
            }
        }

        private void Label(DrawingContext dc, string text, double x, double y, Brush brush, double size, TextAlignment align)
        {
            var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double left = align switch
            {
                TextAlignment.Right => x - ft.Width,
                TextAlignment.Center => x - ft.Width / 2,
                _ => x
            };
            dc.DrawText(ft, new Point(left, y));
        }
    }
}
