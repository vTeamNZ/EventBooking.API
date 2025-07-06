using EventBooking.API.Services;

namespace EventBooking.API.Services
{
    public class ImageService : IImageService
    {
        private readonly ILogger<ImageService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public ImageService(ILogger<ImageService> logger, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _logger = logger;
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<string> SaveImageAsync(IFormFile image, string folder = "events")
        {
            try
            {
                // Validate image file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(image.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    throw new ArgumentException("Invalid image format. Allowed formats: JPG, JPEG, PNG, GIF, WEBP");
                }
                
                // Check file size (limit to 5MB)
                if (image.Length > 5 * 1024 * 1024)
                {
                    throw new ArgumentException("Image file size cannot exceed 5MB");
                }
                
                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var apiImageFolder = Path.Combine(_environment.WebRootPath, folder);
                
                // Ensure API events directory exists
                Directory.CreateDirectory(apiImageFolder);
                
                var apiFilePath = Path.Combine(apiImageFolder, fileName);
                
                // Save the image to API wwwroot
                using (var stream = new FileStream(apiFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }
                
                // Also copy to frontend public folder if it exists
                await CopyToFrontendAsync(apiFilePath, fileName, folder);
                
                _logger.LogInformation("Image uploaded successfully: {FileName}", fileName);
                
                // Return the relative URL
                return $"/{folder}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image");
                throw;
            }
        }

        public async Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                    return false;

                // Extract filename from URL
                var fileName = Path.GetFileName(imageUrl);
                var folder = Path.GetDirectoryName(imageUrl)?.Replace("\\", "/").TrimStart('/') ?? "events";
                
                // Delete from API wwwroot
                var apiFilePath = Path.Combine(_environment.WebRootPath, folder, fileName);
                if (File.Exists(apiFilePath))
                {
                    File.Delete(apiFilePath);
                }
                
                // Delete from frontend public folder if it exists
                await DeleteFromFrontendAsync(fileName, folder);
                
                _logger.LogInformation("Image deleted successfully: {FileName}", fileName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image: {ImageUrl}", imageUrl);
                return false;
            }
        }

        public string GetImagePath(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return string.Empty;

            var fileName = Path.GetFileName(imageUrl);
            var folder = Path.GetDirectoryName(imageUrl)?.Replace("\\", "/").TrimStart('/') ?? "events";
            
            return Path.Combine(_environment.WebRootPath, folder, fileName);
        }

        private async Task CopyToFrontendAsync(string sourceFilePath, string fileName, string folder)
        {
            try
            {
                // Try to find the frontend public folder
                var currentDir = Directory.GetCurrentDirectory();
                var frontendPath = Path.Combine(Path.GetDirectoryName(currentDir) ?? "", "event-booking-frontend", "public", folder);
                
                if (Directory.Exists(frontendPath))
                {
                    var frontendFilePath = Path.Combine(frontendPath, fileName);
                    using (var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read))
                    using (var destinationStream = new FileStream(frontendFilePath, FileMode.Create))
                    {
                        await sourceStream.CopyToAsync(destinationStream);
                    }
                    _logger.LogInformation("Image copied to frontend: {FrontendPath}", frontendFilePath);
                }
                else
                {
                    _logger.LogWarning("Frontend public folder not found at: {FrontendPath}", frontendPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not copy image to frontend folder");
                // Don't throw - this is optional
            }
        }

        private async Task DeleteFromFrontendAsync(string fileName, string folder)
        {
            try
            {
                var currentDir = Directory.GetCurrentDirectory();
                var frontendPath = Path.Combine(Path.GetDirectoryName(currentDir) ?? "", "event-booking-frontend", "public", folder);
                
                if (Directory.Exists(frontendPath))
                {
                    var frontendFilePath = Path.Combine(frontendPath, fileName);
                    if (File.Exists(frontendFilePath))
                    {
                        File.Delete(frontendFilePath);
                        _logger.LogInformation("Image deleted from frontend: {FrontendPath}", frontendFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete image from frontend folder");
                // Don't throw - this is optional
            }
        }
    }
}
