using JustRDP.Domain.Interfaces;
using MaterialDesignThemes.Wpf;

namespace JustRDP.Presentation.Themes;

public class ThemeManager
{
    private const string ThemeKey = "Theme";
    private readonly ISettingsRepository _settings;

    public ThemeManager(ISettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<bool> LoadThemeAsync()
    {
        var value = await _settings.GetAsync(ThemeKey);
        var isDark = value != "Light"; // Default to dark
        ApplyTheme(isDark);
        return isDark;
    }

    public async Task SetThemeAsync(bool isDark)
    {
        ApplyTheme(isDark);
        await _settings.SetAsync(ThemeKey, isDark ? "Dark" : "Light");
    }

    private static void ApplyTheme(bool isDark)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);
    }
}
