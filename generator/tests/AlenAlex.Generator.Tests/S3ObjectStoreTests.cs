using System.Net;
using System.Net.Http;
using Amazon.Runtime;
using Amazon.S3;
using AlenAlex.Generator.Storage;

namespace AlenAlex.Generator.Tests;

public class S3ObjectStoreTests
{
    private static readonly R2Options Options = new() { AccountId = "acc123", AccessKey = "AKID", SecretKey = "SECRET", Bucket = "my-bucket" };

    [Fact]
    public void R2_config_uses_account_endpoint_auto_region_path_style_and_relaxed_checksums()
    {
        var config = S3ObjectStore.CreateR2Config(Options);

        Assert.StartsWith("https://acc123.r2.cloudflarestorage.com", config.ServiceURL);
        Assert.Equal("auto", config.AuthenticationRegion);
        Assert.True(config.ForcePathStyle);
        Assert.Equal(RequestChecksumCalculation.WHEN_REQUIRED, config.RequestChecksumCalculation);
        Assert.Equal(ResponseChecksumValidation.WHEN_REQUIRED, config.ResponseChecksumValidation);
    }

    [Fact]
    public void Put_request_carries_hash_metadata_headers_and_r2_payload_settings()
    {
        using var store = S3ObjectStore.ForR2(Options);
        var upload = new ObjectUpload("assets/hotlink-ok/p/a.png", [1, 2, 3], "deadbeef", "image/png", "public, max-age=86400");

        var request = store.CreatePutRequest(upload);

        Assert.Equal("my-bucket", request.BucketName);
        Assert.Equal("assets/hotlink-ok/p/a.png", request.Key);
        Assert.Equal("image/png", request.ContentType);
        Assert.Equal("public, max-age=86400", request.Headers.CacheControl);
        Assert.Equal("deadbeef", request.Metadata[S3ObjectStore.HashMetadataKey]);
        Assert.True(request.DisablePayloadSigning);
        Assert.False(request.UseChunkEncoding);
        Assert.Equal(3, request.InputStream.Length);
    }

    [Fact]
    public void Forbidden_is_fatal_with_a_credentials_hint()
    {
        var ex = S3ObjectStore.ToObjectStoreException("HEAD", "k",
            new AmazonS3Exception("Forbidden") { StatusCode = HttpStatusCode.Forbidden });

        Assert.True(ex.IsFatal);
        Assert.Contains("HTTP 403", ex.Message);
        Assert.Contains("R2:AccessKey", ex.Message);
    }

    [Fact]
    public void No_such_bucket_names_the_error_code_and_bucket_variable()
    {
        var ex = S3ObjectStore.ToObjectStoreException("PUT", "k",
            new AmazonS3Exception("The specified bucket does not exist.") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchBucket" });

        Assert.True(ex.IsFatal);
        Assert.Contains("NoSuchBucket: The specified bucket does not exist.", ex.Message);
        Assert.Contains("R2:Bucket", ex.Message);
    }

    [Fact]
    public void Network_failures_are_fatal_and_show_the_root_cause()
    {
        var ex = S3ObjectStore.ToObjectStoreException("HEAD", "k",
            new HttpRequestException("outer", new IOException("handshake failure")));

        Assert.True(ex.IsFatal);
        Assert.Contains("handshake failure", ex.Message);
        Assert.Contains("R2:AccountId", ex.Message);
    }
}
