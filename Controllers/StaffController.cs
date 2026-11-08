using EZKLaundrySystem.Data;
using EZKLaundrySystem.Models;
using EZKLaundrySystem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json;

namespace EZKLaundrySystem.Controllers;

public class StaffDashboardVM
{
    public List<User> PendingCustomers { get; set; } = [];
    public List<StaffBookingModel> NewBookings { get; set; } = [];
    public List<StaffBookingModel> ActiveQueue { get; set; } = [];
    public List<StaffPickupModel> PickupMethods { get; set; } = [];
    public Dictionary<string, decimal> Pricing { get; set; } = [];
    public decimal KgPerLoad { get; set; } = LoadPricing.DefaultKgPerLoad;
    public decimal CashCollectionToday { get; set; }
    public int TodayOrderCount { get; set; }
    public InventorySnapshot Inventory { get; set; } = new();
    public MachineSnapshot Machines { get; set; } = new();
    public bool SmsConfigured { get; set; }
    public ShiftSnapshot Shift { get; set; } = new();
    public WalkInFormModel WalkIn { get; set; } = new();
    public string WalkInToken { get; set; } = "";
    public bool OpenWalkIn { get; set; }
}

public class StaffBookingModel
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public string CustomerNotes { get; set; } = "";
    public bool IsRush { get; set; }
    public string CustomerCategory { get; set; } = "";
    public bool IsRegular => CustomerCategory == EZKLaundrySystem.Data.CustomerCategory.Regular;
    public DateTime? ScheduledAt { get; set; }
    public float Weight { get; set; }
    public string Status { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal RewardDiscount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingGCashAmount { get; set; }
    public decimal PerKgRateTotal { get; set; }
    public decimal PerLoadRateTotal { get; set; }
    public decimal FixedAmount { get; set; }
    public decimal ShopPieces { get; set; }
    public string ShopSupplies { get; set; } = "";
    public bool SuppliesIssued { get; set; }
    public bool HasShopSupplies => ShopPieces > 0;
    public decimal BalanceDue => Math.Max(0m, Amount - PaidAmount);
    public bool IsFullyPaid => (Amount > 0m || RewardDiscount > 0m) && BalanceDue <= 0.005m;
}

public class StaffPickupModel
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string ContactNumber { get; set; } = "";
    public string Method { get; set; } = "";
    public DateTime? ScheduledAt { get; set; }
    public string Location { get; set; } = "";
    public string Status { get; set; } = "";
}

[AutoValidateAntiforgeryToken]
public class StaffController(SmsSender sms) : Controller
{
    private readonly DbConnection _db = new();

    public IActionResult Index(bool openWalkIn = false)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");

        var model = new StaffDashboardVM { OpenWalkIn = openWalkIn };
        if (TempData["WalkInDraft"] is string draft)
            model.WalkIn = JsonSerializer.Deserialize<WalkInFormModel>(draft) ?? new();
        model.WalkInToken = HttpContext.Session.GetString("WalkInRequestToken") ?? Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("WalkInRequestToken", model.WalkInToken);
        using var conn = _db.GetConnection();
        conn.Open();

