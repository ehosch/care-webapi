using System.Security.Claims;
using Care.WebApi.Application.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Care.WebApi.Host.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetDocumentsAsync(CancellationToken cancellationToken) =>
        await _documentService.GetDocumentsAsync(cancellationToken);

    [HttpGet("{id}/versions")]
    public async Task<ActionResult<List<DocumentVersionDto>>> GetVersionHistoryAsync(Guid id, CancellationToken cancellationToken) =>
        await _documentService.GetVersionHistoryAsync(id, cancellationToken);

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var download = await _documentService.DownloadCurrentAsync(id, cancellationToken);
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpGet("{id}/versions/{version:int}/download")]
    public async Task<IActionResult> DownloadVersionAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var download = await _documentService.DownloadVersionAsync(id, version, cancellationToken);
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<Guid>> UploadAsync([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        await using var stream = request.File.OpenReadStream();
        return await _documentService.UploadAsync(
            request.Title,
            request.Category,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            stream,
            RequestingUserId,
            cancellationToken);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplaceAsync(Guid id, [FromForm] ReplaceDocumentRequest request, CancellationToken cancellationToken)
    {
        await using var stream = request.File.OpenReadStream();
        await _documentService.ReplaceAsync(
            id,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            stream,
            RequestingUserId,
            cancellationToken);
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _documentService.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    private string RequestingUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
