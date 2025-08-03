using MAUI_App.ViewModels;

namespace MAUI_App.Views;

public partial class LLMPage : ContentPage
{
    public LLMPage(LLMViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
