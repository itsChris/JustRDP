using JustRDP.Domain.Entities;

namespace JustRDP.Domain.Interfaces;

public interface ITreeEntryRepository
{
    Task<List<TreeEntry>> GetAllAsync();
    Task<TreeEntry?> GetByIdAsync(Guid id);
    Task<List<FolderEntry>> GetAncestorsAsync(Guid entryId);
    Task<List<TreeEntry>> GetChildrenAsync(Guid? parentId);
    Task AddAsync(TreeEntry entry);
    Task UpdateAsync(TreeEntry entry);
    Task DeleteAsync(Guid id);
    Task UpdateSortOrderAsync(IEnumerable<(Guid Id, int SortOrder)> updates);
    Task UpdateUsageAsync(Guid connectionId, DateTime lastConnectedAt, int connectCount);
    Task<int> GetNextSortOrderAsync(Guid? parentId);
}
