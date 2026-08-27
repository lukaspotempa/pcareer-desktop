using Microsoft.Web.WebView2.Core;

namespace PCareer.Client;

internal static class WebViewRuntime
{
    private static readonly string UserDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VirtualPilotNetwork",
        "WebView2");

    public static Task<CoreWebView2Environment> CreateEnvironmentAsync() =>
        CoreWebView2Environment.CreateAsync(userDataFolder: UserDataDirectory);
}
