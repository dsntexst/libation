using ApplicationServices;
using DataLayer;
using LibationWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace LibationWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LibraryController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<BookDto>> GetLibrary([FromQuery] bool includeDeleted = false)
    {
        var books = DbContexts.GetLibrary_Flat_NoTracking()
            .Where(lb => includeDeleted || !lb.IsDeleted)
            .Select(BookDto.FromLibraryBook)
            .ToList();
        return Ok(books);
    }

    [HttpGet("stats")]
    public ActionResult<LibraryStatsDto> GetStats()
    {
        var stats = LibraryCommands.GetCounts();
        return Ok(new LibraryStatsDto(
            Total: stats.LibraryBooks.Count(),
            FullyBackedUp: stats.booksFullyBackedUp,
            DownloadedOnly: stats.booksDownloadedOnly,
            NoProgress: stats.booksNoProgress,
            Error: stats.booksError,
            Unavailable: stats.booksUnavailable,
            PdfsDownloaded: stats.pdfsDownloaded,
            PdfsNotDownloaded: stats.pdfsNotDownloaded
        ));
    }

    [HttpGet("{productId}")]
    public ActionResult<BookDto> GetBook(string productId)
    {
        var lb = DbContexts.GetLibraryBook_Flat_NoTracking(productId);
        if (lb is null) return NotFound();
        return Ok(BookDto.FromLibraryBook(lb));
    }

    [HttpGet("unliberated")]
    public ActionResult<IEnumerable<BookDto>> GetUnliberated()
    {
        var books = DbContexts.GetUnliberated_Flat_NoTracking()
            .Select(BookDto.FromLibraryBook)
            .ToList();
        return Ok(books);
    }

    [HttpGet("deleted")]
    public ActionResult<IEnumerable<BookDto>> GetDeleted()
    {
        var books = DbContexts.GetDeletedLibraryBooks()
            .Select(BookDto.FromLibraryBook)
            .ToList();
        return Ok(books);
    }

    [HttpGet("search")]
    public ActionResult<IEnumerable<BookDto>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(DbContexts.GetLibrary_Flat_NoTracking().Select(BookDto.FromLibraryBook));

        var results = SearchEngineCommands.Search(q);
        var productIds = results.Docs.Select(d => d.ProductId).ToHashSet();
        var books = DbContexts.GetLibrary_Flat_NoTracking()
            .Where(lb => productIds.Contains(lb.Book.AudibleProductId) && !lb.IsDeleted)
            .Select(BookDto.FromLibraryBook)
            .ToList();
        return Ok(books);
    }

    [HttpPatch("{productId}")]
    public async Task<ActionResult<BookDto>> UpdateBook(string productId, [FromBody] UpdateBookRequest request)
    {
        var lb = DbContexts.GetLibraryBook_Flat_NoTracking(productId);
        if (lb is null) return NotFound();

        LiberatedStatus? bookStatus = null;
        LiberatedStatus? pdfStatus = null;

        if (request.BookStatus is not null && Enum.TryParse<LiberatedStatus>(request.BookStatus, out var bs))
            bookStatus = bs;
        if (request.PdfStatus is not null && Enum.TryParse<LiberatedStatus>(request.PdfStatus, out var ps))
            pdfStatus = ps;

        await lb.UpdateUserDefinedItemAsync(
            tags: request.Tags,
            bookStatus: bookStatus,
            pdfStatus: pdfStatus
        );

        if (request.IsFinished.HasValue)
        {
            await lb.UpdateUserDefinedItemAsync(udi => udi.IsFinished = request.IsFinished.Value);
        }

        var updated = DbContexts.GetLibraryBook_Flat_NoTracking(productId);
        return Ok(BookDto.FromLibraryBook(updated!));
    }

    [HttpDelete("{productId}")]
    public async Task<IActionResult> RemoveBook(string productId)
    {
        var lb = DbContexts.GetLibraryBook_Flat_NoTracking(productId);
        if (lb is null) return NotFound();

        await new[] { lb }.RemoveBooksAsync();
        return NoContent();
    }

    [HttpPost("{productId}/restore")]
    public async Task<IActionResult> RestoreBook(string productId)
    {
        var lb = DbContexts.GetLibraryBook_Flat_NoTracking(productId);
        if (lb is null) return NotFound();

        await new[] { lb }.RestoreBooksAsync();
        return NoContent();
    }

    [HttpDelete("{productId}/permanent")]
    public async Task<IActionResult> PermanentlyDeleteBook(string productId)
    {
        var lb = DbContexts.GetLibraryBook_Flat_NoTracking(productId);
        if (lb is null) return NotFound();

        await new[] { lb }.PermanentlyDeleteBooksAsync();
        return NoContent();
    }
}
