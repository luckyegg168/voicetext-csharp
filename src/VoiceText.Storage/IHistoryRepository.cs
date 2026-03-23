// src/VoiceText.Storage/IHistoryRepository.cs
namespace VoiceText.Storage;

public interface IHistoryRepository
{
    Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int limit = 50);
    Task<int> AddAsync(HistoryEntry entry);
    Task DeleteAsync(int id);
    Task<IReadOnlyList<HistoryEntry>> SearchAsync(string query);
}
