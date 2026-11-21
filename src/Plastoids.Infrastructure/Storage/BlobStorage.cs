using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Plastoids.Infrastructure.Storage;

/// <summary>Cloud-neutral object storage. MinIO locally, S3-compatible in production (Azure adapter is a one-file addition).</summary>
public interface IBlobStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
    Task<Stream?> GetAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
    Task<Uri> GetPresignedUploadUrlAsync(string key, string contentType, TimeSpan validFor, CancellationToken cancellationToken);
    Task<Uri> GetPresignedDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken cancellationToken);
}

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";
    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public string Bucket { get; set; } = "plastoids";
    public string AccessKey { get; set; } = "plastoids";
    public string SecretKey { get; set; } = "plastoids-secret";
    public bool ForcePathStyle { get; set; } = true;
}

internal sealed class S3BlobStorage : IBlobStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3BlobStorage(IOptions<BlobStorageOptions> options)
    {
        var o = options.Value;
        _bucket = o.Bucket;
        _client = new AmazonS3Client(o.AccessKey, o.SecretKey, new AmazonS3Config
        {
            ServiceURL = o.ServiceUrl,
            ForcePathStyle = o.ForcePathStyle,
            AuthenticationRegion = "us-east-1",
        });
    }

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        => _client.PutObjectAsync(new PutObjectRequest { BucketName = _bucket, Key = key, InputStream = content, ContentType = contentType }, cancellationToken);

    public async Task<Stream?> GetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
        => _client.DeleteObjectAsync(_bucket, key, cancellationToken);

    public async Task<Uri> GetPresignedUploadUrlAsync(string key, string contentType, TimeSpan validFor, CancellationToken cancellationToken)
    {
        var url = await _client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
#pragma warning disable RS0030 // AWS SDK requires an absolute DateTime here
            Expires = DateTime.UtcNow.Add(validFor),
#pragma warning restore RS0030
            Protocol = Protocol.HTTP,
        });
        return new Uri(url);
    }

    public async Task<Uri> GetPresignedDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken cancellationToken)
    {
        var url = await _client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
#pragma warning disable RS0030 // AWS SDK requires an absolute DateTime here
            Expires = DateTime.UtcNow.Add(validFor),
#pragma warning restore RS0030
            Protocol = Protocol.HTTP,
        });
        return new Uri(url);
    }

    public void Dispose() => _client.Dispose();
}
