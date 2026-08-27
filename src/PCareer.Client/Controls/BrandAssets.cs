namespace PCareer.Client;

internal static class BrandAssets
{
    private const string LogoResourceName = "PCareer.Client.Resources.BrandLogo.png";
    private const string IconResourceName = "PCareer.Client.Resources.AppIcon.ico";
    private const string LogoToken = "{{BRAND_LOGO_DATA_URI}}";

    private static readonly Lazy<string> LogoDataUri = new(CreateLogoDataUri);

    public static Icon ApplicationIcon { get; } = LoadApplicationIcon();

    public static string AddLogoToHtml(string html) =>
        html.Replace(LogoToken, LogoDataUri.Value, StringComparison.Ordinal);

    private static string CreateLogoDataUri()
    {
        using var stream = typeof(BrandAssets).Assembly.GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{LogoResourceName}' was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return $"data:image/png;base64,{Convert.ToBase64String(memory.ToArray())}";
    }

    private static Icon LoadApplicationIcon()
    {
        using var stream = typeof(BrandAssets).Assembly.GetManifestResourceStream(IconResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{IconResourceName}' was not found.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
