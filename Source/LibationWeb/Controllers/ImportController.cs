using ApplicationServices;
using AudibleUtilities;
using LibationWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace LibationWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    [HttpPost("scan")]
    public async Task<ActionResult<ImportResult>> ScanAllAccounts()
    {
        if (LibraryCommands.Scanning)
            return Conflict(new { message = "Scan already in progress" });

        using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
        var accounts = persister.AccountsSettings.Accounts.ToArray();

        if (accounts.Length == 0)
            return BadRequest(new { message = "No accounts configured" });

        var (totalCount, newCount) = await LibraryCommands.ImportAccountAsync(accounts);
        return Ok(new ImportResult(totalCount, newCount));
    }

    [HttpPost("scan/{accountId}")]
    public async Task<ActionResult<ImportResult>> ScanAccount(string accountId, [FromQuery] string locale = "us")
    {
        if (LibraryCommands.Scanning)
            return Conflict(new { message = "Scan already in progress" });

        using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
        var account = persister.AccountsSettings.Accounts
            .FirstOrDefault(a => a.AccountId == accountId && (a.Locale?.Name ?? "us") == locale);

        if (account is null)
            return NotFound(new { message = $"Account '{accountId}' not found" });

        var (totalCount, newCount) = await LibraryCommands.ImportAccountAsync(account);
        return Ok(new ImportResult(totalCount, newCount));
    }

    [HttpGet("status")]
    public ActionResult<object> GetScanStatus()
    {
        return Ok(new { scanning = LibraryCommands.Scanning });
    }
}
