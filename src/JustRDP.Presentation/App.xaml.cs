using System.IO;
using System.Windows;
using JustRDP.Application.Services;
using JustRDP.Domain.Interfaces;
using JustRDP.Infrastructure.Persistence;
using JustRDP.Infrastructure.Persistence.Repositories;
using JustRDP.Infrastructure.Security;
using JustRDP.Infrastructure.Services;
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
            await db.Database.EnsureCreatedAsync();
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
