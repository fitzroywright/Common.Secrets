namespace Common.Secrets;

public sealed class BitwardenPasswordManagerOptions
{
    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = "http://127.0.0.1:8087/";

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(5);
}
