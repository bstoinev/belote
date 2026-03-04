using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace Belote.Client.Services;

public sealed class ThemeService(IConfiguration config, NavigationManager nav)
{
    public string ActiveThemeName => GetThemeFromQuery() ?? config["Theme:Name"] ?? "default";

    public string TableBackgroundPath => $"/themes/{ActiveThemeName}/table.svg";
    public string CardBackPath => $"/themes/{ActiveThemeName}/card-back.svg";

    public string CardFacePath(Belote.Engine.Card card) => $"/themes/{ActiveThemeName}/cards/{card}.svg";

    private string? GetThemeFromQuery()
    {
        var uri = new Uri(nav.Uri);
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            if (string.Equals(parts[0], "theme", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }
}

