# Flyout Navigation Guide

This document explains the flyout navigation system implemented in the Beats, Rhymes, and Neural Nets MAUI app.

## Overview

The app now uses Shell-based flyout navigation, providing an intuitive way to navigate between different sections of the application.

## Navigation Structure

### Flyout Menu Items

1. **🏠 Home** - Welcome page with navigation guide
2. **💬 LLM Chat** - Interface with the LLM API service
3. **⚙️ Settings** - Configure app preferences and API settings
4. **ℹ️ About** - App information and system details

### Flyout Components

#### Header
- App logo (dotnet_bot.png)
- App title: "Beats, Rhymes, and Neural Nets"
- Blue background with white text

#### Footer
- Version information (v1.0.0)
- Gray background

#### Menu Items
- Four main navigation items
- Clean text labels (icons can be added later)
- Automatic route management

## Implementation Details

### AppShell.xaml
```xml
<Shell FlyoutBehavior="Flyout">
    <!-- Flyout Header -->
    <Shell.FlyoutHeader>
        <!-- App branding and logo -->
    </Shell.FlyoutHeader>
    
    <!-- Navigation Items -->
    <FlyoutItem Title="Home">
        <ShellContent ContentTemplate="{DataTemplate local:MainPage}" />
    </FlyoutItem>
    
    <!-- More items... -->
    
    <!-- Flyout Footer -->
    <Shell.FlyoutFooter>
        <!-- Version info -->
    </Shell.FlyoutFooter>
</Shell>
```

### Route Registration
Routes are automatically registered in `AppShell.xaml.cs`:
```csharp
Routing.RegisterRoute(nameof(LLMPage), typeof(LLMPage));
Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
```

### Navigation Methods

#### Programmatic Navigation
```csharp
// Navigate to a specific page
await Shell.Current.GoToAsync("//LLMPage");

// Navigate with parameters
await Shell.Current.GoToAsync($"//SettingsPage?parameter=value");
```

#### User Navigation
- Tap the hamburger menu (☰) to open the flyout
- Tap any menu item to navigate to that page
- Current page is automatically highlighted

## Page Descriptions

### Home Page (MainPage.xaml)
- **Purpose**: Welcome users and explain navigation
- **Features**: 
  - App introduction
  - Navigation guide
  - Quick start button for LLM Chat
  - Status messages

### LLM Chat Page (LLMPage.xaml)
- **Purpose**: Interface with the LLM API
- **Features**:
  - Model status monitoring
  - Text input for prompts
  - Standard and streaming response generation
  - Real-time status updates

### Settings Page (SettingsPage.xaml)
- **Purpose**: Configure app preferences
- **Features**:
  - API URL configuration
  - Theme selection (Light/Dark/System)
  - Request timeout settings
  - Auto-save preferences
  - Save/Reset functionality

### About Page (AboutPage.xaml)
- **Purpose**: App information and system details
- **Features**:
  - App description and version
  - Feature list
  - Technology stack information
  - System information (device, platform, OS version)
  - Link to source code

## Customization Options

### Flyout Behavior
The flyout behavior can be changed in AppShell.xaml:
- `Flyout`: Shows hamburger menu (default)
- `Locked`: Always visible (good for tablets)
- `Disabled`: No flyout menu

### Adding New Pages
1. Create the new page (XAML + code-behind)
2. Add FlyoutItem to AppShell.xaml
3. Register route in AppShell.xaml.cs
4. Register page in MauiProgram.cs (if using DI)

### Styling
- Customize flyout header/footer in AppShell.xaml
- Add icons by including Icon="filename.png" in FlyoutItem
- Modify colors and fonts in App.xaml or individual pages

## Navigation Best Practices

1. **Consistent Navigation**: Always use Shell navigation methods
2. **Route Names**: Use clear, descriptive route names
3. **State Management**: Remember that pages may be recreated during navigation
4. **Error Handling**: Always wrap navigation in try-catch blocks
5. **User Feedback**: Provide visual feedback during navigation

## Troubleshooting

### Common Issues
1. **Page not found**: Ensure route is registered in AppShell.xaml.cs
2. **Navigation fails**: Check route spelling and casing
3. **Dependency injection errors**: Verify page registration in MauiProgram.cs

### Debug Navigation
```csharp
try
{
    await Shell.Current.GoToAsync("//PageName");
}
catch (Exception ex)
{
    // Log or display error
    await DisplayAlert("Navigation Error", ex.Message, "OK");
}
```

## Future Enhancements

1. **Icons**: Add custom icons for each menu item
2. **Badges**: Show notification counts on menu items
3. **Sub-menus**: Add hierarchical navigation for complex apps
4. **Dynamic Menu**: Load menu items from configuration or user permissions
5. **Animations**: Add custom animations for flyout transitions

## Accessibility

The current implementation includes:
- Semantic properties for screen readers
- Clear navigation labels
- Keyboard navigation support (via Shell)
- High contrast support

For better accessibility, consider:
- Adding semantic descriptions
- Testing with screen readers
- Ensuring proper tab order
- Adding voice navigation support
