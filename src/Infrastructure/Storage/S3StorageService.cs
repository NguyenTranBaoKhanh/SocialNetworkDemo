using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Application.Common;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

/// <summary>
/// Lưu media lên MinIO (S3-compatible). MinIO có sẵn trong docker-compose (cổng 9000).
/// Bucket giữ private; ảnh được phục vụ qua API (proxy) nên không cần public bucket.
/// </summary>
public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly MinioOptions _opt;
    private bool _bucketEnsured;

    public S3StorageService(IOptions<MinioOptions> opt)
    {
        _opt = opt.Value;
        var config = new AmazonS3Config
        {
            ServiceURL = _opt.Endpoint,
            ForcePathStyle = true,   // BẮT BUỘC cho MinIO (không dùng virtual-host style)
            AuthenticationRegion = "us-east-1",
        };
        _s3 = new AmazonS3Client(new BasicAWSCredentials(_opt.AccessKey, _opt.SecretKey), config);
    }

    public async Task<string> UploadAsync(
        Stream content, string contentType, string extension, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);

        var key = $"{Guid.NewGuid():N}{extension}";
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opt.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
        }, ct);

        return key;
    }

    public async Task<StoredObject?> GetAsync(string objectKey, CancellationToken ct = default)
    {
        try
        {
            var res = await _s3.GetObjectAsync(_opt.Bucket, objectKey, ct);
            return new StoredObject(res.ResponseStream, res.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketEnsured) return;
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3, _opt.Bucket);
        if (!exists)
            await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _opt.Bucket }, ct);
        _bucketEnsured = true;
    }
}
