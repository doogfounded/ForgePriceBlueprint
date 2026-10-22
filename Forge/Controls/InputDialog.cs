using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Forge.Controls
{
    /// <summary>Minimal modal single-line text prompt.</summary>
    public static class InputDialog
    {
        public static string? Show(Window? owner, string title, string prompt, string initial = "")
        {
            var box = new TextBox { Text = initial, Margin = new Thickness(0, 8, 0, 12), Padding = new Thickness(4, 4, 4, 4) };
            var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = prompt });
            panel.Children.Add(box);
            panel.Children.Add(buttons);

            var window = new Window
            {
                Title = title,
                Content = panel,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Owner = owner
            };

            ok.Click += (_, _) => window.DialogResult = true;
            window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };

            return window.ShowDialog() == true ? box.Text.Trim() : null;
        }
    }
}
