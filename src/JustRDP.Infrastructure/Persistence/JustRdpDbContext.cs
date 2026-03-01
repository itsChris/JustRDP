using JustRDP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JustRDP.Infrastructure.Persistence;

public class JustRdpDbContext : DbContext
{
    public DbSet<TreeEntry> TreeEntries => Set<TreeEntry>();
    public DbSet<FolderEntry> Folders => Set<FolderEntry>();
    public DbSet<ConnectionEntry> Connections => Set<ConnectionEntry>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public JustRdpDbContext(DbContextOptions<JustRdpDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JustRdpDbContext).Assembly);
    }
}

public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
