using Amazon.S3;
using Amazon.S3.Transfer;

namespace Cylo_Backend.Services
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string folderName);
    }

    public class R2StorageService : IStorageService
    {
        private readonly IConfiguration _config;
        private readonly IAmazonS3 _s3Client;

        public R2StorageService(IConfiguration config)
        {
            _config = config;
            var options = _config.GetSection("CloudflareR2");

            var s3Config = new AmazonS3Config
            {
                ServiceURL = $"https://{options["AccountId"]}.r2.cloudflarestorage.com",
            };

            _s3Client = new AmazonS3Client(options["AccessKey"], options["SecretKey"], s3Config);
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            var options = _config.GetSection("CloudflareR2");
            var fileName = $"{folderName}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

            using var newStream = new MemoryStream();
            await file.CopyToAsync(newStream);
            newStream.Position = 0; // Essential: Reset stream to start

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = newStream,
                Key = fileName,
                BucketName = options["BucketName"],
                ContentType = file.ContentType,
                // THIS IS THE FIX: Disable chunked encoding to avoid the trailer error
                DisablePayloadSigning = true
            };

            // Wait, if DisablePayloadSigning is still causing a build error on the request object:
            // USE THIS INSTEAD:
            var fileTransferUtility = new TransferUtility(_s3Client);

            // Some versions of the SDK require setting this on the underlying config
            await fileTransferUtility.UploadAsync(uploadRequest);

            return $"{options["PublicUrl"]}/{fileName}";
        }
    }
}