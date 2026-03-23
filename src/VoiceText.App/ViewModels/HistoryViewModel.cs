// src/VoiceText.App/ViewModels/HistoryViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceText.Storage;

namespace VoiceText.App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryRepository _history;

    [ObservableProperty] private IReadOnlyList<HistoryEntry> _entries = [];
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private HistoryEntry? _selectedEntry;

    public HistoryViewModel(IHistoryRepository history)
    {
        _history = history;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        Entries = await _history.GetRecentAsync(50);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            Entries = await _history.GetRecentAsync(50);
        else
            Entries = await _history.SearchAsync(SearchQuery);
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedEntry is null) return;
        await _history.DeleteAsync(SelectedEntry.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private void CopySelectedToClipboard()
    {
        if (SelectedEntry is null) return;
        var text = SelectedEntry.PolishedText ?? SelectedEntry.RawText;
        System.Windows.Clipboard.SetText(text);
    }
}
