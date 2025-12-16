using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Authorize] // JWT zorunlu
    [ApiController]
    public class FilesController : ControllerBase
    {
        [HttpGet("uploads/{fileName}")]
        public IActionResult GetFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest("Dosya adı geçersiz.");

            // Path traversal engeli (../ vs kaldır)
            fileName = Path.GetFileName(fileName);

            var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "UploadsStorage");
            var fullPath = Path.Combine(uploadRoot, fileName);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Dosya bulunamadı.");

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, contentType);
        }
    }
}
