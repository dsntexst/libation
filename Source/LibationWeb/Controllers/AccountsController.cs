using AudibleUtilities;
using LibationWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace LibationWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<AccountDto>> GetAccounts()
    {
        using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
        var accounts = persister.AccountsSettings.Accounts
            .Select(a => new AccountDto(
                AccountId: a.AccountId,
                AccountName: a.AccountName ?? a.AccountId,
                LocaleName: a.Locale?.Name ?? "unknown"
            ))
            .ToList();
        return Ok(accounts);
    }
}
