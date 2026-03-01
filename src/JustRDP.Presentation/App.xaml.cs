using System.IO;
using System.Windows;
using JustRDP.Application.Services;
using JustRDP.Domain.Interfaces;
using JustRDP.Infrastructure.Persistence;
using JustRDP.Infrastructure.Persistence.Repositories;
using JustRDP.Infrastructure.Security;
using JustRDP.Infrastructure.Services;
using JustRDP.Presentation.Services;
using JustRDP.Presentation.Themes;
using JustRDP.Presentation.ViewModels;
using JustRDP.Presentation.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace JustRDP.Presentation;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration))
            .ConfigureServices((context, services) =>
            {
                var dbPath = ResolveDatabasePath(context.Configuration);
                services.AddDbContext<JustRdpDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));

                // Repositories
                services.AddScoped<ITreeEntryRepository, TreeEntryRepository>();
                services.AddScoped<ISettingsRepository, SettingsRepository>();
                services.AddSingleton<ICredentialEncryptor, DpapiCredentialEncryptor>();

                // Application services
                services.AddScoped<TreeService>();
                services.AddScoped<CredentialInheritanceService>();
                services.AddScoped<ImportExportService>();

                // Theme
                services.AddScoped<ThemeManager>();

                // Network scanning
                services.AddTransient<INetworkScanner, NetworkScanner>();

                // Availability monitoring
                services.AddTransient<IAvailabilityChecker, AvailabilityChecker>();
                services.AddScoped<AvailabilityMonitorService>();

                // ViewModels
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<NetworkScanViewModel>();

                // Views
                services.AddTransient<MainWindow>();
                services.AddTransient<NetworkScanWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled UI exception");
            args.Handled = true;
        };

        Log.Information("JustRDP starting");

        await _host.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JustRdpDbContext>();
            await MigrateDatabaseAsync(db);
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("JustRDP shutting down");
        await _host.StopAsync();
        _host.Dispose();
        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }

    private static async Task MigrateDatabaseAsync(JustRdpDbContext db)
    {
        // Check if this is a legacy database created by EnsureCreated (has tables but no migration history).
        // In that case, the schema already matches InitialCreate, so we seed the history and then apply
        // any subsequent migrations.
        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingMigrations.Count > 0)
        {
            bool tablesExist = false;
            try
            {
                // SQLite-specific check: if the TreeEntries table exists, this is a legacy DB
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT 1 FROM TreeEntries LIMIT 1");
                tablesExist = true;
            }
            catch
            {
                // Table doesn't exist — fresh database
            }

            if (tablesExist && pendingMigrations.Contains("20260301232951_InitialCreate"))
            {
                // Legacy DB: mark InitialCreate as already applied, then run remaining migrations
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
                    "\"MigrationId\" TEXT NOT NULL PRIMARY KEY, " +
                    "\"ProductVersion\" TEXT NOT NULL)");
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                    "VALUES ('20260301232951_InitialCreate', '10.0.0')");

                // Now add any columns that the legacy DB might be missing (SSH support)
                var alterStatements = new[]
                {
                    "ALTER TABLE TreeEntries ADD COLUMN ConnectionType INTEGER DEFAULT 0",
                    "ALTER TABLE TreeEntries ADD COLUMN SshPrivateKeyPath TEXT",
                    "ALTER TABLE TreeEntries ADD COLUMN SshPrivateKeyPassphraseEncrypted BLOB",
                    "ALTER TABLE TreeEntries ADD COLUMN SshTerminalFontFamily TEXT",
                    "ALTER TABLE TreeEntries ADD COLUMN SshTerminalFontSize REAL",
                };

                foreach (var sql in alterStatements)
                {
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(sql);
                    }
                    catch
                    {
                        // Column already exists — ignore
                    }
                }
            }
        }

        // Apply any remaining pending migrations
        await db.Database.MigrateAsync();
    }

    private static string ResolveDatabasePath(IConfiguration configuration)
    {
        var configuredPath = configuration["Database:Path"]
            ?? "%LOCALAPPDATA%/JustRDP/justrdp.db";

        var expanded = Environment.ExpandEnvironmentVariables(configuredPath);
        var normalized = Path.GetFullPath(expanded);
        var directory = Path.GetDirectoryName(normalized);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return normalized;
    }
}
