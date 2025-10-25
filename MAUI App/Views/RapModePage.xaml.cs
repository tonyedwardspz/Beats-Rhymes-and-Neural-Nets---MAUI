using MAUI_App.ViewModels;

namespace MAUI_App.Views;

public partial class RapModePage : ContentPage
{
    public RapModePage(RapModeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
