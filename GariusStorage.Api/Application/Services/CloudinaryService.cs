using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Configuration;
using Microsoft.Extensions.Options;

namespace GariusStorage.Api.Application.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IOptions<CloudinarySettings> config)
        {
            var account = new Account(config.Value.CloudName, config.Value.ApiKey, config.Value.ApiSecret);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadImageAsync(byte[] imageBytes, string userId, string imgName = "user_photo", string folderName = "profile_pictures")
        {
            using var stream = new MemoryStream(imageBytes);
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(imgName, stream),
                PublicId = $"{folderName}/{userId}",
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult.SecureUrl.ToString(); // URL pública da imagem
        }
    }
}
