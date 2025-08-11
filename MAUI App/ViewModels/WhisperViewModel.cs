using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MAUI_App.Models;
using MAUI_App.Services;
using Microsoft.Extensions.Logging;

namespace MAUI_App.ViewModels;

public class WhisperViewModel : INotifyPropertyChanged
{
    
    
    
    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}