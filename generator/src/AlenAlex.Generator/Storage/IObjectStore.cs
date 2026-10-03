using System.Net;

namespace AlenAlex.Generator.Storage;

/// <param name="ContentHash">Hex SHA-256 stored in the <c>x-amz-meta-file-metadata</c> header, if any.</param>
internal sealed record RemoteObject(string? ContentHash, string? ContentType, string? CacheControl, long? ContentLength);

internal sealed record ObjectUpload(string Key, byte[] Content, string ContentSha256, string ContentType, string CacheControl);

internal interface IObjectStore
{
    /// <summary>Returns the object's metadata, or <c>null</c> when it does not exist.</summary>
    Task<RemoteObject?> HeadAsync(string key, CancellationToken cancellationToken);

    Task PutAsync(ObjectUpload upload, CancellationToken cancellationToken);
}

internal sealed class ObjectStoreException(string message, HttpStatusCode? statusCode = null, bool isFatal = false, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;

    /// <summary>Bad credentials, unknown bucket or unreachable endpoint: no point trying the other files.</summary>
    public bool IsFatal { get; } = isFatal;
}
