using Microsoft.AspNetCore.Mvc;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly string _imagesRoot;

    public ImagesController(IWebHostEnvironment env)
    {
        _imagesRoot = Path.Combine(env.ContentRootPath, "Data", "Images");
        Directory.CreateDirectory(_imagesRoot);
    }

    [HttpGet("{**path}")]
    public IActionResult GetImage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return BadRequest();

        // Prevent directory traversal
        var fullPath = Path.GetFullPath(Path.Combine(_imagesRoot, path));
        if (!fullPath.StartsWith(_imagesRoot))
            return BadRequest("Invalid path");

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };

        return PhysicalFile(fullPath, contentType);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string? folder = "cards")
    {
        if (file.Length == 0)
            return BadRequest("No file uploaded");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"))
            return BadRequest("Invalid file type");

        var folderPath = Path.Combine(_imagesRoot, folder ?? "cards");
        Directory.CreateDirectory(folderPath);

        var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid().ToString("N")[..8]}{ext}";
        var filePath = Path.Combine(folderPath, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var relativePath = Path.Combine(folder ?? "cards", fileName).Replace('\\', '/');
        return Ok(new { path = relativePath });
    }
}
