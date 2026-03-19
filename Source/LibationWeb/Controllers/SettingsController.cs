using LibationFileManager;
using Microsoft.AspNetCore.Mvc;

namespace LibationWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> GetSettings()
    {
        var config = Configuration.Instance;
        return Ok(new
        {
            booksDirectory = AudibleFileStorage.BooksDirectory?.ToString(),
            libationFilesPath = config.LibationFiles?.Location,
            autoScan = config.AutoScan,
            betaOptIn = config.BetaOptIn,
            downloadEpisodes = config.DownloadEpisodes,
            splitFilesByChapter = config.SplitFilesByChapter,
            retainAaxFile = config.RetainAaxFile,
            allowLibationFixup = config.AllowLibationFixup,
            badBookAction = config.BadBook.ToString(),
            maxSampleRate = config.MaxSampleRate.ToString(),
        });
    }

    [HttpPatch]
    public ActionResult UpdateSettings([FromBody] Dictionary<string, object?> updates)
    {
        var config = Configuration.Instance;

        foreach (var (key, value) in updates)
        {
            switch (key.ToLowerInvariant())
            {
                case "autoscan" when value is bool b:
                    config.AutoScan = b;
                    break;
                case "betaoptin" when value is bool b:
                    config.BetaOptIn = b;
                    break;
                case "downloadepisodes" when value is bool b:
                    config.DownloadEpisodes = b;
                    break;
                case "splitfilesbychapter" when value is bool b:
                    config.SplitFilesByChapter = b;
                    break;
                case "retainaaxfile" when value is bool b:
                    config.RetainAaxFile = b;
                    break;
                case "allowlibationfixup" when value is bool b:
                    config.AllowLibationFixup = b;
                    break;
            }
        }

        return Ok(new { message = "Settings updated" });
    }
}
