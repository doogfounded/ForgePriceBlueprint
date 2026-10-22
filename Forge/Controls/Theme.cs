using System.Windows.Media;

namespace Forge.Controls
{
    /// <summary>Shared frozen brushes so XAML and code-built controls use one palette.</summary>
    public static class Theme
    {
        private static SolidColorBrush B(string hex)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }

        public static readonly Brush Window = B("#F3F4F6");
        public static readonly Brush Panel = B("#FFFFFF");
        public static readonly Brush Border = B("#E5E7EB");
        public static readonly Brush Text = B("#111827");
        public static readonly Brush Muted = B("#6B7280");
        public static readonly Brush Accent = B("#4F46E5");
        public static readonly Brush AccentSoft = B("#EEF2FF");
        public static readonly Brush Error = B("#DC2626");
        public static readonly Brush ErrorSoft = B("#FEE2E2");
        public static readonly Brush Ok = B("#059669");
        public static readonly Brush Warn = B("#D97706");
    }
}
