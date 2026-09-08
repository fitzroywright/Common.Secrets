using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Common.Secrets;

public sealed class BitwardenSecretProvider : SecretProviderBase
{
    private readonly BitwardenSecretsManagerOptions options;
    private readonly ConcurrentDictionary<string, string> memoryCache = new(StringComparer.OrdinalIgnoreCase);

    public BitwardenSecretProvider(BitwardenSecretsManagerOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public override async Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!options.Enabled)
        {
            return null;
        }

        if (memoryCache.TryGetValue(name, out string? cached))
        {
            return cached;
        }

        if (!options.SecretIds.TryGetValue(name, out string? secretId) || string.IsNullOrWhiteSpace(secretId))
        {
            return null;
        }

        if (!Guid.TryParse(secretId, out _))
        {
            throw new InvalidOperationException($"The Bitwarden secret ID configured for '{name}' is not a valid UUID.");
        }

        string tokenEnvironmentVariable = string.IsNullOrWhiteSpace(options.AccessTokenEnvironmentVariable)
            ? "BWS_ACCESS_TOKEN"
            : options.AccessTokenEnvironmentVariable.Trim();
        string? accessToken = Environment.GetEnvironmentVariable(tokenEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                $"Bitwarden Secrets Manager is enabled but bootstrap token '{tokenEnvironmentVariable}' is not set.");
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = string.IsNullOrWhiteSpace(options.CliPath) ? "bws" : options.CliPath.Trim(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(options.ServerUrl))
        {
            startInfo.ArgumentList.Add("--server-url");
            startInfo.ArgumentList.Add(options.ServerUrl.Trim());
        }

        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("secret");
        startInfo.ArgumentList.Add("get");
        startInfo.ArgumentList.Add(secretId);
        startInfo.Environment["BWS_ACCESS_TOKEN"] = accessToken;
        startInfo.Environment["NO_COLOR"] = "1";

        using Process process = new() { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Bitwarden Secrets Manager CLI could not be started.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Unable to start Bitwarden Secrets Manager CLI '{startInfo.FileName}'. Ensure bws is installed and on PATH.",
                exception);
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.CommandTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(15) : options.CommandTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException("Bitwarden Secrets Manager CLI did not respond before the configured timeout.");
        }

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Bitwarden Secrets Manager could not resolve '{name}' (bws exit code {process.ExitCode}). {stderr}".Trim());
        }

        string value = ParseSecretValue(stdout, name);
        memoryCache.TryAdd(name, value);
        return value;
    }

    internal static string ParseSecretValue(string json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"Bitwarden returned no data for '{name}'.");
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            if (root.GetArrayLength() != 1)
            {
                throw new InvalidOperationException($"Bitwarden returned an unexpected number of objects for '{name}'.");
            }

            root = root[0];
        }

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("value", out JsonElement valueElement) ||
            valueElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Bitwarden returned an invalid secret object for '{name}'.");
        }

        string? value = valueElement.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Bitwarden secret '{name}' has an empty value.");
        }

        return value;
    }
}
