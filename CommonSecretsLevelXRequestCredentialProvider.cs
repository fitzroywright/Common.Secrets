using Common.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace Common.Secrets;

public sealed class CommonSecretsLevelXRequestCredentialProvider(
    ISecretProvider secrets,
    IConfiguration configuration) : ILevelXRequestCredentialProvider
{
    public async ValueTask<string?> GetCredentialAsync(
        Microsoft.AspNetCore.Http.HttpContext context,
        CancellationToken cancellationToken = default)
    {
        string? secretName = configuration["Aegis:Diagnostics:RequestCredentialSecretName"]?.Trim();
        if (string.IsNullOrWhiteSpace(secretName))
            return null;

        string? value = await secrets.GetAsync(secretName, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
