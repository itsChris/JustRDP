using System.ComponentModel;

namespace JustRDP.Presentation.ViewModels;

public interface IConnectionTab : INotifyPropertyChanged
{
    Guid ConnectionId { get; }
    string TabTitle { get; set; }
    bool IsSelected { get; set; }
    event Action<IConnectionTab>? CloseRequested;
    void Disconnect();
}
