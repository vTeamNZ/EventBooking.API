namespace EventBooking.API.Services
{
    public interface IImageService
    {
        Task<string> SaveImageAsync(IFormFile image, string folder = "events");
        Task<bool> DeleteImageAsync(string imageUrl);
        string GetImagePath(string imageUrl);
    }
}
