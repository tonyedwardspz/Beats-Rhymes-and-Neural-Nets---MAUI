using MAUI_App.ViewModels;

namespace MAUI_App.Views;

public partial class WhisperPage : ContentPage
{
    public WhisperPage(WhisperPageViewModel viewmodel)
    {
        InitializeComponent();
        BindingContext = viewmodel;
    }
}