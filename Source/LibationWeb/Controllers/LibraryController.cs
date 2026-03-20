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

    /// <summary>
    /// Export the full library as JSON in the same format as Libation's built-in
    /// File → Export → Library as JSON. The resulting file can be loaded into
    /// the standalone library.html viewer.
    /// </summary>
    [HttpGet("export")]
    public IActionResult ExportLibrary()
    {
        var books = DbContexts.GetLibrary_Flat_NoTracking();
        var dtos = books.Select(lb => new
        {
            account               = lb.Account,
            dateAdded             = lb.DateAdded,
            isAudiblePlus         = lb.IsAudiblePlus,
            absentFromLastScan    = lb.AbsentFromLastScan,
            audibleProductId      = lb.Book.AudibleProductId,
            locale                = lb.Book.Locale,
            title                 = lb.Book.Title,
            subtitle              = lb.Book.Subtitle,
            authorNames           = string.Join(", ", lb.Book.Authors.Select(a => a.Name)),
            narratorNames         = string.Join(", ", lb.Book.Narrators.Select(n => n.Name)),
            lengthInMinutes       = lb.Book.LengthInMinutes,
            description           = lb.Book.Description,
            publisher             = lb.Book.Publisher,
            hasPdf                = lb.Book.Supplements.Any(),
            seriesNames           = string.Join(", ", lb.Book.SeriesLink.Select(s => s.Series.Name).Where(n => n != null)),
            seriesOrder           = string.Join(", ", lb.Book.SeriesLink.Select(s => $"{s.Order} : {s.Series.Name}")),
            communityRatingOverall      = lb.Book.Rating?.OverallRating,
            communityRatingPerformance  = lb.Book.Rating?.PerformanceRating,
            communityRatingStory        = lb.Book.Rating?.StoryRating,
            pictureId             = lb.Book.PictureId,
            pictureLarge          = lb.Book.PictureLarge,
            isAbridged            = lb.Book.IsAbridged,
            datePublished         = lb.Book.DatePublished,
            myLibationTags        = lb.Book.UserDefinedItem.Tags,
            bookStatus            = lb.Book.UserDefinedItem.BookStatus.ToString(),
            pdfStatus             = lb.Book.UserDefinedItem.PdfStatus?.ToString(),
            contentType           = lb.Book.ContentType.ToString(),
            language              = lb.Book.Language,
            lastDownloaded        = lb.Book.UserDefinedItem.LastDownloaded,
            isFinished            = lb.Book.UserDefinedItem.IsFinished,
            includedUntil         = lb.IncludedUntil,
        }).ToList();

        return new JsonResult(dtos) { StatusCode = 200 };
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
