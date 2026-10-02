using System.Text.RegularExpressions;
using HrServiceDesk.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Infrastructure.Files;

/// <summary>
/// Stores files on local disk under <c>{tenant}/{yyyy}/{MM}/{random}</c>. File names never reach the
/// file system, and keys are checked against that exact shape before use, so paths cannot be traversed.
/// </summary>
internal sealed partial class LocalFileStorage(IOptions<StorageOptions> options, TimeProvider clock) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Guid tenantId, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var now = clock.GetUtcNow();
        var key = $"{tenantId:N}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}";
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new FileStream(PathFor(key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        File.Delete(PathFor(key));
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        if (!KeyPattern().IsMatch(key))
            throw new ArgumentException("Invalid storage key.", nameof(key));
        return Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
    }

    [GeneratedRegex("^[0-9a-f]{32}/[0-9]{4}/[0-9]{2}/[0-9a-f]{32}$")]
    private static partial Regex KeyPattern();
}
