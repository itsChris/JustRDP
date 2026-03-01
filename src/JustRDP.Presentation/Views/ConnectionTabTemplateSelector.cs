using System.Windows;
using System.Windows.Controls;
using JustRDP.Presentation.ViewModels;

namespace JustRDP.Presentation.Views;

public class ConnectionTabTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RdpTemplate { get; set; }
    public DataTemplate? SshTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item switch
        {
            ConnectionTabViewModel => RdpTemplate,
            SshTabViewModel => SshTemplate,
            _ => base.SelectTemplate(item, container)
        };
}
