namespace Common.Secrets;

public interface ISecretProvider
{
    Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<string> GetRequiredAsync(
        string name,
        CancellationToken cancellationToken = default);
}
