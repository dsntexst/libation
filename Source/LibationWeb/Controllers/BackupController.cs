using ApplicationServices;
using DataLayer;
using FileLiberator;
using LibationFileManager;
using LibationWeb.Hubs;
using LibationWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace LibationWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly IHubContext<ProgressHub> _hub;

    public BackupController(IHubContext<ProgressHub> hub)
    {
        _hub = hub;
    }

    [HttpPost("books")]
    public ActionResult<BackupQueueResult> QueueAllBooks()
    {
        var unliberated = DbContexts.GetUnliberated_Flat_NoTracking();
        _ = RunDownloadsAsync(unliberated);
        return Accepted(new BackupQueueResult(unliberated.Count));
    }

    [HttpPost("books/{productId}")]
    public ActionResult<BackupQueueResult> QueueBook(string productId)
    {
        var lb = DbContexts.GetLibraryBook_Flat_NoTracking(productId);
        if (lb is null) return NotFound();

        _ = RunDownloadsAsync(new[] { lb });
        return Accepted(new BackupQueueResult(1));
    }

    private async Task RunDownloadsAsync(IEnumerable<LibraryBook> books)
    {
        var config = Configuration.Instance;
        var downloader = DownloadDecryptBook.Create(config);

        downloader.Begin += async (_, lb) =>
            await _hub.Clients.All.SendAsync("BookBegin", lb.Book.AudibleProductId, lb.Book.Title);

        downloader.StatusUpdate += async (_, msg) =>
            await _hub.Clients.All.SendAsync("StatusUpdate", msg);

        downloader.StreamingProgressChanged += async (_, progress) =>
            await _hub.Clients.All.SendAsync("Progress", progress.BytesReceived, progress.TotalBytesToReceive);

        downloader.Completed += async (_, lb) =>
            await _hub.Clients.All.SendAsync("BookCompleted", lb.Book.AudibleProductId);

        foreach (var lb in books)
        {
            try
            {
                await downloader.ProcessSingleAsync(lb, validate: true);
            }
            catch (Exception ex)
            {
                await _hub.Clients.All.SendAsync("BookError", lb.Book.AudibleProductId, ex.Message);
            }
        }

        await _hub.Clients.All.SendAsync("QueueCompleted");
    }

    [HttpPost("pdfs")]
    public ActionResult<BackupQueueResult> QueueAllPdfs()
    {
        var library = DbContexts.GetLibrary_Flat_NoTracking();
        var withPdfs = library
            .Where(lb => lb.Book.UserDefinedItem.PdfStatus == LiberatedStatus.NotLiberated
                         && lb.Book.Supplements.Any())
            .ToList();

        _ = RunPdfDownloadsAsync(withPdfs);
        return Accepted(new BackupQueueResult(withPdfs.Count));
    }

    private async Task RunPdfDownloadsAsync(IEnumerable<LibraryBook> books)
    {
        var config = Configuration.Instance;
        var pdfDownloader = DownloadPdf.Create(config);

        pdfDownloader.Begin += async (_, lb) =>
            await _hub.Clients.All.SendAsync("PdfBegin", lb.Book.AudibleProductId, lb.Book.Title);

        pdfDownloader.Completed += async (_, lb) =>
            await _hub.Clients.All.SendAsync("PdfCompleted", lb.Book.AudibleProductId);

        foreach (var lb in books)
        {
            try
            {
                await pdfDownloader.ProcessSingleAsync(lb, validate: true);
            }
            catch (Exception ex)
            {
                await _hub.Clients.All.SendAsync("PdfError", lb.Book.AudibleProductId, ex.Message);
            }
        }
    }
}
