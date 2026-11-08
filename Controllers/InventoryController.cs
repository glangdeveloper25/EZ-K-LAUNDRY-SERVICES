using System.Globalization;
using EZKLaundrySystem.Data;
using EZKLaundrySystem.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Controllers;

[AutoValidateAntiforgeryToken]
public class InventoryController : Controller
{
    private readonly DbConnection _db = new();
    private bool CanManage => HttpContext.Session.GetInt32("UserId").HasValue &&
        HttpContext.Session.GetString("UserRole") is "Owner" or "Staff";
    private bool IsOwner => HttpContext.Session.GetString("UserRole") == "Owner";
    private int ActorId => HttpContext.Session.GetInt32("UserId")!.Value;

    [HttpGet]
    public IActionResult Index()
    {
        if (!CanManage) return RedirectToAction("Login", "Account");
        using var conn = _db.GetConnection();
        conn.Open();
        string token = HttpContext.Session.GetString("InventoryRequestToken") ?? Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("InventoryRequestToken", token);
        return View(new InventoryPageVM
        {
            Snapshot = InventoryStore.ReadSnapshot(conn),
            IsOwner = IsOwner,
            RequestToken = token
        });
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Report(string? fromDate, string? toDate)
    {
        Response.Headers.CacheControl = "no-store";
        if (!CanManage)
            return ReportError(403, "Only signed-in owners and staff can print inventory reports.");
        if (!TryReportDate(fromDate, out DateOnly from))
            return ReportError(400, "Enter a valid From Date in yyyy-MM-dd format, with a year from 1000 to 9999.");
        if (!TryReportDate(toDate, out DateOnly to))
            return ReportError(400, "Enter a valid To Date in yyyy-MM-dd format, with a year from 1000 to 9999.");
        if (from > to)
            return ReportError(400, "From Date must be on or before To Date.");

        try
        {
            using var conn = _db.GetConnection();
            conn.Open();
            if (!InventoryStore.IsReady(conn))
                return ReportError(409, "Inventory is not ready. Ask the owner to set up inventory before printing a report.");

            return PartialView("/Views/Inventory/_InventoryReportPrint.cshtml", new InventoryPageVM
            {
                IsOwner = IsOwner,
                ReportFromDate = from,
                ReportToDate = to,
                Snapshot = new InventorySnapshot
                {
                    IsReady = true,
                    Movements = InventoryStore.ReadMovementsForReport(conn, from, to)
                }
            });
        }
        catch (MySqlException)
        {
            return ReportError(503, "The inventory report is temporarily unavailable. Please try again.");
        }
    }

    private static bool TryReportDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        && value?.Length == 10 && date.Year >= 1000;

    private static ContentResult ReportError(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        ContentType = "text/plain; charset=utf-8",
        Content = message
    };

    [HttpPost]
    public IActionResult Setup(string requestToken)
    {
        if (!CanManage || !IsOwner) return StatusCode(403);
        if (!ValidRequest(requestToken)) return Result("This form expired or was already submitted. Please try again.");
        using var conn = _db.GetConnection();
        conn.Open();
        InventoryStore.EnsureSchema(conn);
        CompleteRequest();
        return Result("Inventory is ready. New items start at zero pieces. Record the actual opening stock using Stock In; existing stock was not reset.");
    }

    [HttpPost]
    public IActionResult StockIn(int serviceId, int quantity, string reason, string requestToken)
    {
        if (!CanManage || !IsOwner) return StatusCode(403);
        return Run(requestToken, conn => InventoryStore.ReceiveStock(conn, serviceId, quantity, ActorId, reason, requestToken),
            "Stock received and recorded in the inventory history.");
    }

    private IActionResult Run(string requestToken, Func<MySqlConnection, bool> action, string successMessage)
    {
        if (!CanManage) return StatusCode(403);
        if (!ModelState.IsValid) return Result("Enter valid whole-piece quantities and complete the required details.");
        if (!ValidRequest(requestToken)) return Result("This form expired or was already submitted. Check the history before trying again.");
        using var conn = _db.GetConnection();
        conn.Open();
        if (!InventoryStore.IsReady(conn)) return Result("Ask the owner to set up inventory before recording stock movements.");
        try
        {
            bool changed = action(conn);
            CompleteRequest();
            return Result(changed ? successMessage : "This stock entry was already recorded. No duplicate stock was added.");
        }
        catch (InventoryValidationException error) { return Result(error.Message); }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            return Result("Another staff member is updating the same stock. No partial change was saved; please try again.");
        }
    }

    private bool ValidRequest(string? token) => Guid.TryParseExact(token, "N", out _) &&
        token == HttpContext.Session.GetString("InventoryRequestToken");

    private void CompleteRequest() => HttpContext.Session.Remove("InventoryRequestToken");

    private IActionResult Result(string message)
    {
        TempData["InventoryMessage"] = message;
        return RedirectToAction(nameof(Index));
    }

}
