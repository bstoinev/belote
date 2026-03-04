using System.Globalization;
using Microsoft.JSInterop;

namespace Belote.Client.Services;

public sealed class CultureService(IJSRuntime js)
{
    private const string StorageKey = "belote.culture";

    public async Task InitializeAsync()
    {
        var stored = await js.InvokeAsync<string?>("beloteCulture.get");
        if (!string.IsNullOrWhiteSpace(stored))
        {
            SetCulture(stored);
        }
        else
        {
            SetCulture("en");
        }
    }

    public async Task ChangeCultureAsync(string culture)
    {
        SetCulture(culture);
        await js.InvokeVoidAsync("beloteCulture.set", culture);
        await js.InvokeVoidAsync("beloteCulture.reload");
    }

    private static void SetCulture(string culture)
    {
        var ci = new CultureInfo(culture);
        CultureInfo.DefaultThreadCurrentCulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci;
    }
}

