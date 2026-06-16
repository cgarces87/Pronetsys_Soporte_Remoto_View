#nullable enable

using MessagePack;
using Pronetsys.Shared.Models;
using Pronetsys.Shared.Primitives;

namespace Pronetsys.Shared.Services;

public interface IEmbeddedServerDataProvider
{
    string GetEncodedFileName(string filePath, EmbeddedServerData serverData, string? downloadName = null);
    Result<EmbeddedServerData> TryGetEmbeddedData(string filePath);
}

public class EmbeddedServerDataProvider : IEmbeddedServerDataProvider
{
    public static EmbeddedServerDataProvider Instance { get; } = new();

    public string GetEncodedFileName(string filePath, EmbeddedServerData serverData, string? downloadName = null)
    {
        // The prefix (before the brackets) is purely cosmetic; only the data inside
        // the [brackets] is read back by TryGetEmbeddedData. So callers may pass a
        // friendly downloadName without breaking the embedded-server-data mechanism.
        var fileName = string.IsNullOrWhiteSpace(downloadName)
            ? Path.GetFileNameWithoutExtension(filePath)
            : downloadName;
        var ext = Path.GetExtension(filePath);

        // Make the base64 string safe file paths and URIs.
        var encodedData = Convert
            .ToBase64String(MessagePackSerializer.Serialize(serverData))
            .Replace("/", "_")
            .Replace("+", "-");

        return $"{fileName}[{encodedData}]{ext}";
    }

    public Result<EmbeddedServerData> TryGetEmbeddedData(string filePath)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var start = fileName.LastIndexOf('[') + 1;
            var end = fileName.LastIndexOf(']');
            var base64 = fileName[start..end]
                .Replace("_", "/")
                .Replace("-", "+");

            var embeddedData = MessagePackSerializer.Deserialize<EmbeddedServerData>(Convert.FromBase64String(base64));
            return Result.Ok(embeddedData);
        }
        catch (Exception ex)
        {
            return Result.Fail<EmbeddedServerData>(ex);
        }
    }
}
