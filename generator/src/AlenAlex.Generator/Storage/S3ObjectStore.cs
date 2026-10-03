using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace AlenAlex.Generator.Storage;

internal sealed class S3ObjectStore(IAmazonS3 client, string bucket) : IObjectStore, IDisposable
{
    /// <summary>
    /// <c>x-amz-meta-file-metadata</c>: SHA-256 of the content. Objects uploaded earlier carry the same key,
    /// so they are recognised as unchanged.
    /// </summary>
    public const string HashMetadataKey = "file-metadata";

    public static S3ObjectStore ForR2(R2Options options) =>
        new(new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), CreateR2Config(options)), options.Bucket);

    internal static AmazonS3Config CreateR2Config(R2Options options) => new()
    {
        ServiceURL = options.Endpoint.ToString(),
        AuthenticationRegion = R2Options.Region,
        ForcePathStyle = true,
        // Recent SDKs send CRC checksums on every request by default, and R2 rejects some of them.
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        MaxErrorRetry = 2,
        Timeout = TimeSpan.FromSeconds(60),
    };

    public async Task<RemoteObject?> HeadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = bucket, Key = key }, cancellationToken);

            var hash = response.Metadata?[HashMetadataKey];
            return new RemoteObject(
                string.IsNullOrEmpty(hash) ? null : hash,
                response.Headers?.ContentType,
                response.Headers?.CacheControl,
                response.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw ToObjectStoreException("HEAD", key, ex);
        }
    }

    public async Task PutAsync(ObjectUpload upload, CancellationToken cancellationToken)
    {
        try
        {
            await client.PutObjectAsync(CreatePutRequest(upload), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw ToObjectStoreException("PUT", upload.Key, ex);
        }
    }

    internal PutObjectRequest CreatePutRequest(ObjectUpload upload)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = upload.Key,
            InputStream = new MemoryStream(upload.Content, writable: false),
            ContentType = upload.ContentType,
            // R2 rejects streaming (aws-chunked) SigV4 payloads.
            DisablePayloadSigning = true,
            UseChunkEncoding = false,
        };
        request.Headers.CacheControl = upload.CacheControl;
        request.Metadata.Add(HashMetadataKey, upload.ContentSha256);
        return request;
    }

    /// <summary>A one-line message with a hint about which setting to check.</summary>
    internal static ObjectStoreException ToObjectStoreException(string operation, string key, Exception ex)
    {
        if (ex is AmazonServiceException { StatusCode: not 0 } service)
        {
            var status = service.StatusCode;
            var code = service.ErrorCode;
            var hint = status switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    $" (check {R2Options.AccessKeyKey} / {R2Options.SecretKeyKey} and that the token can write to the bucket)",
                HttpStatusCode.BadRequest when operation == "HEAD" =>
                    $" (often an invalid {R2Options.AccountIdKey} or credentials)",
                _ when code == "NoSuchBucket" => $" (check {R2Options.BucketKey})",
                _ => string.Empty,
            };

            var detail = string.IsNullOrEmpty(code) ? status.ToString() : $"{code}: {service.Message}";
            var isFatal = status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest
                          || code is "NoSuchBucket" or "InvalidAccessKeyId" or "SignatureDoesNotMatch";

            return new ObjectStoreException(
                $"{operation} {key} failed: HTTP {(int)status} {detail}{hint}", status, isFatal, ex);
        }

        // DNS failures, TLS errors, timeouts: nothing else will succeed either.
        return new ObjectStoreException(
            $"{operation} {key} failed: {ex.GetBaseException().Message} (check {R2Options.AccountIdKey} and network access)",
            isFatal: true, inner: ex);
    }

    public void Dispose() => client.Dispose();
}
