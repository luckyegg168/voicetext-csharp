// src/VoiceText.App/Views/SettingsWindow.xaml.cs
using System.Windows;
using VoiceText.App.ViewModels;

namespace VoiceText.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        => Close();
}
