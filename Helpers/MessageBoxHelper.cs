using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NovaSCM.Helpers;

public static class MessageBoxHelper
{
    public static async Task ShowInfo(string message, Window? owner = null, string title = "NovaSCM")
    {
        var win = new Window
        {
            Title = title,
            Width = 420,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(24, 20, 24, 16), Spacing = 16 };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        var btn = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Avalonia.Thickness(32, 8),
        };
        btn.Click += (_, _) => win.Close();
        panel.Children.Add(btn);

        win.Content = panel;

        if (owner != null)
            await win.ShowDialog(owner);
        else
            win.Show();
    }

    public static async Task<bool> ShowConfirm(string message, Window? owner = null, string title = "NovaSCM")
    {
        bool result = false;
        var win = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(24, 20, 24, 16), Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 12 };
        var btnOk = new Button { Content = "OK", Padding = new Avalonia.Thickness(24, 8) };
        var btnCancel = new Button { Content = "Annulla", Padding = new Avalonia.Thickness(16, 8) };
        btnOk.Click += (_, _) => { result = true; win.Close(); };
        btnCancel.Click += (_, _) => win.Close();
        btnRow.Children.Add(btnOk);
        btnRow.Children.Add(btnCancel);
        panel.Children.Add(btnRow);

        win.Content = panel;

        if (owner != null)
            await win.ShowDialog(owner);
        else
            win.Show();

        return result;
    }
}
