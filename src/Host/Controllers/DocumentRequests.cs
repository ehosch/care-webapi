using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Care.WebApi.Host.Controllers;

public class UploadDocumentRequest
{
    [Required]
    public string Title { get; set; } = default!;

    [Required]
    public string Category { get; set; } = default!;

    [Required]
    public IFormFile File { get; set; } = default!;
}

public class ReplaceDocumentRequest
{
    [Required]
    public IFormFile File { get; set; } = default!;
}
