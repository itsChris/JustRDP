using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace JustRDP.Presentation.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "1.0.0";

        // Strip the +commithash suffix (e.g. "1.0.0+09e89faa...")
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        VersionText.Text = $"Version {version}";
    }

    private void EmailLink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("mailto:info@solvia.ch") { UseShellExecute = true });
    }

    private void WebLink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://www.solvia.ch") { UseShellExecute = true });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
