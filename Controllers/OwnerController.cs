using EZKLaundrySystem.Data;
using EZKLaundrySystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Controllers;

public class OwnerDashboardVM
{
    public List<User> StaffList { get; set; } = [];
    public HashSet<int> OnDutyStaffIds { get; set; } = [];
    public List<CustomerRecordModel> CustomerList { get; set; } = [];
    public List<BookingModel> BookingList { get; set; } = [];
    public List<User> PendingStaff { get; set; } = [];
    public Dictionary<string, decimal> Pricing { get; set; } = [];
    public decimal KgPerLoad { get; set; } = LoadPricing.DefaultKgPerLoad;
    public List<PosCatalogItem> Products { get; set; } = [];
    public List<PosCatalogItem> NeedsRestock => Products
        .Where(product => product.IsActive && (product.Stock ?? 0) <= InventorySnapshot.LowStockLevel).ToList();
    public decimal TotalEarnings { get; set; }
    public List<MonthlyIncomeModel> MonthlyIncome { get; set; } = [];
    public List<TransactionReportModel> TransactionReport { get; set; } = [];
    public TransactionReportFilter ReportFilter { get; set; } = new();
    public bool OpenReports { get; set; }
    public decimal ReportCashTotal => TransactionReport
        .Where(transaction => transaction.Status == "Paid" && transaction.PaymentMethod == "Cash")
        .Sum(transaction => transaction.Amount);
    public decimal ReportGCashTotal => TransactionReport
        .Where(transaction => transaction.Status == "Paid" && transaction.PaymentMethod == "GCash")
        .Sum(transaction => transaction.Amount);
    // Keep every recorded Paid amount in the total, including any legacy methods.
    public decimal ReportGrandTotal => TransactionReport
        .Where(transaction => transaction.Status == "Paid")
        .Sum(transaction => transaction.Amount);
}

public class TransactionReportFilter
{
    public string Period { get; set; } = "All";
    public DateTime? ReportDate { get; set; }
    public int Month { get; set; } = DateTime.Today.Month;
    public int Year { get; set; } = DateTime.Today.Year;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string PaymentMethod { get; set; } = "All";
    public string PaymentStatus { get; set; } = "All";
    public string ScopeLabel { get; set; } = "All Transactions";
}

public class TransactionReportModel
{
    public long PaymentId { get; set; }
    public string TransactionId { get; set; } = "";
    public string BookingReference { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Services { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public DateTime TransactionDate { get; set; }
}

public class MonthlyIncomeModel
{
    public string Month { get; set; } = "";
    public decimal Cash { get; set; }
    public decimal GCash { get; set; }
    public decimal Total { get; set; }
}

public class CustomerRecordModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string ContactNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public List<BookingModel> Transactions { get; set; } = [];
}

public class BookingModel
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public string CustomerNotes { get; set; } = "";
    public float Weight { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Date { get; set; } = "";
}

[AutoValidateAntiforgeryToken]
public class OwnerController : Controller
{
    private readonly DbConnection _db = new();

