using DataLayer;

namespace LibationWeb.Models;

public record BookDto(
    string ProductId,
    string Title,
    string? Subtitle,
    string Authors,
    string Narrators,
    string? Series,
    string? SeriesOrder,
    int LengthInMinutes,
    string? Publisher,
    DateTime? DatePublished,
    string? Language,
    bool IsAbridged,
    float OverallRating,
    string Tags,
    string BookStatus,
    string? PdfStatus,
    bool IsFinished,
    string Account,
    DateTime DateAdded,
    bool IsDeleted,
    bool AudioExists,
    string? CoverUrl
)
{
    public static BookDto FromLibraryBook(LibraryBook lb)
    {
        var book = lb.Book;
        var udi = book.UserDefinedItem;
        var series = book.SeriesLink.FirstOrDefault();

        return new BookDto(
            ProductId: book.AudibleProductId,
            Title: book.Title,
            Subtitle: book.Subtitle,
            Authors: string.Join(", ", book.Authors.Select(a => a.Name)),
            Narrators: string.Join(", ", book.Narrators.Select(n => n.Name)),
            Series: series?.Series?.Name,
            SeriesOrder: series?.Order,
            LengthInMinutes: book.LengthInMinutes,
            Publisher: book.Publisher,
            DatePublished: book.DatePublished,
            Language: book.Language,
            IsAbridged: book.IsAbridged,
            OverallRating: book.Rating?.OverallRating ?? 0f,
            Tags: udi?.Tags ?? string.Empty,
            BookStatus: (udi?.BookStatus ?? LiberatedStatus.NotLiberated).ToString(),
            PdfStatus: udi?.PdfStatus?.ToString(),
            IsFinished: udi?.IsFinished ?? false,
            Account: lb.Account,
            DateAdded: lb.DateAdded,
            IsDeleted: lb.IsDeleted,
            AudioExists: book.AudioExists,
            CoverUrl: book.PictureLarge ?? book.PictureId
        );
    }
}

public record LibraryStatsDto(
    int Total,
    int FullyBackedUp,
    int DownloadedOnly,
    int NoProgress,
    int Error,
    int Unavailable,
    int PdfsDownloaded,
    int PdfsNotDownloaded
);

public record AccountDto(string AccountId, string AccountName, string LocaleName);

public record UpdateBookRequest(string? Tags, string? BookStatus, string? PdfStatus, bool? IsFinished);

public record ImportResult(int TotalCount, int NewCount);

public record BackupQueueResult(int Queued);
