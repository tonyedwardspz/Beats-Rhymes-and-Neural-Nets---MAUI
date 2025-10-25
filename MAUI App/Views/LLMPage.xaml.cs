
namespace MAUI_App.Views;

public partial class LLMPage : ContentPage
{
    public LLMPage(LLMViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        
        // Subscribe to conversation changes to auto-scroll
        if (viewModel != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }
    
    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LLMViewModel.Conversation))
        {
            // Scroll to bottom when conversation changes
            await Task.Delay(100); // Small delay to ensure UI is updated
            await ConversationScrollView.ScrollToAsync(0, ConversationScrollView.ContentSize.Height, false);
        }
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Unsubscribe from events
        if (BindingContext is LLMViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
