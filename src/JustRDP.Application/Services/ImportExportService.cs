using JustRDP.Domain.Entities;
using JustRDP.Domain.Interfaces;

namespace JustRDP.Application.Services;

public class ImportExportService
{
    private readonly ITreeEntryRepository _repository;

    public ImportExportService(ITreeEntryRepository repository)
    {
        _repository = repository;
    }

    public async Task ImportEntriesAsync(IEnumerable<TreeEntry> entries)
    {
        foreach (var entry in entries)
        {
            await _repository.AddAsync(entry);
        }
    }

    public async Task<List<TreeEntry>> GetAllForExportAsync()
    {
        return await _repository.GetAllAsync();
    }
}
