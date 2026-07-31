using Melper.Data;
using Microsoft.JSInterop;

namespace Melper.Web;

/// <summary>
/// Keeps edited unit stats alive for one browser tab. sessionStorage rather than
/// localStorage is the whole point: a new tab starts from the roster in the repository,
/// while the tab that did the editing keeps its numbers across page switches and reloads.
/// </summary>
public static class UnitsStorage
{
    private const string Key = "units";

    public static async Task LoadAsync(IJSRuntime js)
    {
        string? saved;
        try
        {
            saved = await js.InvokeAsync<string?>("sessionStorage.getItem", Key);
        }
        catch
        {
            // No storage available (private mode, blocked cookies) — the shipped roster stands.
            return;
        }

        if (string.IsNullOrWhiteSpace(saved))
        {
            return;
        }

        try
        {
            UnitsCollection.Replace(UnitsJson.Deserialize(saved));
        }
        catch
        {
            // A payload from an older shape would otherwise throw on every load and leave
            // the tab stuck with no reachable way to reset. Drop it and carry on.
            UnitsCollection.Replace(UnitsCollection.Defaults());
            await ClearAsync(js);
        }
    }

    public static async Task SaveAsync(IJSRuntime js, IEnumerable<Unit> units)
    {
        UnitsCollection.Replace(units);
        await js.InvokeVoidAsync("sessionStorage.setItem", Key, UnitsJson.Serialize(UnitsCollection.Units));
    }

    public static async Task ResetAsync(IJSRuntime js)
    {
        UnitsCollection.Replace(UnitsCollection.Defaults());
        await ClearAsync(js);
    }

    private static Task ClearAsync(IJSRuntime js) => js.InvokeVoidAsync("sessionStorage.removeItem", Key).AsTask();
}
