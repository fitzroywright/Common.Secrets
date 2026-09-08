using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Common.Secrets;

public sealed class BitwardenSecretProvider : SecretProviderBase, ISecretProviderHealth
{
    private readonly BitwardenSecretsManagerOptions options;
    private readonly ConcurrentDictionary<string, string> memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private Exception? lastError;

    public BitwardenSecretProvider(BitwardenSecretsManagerOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string ProviderName => "Bitwarden";

    public bool IsEnabled => options.Enabled;

    public Exception? LastError => lastError;

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

        try
        {
            string value = await GetSecretByIdAsync(name, secretId, cancellationToken).ConfigureAwait(false);
            memoryCache.TryAdd(name, value);
            lastError = null;
            return value;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            lastError = exception;
            throw;
        }
    }

    public async Task<SecretProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return new SecretProviderHealth(ProviderName, false, false, "Provider is disabled.");
        }

        try
        {
            ValidateBootstrap();
            string cliPath = string.IsNullOrWhiteSpace(options.CliPath) ? "bws" : options.CliPath.Trim();
            ProcessStartInfo startInfo = new()
            {
                FileName = cliPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--version");

            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException("Bitwarden Secrets Manager CLI could not be started.");
            }

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(options.CommandTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(15) : options.CommandTimeout);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                string stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"Bitwarden Secrets Manager CLI health check failed. {stderr}".Trim());
            }

            lastError = null;
            return new SecretProviderHealth(ProviderName, true, true, "Bitwarden Secrets Manager CLI and bootstrap token are available.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            lastError = exception;
            return new SecretProviderHealth(ProviderName, true, false, exception.Message, exception);
        }
    }

    private async Task<string> GetSecretByIdAsync(string name, string secretId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(secretId, out _))
        {
            throw new InvalidOperationException($"The Bitwarden secret ID configured for '{name}' is not a valid UUID.");
        }

        string accessToken = ValidateBootstrap();
        ProcessStartInfo startInfo = CreateStartInfo(accessToken, secretId);

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

        return ParseSecretValue(stdout, name);
    }

    private string ValidateBootstrap()
    {
        string tokenEnvironmentVariable = string.IsNullOrWhiteSpace(options.AccessTokenEnvironmentVariable)
            ? "BWS_ACCESS_TOKEN"
            : options.AccessTokenEnvironmentVariable.Trim();
        string? accessToken = Environment.GetEnvironmentVariable(tokenEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                $"Bitwarden Secrets Manager is enabled but bootstrap token '{tokenEnvironmentVariable}' is not set.");
        }

        return accessToken;
    }

    private ProcessStartInfo CreateStartInfo(string accessToken, string secretId)
    {
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
        return startInfo;
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