    public IActionResult Index(
        string period = "All",
        DateTime? reportDate = null,
        int? reportMonth = null,
        int? reportYear = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string paymentMethod = "All",
        string paymentStatus = "All",
        bool openReports = false)
    {
        if (!IsOwner()) return RedirectToAction(nameof(Login));

        string[] allowedPeriods = ["All", "Today", "Yesterday", "SpecificDate", "Monthly", "Yearly", "Custom"];
        string[] allowedMethods = ["All", "Cash", "GCash"];
        string[] allowedStatuses = ["All", "Paid", "Pending", "Partial", "Failed", "Refunded"];
        if (!allowedPeriods.Contains(period)) period = "All";
        if (!allowedMethods.Contains(paymentMethod)) paymentMethod = "All";
        if (!allowedStatuses.Contains(paymentStatus)) paymentStatus = "All";

        int selectedYear = Math.Clamp(reportYear ?? DateTime.Today.Year, 2000, 2100);
        int selectedMonth = Math.Clamp(reportMonth ?? DateTime.Today.Month, 1, 12);
        DateTime selectedStartDate = (startDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        DateTime selectedEndDate = (endDate ?? DateTime.Today).Date;
        if (selectedStartDate > selectedEndDate) (selectedStartDate, selectedEndDate) = (selectedEndDate, selectedStartDate);

        var model = new OwnerDashboardVM
        {
            OpenReports = openReports,
            ReportFilter = new TransactionReportFilter
            {
                Period = period,
                ReportDate = (reportDate ?? DateTime.Today).Date,
                Month = selectedMonth,
                Year = selectedYear,
                StartDate = selectedStartDate,
                EndDate = selectedEndDate,
                PaymentMethod = paymentMethod,
                PaymentStatus = paymentStatus
            }
        };
        using var conn = _db.GetConnection();
        conn.Open();

        const string staffQuery = """
            SELECT u.user_id, CONCAT(u.first_name, ' ', u.last_name) AS full_name,
                   u.email, u.phone, us.status_name
            FROM users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.user_status_id = u.user_status_id
            WHERE r.role_name = 'Staff'
            ORDER BY u.last_name, u.first_name
            """;
        using (var command = new MySqlCommand(staffQuery, conn))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var user = ReadUser(reader);
                if (user.Status == "Pending") model.PendingStaff.Add(user);
                else model.StaffList.Add(user);
            }
        }
        model.OnDutyStaffIds = ShiftStore.OnDutyStaffIds(conn);

