// src/VoiceText.App/Views/HistoryWindow.xaml.cs
using System.Windows;
using VoiceText.App.ViewModels;

namespace VoiceText.App.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(HistoryViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadCommand.ExecuteAsync(null);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