        const string pendingCustomers = """
            SELECT u.user_id, CONCAT(u.first_name, ' ', u.last_name) AS full_name, u.email,
                COALESCE(u.phone, '') AS phone, u.created_at
            FROM users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.user_status_id = u.user_status_id
            WHERE r.role_name = 'Customer' AND us.status_name = 'Pending'
            ORDER BY u.created_at
            """;
        using (var command = new MySqlCommand(pendingCustomers, conn))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                model.PendingCustomers.Add(new User
                {
                    Id = Convert.ToInt32(reader["user_id"]),
                    FullName = reader["full_name"]?.ToString() ?? "",
                    Email = reader["email"]?.ToString() ?? "",
                    ContactNumber = reader.GetString("phone"),
                    CreatedAt = reader["created_at"] == DBNull.Value ? null : reader.GetDateTime("created_at")
                });
            }
        }

        model.Inventory = InventoryStore.ReadSnapshot(conn);
        var issuedBookings = new HashSet<int>();
        if (model.Inventory.IsReady)
        {
            using var issued = new MySqlCommand("SELECT booking_id FROM inventory_booking_issues", conn);
            using var reader = issued.ExecuteReader();
            while (reader.Read()) issuedBookings.Add(Convert.ToInt32(reader["booking_id"]));
        }
        bool rewardsReady = RewardStore.IsReady(conn);
        LoadBookings(conn, "Pending", model.NewBookings, issuedBookings, rewardsReady);
        LoadBookings(conn, "Washing,Drying,Folding,Ready", model.ActiveQueue, issuedBookings, rewardsReady);
        LoadPickupMethods(conn, model.PickupMethods);
        model.Machines = MachineStore.ReadSnapshot(conn);
        model.SmsConfigured = sms.IsConfigured;
        model.KgPerLoad = LoadPricing.ReadKgPerLoad(conn);
        model.Shift = ShiftStore.ReadSnapshot(conn, HttpContext.Session.GetInt32("UserId")!.Value, DateTime.Now);

        const string cashCollectionQuery = """
            SELECT COALESCE(SUM(p.amount), 0)
            FROM payments p
            JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
            JOIN payment_methods pm ON pm.payment_method_id = p.payment_method_id
            WHERE ps.status_name = 'Paid'
              AND pm.method_name = 'Cash'
              AND DATE(p.paid_at) = CURDATE()
            """;
        using (var command = new MySqlCommand(cashCollectionQuery, conn))
            model.CashCollectionToday = Convert.ToDecimal(command.ExecuteScalar());

        // Orders booked today (online or walk-in), excluding cancelled ones.
        const string todayOrdersQuery = """
            SELECT COUNT(*)
            FROM bookings b
            JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
            WHERE DATE(b.booked_at) = CURDATE()
              AND bs.status_name <> 'Cancelled'
            """;
        using (var command = new MySqlCommand(todayOrdersQuery, conn))
            model.TodayOrderCount = Convert.ToInt32(command.ExecuteScalar());

        using (var command = new MySqlCommand("SELECT service_name, current_price FROM services WHERE is_active = TRUE", conn))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                string key = (reader["service_name"]?.ToString() ?? "").Replace(" ", "");
                model.Pricing[key] = Convert.ToDecimal(reader["current_price"]);
            }
        }

        return View(model);
    }

    [HttpPost]
    public IActionResult TimeIn()
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        using var conn = _db.GetConnection();
        conn.Open();
        TempData["Message"] = ShiftStore.TimeIn(conn, HttpContext.Session.GetInt32("UserId")!.Value, DateTime.Now)
            ? $"Timed in at {DateTime.Now:h:mm tt}. Have a good shift!"
            : "You are already timed in.";
        return RedirectToAction(nameof(Index), null, "shift-section");
    }

    [HttpPost]
    public IActionResult TimeOut()
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        using var conn = _db.GetConnection();
        conn.Open();
        TempData["Message"] = ShiftStore.TimeOut(conn, HttpContext.Session.GetInt32("UserId")!.Value, DateTime.Now)
            ? $"Timed out at {DateTime.Now:h:mm tt}. Thank you for your work today!"
            : "You are not timed in.";
        return RedirectToAction(nameof(Index), null, "shift-section");
    }

    [HttpPost]
    public IActionResult UpdateMachineStatus(int machineId, string status)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");

        using var conn = _db.GetConnection();
        conn.Open();
        int staffId = HttpContext.Session.GetInt32("UserId")!.Value;
        TempData["Message"] = MachineStore.UpdateStatus(conn, machineId, status, staffId)
            ? $"Machine status updated to {status}."
            : "Machine status was not changed. Select a valid machine and status.";
        return RedirectToAction(nameof(Index), null, "machines-section");
    }

    // Polled by the staff dashboard audio alert to detect newly received bookings.
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult PendingBookingStatus()
    {
        if (!IsStaff()) return Unauthorized();

        using var conn = _db.GetConnection();
        conn.Open();
        const string query = """
            SELECT COUNT(*) AS pending_count, COALESCE(MAX(b.booking_id), 0) AS latest_id
            FROM bookings b
            JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
            WHERE bs.status_name = 'Pending'
            """;
        using var command = new MySqlCommand(query, conn);
        using var reader = command.ExecuteReader();
        reader.Read();
        return Json(new
        {
            pendingCount = Convert.ToInt32(reader["pending_count"]),
            latestId = Convert.ToInt32(reader["latest_id"])
        });
    }

    [HttpPost]
    public IActionResult ProcessWalkIn(WalkInFormModel form)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        if (!Guid.TryParseExact(form.RequestToken, "N", out Guid requestId) ||
            form.RequestToken != HttpContext.Session.GetString("WalkInRequestToken"))
            return WalkInError(form, "This form expired or was already submitted. Check the active queue before trying again.");
        string? validationError = form.Validate();
        if (!ModelState.IsValid || validationError != null)
            return WalkInError(form, validationError ?? "Enter valid whole-piece quantities and a valid weight.");

        // The shop's detergent and fabric conditioner brands are sold through the POS,
        // which lets staff pick the exact product and shows its stock.
        if (form.UsesShopSupplies)
            return WalkInError(form, "Shop detergent and fabric conditioner are now sold through Walk-in (POS). Use the POS for this order, or choose the customer's own supplies here.");

        using var conn = _db.GetConnection();
        conn.Open();
        int staffId = HttpContext.Session.GetInt32("UserId")!.Value;
        // A deterministic code prevents concurrent retries of the same form
        // from creating a second booking and deducting inventory twice.
        string bookingCode = $"EZK-W-{requestId:N}"[..30].ToUpperInvariant();
        using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            int customerId = FindOrCreateWalkInCustomer(conn, transaction, form.CustomerName, form.Contact);

            const string bookingInsert = """
                INSERT INTO bookings
                    (booking_code, customer_id, current_status_id, created_by_user_id, is_rush, customer_notes)
                SELECT @code, @customerId, bs.booking_status_id, @staffId, @isRush, @notes
                FROM booking_statuses bs WHERE bs.status_name = 'Washing'
                """;
            int bookingId;
            using (var command = new MySqlCommand(bookingInsert + "; SELECT LAST_INSERT_ID();", conn, transaction))
            {
                command.Parameters.AddWithValue("@code", bookingCode);
                command.Parameters.AddWithValue("@customerId", customerId);
                command.Parameters.AddWithValue("@staffId", staffId);
                command.Parameters.AddWithValue("@isRush", form.IsRush);
                command.Parameters.AddWithValue("@notes", LaundrySupplyChoices.BuildInstructions(
                    form.DetergentSource, form.PowderQuantity, form.SoftenerSource, form.DownyQuantity));
                bookingId = Convert.ToInt32(command.ExecuteScalar());
            }

            const string itemInsert = """
                INSERT INTO booking_items (booking_id, item_type_id, weight_kg)
                SELECT @bookingId, item_type_id, @weight FROM item_types WHERE item_name = 'Clothes'
                """;
            using (var command = new MySqlCommand(itemInsert, conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.Parameters.AddWithValue("@weight", form.Weight);
                command.ExecuteNonQuery();
            }

            decimal calculatedTotal = 0m;
            foreach (string serviceName in form.CoreServices)
                calculatedTotal += AddBookingService(conn, transaction, bookingId, serviceName, form.Weight);
            if (form.ServiceMode == ServiceRules.SelfService)
                AddBookingService(conn, transaction, bookingId, ServiceRules.SelfService, form.Weight);
            if (form.DetergentSource == LaundrySupplyChoices.Shop)
                calculatedTotal += AddBookingService(conn, transaction, bookingId, "Powder", form.Weight, form.PowderQuantity);
            if (form.SoftenerSource == LaundrySupplyChoices.Shop)
                calculatedTotal += AddBookingService(conn, transaction, bookingId, "Downy", form.Weight, form.DownyQuantity);
            if (form.IsRush)
                calculatedTotal += AddBookingService(conn, transaction, bookingId, "Rush Fee", form.Weight);
            if (form.UsesShopSupplies)
                InventoryStore.IssueBooking(conn, transaction, bookingId, staffId);

            AddStatusHistory(conn, transaction, bookingId, "Washing", staffId, "Walk-in booking created by Staff.");
            transaction.Commit();
            HttpContext.Session.Remove("WalkInRequestToken");

            TempData["Message"] = $"Walk-in order {bookingCode} was processed. Server-calculated total: ₱{calculatedTotal:N2}";
            return RedirectToAction(nameof(Index));
        }
        catch (InventoryValidationException error)
        {
            transaction.Rollback();
            return WalkInError(form, error.Message);
        }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            transaction.Rollback();
            return WalkInError(form, "Another request is updating the same order or stock. Please review and try again; no partial order was saved.");
        }
        catch (MySqlException error) when (error.Number == 1062)
        {
            transaction.Rollback();
            using var existing = new MySqlCommand(
                "SELECT COUNT(*) FROM bookings WHERE booking_code = @code AND created_by_user_id = @staffId", conn);
            existing.Parameters.AddWithValue("@code", bookingCode);
            existing.Parameters.AddWithValue("@staffId", staffId);
            if (Convert.ToInt32(existing.ExecuteScalar()) == 0)
                return WalkInError(form, "The order could not be saved because another request changed its details. Please review and try again.");
            HttpContext.Session.Remove("WalkInRequestToken");
            TempData["Message"] = $"Walk-in order {bookingCode} was already saved. It was not submitted or deducted twice.";
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private IActionResult WalkInError(WalkInFormModel form, string message)
    {
        form.IssueShopSupplies = false;
        TempData["WalkInDraft"] = JsonSerializer.Serialize(form);
        TempData["Message"] = message;
        return RedirectToAction(nameof(Index), new { openWalkIn = true });
    }

    [HttpPost]
    public IActionResult ApproveCustomer(int id)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        using var conn = _db.GetConnection();
        conn.Open();

        const string query = """
            UPDATE users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.status_name = 'Active'
            SET u.user_status_id = us.user_status_id
            WHERE u.user_id = @id AND r.role_name = 'Customer'
            """;
        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();

        TempData["Message"] = "Customer account has been activated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult AcceptBooking(int id, bool confirmShopSupplies = false) =>
        ChangeBookingStatus(id, "Washing", "Booking accepted and processing started by Staff.", confirmShopSupplies, accepting: true);

    [HttpPost]
    public IActionResult CompleteOrder(int bookingId) => ChangeTerminalStatus(
        bookingId,
        "Completed",
        "Order completed and released by Staff.",
        ["Ready"]);

    [HttpPost]
    public IActionResult CancelOrder(int bookingId, string reason)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");

        string safeReason = reason?.Trim() ?? "";
        if (safeReason.Length is < 3 or > 150)
        {
            TempData["Message"] = "Please provide a cancellation reason between 3 and 150 characters.";
            return RedirectToAction(nameof(Index));
        }

        return ChangeTerminalStatus(
            bookingId,
            "Cancelled",
            $"Order cancelled by Staff. Reason: {safeReason}",
            ["Pending", "Washing", "Drying", "Folding", "Ready"]);
    }

    [HttpPost]
    public IActionResult UpdateStatus(int orderId, string status, bool confirmShopSupplies = false)
    {
        string[] allowed = ["Washing", "Drying", "Folding", "Ready"];
        if (!allowed.Contains(status)) return BadRequest();
        return ChangeBookingStatus(orderId, status, $"Status updated to {status}.", confirmShopSupplies);
    }

    [HttpPost]
    public IActionResult UpdateActualWeight(int bookingId, decimal actualWeight)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        if (actualWeight is <= 0m or > 999.99m)
        {
            TempData["Message"] = "Enter a valid actual weight from 0.01 to 999.99 kg.";
            return RedirectToAction(nameof(Index));
        }

        using var conn = _db.GetConnection();
        conn.Open();
        bool rewardsReady = RewardStore.IsReady(conn);
        using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            const string bookingQuery = """
                SELECT b.booking_code, b.customer_id,
                       (SELECT status_name FROM booking_statuses WHERE booking_status_id = b.current_status_id) AS status_name,
                       (SELECT is_terminal FROM booking_statuses WHERE booking_status_id = b.current_status_id) AS is_terminal
                FROM bookings b
                WHERE b.booking_id = @bookingId
                LIMIT 1
                FOR UPDATE
                """;

            string bookingCode = "";
            string currentStatus = "";
            int customerId = 0;
            bool isTerminal = false;
            using (var command = new MySqlCommand(bookingQuery, conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    bookingCode = reader["booking_code"]?.ToString() ?? $"BK-{bookingId}";
                    customerId = Convert.ToInt32(reader["customer_id"]);
                    currentStatus = reader["status_name"]?.ToString() ?? "";
                    isTerminal = Convert.ToBoolean(reader["is_terminal"]);
                }
            }

            if (customerId == 0)
            {
                TempData["Message"] = "Booking was not found.";
                return RedirectToAction(nameof(Index));
            }

            if (isTerminal)
            {
                TempData["Message"] = "The weight of a completed or cancelled booking can no longer be changed.";
                return RedirectToAction(nameof(Index));
            }

            long bookingItemId = 0;
            decimal previousWeight = 0m;
            const string itemQuery = """
                SELECT bi.booking_item_id, COALESCE(bi.weight_kg, 0) AS weight_kg
                FROM booking_items bi
                JOIN item_types item_type ON item_type.item_type_id = bi.item_type_id
                WHERE bi.booking_id = @bookingId AND item_type.item_name = 'Clothes'
                ORDER BY bi.booking_item_id
                LIMIT 1
                FOR UPDATE
                """;
            using (var command = new MySqlCommand(itemQuery, conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    bookingItemId = Convert.ToInt64(reader["booking_item_id"]);
                    previousWeight = Convert.ToDecimal(reader["weight_kg"]);
                }
            }

            if (bookingItemId == 0)
            {
                TempData["Message"] = "This booking has no laundry weight record to update.";
                return RedirectToAction(nameof(Index));
            }

            int loads = LoadPricing.LoadsFor(actualWeight, LoadPricing.ReadKgPerLoad(conn, transaction));
            decimal projectedTotal;
            const string projectedTotalQuery = """
                SELECT COALESCE(SUM(
                    CASE
                        WHEN COALESCE(bs.charging_basis, service.charging_basis) = 'PerKg' THEN @actualWeight * bs.unit_price
                        WHEN COALESCE(bs.charging_basis, service.charging_basis) = 'PerLoad' THEN @loads * bs.unit_price
                        ELSE bs.quantity * bs.unit_price
                    END
                ), 0)
                FROM booking_services bs
                JOIN services service ON service.service_id = bs.service_id
                WHERE bs.booking_id = @bookingId
                """;
            using (var command = new MySqlCommand(projectedTotalQuery, conn, transaction))
            {
                command.Parameters.AddWithValue("@actualWeight", actualWeight);
                command.Parameters.AddWithValue("@loads", loads);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                projectedTotal = Convert.ToDecimal(command.ExecuteScalar());
            }
            decimal rewardDiscount = rewardsReady
                ? RewardStore.GetBookingDiscount(conn, transaction, bookingId)
                : 0m;
            projectedTotal = RewardStore.NetTotal(projectedTotal, rewardDiscount);

            decimal paidAmount;
            const string paidQuery = """
                SELECT COALESCE(SUM(p.amount), 0)
                FROM payments p
                JOIN payment_statuses status ON status.payment_status_id = p.payment_status_id
                WHERE p.booking_id = @bookingId AND status.status_name = 'Paid'
                """;
            using (var command = new MySqlCommand(paidQuery, conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                paidAmount = Convert.ToDecimal(command.ExecuteScalar());
            }

            int staffId = HttpContext.Session.GetInt32("UserId")!.Value;
            // A lighter load than estimated can leave the declared GCash amount above the final
            // total; the difference is refunded and recorded instead of blocking the weight.
            decimal refunded = projectedTotal + 0.005m < paidAmount
                ? RefundPaidExcess(conn, transaction, bookingId, paidAmount - projectedTotal, staffId,
                    $"final total PHP {projectedTotal:N2} is below the amount paid.")
                : 0m;

            using (var command = new MySqlCommand(
                       "UPDATE booking_items SET weight_kg = @actualWeight WHERE booking_item_id = @bookingItemId",
                       conn, transaction))
            {
                command.Parameters.AddWithValue("@actualWeight", actualWeight);
                command.Parameters.AddWithValue("@bookingItemId", bookingItemId);
                command.ExecuteNonQuery();
            }

            const string updateWeightBasedServices = """
                UPDATE booking_services bs
                JOIN services service ON service.service_id = bs.service_id
                SET bs.quantity = CASE WHEN COALESCE(bs.charging_basis, service.charging_basis) = 'PerLoad' THEN @loads ELSE @actualWeight END
                WHERE bs.booking_id = @bookingId AND COALESCE(bs.charging_basis, service.charging_basis) IN ('PerKg', 'PerLoad')
                """;
            using (var command = new MySqlCommand(updateWeightBasedServices, conn, transaction))
            {
                command.Parameters.AddWithValue("@actualWeight", actualWeight);
                command.Parameters.AddWithValue("@loads", loads);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.ExecuteNonQuery();
            }

            AddStatusHistory(
                conn,
                transaction,
                bookingId,
                currentStatus,
                staffId,
                $"Actual weight recorded by Staff: {previousWeight:N2} kg changed to {actualWeight:N2} kg ({loads} load(s)). Final total recalculated to PHP {projectedTotal:N2}."
                + (refunded > 0m ? $" Refund of PHP {refunded:N2} recorded." : ""));

            const string notificationQuery = """
                INSERT INTO notifications (user_id, booking_id, notification_type, title, message)
                VALUES (@customerId, @bookingId, 'Booking', 'Actual weight recorded', @message)
                """;
            using (var command = new MySqlCommand(notificationQuery, conn, transaction))
            {
                command.Parameters.AddWithValue("@customerId", customerId);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.Parameters.AddWithValue(
                    "@message",
                    $"The actual weight for {bookingCode} is {actualWeight:N2} kg ({loads} load(s)). The recalculated total is PHP {projectedTotal:N2}."
                    + (refunded > 0m ? $" PHP {refunded:N2} of your payment will be refunded at the shop." : ""));
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            TempData["Message"] = $"Actual weight for #{bookingId} was updated to {actualWeight:N2} kg ({loads} load(s)). New total: PHP {projectedTotal:N2}."
                + (refunded > 0m ? $" REFUND PHP {refunded:N2} to the customer." : "");
        }
        catch (RewardValidationException error)
        {
            transaction.Rollback();
            TempData["Message"] = error.Message;
        }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            transaction.Rollback();
            TempData["Message"] = "Another request is updating this order or its rewards. No partial change was saved. Reload and try again.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult RecordPayment(string OrderID, decimal Amount, string pm) =>
        RecordPaymentCore(OrderID, Amount, pm);

    private IActionResult RecordPaymentCore(string orderId, decimal amount, string paymentMethod)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        if (amount <= 0 || string.IsNullOrWhiteSpace(orderId))
        {
            TempData["Message"] = "Enter a valid booking reference and payment amount.";
            return RedirectToAction(nameof(Index));
        }

        string method = paymentMethod?.Trim() ?? "";
        if (method is not ("Cash" or "GCash"))
        {
            TempData["Message"] = "Only Cash and GCash payments can be recorded here.";
            return RedirectToAction(nameof(Index));
        }

        string reference = NormalizeBookingReference(orderId);
        int? numericBookingId = int.TryParse(reference, out int parsedId) ? parsedId : null;
        int staffId = HttpContext.Session.GetInt32("UserId")!.Value;

        using var conn = _db.GetConnection();
        conn.Open();
        bool rewardsReady = RewardStore.IsReady(conn);
        using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            const string bookingQuery = """
                SELECT b.booking_id, b.booking_code, b.customer_id,
                       (SELECT status_name FROM booking_statuses WHERE booking_status_id = b.current_status_id) AS status_name
                FROM bookings b
                WHERE b.booking_code = @reference
                   OR (@bookingId IS NOT NULL AND b.booking_id = @bookingId)
                LIMIT 1
                FOR UPDATE
                """;

            int bookingId = 0;
            int customerId = 0;
            string bookingCode = "";
            string bookingStatus = "";
            using (var command = new MySqlCommand(bookingQuery, conn, transaction))
            {
                command.Parameters.AddWithValue("@reference", reference);
                command.Parameters.AddWithValue("@bookingId", numericBookingId.HasValue ? numericBookingId.Value : DBNull.Value);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    bookingId = Convert.ToInt32(reader["booking_id"]);
                    customerId = Convert.ToInt32(reader["customer_id"]);
                    bookingCode = reader["booking_code"]?.ToString() ?? $"BK-{bookingId}";
                    bookingStatus = reader["status_name"]?.ToString() ?? "";
                }
            }

            if (bookingId == 0)
            {
                TempData["Message"] = "Booking was not found. Use #BK-14, BK-14, 14, or the full EZK reference.";
                return RedirectToAction(nameof(Index));
            }

            if (bookingStatus == "Cancelled")
            {
                TempData["Message"] = "A cancelled booking cannot receive a payment.";
                return RedirectToAction(nameof(Index));
            }

            decimal orderTotal;
            using (var command = new MySqlCommand(
                       "SELECT COALESCE(SUM(quantity * unit_price), 0) FROM booking_services WHERE booking_id = @bookingId",
                       conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                orderTotal = Convert.ToDecimal(command.ExecuteScalar());
            }
            orderTotal = RewardStore.NetTotal(orderTotal, rewardsReady
                ? RewardStore.GetBookingDiscount(conn, transaction, bookingId)
                : 0m);

            decimal paidAmount;
            using (var command = new MySqlCommand("""
                       SELECT COALESCE(SUM(p.amount), 0)
                       FROM payments p
                       JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
                       WHERE p.booking_id = @bookingId AND ps.status_name = 'Paid'
                       """, conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                paidAmount = Convert.ToDecimal(command.ExecuteScalar());
            }

            decimal balance = Math.Max(0m, orderTotal - paidAmount);
            if (balance <= 0.005m)
            {
                TempData["Message"] = $"Booking #{bookingId} is already fully paid.";
                return RedirectToAction(nameof(Index));
            }

            int pendingGCashPaymentId = 0;
            if (method == "GCash")
            {
                const string pendingQuery = """
                    SELECT p.payment_id
                    FROM payments p
                    JOIN payment_methods pm ON pm.payment_method_id = p.payment_method_id
                    JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
                    WHERE p.booking_id = @bookingId
                      AND pm.method_name = 'GCash'
                      AND ps.status_name = 'Pending'
                    ORDER BY p.created_at, p.payment_id
                    LIMIT 1
                    FOR UPDATE
                    """;
                using var command = new MySqlCommand(pendingQuery, conn, transaction);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                object? pendingId = command.ExecuteScalar();
                if (pendingId is not null) pendingGCashPaymentId = Convert.ToInt32(pendingId);
            }

            // A GCash transfer already sent may exceed the final balance (e.g. the laundry weighed
            // less than estimated). Keep the balance as paid and record the excess as a refund.
            // New payments above the balance are still rejected.
            decimal gcashRefund = 0m;
            if (amount > balance + 0.005m)
            {
                if (pendingGCashPaymentId == 0)
                {
                    TempData["Message"] = $"Payment was not recorded. The remaining balance is PHP {balance:N2}.";
                    return RedirectToAction(nameof(Index));
                }
                gcashRefund = amount - balance;
                amount = balance;
            }

            if (pendingGCashPaymentId > 0)
            {
                const string verifyQuery = """
                    UPDATE payments p
                    JOIN payment_statuses ps ON ps.status_name = 'Paid'
                    SET p.payment_status_id = ps.payment_status_id,
                        p.recorded_by_user_id = @staffId,
                        p.amount = @amount,
                        p.paid_at = NOW(),
                        p.notes = 'GCash payment verified by Staff.'
                    WHERE p.payment_id = @paymentId
                    """;
                using var command = new MySqlCommand(verifyQuery, conn, transaction);
                command.Parameters.AddWithValue("@staffId", staffId);
                command.Parameters.AddWithValue("@amount", amount);
                command.Parameters.AddWithValue("@paymentId", pendingGCashPaymentId);
                command.ExecuteNonQuery();

                if (gcashRefund > 0m)
                {
                    int gcashMethodId;
                    using (var methodCommand = new MySqlCommand(
                               "SELECT payment_method_id FROM payment_methods WHERE method_name = 'GCash'", conn, transaction))
                        gcashMethodId = Convert.ToInt32(methodCommand.ExecuteScalar());
                    InsertRefundRecord(conn, transaction, bookingId, gcashMethodId, gcashRefund, staffId,
                        $"Refunded: GCash received exceeded the remaining balance of PHP {balance:N2}.");
                }
            }
            else
            {
                const string insertQuery = """
                    INSERT INTO payments
                        (booking_id, payment_method_id, payment_status_id, recorded_by_user_id, amount, paid_at, notes)
                    SELECT @bookingId, method.payment_method_id, status.payment_status_id,
                           @staffId, @amount, NOW(), 'Payment recorded by Staff.'
                    FROM payment_methods method
                    CROSS JOIN payment_statuses status
                    WHERE method.method_name = @method
                      AND method.is_active = TRUE
                      AND status.status_name = 'Paid'
                    """;
                using var command = new MySqlCommand(insertQuery, conn, transaction);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.Parameters.AddWithValue("@staffId", staffId);
                command.Parameters.AddWithValue("@amount", amount);
                command.Parameters.AddWithValue("@method", method);
                if (command.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    TempData["Message"] = "The selected payment method is unavailable.";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (method == "Cash" && amount >= balance - 0.005m)
            {
                const string supersedePendingGCash = """
                    UPDATE payments p
                    JOIN payment_methods pm ON pm.payment_method_id = p.payment_method_id
                    JOIN payment_statuses current_status ON current_status.payment_status_id = p.payment_status_id
                    JOIN payment_statuses failed_status ON failed_status.status_name = 'Failed'
                    SET p.payment_status_id = failed_status.payment_status_id,
                        p.notes = 'Pending GCash declaration superseded by a recorded Cash payment.'
                    WHERE p.booking_id = @bookingId
                      AND pm.method_name = 'GCash'
                      AND current_status.status_name = 'Pending'
                    """;
                using var command = new MySqlCommand(supersedePendingGCash, conn, transaction);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.ExecuteNonQuery();
            }

            const string notificationQuery = """
                INSERT INTO notifications (user_id, notification_type, title, message)
                VALUES (@customerId, 'Payment', 'Payment recorded', @message)
                """;
            using (var command = new MySqlCommand(notificationQuery, conn, transaction))
            {
                command.Parameters.AddWithValue("@customerId", customerId);
                command.Parameters.AddWithValue("@message", $"A {method} payment of PHP {amount:N2} was recorded for {bookingCode}."
                    + (gcashRefund > 0m ? $" PHP {gcashRefund:N2} will be refunded to you." : ""));
                command.ExecuteNonQuery();
            }

            if (rewardsReady)
                RewardStore.AwardCompletedBooking(conn, transaction, bookingId);
            transaction.Commit();
            TempData["Message"] = $"{method} payment of PHP {amount:N2} was recorded for #{bookingId}."
                + (gcashRefund > 0m ? $" REFUND PHP {gcashRefund:N2} to the customer." : "");
        }
        catch (RewardValidationException error)
        {
            transaction.Rollback();
            TempData["Message"] = error.Message;
        }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            transaction.Rollback();
            TempData["Message"] = "Another request is updating this order or its rewards. No payment change was saved. Reload and try again.";
        }
        return RedirectToAction(nameof(Index));
    }

    private static string NormalizeBookingReference(string value)
    {
        string reference = value.Trim();
        if (reference.StartsWith('#')) reference = reference[1..].Trim();
        if (reference.StartsWith("BK-", StringComparison.OrdinalIgnoreCase)) reference = reference[3..].Trim();
        return reference;
    }

    public const string DowntimeNoticeTitle = "Shop Downtime Notice";

    // Staff-initiated SMS (Objective 2): sent only when the laundry is Ready for Pickup, so the
    // customer or their rider comes when it is truly finished. The in-app notification is always
    // saved, which is the documented log of every SMS sent by the staff.
    [HttpPost]
    public async Task<IActionResult> SendPickupSms(int bookingId)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");

        using var conn = _db.GetConnection();
        conn.Open();
        string bookingCode = "", status = "", firstName = "", phone = "";
        int customerId = 0;
        using (var command = new MySqlCommand("""
                   SELECT b.booking_code, b.customer_id, bs.status_name, u.first_name, u.phone
                   FROM bookings b
                   JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
                   JOIN users u ON u.user_id = b.customer_id
                   WHERE b.booking_id = @bookingId
                   """, conn))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                bookingCode = reader["booking_code"]?.ToString() ?? "";
                customerId = Convert.ToInt32(reader["customer_id"]);
                status = reader["status_name"]?.ToString() ?? "";
                firstName = reader["first_name"]?.ToString() ?? "";
                phone = reader["phone"]?.ToString() ?? "";
            }
        }

        if (customerId == 0 || status != "Ready")
        {
            TempData["Message"] = "The pickup SMS can be sent only when the order is Ready for Pickup.";
            return RedirectToAction(nameof(Index));
        }

        // Use the short reference staff see on the dashboard; the full booking code is too long for a text.
        string message = $"Hi {firstName}, your laundry #BK-{bookingId} at EZ-K Laundry Services is ready for pickup. You or your rider may claim it at the shop. Thank you!";
        SmsResult result = await sms.SendAsync([phone], message);

        using (var transaction = conn.BeginTransaction())
        {
            AddCustomerNotification(conn, transaction, customerId, bookingId, "Booking", "Laundry ready for pickup", message);
            AddStatusHistory(conn, transaction, bookingId, "Ready", HttpContext.Session.GetInt32("UserId")!.Value,
                result.Sent ? $"Pickup SMS sent to {phone} by Staff." : $"Pickup notification saved in-app; SMS not sent: {result.Detail}.");
            transaction.Commit();
        }

        TempData["Message"] = result.Sent
            ? $"Pickup SMS sent to {phone} for #{bookingId}."
            : $"Pickup notification saved in the customer's dashboard, but the SMS was not sent: {result.Detail}.";
        return RedirectToAction(nameof(Index));
    }

    // Power-outage/downtime notice typed by the staff. "ActiveOrders" reaches customers who are
    // waiting on laundry; "All" reaches every active customer account. Optionally also sent by SMS.
    [HttpPost]
    public async Task<IActionResult> SendDowntimeAlert(string message, string audience, bool sendSms = false)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        string safeMessage = message?.Trim() ?? "";
        if (safeMessage.Length is < 5 or > 300)
        {
            TempData["Message"] = "Downtime alert was not sent. Enter a message between 5 and 300 characters.";
            return RedirectToAction(nameof(Index));
        }
        bool activeOrdersOnly = audience != "All";

        using var conn = _db.GetConnection();
        conn.Open();
        string audienceFilter = """
            FROM users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.user_status_id = u.user_status_id
            WHERE r.role_name = 'Customer' AND us.status_name = 'Active'
            """ + (activeOrdersOnly ? """

              AND EXISTS (
                  SELECT 1 FROM bookings b
                  JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
                  WHERE b.customer_id = u.user_id AND bs.is_terminal = FALSE)
            """ : "");
        int recipients;
        using (var command = new MySqlCommand(
                   "INSERT INTO notifications (user_id, notification_type, title, message) SELECT u.user_id, 'System', @title, @message " + audienceFilter,
                   conn))
        {
            command.Parameters.AddWithValue("@title", DowntimeNoticeTitle);
            command.Parameters.AddWithValue("@message", safeMessage);
            recipients = command.ExecuteNonQuery();
        }

        string smsNote = "";
        if (sendSms && recipients > 0)
        {
            var phones = new List<string>();
            using (var command = new MySqlCommand("SELECT u.phone " + audienceFilter, conn))
            using (var reader = command.ExecuteReader())
                while (reader.Read()) phones.Add(reader["phone"]?.ToString() ?? "");
            SmsResult result = await sms.SendAsync(phones, $"EZ-K Laundry: {safeMessage}");
            smsNote = result.Sent ? $" SMS {result.Detail}." : $" SMS not sent: {result.Detail}.";
        }

        TempData["Message"] = $"Downtime alert sent to {recipients} customer(s){(activeOrdersOnly ? " with active orders" : "")}.{smsNote}";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ChangeBookingStatus(int bookingId, string status, string notes,
        bool confirmShopSupplies, bool accepting = false)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        if (!ModelState.IsValid || bookingId <= 0) return BadRequest();

        using var conn = _db.GetConnection();
        conn.Open();
        using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            // Serialize acceptance, status updates, supply issuance and cancellations
            // on the booking row. Do not lock shared status catalogue rows.
            object? currentStatusId;
            using (var booking = new MySqlCommand(
                "SELECT current_status_id FROM bookings WHERE booking_id = @id FOR UPDATE", conn, transaction))
            {
                booking.Parameters.AddWithValue("@id", bookingId);
                currentStatusId = booking.ExecuteScalar();
            }
            if (currentStatusId == null)
                throw new InventoryValidationException("Booking was not found.");
            string currentStatus;
            using (var current = new MySqlCommand(
                "SELECT status_name FROM booking_statuses WHERE booking_status_id = @id", conn, transaction))
            {
                current.Parameters.AddWithValue("@id", currentStatusId);
                currentStatus = current.ExecuteScalar()?.ToString() ?? "";
            }
            if (accepting && currentStatus != "Pending")
                throw new InventoryValidationException("This booking is no longer pending. No duplicate acceptance or stock deduction was saved.");
            if (!accepting && currentStatus is not ("Washing" or "Drying" or "Folding" or "Ready"))
                throw new InventoryValidationException("Only an active order can change phase. Accept a pending booking first; completed or cancelled orders cannot be reopened here.");

            // Newly accepted orders must record their issued shop pieces. Older
            // active orders are never backfilled merely because their phase changes.
            bool needsSupplies = (accepting || confirmShopSupplies) &&
                InventoryStore.RequiresBookingIssue(conn, transaction, bookingId);
            if (needsSupplies && !confirmShopSupplies)
                throw new InventoryValidationException("Check the saved detergent and fabric conditioner pieces and confirm that you are issuing those shop supplies now. The order and stock were not changed.");

            if (currentStatus != status)
            {
                using var update = new MySqlCommand("""
                    UPDATE bookings SET current_status_id =
                        (SELECT booking_status_id FROM booking_statuses WHERE status_name = @status)
                    WHERE booking_id = @id
                    """, conn, transaction);
                update.Parameters.AddWithValue("@status", status);
                update.Parameters.AddWithValue("@id", bookingId);
                update.ExecuteNonQuery();
            }
            int staffId = HttpContext.Session.GetInt32("UserId")!.Value;
            bool issued = needsSupplies && InventoryStore.IssueBooking(conn, transaction, bookingId, staffId);
            if (currentStatus != status || issued)
                AddStatusHistory(conn, transaction, bookingId, status, staffId,
                    issued ? notes + " Saved shop-supply pieces confirmed issued and deducted from inventory." : notes);
            transaction.Commit();
            TempData["Message"] = issued
                ? $"Order #{bookingId} updated to {status}. Shop products were deducted once and recorded in inventory history."
                : $"Order #{bookingId} is {status}. No new stock deduction was needed.";
        }
        catch (InventoryValidationException error)
        {
            transaction.Rollback();
            TempData["Message"] = error.Message;
        }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            transaction.Rollback();
            TempData["Message"] = "Another request is updating this order or stock. No partial change was saved. Reload and try again.";
        }
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ChangeTerminalStatus(
        int bookingId,
        string targetStatus,
        string notes,
        string[] allowedCurrentStatuses)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");

        using var conn = _db.GetConnection();
        conn.Open();
        bool rewardsReady = RewardStore.IsReady(conn);
        using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            // Keep the same booking-first lock order used by payment and reward writes.
            // Status catalogue rows are read without locking shared catalogue records.
            object? currentStatusId;
            using (var command = new MySqlCommand(
                "SELECT current_status_id FROM bookings WHERE booking_id = @bookingId FOR UPDATE", conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                currentStatusId = command.ExecuteScalar();
            }
            string currentStatus = "";
            if (currentStatusId != null)
            {
                using var command = new MySqlCommand(
                    "SELECT status_name FROM booking_statuses WHERE booking_status_id = @statusId", conn, transaction);
                command.Parameters.AddWithValue("@statusId", currentStatusId);
                currentStatus = command.ExecuteScalar()?.ToString() ?? "";
            }

            if (!allowedCurrentStatuses.Contains(currentStatus))
            {
                transaction.Rollback();
                TempData["Message"] = targetStatus == "Completed"
                    ? "Only an order that is Ready for Pickup can be completed."
                    : "This order is no longer active and cannot be cancelled.";
                return RedirectToAction(nameof(Index));
            }

            // Laundry is released (to the customer or their rider) only when fully paid.
            if (targetStatus == "Completed")
            {
                decimal balance = GetOrderTotal(conn, transaction, bookingId, rewardsReady)
                                  - GetPaidAmount(conn, transaction, bookingId);
                if (balance > 0.005m)
                {
                    transaction.Rollback();
                    TempData["Message"] = $"Order #{bookingId} cannot be released yet. Remaining balance: PHP {balance:N2}. Record the payment first.";
                    return RedirectToAction(nameof(Index));
                }
            }

            int staffId = HttpContext.Session.GetInt32("UserId")!.Value;
            decimal cancelRefund = 0m;
            if (targetStatus == "Cancelled")
            {
                // Verified payments are returned in full when a booking is cancelled.
                cancelRefund = RefundPaidExcess(conn, transaction, bookingId,
                    GetPaidAmount(conn, transaction, bookingId), staffId, "booking cancelled.");
                using var command = new MySqlCommand("""
                    UPDATE payments p
                    JOIN payment_statuses pending ON pending.payment_status_id = p.payment_status_id AND pending.status_name = 'Pending'
                    JOIN payment_statuses failed ON failed.status_name = 'Failed'
                    SET p.payment_status_id = failed.payment_status_id,
                        p.notes = LEFT(CONCAT_WS(' ', p.notes, 'Booking cancelled before verification; return any amount received.'), 500)
                    WHERE p.booking_id = @bookingId
                    """, conn, transaction);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.ExecuteNonQuery();
            }

            using (var command = new MySqlCommand("""
                UPDATE bookings
                SET current_status_id =
                        (SELECT booking_status_id FROM booking_statuses WHERE status_name = @targetStatus),
                    completed_at = CASE WHEN @targetStatus = 'Completed' THEN NOW() ELSE completed_at END
                WHERE booking_id = @bookingId
                """, conn, transaction))
            {
                command.Parameters.AddWithValue("@targetStatus", targetStatus);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.ExecuteNonQuery();
            }

            AddStatusHistory(
                conn,
                transaction,
                bookingId,
                targetStatus,
                staffId,
                notes + (cancelRefund > 0m ? $" Refund of PHP {cancelRefund:N2} recorded." : ""));
            if (cancelRefund > 0m)
            {
                using var customerCommand = new MySqlCommand(
                    "SELECT customer_id FROM bookings WHERE booking_id = @bookingId", conn, transaction);
                customerCommand.Parameters.AddWithValue("@bookingId", bookingId);
                int customerId = Convert.ToInt32(customerCommand.ExecuteScalar());
                AddCustomerNotification(conn, transaction, customerId, bookingId, "Payment", "Payment refunded",
                    $"Your booking #{bookingId} was cancelled. PHP {cancelRefund:N2} of your payment will be refunded.");
            }
            if (rewardsReady)
            {
                if (targetStatus == "Completed")
                    RewardStore.AwardCompletedBooking(conn, transaction, bookingId);
                else if (targetStatus == "Cancelled")
                    RewardStore.RestoreCancelledBooking(conn, transaction, bookingId);
            }
            transaction.Commit();

            TempData["Message"] = targetStatus == "Completed"
                ? $"Order #{bookingId} was completed and removed from the active queue."
                : $"Order #{bookingId} was cancelled and removed from the active queue."
                  + (cancelRefund > 0m ? $" REFUND PHP {cancelRefund:N2} to the customer." : "");
        }
        catch (RewardValidationException error)
        {
            transaction.Rollback();
            TempData["Message"] = error.Message;
        }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            transaction.Rollback();
            TempData["Message"] = "Another request is updating this order or its rewards. No partial status change was saved. Reload and try again.";
        }
        return RedirectToAction(nameof(Index));
    }

    private bool IsStaff() => HttpContext.Session.GetString("UserRole") == "Staff"
                              && HttpContext.Session.GetInt32("UserId").HasValue;

    private static decimal GetOrderTotal(MySqlConnection conn, MySqlTransaction transaction, int bookingId, bool rewardsReady)
    {
        decimal total;
        using (var command = new MySqlCommand(
                   "SELECT COALESCE(SUM(quantity * unit_price), 0) FROM booking_services WHERE booking_id = @bookingId",
                   conn, transaction))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId);
            total = Convert.ToDecimal(command.ExecuteScalar());
        }
        return RewardStore.NetTotal(total, rewardsReady ? RewardStore.GetBookingDiscount(conn, transaction, bookingId) : 0m);
    }

    private static decimal GetPaidAmount(MySqlConnection conn, MySqlTransaction transaction, int bookingId)
    {
        using var command = new MySqlCommand("""
            SELECT COALESCE(SUM(p.amount), 0)
            FROM payments p
            JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
            WHERE p.booking_id = @bookingId AND ps.status_name = 'Paid'
            """, conn, transaction);
        command.Parameters.AddWithValue("@bookingId", bookingId);
        return Convert.ToDecimal(command.ExecuteScalar());
    }

    // Refunds are kept as their own 'Refunded' rows so every report that sums 'Paid'
    // payments stays correct without changes.
    private static void InsertRefundRecord(MySqlConnection conn, MySqlTransaction transaction,
        int bookingId, int paymentMethodId, decimal amount, int staffId, string note)
    {
        using var command = new MySqlCommand("""
            INSERT INTO payments
                (booking_id, payment_method_id, payment_status_id, recorded_by_user_id, amount, paid_at, notes)
            SELECT @bookingId, @methodId, status.payment_status_id, @staffId, @amount, NOW(), @note
            FROM payment_statuses status
            WHERE status.status_name = 'Refunded'
            """, conn, transaction);
        command.Parameters.AddWithValue("@bookingId", bookingId);
        command.Parameters.AddWithValue("@methodId", paymentMethodId);
        command.Parameters.AddWithValue("@staffId", staffId);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@note", note);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Returns money the customer paid above the final total, starting from the latest payment.
    /// A fully returned payment becomes 'Refunded'; a partly returned one keeps the kept amount
    /// as 'Paid' and records the returned part as a separate 'Refunded' row.
    /// </summary>
    private static decimal RefundPaidExcess(MySqlConnection conn, MySqlTransaction transaction,
        int bookingId, decimal excess, int staffId, string reason)
    {
        var paid = new List<(long Id, int MethodId, decimal Amount)>();
        using (var command = new MySqlCommand("""
                   SELECT p.payment_id, p.payment_method_id, p.amount
                   FROM payments p
                   JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
                   WHERE p.booking_id = @bookingId AND ps.status_name = 'Paid'
                   ORDER BY p.paid_at DESC, p.payment_id DESC
                   FOR UPDATE
                   """, conn, transaction))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                paid.Add((Convert.ToInt64(reader["payment_id"]), Convert.ToInt32(reader["payment_method_id"]), Convert.ToDecimal(reader["amount"])));
        }

        decimal remaining = excess;
        foreach (var payment in paid)
        {
            if (remaining <= 0.005m) break;
            if (payment.Amount <= remaining + 0.005m)
            {
                using var command = new MySqlCommand("""
                    UPDATE payments p
                    JOIN payment_statuses refunded ON refunded.status_name = 'Refunded'
                    SET p.payment_status_id = refunded.payment_status_id,
                        p.notes = LEFT(CONCAT_WS(' ', p.notes, @note), 500)
                    WHERE p.payment_id = @paymentId
                    """, conn, transaction);
                command.Parameters.AddWithValue("@note", $"Refunded: {reason}");
                command.Parameters.AddWithValue("@paymentId", payment.Id);
                command.ExecuteNonQuery();
                remaining -= payment.Amount;
            }
            else
            {
                using (var command = new MySqlCommand(
                           "UPDATE payments SET amount = amount - @refund WHERE payment_id = @paymentId", conn, transaction))
                {
                    command.Parameters.AddWithValue("@refund", remaining);
                    command.Parameters.AddWithValue("@paymentId", payment.Id);
                    command.ExecuteNonQuery();
                }
                InsertRefundRecord(conn, transaction, bookingId, payment.MethodId, remaining, staffId, $"Refunded: {reason}");
                remaining = 0m;
            }
        }
        return excess - Math.Max(0m, remaining);
    }

    private static void AddCustomerNotification(MySqlConnection conn, MySqlTransaction transaction,
        int customerId, int bookingId, string type, string title, string message)
    {
        using var command = new MySqlCommand("""
            INSERT INTO notifications (user_id, booking_id, notification_type, title, message)
            VALUES (@customerId, @bookingId, @type, @title, @message)
            """, conn, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@bookingId", bookingId);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@title", title);
        command.Parameters.AddWithValue("@message", message);
        command.ExecuteNonQuery();
    }

    private static void LoadBookings(MySqlConnection conn, string statuses, List<StaffBookingModel> target,
        HashSet<int> issuedBookings, bool rewardsReady)
    {
        string[] names = statuses.Split(',');
        string parameters = string.Join(",", names.Select((_, index) => $"@status{index}"));
        string discountQuery = rewardsReady
            ? "COALESCE((SELECT discount_amount FROM booking_rewards WHERE booking_id = b.booking_id AND returned_at IS NULL), 0)"
            : "0";
        string query = BookingQuery.Replace("{rewardDiscount}", discountQuery)
                .Replace("{completedOrders}", CustomerCategory.CompletedOrdersSql)
            // Rush orders come first, then Regular customers (priority requests), then oldest bookings.
            + $" WHERE status.status_name IN ({parameters})"
            + $" ORDER BY b.is_rush DESC, completed_orders >= {CustomerCategory.RegularThreshold} DESC, b.booked_at";
        using var command = new MySqlCommand(query, conn);
        for (int index = 0; index < names.Length; index++) command.Parameters.AddWithValue($"@status{index}", names[index]);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            target.Add(new StaffBookingModel
            {
                Id = Convert.ToInt32(reader["booking_id"]),
                BookingCode = reader["booking_code"]?.ToString() ?? "",
                CustomerName = reader["customer_name"]?.ToString() ?? "",
                ServiceType = reader["service_names"]?.ToString() ?? "",
                CustomerNotes = reader["customer_notes"]?.ToString() ?? "",
                IsRush = Convert.ToBoolean(reader["is_rush"]),
                CustomerCategory = CustomerCategory.Classify(Convert.ToInt32(reader["completed_orders"])),
                ScheduledAt = reader["scheduled_pickup_at"] == DBNull.Value
                    ? null : Convert.ToDateTime(reader["scheduled_pickup_at"]),
                ShopPieces = Convert.ToDecimal(reader["shop_pieces"]),
                ShopSupplies = reader["shop_supplies"]?.ToString() ?? "",
                SuppliesIssued = issuedBookings.Contains(Convert.ToInt32(reader["booking_id"])),
                Weight = reader["weight_kg"] == DBNull.Value ? 0f : Convert.ToSingle(reader["weight_kg"]),
                Status = reader["status_name"]?.ToString() ?? "",
                Amount = reader["order_total"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["order_total"]),
                RewardDiscount = Convert.ToDecimal(reader["reward_discount"]),
                PaidAmount = reader["paid_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["paid_amount"]),
                PendingGCashAmount = reader["pending_gcash_amount"] == DBNull.Value
                    ? 0m
                    : Convert.ToDecimal(reader["pending_gcash_amount"]),
                PerKgRateTotal = reader["per_kg_rate_total"] == DBNull.Value
                    ? 0m
                    : Convert.ToDecimal(reader["per_kg_rate_total"]),
                PerLoadRateTotal = Convert.ToDecimal(reader["per_load_rate_total"]),
                FixedAmount = reader["fixed_amount"] == DBNull.Value
                    ? 0m
                    : Convert.ToDecimal(reader["fixed_amount"])
            });
        }
    }

    private static void LoadPickupMethods(MySqlConnection conn, List<StaffPickupModel> target)
    {
        const string query = """
            SELECT b.booking_id,
                   b.booking_code,
                   CONCAT(u.first_name, ' ', u.last_name) AS customer_name,
                   u.phone,
                   b.scheduled_pickup_at,
                   status.status_name
            FROM bookings b
            JOIN users u ON u.user_id = b.customer_id
            JOIN booking_statuses status ON status.booking_status_id = b.current_status_id
            WHERE status.is_terminal = FALSE
            ORDER BY COALESCE(b.scheduled_pickup_at, b.booked_at), b.booking_id
            """;

        using var command = new MySqlCommand(query, conn);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // The shop has no delivery service: laundry is always claimed at the shop,
            // either by the customer or by a third-party rider they arrange.
            target.Add(new StaffPickupModel
            {
                Id = Convert.ToInt32(reader["booking_id"]),
                BookingCode = reader["booking_code"]?.ToString() ?? "",
                CustomerName = reader["customer_name"]?.ToString() ?? "",
                ContactNumber = reader["phone"]?.ToString() ?? "",
                Method = "Claim at Shop",
                ScheduledAt = reader["scheduled_pickup_at"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["scheduled_pickup_at"]),
                Location = "EZ-K Laundry Shop (customer or third-party rider)",
                Status = reader["status_name"]?.ToString() ?? ""
            });
        }
    }

    private static int FindOrCreateWalkInCustomer(MySqlConnection conn, MySqlTransaction transaction, string name, string phone)
    {
        using (var find = new MySqlCommand("SELECT user_id FROM users WHERE phone = @phone LIMIT 1", conn, transaction))
        {
            find.Parameters.AddWithValue("@phone", phone.Trim());
            object? existing = find.ExecuteScalar();
            if (existing is not null) return Convert.ToInt32(existing);
        }

        var (firstName, lastName) = AccountController.SplitFullName(name);
        const string insert = """
            INSERT INTO users
                (role_id, user_status_id, first_name, last_name, phone, is_walk_in)
            SELECT r.role_id, us.user_status_id, @firstName, @lastName, @phone, TRUE
            FROM roles r CROSS JOIN user_statuses us
            WHERE r.role_name = 'Customer' AND us.status_name = 'Active';
            SELECT LAST_INSERT_ID();
            """;
        using var command = new MySqlCommand(insert, conn, transaction);
        command.Parameters.AddWithValue("@firstName", firstName);
        command.Parameters.AddWithValue("@lastName", lastName);
        command.Parameters.AddWithValue("@phone", phone.Trim());
        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <param name="quantityOverride">Used by the POS, where staff set the number of loads directly.</param>
    /// <param name="priceOverride">A unit price other than the catalogue price, e.g. products included in a package.</param>
    internal static decimal AddBookingService(MySqlConnection conn, MySqlTransaction transaction,
        int bookingId, string serviceName, decimal weight, int pieces = 1, decimal? quantityOverride = null,
        decimal? priceOverride = null)
    {
        int serviceId;
        decimal price;
        string basis;
        using (var find = new MySqlCommand(
                   "SELECT service_id, current_price, charging_basis FROM services WHERE service_name = @name AND is_active = TRUE",
                   conn, transaction))
        {
            find.Parameters.AddWithValue("@name", serviceName);
            using var reader = find.ExecuteReader();
            if (!reader.Read()) throw new InventoryValidationException($"{serviceName} is unavailable. Please review the selected services.");
            serviceId = Convert.ToInt32(reader["service_id"]);
            price = Convert.ToDecimal(reader["current_price"]);
            basis = reader["charging_basis"]?.ToString() ?? "Flat";
        }

        if (serviceName == ServiceRules.SelfService)
        {
            price = 0m;
            basis = "Flat";
        }
        if (serviceName is "Powder" or "Downy" && basis != "PerItem")
            throw new InventoryValidationException("Products must be priced per piece before they can be issued.");
        if (priceOverride is decimal overridePrice) price = overridePrice;
        decimal quantity = quantityOverride
            ?? LoadPricing.QuantityFor(basis, weight, pieces, LoadPricing.ReadKgPerLoad(conn, transaction));
        using var insert = new MySqlCommand("""
            INSERT INTO booking_services (booking_id, service_id, quantity, unit_price, charging_basis)
            VALUES (@bookingId, @serviceId, @quantity, @price, @basis)
            """, conn, transaction);
        insert.Parameters.AddWithValue("@bookingId", bookingId);
        insert.Parameters.AddWithValue("@serviceId", serviceId);
        insert.Parameters.AddWithValue("@quantity", quantity);
        insert.Parameters.AddWithValue("@price", price);
        insert.Parameters.AddWithValue("@basis", basis);
        insert.ExecuteNonQuery();
        return quantity * price;
    }

    private static void AddStatusHistory(MySqlConnection conn, MySqlTransaction transaction,
        int bookingId, string status, int staffId, string notes)
    {
        const string query = """
            INSERT INTO booking_status_history (booking_id, booking_status_id, changed_by_user_id, notes)
            SELECT @bookingId, booking_status_id, @staffId, @notes
            FROM booking_statuses WHERE status_name = @status
            """;
        using var command = new MySqlCommand(query, conn, transaction);
        command.Parameters.AddWithValue("@bookingId", bookingId);
        command.Parameters.AddWithValue("@staffId", staffId);
        command.Parameters.AddWithValue("@notes", notes);
        command.Parameters.AddWithValue("@status", status);
        command.ExecuteNonQuery();
    }

    private static string CreateBookingCode() => $"EZK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private const string BookingQuery = """
        SELECT b.booking_id, b.booking_code, b.customer_notes, b.is_rush, b.scheduled_pickup_at,
               {completedOrders} AS completed_orders,
               CONCAT(u.first_name, ' ', u.last_name) AS customer_name,
               status.status_name, COALESCE(items.weight_kg, 0) AS weight_kg,
               COALESCE(service_lines.service_names, '') AS service_names,
               ROUND(GREATEST(0, COALESCE(service_lines.order_total, 0) - {rewardDiscount}), 2) AS order_total,
               {rewardDiscount} AS reward_discount,
               COALESCE(service_lines.per_kg_rate_total, 0) AS per_kg_rate_total,
               COALESCE(service_lines.per_load_rate_total, 0) AS per_load_rate_total,
               COALESCE(service_lines.fixed_amount, 0) AS fixed_amount,
               COALESCE(service_lines.shop_pieces, 0) AS shop_pieces,
               COALESCE(service_lines.shop_supplies, '') AS shop_supplies,
               COALESCE(payment_lines.paid_amount, 0) AS paid_amount,
               COALESCE(payment_lines.pending_gcash_amount, 0) AS pending_gcash_amount
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
                   SUM(bs.quantity * bs.unit_price) AS order_total,
                   SUM(CASE WHEN s.service_category = 'Consumable' THEN bs.quantity ELSE 0 END) AS shop_pieces,
                   GROUP_CONCAT(CASE WHEN s.service_category = 'Consumable'
                       THEN CONCAT(s.service_name, ' ×', TRIM(TRAILING '.' FROM TRIM(TRAILING '0' FROM bs.quantity))) END
                       ORDER BY s.service_name SEPARATOR ', ') AS shop_supplies,
                   SUM(CASE WHEN COALESCE(bs.charging_basis, s.charging_basis) = 'PerKg' THEN bs.unit_price ELSE 0 END) AS per_kg_rate_total,
                   SUM(CASE WHEN COALESCE(bs.charging_basis, s.charging_basis) = 'PerLoad' THEN bs.unit_price ELSE 0 END) AS per_load_rate_total,
                   SUM(CASE
                           WHEN COALESCE(bs.charging_basis, s.charging_basis) NOT IN ('PerKg', 'PerLoad') THEN bs.quantity * bs.unit_price
                           ELSE 0
                       END) AS fixed_amount
            FROM booking_services bs JOIN services s ON s.service_id = bs.service_id
            GROUP BY bs.booking_id
        ) service_lines ON service_lines.booking_id = b.booking_id
        LEFT JOIN (
            SELECT p.booking_id,
                   SUM(CASE WHEN ps.status_name = 'Paid' THEN p.amount ELSE 0 END) AS paid_amount,
                   SUM(CASE
                           WHEN ps.status_name = 'Pending' AND pm.method_name = 'GCash' THEN p.amount
                           ELSE 0
                       END) AS pending_gcash_amount
            FROM payments p
            JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
            JOIN payment_methods pm ON pm.payment_method_id = p.payment_method_id
            GROUP BY p.booking_id
        ) payment_lines ON payment_lines.booking_id = b.booking_id
        """;
}
