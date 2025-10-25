using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MAUI_App.ViewModels;

public class RapModeViewModel : INotifyPropertyChanged
{
    private string _title = "Rap Mode";

    public RapModeViewModel()
    {
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
