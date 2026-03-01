using JustRDP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JustRDP.Infrastructure.Persistence.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly JustRdpDbContext _db;

    public SettingsRepository(JustRdpDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _db.AppSettings.FindAsync(key);
        return setting?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        var setting = await _db.AppSettings.FindAsync(key);
        if (setting is null)
        {
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        return await _db.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
    }
}
