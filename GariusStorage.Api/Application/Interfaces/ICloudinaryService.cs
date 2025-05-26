namespace GariusStorage.Api.Application.Interfaces
{
    public interface ICloudinaryService
    {
        Task<string> UploadImageAsync(byte[] imageBytes, string userId, string imgName = "user_photo", string folderName = "profile_pictures");
    }
}
