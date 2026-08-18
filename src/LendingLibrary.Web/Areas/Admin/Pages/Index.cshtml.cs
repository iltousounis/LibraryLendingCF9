using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Enums;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LendingLibrary.Web.Areas.Admin.Pages;

public class IndexModel(AppDbContext db, TimeProvider timeProvider) : PageModel
{
    public int TotalItems { get; private set; }

    public int ItemsOnLoan { get; private set; }

    public int OverdueLoans { get; private set; }

    public int ActiveReservations { get; private set; }

    public int RegisteredUsers { get; private set; }

    public async Task OnGetAsync()
    {
        var now = timeProvider.GetUtcNow();

        TotalItems = await db.CatalogueItems.CountAsync();
        ItemsOnLoan = await db.Loans.CountAsync(l => l.Status == LoanStatus.Active);
        OverdueLoans = await db.Loans.CountAsync(l => l.Status == LoanStatus.Active && l.DueAtUtc < now);
        ActiveReservations = await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Pending);
        RegisteredUsers = await db.Users.CountAsync();
    }
}
