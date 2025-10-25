using MAUI_App.ViewModels;

namespace MAUI_App.Views;

public partial class MetricsPage : ContentPage
{
    public MetricsPage(MetricsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is MetricsViewModel viewModel)
        {
            await viewModel.LoadMetricsAsync();
        }
    }
}