        const string customerQuery = """
            SELECT u.user_id, CONCAT(u.first_name, ' ', u.last_name) AS full_name,
                   u.email, u.phone, us.status_name
            FROM users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.user_status_id = u.user_status_id
            WHERE r.role_name = 'Customer'
            ORDER BY u.last_name, u.first_name
            """;
        using (var command = new MySqlCommand(customerQuery, conn))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                model.CustomerList.Add(new CustomerRecordModel
                {
                    Id = Convert.ToInt32(reader["user_id"]),
                    FullName = reader["full_name"]?.ToString() ?? "",
                    Email = reader["email"]?.ToString() ?? "",
                    ContactNumber = reader["phone"]?.ToString() ?? "",
                    Status = reader["status_name"]?.ToString() ?? ""
                });
            }
        }

        string discountQuery = RewardStore.IsReady(conn)
            ? "COALESCE((SELECT discount_amount FROM booking_rewards WHERE booking_id = b.booking_id AND returned_at IS NULL), 0)"
            : "0";
        string bookingQuery = BookingQuery.Replace("{rewardDiscount}", discountQuery);
        foreach (var customer in model.CustomerList)
        {
            using var command = new MySqlCommand(bookingQuery + " WHERE b.customer_id = @customerId ORDER BY b.booked_at DESC", conn);
            command.Parameters.AddWithValue("@customerId", customer.Id);
            using var reader = command.ExecuteReader();
            while (reader.Read()) customer.Transactions.Add(ReadBooking(reader));
        }

        using (var command = new MySqlCommand(bookingQuery + " ORDER BY b.booked_at DESC", conn))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read()) model.BookingList.Add(ReadBooking(reader));
        }

        using (var command = new MySqlCommand("SELECT service_name, current_price FROM services WHERE is_active = TRUE", conn))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                string key = (reader["service_name"]?.ToString() ?? "").Replace(" ", "");
                model.Pricing[key] = Convert.ToDecimal(reader["current_price"]);
            }
        }
        model.KgPerLoad = LoadPricing.ReadKgPerLoad(conn);
        model.Products = ProductCatalog.Read(conn, activeOnly: false);

        LoadIncomeReport(conn, model);
        LoadTransactionReport(conn, model);

        return View(model);
    }

    private static void LoadIncomeReport(MySqlConnection conn, OwnerDashboardVM model)
    {
        const string totalQuery = """
            SELECT COALESCE(SUM(p.amount), 0)
            FROM payments p
            JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
            WHERE ps.status_name = 'Paid'
            """;
        using (var command = new MySqlCommand(totalQuery, conn))
        {
            model.TotalEarnings = Convert.ToDecimal(command.ExecuteScalar());
        }

        const string monthlyQuery = """
            SELECT DATE_FORMAT(p.paid_at, '%M %Y') AS income_month,
                   SUM(CASE WHEN pm.method_name = 'Cash' THEN p.amount ELSE 0 END) AS cash_income,
                   SUM(CASE WHEN pm.method_name = 'GCash' THEN p.amount ELSE 0 END) AS gcash_income,
                   SUM(p.amount) AS total_income
            FROM payments p
            JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
            JOIN payment_methods pm ON pm.payment_method_id = p.payment_method_id
            WHERE ps.status_name = 'Paid' AND p.paid_at IS NOT NULL
            GROUP BY YEAR(p.paid_at), MONTH(p.paid_at), DATE_FORMAT(p.paid_at, '%M %Y')
            ORDER BY YEAR(p.paid_at) DESC, MONTH(p.paid_at) DESC
            """;
        using var monthlyCommand = new MySqlCommand(monthlyQuery, conn);
        using var reader = monthlyCommand.ExecuteReader();
        while (reader.Read())
        {
            model.MonthlyIncome.Add(new MonthlyIncomeModel
            {
                Month = reader["income_month"]?.ToString() ?? "",
                Cash = Convert.ToDecimal(reader["cash_income"]),
                GCash = Convert.ToDecimal(reader["gcash_income"]),
                Total = Convert.ToDecimal(reader["total_income"])
            });
        }
    }

    private static void LoadTransactionReport(MySqlConnection conn, OwnerDashboardVM model)
    {
        const string baseQuery = """
            SELECT p.payment_id,
                   COALESCE(NULLIF(p.transaction_reference, ''), CONCAT('TXN-', LPAD(p.payment_id, 8, '0'))) AS transaction_id,
                   b.booking_code,
                   CONCAT(customer.first_name, ' ', customer.last_name) AS customer_name,
                   COALESCE(service_lines.service_names, '') AS service_names,
                   method.method_name,
                   p.amount,
                   status.status_name,
                   COALESCE(NULLIF(TRIM(CONCAT(COALESCE(staff.first_name, ''), ' ', COALESCE(staff.last_name, ''))), ''), 'Customer/System') AS recorded_by,
                   COALESCE(p.paid_at, p.created_at) AS transaction_date
            FROM payments p
            JOIN bookings b ON b.booking_id = p.booking_id
            JOIN users customer ON customer.user_id = b.customer_id
            JOIN payment_methods method ON method.payment_method_id = p.payment_method_id
            JOIN payment_statuses status ON status.payment_status_id = p.payment_status_id
            LEFT JOIN users staff ON staff.user_id = p.recorded_by_user_id
            LEFT JOIN (
                SELECT bs.booking_id,
                       GROUP_CONCAT(service.service_name ORDER BY service.service_id SEPARATOR ', ') AS service_names
                FROM booking_services bs
                JOIN services service ON service.service_id = bs.service_id
                GROUP BY bs.booking_id
            ) service_lines ON service_lines.booking_id = b.booking_id
            """;

        var conditions = new List<string>();
        string transactionDate = "COALESCE(p.paid_at, p.created_at)";
        switch (model.ReportFilter.Period)
        {
            case "Today":
                conditions.Add($"DATE({transactionDate}) = CURDATE()");
                model.ReportFilter.ScopeLabel = $"Today - {DateTime.Today:MMMM dd, yyyy}";
                break;
            case "Yesterday":
                conditions.Add($"DATE({transactionDate}) = DATE_SUB(CURDATE(), INTERVAL 1 DAY)");
                model.ReportFilter.ScopeLabel = $"Yesterday - {DateTime.Today.AddDays(-1):MMMM dd, yyyy}";
                break;
            case "SpecificDate":
                conditions.Add($"DATE({transactionDate}) = @reportDate");
                model.ReportFilter.ScopeLabel = model.ReportFilter.ReportDate!.Value.ToString("MMMM dd, yyyy");
                break;
            case "Monthly":
                conditions.Add($"YEAR({transactionDate}) = @reportYear AND MONTH({transactionDate}) = @reportMonth");
                model.ReportFilter.ScopeLabel = new DateTime(model.ReportFilter.Year, model.ReportFilter.Month, 1).ToString("MMMM yyyy");
                break;
            case "Yearly":
                conditions.Add($"YEAR({transactionDate}) = @reportYear");
                model.ReportFilter.ScopeLabel = model.ReportFilter.Year.ToString();
                break;
            case "Custom":
                conditions.Add($"{transactionDate} >= @startDate AND {transactionDate} < DATE_ADD(@endDate, INTERVAL 1 DAY)");
                model.ReportFilter.ScopeLabel = $"{model.ReportFilter.StartDate:MMMM dd, yyyy} to {model.ReportFilter.EndDate:MMMM dd, yyyy}";
                break;
            default:
                model.ReportFilter.ScopeLabel = "All Transactions";
                break;
        }

        if (model.ReportFilter.PaymentMethod != "All")
            conditions.Add("method.method_name = @paymentMethod");

        if (model.ReportFilter.PaymentStatus != "All")
            conditions.Add("status.status_name = @paymentStatus");

        if (model.ReportFilter.PaymentMethod != "All")
            model.ReportFilter.ScopeLabel += $" | {model.ReportFilter.PaymentMethod}";
        if (model.ReportFilter.PaymentStatus != "All")
            model.ReportFilter.ScopeLabel += $" | {model.ReportFilter.PaymentStatus}";

        string query = baseQuery
                       + (conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "")
                       + $" ORDER BY {transactionDate} DESC, p.payment_id DESC";

        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@reportDate", model.ReportFilter.ReportDate!.Value.Date);
        command.Parameters.AddWithValue("@reportYear", model.ReportFilter.Year);
        command.Parameters.AddWithValue("@reportMonth", model.ReportFilter.Month);
        command.Parameters.AddWithValue("@startDate", model.ReportFilter.StartDate!.Value.Date);
        command.Parameters.AddWithValue("@endDate", model.ReportFilter.EndDate!.Value.Date);
        command.Parameters.AddWithValue("@paymentMethod", model.ReportFilter.PaymentMethod);
        command.Parameters.AddWithValue("@paymentStatus", model.ReportFilter.PaymentStatus);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            model.TransactionReport.Add(new TransactionReportModel
            {
                PaymentId = Convert.ToInt64(reader["payment_id"]),
                TransactionId = reader["transaction_id"]?.ToString() ?? "",
                BookingReference = reader["booking_code"]?.ToString() ?? "",
                CustomerName = reader["customer_name"]?.ToString() ?? "",
                Services = reader["service_names"]?.ToString() ?? "",
                PaymentMethod = reader["method_name"]?.ToString() ?? "",
                Amount = Convert.ToDecimal(reader["amount"]),
                Status = reader["status_name"]?.ToString() ?? "",
                RecordedBy = reader["recorded_by"]?.ToString() ?? "Customer/System",
                TransactionDate = Convert.ToDateTime(reader["transaction_date"])
            });
        }
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public IActionResult Login(string email, string password)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        const string query = """
            SELECT u.user_id, u.first_name, u.last_name, u.password_hash, us.status_name
            FROM users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.user_status_id = u.user_status_id
            WHERE u.email = @email AND r.role_name = 'Owner'
            LIMIT 1
            """;
        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@email", email?.Trim() ?? "");
        using var reader = command.ExecuteReader();

        if (reader.Read()
            && reader["status_name"]?.ToString() == "Active"
            && PasswordSecurity.VerifyPassword(password ?? "", reader["password_hash"]?.ToString() ?? ""))
        {
            string fullName = $"{reader["first_name"]} {reader["last_name"]}".Trim();
            HttpContext.Session.SetInt32("UserId", Convert.ToInt32(reader["user_id"]));
            HttpContext.Session.SetString("UserRole", "Owner");
            HttpContext.Session.SetString("UserName", fullName);
            TempData["Msg"] = $"Successfully logged in. Welcome, {fullName}!";
            TempData["Type"] = "success";
            return RedirectToAction(nameof(Index));
        }

        TempData["Msg"] = "Invalid Owner credentials or inactive account.";
        TempData["Type"] = "error";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult RegisterShop()
    {
        using var conn = _db.GetConnection();
        conn.Open();
        return OwnerExists(conn) ? RedirectToAction(nameof(Login)) : View();
    }

    [HttpPost]
    public IActionResult RegisterShop(User user)
    {
        if (!ModelState.IsValid) return View(user);

        using var conn = _db.GetConnection();
        conn.Open();
        if (OwnerExists(conn))
        {
            TempData["Msg"] = "An Owner account already exists.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Login));
        }

        InsertUser(conn, user, "Owner", "Active");
        TempData["Msg"] = "Owner registered successfully. You can now log in.";
        TempData["Type"] = "success";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    public IActionResult AddStaff(User user)
    {
        if (!IsOwner()) return RedirectToAction(nameof(Login));
        if (string.IsNullOrWhiteSpace(user.FullName) || string.IsNullOrWhiteSpace(user.Email)
            || string.IsNullOrWhiteSpace(user.Password) || string.IsNullOrWhiteSpace(user.ConfirmPassword)
            || string.IsNullOrWhiteSpace(user.ContactNumber))
        {
            TempData["Msg"] = "Complete all Staff account fields.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Index));
        }

        if (user.ContactNumber.Length != 11 || user.ContactNumber.Any(character => character < '0' || character > '9'))
        {
            TempData["Msg"] = "Staff contact number must be exactly 11 digits (0-9), without letters, spaces, or special characters.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Index));
        }

        if (user.Password != user.ConfirmPassword)
        {
            TempData["Msg"] = "Staff password and confirmation do not match.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Index));
        }

        if (!PasswordSecurity.IsStrongPassword(user.Password))
        {
            TempData["Msg"] = PasswordSecurity.StrengthError;
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Index));
        }

        using var conn = _db.GetConnection();
        conn.Open();
        if (UserExists(conn, user.Email, user.ContactNumber))
        {
            TempData["Msg"] = "Staff email or contact number already exists.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Index));
        }

        InsertUser(conn, user, "Staff", "Active");
        TempData["Msg"] = "New Staff account added successfully.";
        TempData["Type"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ToggleStaffStatus(int id, string status)
    {
        if (!IsOwner()) return RedirectToAction(nameof(Login));
        if (status is not ("Active" or "Inactive")) return BadRequest();

        using var conn = _db.GetConnection();
        conn.Open();
        const string query = """
            UPDATE users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.status_name = @status
            SET u.user_status_id = us.user_status_id
            WHERE u.user_id = @id AND r.role_name = 'Staff'
            """;
        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();

        TempData["Msg"] = $"Staff account status updated to {status}.";
        TempData["Type"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult DeleteStaff(int id)
    {
        if (!IsOwner()) return RedirectToAction(nameof(Login));

        using var conn = _db.GetConnection();
        conn.Open();
        const string query = """
            DELETE u FROM users u
            JOIN roles r ON r.role_id = u.role_id
            WHERE u.user_id = @id AND r.role_name = 'Staff'
            """;
        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@id", id);
        try
        {
            command.ExecuteNonQuery();
            TempData["Msg"] = "Staff account deleted successfully.";
            TempData["Type"] = "success";
        }
        catch (MySqlException error) when (error.Number == 1451)
        {
            TempData["Msg"] = "This staff account has recorded transactions or inventory history and cannot be deleted. Deactivate the account instead to preserve those records.";
            TempData["Type"] = "warning";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult UpdatePricing(decimal Washing, decimal Drying, decimal Folding,
        decimal RushFee, decimal KgPerLoad, decimal PackageFee,
        Dictionary<int, decimal> ProductPrices, Dictionary<int, decimal> ProductExtras)
    {
        if (!IsOwner()) return RedirectToAction(nameof(Login));
        ProductPrices ??= [];
        ProductExtras ??= [];
        if (new[] { Washing, Drying, Folding, RushFee, PackageFee }.Concat(ProductPrices.Values).Concat(ProductExtras.Values).Any(p => p < 0))
            return BadRequest();

        using var conn = _db.GetConnection();
        conn.Open();
        if (!LoadPricing.SaveKgPerLoad(conn, KgPerLoad))
        {
            TempData["Msg"] = "Enter a load size from 1 to 50 kg (up to two decimal places). Prices were not changed.";
            TempData["Type"] = "warning";
            return RedirectToAction(nameof(Index));
        }
        UpdatePrice(conn, "Washing", Washing);
        UpdatePrice(conn, "Drying", Drying);
        UpdatePrice(conn, "Folding", Folding);
        // Self-Service is a fulfillment method, not an additional paid service.
        UpsertSelfService(conn, 0m);
        UpdatePrice(conn, "Rush Fee", RushFee);
        UpdatePrice(conn, ProductCatalog.PackageFee, PackageFee);
        foreach (var (serviceId, price) in ProductPrices)
        {
            using var command = new MySqlCommand("""
                UPDATE services SET current_price = @price, package_extra = @extra
                WHERE service_id = @id AND service_category = 'Consumable'
                """, conn);
            command.Parameters.AddWithValue("@price", decimal.Round(price, 2));
            command.Parameters.AddWithValue("@extra", decimal.Round(ProductExtras.GetValueOrDefault(serviceId), 2));
            command.Parameters.AddWithValue("@id", serviceId);
            command.ExecuteNonQuery();
        }

        TempData["Msg"] = "Service pricing updated successfully.";
        TempData["Type"] = "success";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Adds a detergent or fabric conditioner brand sold by the shop, starting at zero stock.</summary>
    [HttpPost]
    public IActionResult AddProduct(string name, string productType, decimal price)
    {
        if (!IsOwner()) return RedirectToAction(nameof(Login));
        name = (name ?? "").Trim();
        if (name.Length is < 2 or > 100 || !ProductCatalog.Types.Contains(productType) || price is < 0 or > 100000)
        {
            TempData["Msg"] = "Enter the product name (2 to 100 characters), choose its type and enter a valid price.";
            TempData["Type"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        using var conn = _db.GetConnection();
        conn.Open();
        try
        {
            using var command = new MySqlCommand("""
                INSERT INTO services (service_name, service_category, product_type, charging_basis, current_price, description, is_active)
                VALUES (@name, 'Consumable', @type, 'PerItem', @price, @description, TRUE);
                SELECT LAST_INSERT_ID();
                """, conn);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@type", productType);
            command.Parameters.AddWithValue("@price", decimal.Round(price, 2));
            command.Parameters.AddWithValue("@description", $"{productType} sachet");
            InventoryStore.EnsureStockRow(conn, Convert.ToInt32(command.ExecuteScalar()));
            TempData["Msg"] = $"{name} was added. Record its stock under Inventory → Stock In.";
            TempData["Type"] = "success";
        }
        catch (MySqlException error) when (error.Number == 1062)
        {
            TempData["Msg"] = $"A product or service named \"{name}\" already exists.";
            TempData["Type"] = "warning";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Hides a product the shop no longer sells, or shows it again. Past orders keep it.</summary>
    [HttpPost]
    public IActionResult ToggleProduct(int serviceId)
    {
        if (!IsOwner()) return RedirectToAction(nameof(Login));
        using var conn = _db.GetConnection();
        conn.Open();
        using var command = new MySqlCommand("""
            UPDATE services SET is_active = NOT is_active
            WHERE service_id = @id AND service_category = 'Consumable'
            """, conn);
        command.Parameters.AddWithValue("@id", serviceId);
        bool changed = command.ExecuteNonQuery() == 1;
        if (changed) InventoryStore.EnsureStockRow(conn, serviceId);
        TempData["Msg"] = changed ? "Product visibility updated." : "Product was not found.";
        TempData["Type"] = changed ? "success" : "warning";
        return RedirectToAction(nameof(Index));
    }

    private bool IsOwner() => HttpContext.Session.GetString("UserRole") == "Owner";

    private static bool OwnerExists(MySqlConnection conn)
    {
        const string query = "SELECT COUNT(*) FROM users u JOIN roles r ON r.role_id = u.role_id WHERE r.role_name = 'Owner'";
        using var command = new MySqlCommand(query, conn);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool UserExists(MySqlConnection conn, string email, string phone)
    {
        using var command = new MySqlCommand("SELECT COUNT(*) FROM users WHERE email = @email OR phone = @phone", conn);
        command.Parameters.AddWithValue("@email", email.Trim());
        command.Parameters.AddWithValue("@phone", phone.Trim());
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void InsertUser(MySqlConnection conn, User user, string role, string status)
    {
        var (firstName, lastName) = AccountController.SplitFullName(user.FullName);
        const string query = """
            INSERT INTO users
                (role_id, user_status_id, first_name, last_name, email, phone, password_hash, is_walk_in)
            SELECT r.role_id, us.user_status_id, @firstName, @lastName, @email, @phone, @passwordHash, FALSE
            FROM roles r CROSS JOIN user_statuses us
            WHERE r.role_name = @role AND us.status_name = @status
            """;
        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@firstName", firstName);
        command.Parameters.AddWithValue("@lastName", lastName);
        command.Parameters.AddWithValue("@email", user.Email.Trim());
        command.Parameters.AddWithValue("@phone", user.ContactNumber.Trim());
        command.Parameters.AddWithValue("@passwordHash", PasswordSecurity.HashPassword(user.Password));
        command.Parameters.AddWithValue("@role", role);
        command.Parameters.AddWithValue("@status", status);
        command.ExecuteNonQuery();
    }

    private static void UpdatePrice(MySqlConnection conn, string serviceName, decimal price)
    {
        using var command = new MySqlCommand(
            "UPDATE services SET current_price = @price WHERE service_name = @serviceName", conn);
        command.Parameters.AddWithValue("@price", price);
        command.Parameters.AddWithValue("@serviceName", serviceName);
        command.ExecuteNonQuery();
    }

    private static void UpsertSelfService(MySqlConnection conn, decimal price)
    {
        const string query = """
            INSERT INTO services
                (service_name, service_category, charging_basis, current_price, estimated_hours, description, is_active)
            VALUES
                ('Self Service', 'Laundry', 'Flat', @price, NULL, 'Customer-operated service method; no separate fee', TRUE)
            ON DUPLICATE KEY UPDATE
                service_category = 'Laundry',
                charging_basis = 'Flat',
                current_price = @price,
                description = 'Customer-operated service method; no separate fee',
                is_active = TRUE
            """;
        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@price", price);
        command.ExecuteNonQuery();
    }

    private static User ReadUser(MySqlDataReader reader) => new()
    {
        Id = Convert.ToInt32(reader["user_id"]),
        FullName = reader["full_name"]?.ToString() ?? "",
        Email = reader["email"]?.ToString() ?? "",
        ContactNumber = reader["phone"]?.ToString() ?? "",
        Status = reader["status_name"]?.ToString() ?? ""
    };

    private static BookingModel ReadBooking(MySqlDataReader reader) => new()
    {
        Id = Convert.ToInt32(reader["booking_id"]),
        CustomerName = reader["customer_name"]?.ToString() ?? "",
        CustomerPhone = reader["phone"]?.ToString() ?? "",
        ServiceType = reader["service_names"]?.ToString() ?? "",
        CustomerNotes = reader["customer_notes"]?.ToString() ?? "",
        Weight = reader["weight_kg"] == DBNull.Value ? 0f : Convert.ToSingle(reader["weight_kg"]),
        Amount = reader["order_total"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["order_total"]),
        Status = reader["status_name"]?.ToString() ?? "",
        Priority = Convert.ToBoolean(reader["is_rush"]) ? "Rush" : "Regular",
        Date = Convert.ToDateTime(reader["booked_at"]).ToString("MMM dd, yyyy")
    };

    private const string BookingQuery = """
        SELECT b.booking_id, b.booked_at, b.is_rush, b.customer_notes,
               CONCAT(u.first_name, ' ', u.last_name) AS customer_name,
               u.phone, status.status_name,
               COALESCE(items.weight_kg, 0) AS weight_kg,
               COALESCE(service_lines.service_names, '') AS service_names,
               ROUND(GREATEST(0, COALESCE(service_lines.order_total, 0) - {rewardDiscount}), 2) AS order_total
        FROM bookings b
        JOIN users u ON u.user_id = b.customer_id
        JOIN booking_statuses status ON status.booking_status_id = b.current_status_id
        LEFT JOIN (
            SELECT booking_id, SUM(COALESCE(weight_kg, 0)) AS weight_kg
            FROM booking_items GROUP BY booking_id
        ) items ON items.booking_id = b.booking_id
        LEFT JOIN (
            SELECT bs.booking_id,
                   GROUP_CONCAT(s.service_name ORDER BY s.service_id SEPARATOR ', ') AS service_names,
                   SUM(bs.quantity * bs.unit_price) AS order_total
            FROM booking_services bs
            JOIN services s ON s.service_id = bs.service_id
            GROUP BY bs.booking_id
        ) service_lines ON service_lines.booking_id = b.booking_id
        """;
}
