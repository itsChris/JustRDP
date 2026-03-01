using JustRDP.Domain.Entities;
using JustRDP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JustRDP.Infrastructure.Persistence.Repositories;

public class TreeEntryRepository : ITreeEntryRepository
{
    private readonly JustRdpDbContext _db;

    public TreeEntryRepository(JustRdpDbContext db)
    {
        _db = db;
    }

    public async Task<List<TreeEntry>> GetAllAsync()
    {
        return await _db.TreeEntries
            .OrderBy(e => e.SortOrder)
            .ToListAsync();
    }

    public async Task<TreeEntry?> GetByIdAsync(Guid id)
    {
        return await _db.TreeEntries.FindAsync(id);
    }

    public async Task<List<FolderEntry>> GetAncestorsAsync(Guid entryId)
    {
        var entry = await _db.TreeEntries.FindAsync(entryId);
        if (entry is null) return [];

        var ancestors = new List<FolderEntry>();
        var currentParentId = entry.ParentId;

        while (currentParentId.HasValue)
        {
            var parent = await _db.Folders.FindAsync(currentParentId.Value);
            if (parent is null) break;
            ancestors.Add(parent);
            currentParentId = parent.ParentId;
        }

        return ancestors;
    }

    public async Task<List<TreeEntry>> GetChildrenAsync(Guid? parentId)
    {
        return await _db.TreeEntries
            .Where(e => e.ParentId == parentId)
            .OrderBy(e => e.SortOrder)
            .ToListAsync();
    }

    public async Task AddAsync(TreeEntry entry)
    {
        _db.TreeEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(TreeEntry entry)
    {
        _db.TreeEntries.Update(entry);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await _db.TreeEntries.FindAsync(id);
        if (entry is null) return;
        _db.TreeEntries.Remove(entry);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateSortOrderAsync(IEnumerable<(Guid Id, int SortOrder)> updates)
    {
        foreach (var (id, sortOrder) in updates)
        {
            var entry = await _db.TreeEntries.FindAsync(id);
            if (entry is not null)
            {
                entry.SortOrder = sortOrder;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetNextSortOrderAsync(Guid? parentId)
    {
        var max = await _db.TreeEntries
            .Where(e => e.ParentId == parentId)
            .MaxAsync(e => (int?)e.SortOrder);
        return (max ?? -1) + 1;
    }
}
