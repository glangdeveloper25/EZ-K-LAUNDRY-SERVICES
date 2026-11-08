using EZKLaundrySystem.Data;
using EZKLaundrySystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json;

namespace EZKLaundrySystem.Controllers;

public class CustomerDashboardVM
{
    public List<CustomerBooking> History { get; set; } = [];
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string ContactNumber { get; set; } = "";
    public int ActiveOrderCount { get; set; }
    public List<CustomerActiveBooking> ActiveBookings { get; set; } = [];
    public List<string> DraftServices { get; set; } = [];
    public string DraftServiceMode { get; set; } = ServiceRules.FullService;
    public decimal DraftWeight { get; set; } = 5m;
    public string DraftPaymentMethod { get; set; } = "Cash";
    public int DraftPowderQuantity { get; set; } = 1;
    public int DraftDownyQuantity { get; set; } = 1;
    public int DraftDetergentProductId { get; set; }
    public int DraftSoftenerProductId { get; set; }
    public List<PosCatalogItem> Products { get; set; } = [];
    public string DraftDetergentSource { get; set; } = "";
    public string DraftSoftenerSource { get; set; } = "";
    public bool DraftIncludeRushFee { get; set; }
    public DateTime? DraftScheduledAt { get; set; }
    public List<BookingSlot> AvailableSlots { get; set; } = [];
    public Dictionary<string, decimal> Pricing { get; set; } = [];
    public decimal KgPerLoad { get; set; } = LoadPricing.DefaultKgPerLoad;
    public bool OpenBookingForm { get; set; }
    public bool OpenNotifications { get; set; }
    public bool OpenRewards { get; set; }
    // Which customer page is shown: home, book, orders, rewards, notifications or account.
    public string Page { get; set; } = CustomerController.HomePage;
    public bool DraftUseReward { get; set; }
    public RewardSnapshot Rewards { get; set; } = new();
    public MachineSnapshot Machines { get; set; } = new();
    public List<CustomerNotification> Notifications { get; set; } = [];
    public int UnreadNotificationCount => Notifications.Count(notification => !notification.IsRead);
}

public class CustomerNotification
{
    public long Id { get; set; }
    public string Type { get; set; } = "System";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }

    public string TimeAgo
    {
        get
        {
            TimeSpan elapsed = DateTime.Now - SentAt;
            if (elapsed.TotalMinutes < 1) return "Just now";
            if (elapsed.TotalHours < 1) return $"{Math.Max(1, (int)elapsed.TotalMinutes)} minute(s) ago";
            if (elapsed.TotalDays < 1) return $"{Math.Max(1, (int)elapsed.TotalHours)} hour(s) ago";
            if (elapsed.TotalDays < 7) return $"{Math.Max(1, (int)elapsed.TotalDays)} day(s) ago";
            return SentAt.ToString("MMM dd, yyyy h:mm tt");
        }
    }
}

public class CustomerActiveBooking
{
    public string BookingCode { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? ScheduledAt { get; set; }
    public DateTime BookedAt { get; set; }
    public DateTime CancelDeadline => BookedAt.AddMinutes(CustomerController.CancelWindowMinutes);
    public bool CanCancel => Status == "Pending" && DateTime.Now <= CancelDeadline;
}

public class CustomerBooking
{
    public string RefId { get; set; } = "";
    public DateTime BookedAt { get; set; }
    public string Date { get; set; } = "";
    public string Service { get; set; } = "";
    public decimal PaymentAmount { get; set; }
    public string Payment { get; set; } = "";
    public string Status { get; set; } = "";
}

public class BookingReviewVM
{
    public List<BookingReviewLineVM> Services { get; set; } = [];
    public decimal Weight { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string SupplyInstructions { get; set; } = "";
    public bool UseReward { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string ServiceMode { get; set; } = ServiceRules.FullService;
    public decimal Subtotal => Math.Round(Services.Sum(service => service.LineTotal), 2, MidpointRounding.AwayFromZero);
    public decimal RewardDiscount => UseReward ? Math.Min(RewardStore.DiscountAmount, Subtotal) : 0m;
    public decimal EstimatedTotal => Math.Max(0m, Subtotal - RewardDiscount);
    public decimal GCashAmount => PaymentMethod == "GCash" ? EstimatedTotal : 0m;
}

public class BookingReviewLineVM
{
    public string ServiceName { get; set; } = "";
    public string ChargingBasis { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public class BookingReceiptVM
{
    public string BookingCode { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime BookedAt { get; set; }
    public decimal Weight { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string SupplyInstructions { get; set; } = "";
    public DateTime? ScheduledAt { get; set; }
    public string ServiceMode { get; set; } = ServiceRules.FullService;
    public List<BookingReviewLineVM> Services { get; set; } = [];
    public decimal Subtotal => Math.Round(Services.Sum(service => service.LineTotal), 2, MidpointRounding.AwayFromZero);
    public decimal RewardDiscount { get; set; }
    public decimal EstimatedTotal => Math.Max(0m, Subtotal - RewardDiscount);
    public decimal GCashAmount => PaymentMethod == "GCash" ? EstimatedTotal : 0m;
}

public class CustomerBookingDraft
{
    public int CustomerId { get; set; }
    public string BookingCode { get; set; } = "";
    public decimal Weight { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string DetergentSource { get; set; } = "";
    public string SoftenerSource { get; set; } = "";
    public bool UseReward { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public List<CustomerBookingDraftService> Services { get; set; } = [];
}

public class CustomerBookingDraftService
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = "";
    public string ChargingBasis { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string ProductType { get; set; } = "";
}

[AutoValidateAntiforgeryToken]
public class CustomerController : Controller
{
    // Use Case "Cancel Booking": customers may cancel within 15 minutes of booking, while still Pending.
    public const int CancelWindowMinutes = 15;
    private const string BookingDraftSessionKey = "CustomerBookingDraft";
    private const string RewardSelectionSessionKey = "CustomerUseReward";
    private readonly DbConnection _db = new();

    public const string HomePage = "home";
    public const string BookPage = "book";
    public const string OrdersPage = "orders";
    public const string RewardsPage = "rewards";
    public const string NotificationsPage = "notifications";
    public const string AccountPage = "account";

    // The customer side has no sidebar: the home page links to each of these as a separate page.
    public IActionResult Book(bool edit = false) => Index(editBooking: edit, openBooking: true);
    public IActionResult Orders() => Index(page: OrdersPage);
    public IActionResult Rewards() => Index(openRewards: true);
    public IActionResult Notifications() => Index(openNotifications: true);
    public IActionResult Account() => Index(page: AccountPage);

    public IActionResult Index(bool editBooking = false, bool openBooking = false, bool openNotifications = false, bool openRewards = false, string page = HomePage)
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");

        var model = new CustomerDashboardVM();
        using var conn = _db.GetConnection();
        conn.Open();
        model.Rewards = RewardStore.ReadSnapshot(conn, customerId);
        model.Machines = MachineStore.ReadSnapshot(conn);
        model.OpenRewards = openRewards;
        model.DraftUseReward = HttpContext.Session.GetString(RewardSelectionSessionKey) == customerId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        const string userQuery = """
            SELECT CONCAT(first_name, ' ', last_name) AS full_name, email, phone
            FROM users WHERE user_id = @userId
            """;
        using (var command = new MySqlCommand(userQuery, conn))
        {
            command.Parameters.AddWithValue("@userId", customerId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return RedirectToAction("Logout", "Account");
            model.FullName = reader["full_name"]?.ToString() ?? "";
            model.Email = reader["email"]?.ToString() ?? "";
            model.ContactNumber = reader["phone"]?.ToString() ?? "";
        }

        string rewardJoin = model.Rewards.IsReady
            ? "LEFT JOIN booking_rewards reward ON reward.booking_id = b.booking_id" : "";
        string rewardDeduction = model.Rewards.IsReady
            ? "COALESCE(CASE WHEN reward.returned_at IS NULL THEN reward.discount_amount ELSE 0 END, 0)" : "0";
        string historyQuery = $"""
            SELECT b.booking_code, b.booked_at, status.status_name,
                   COALESCE(service_lines.service_names, '') AS service_names,
                   ROUND(GREATEST(0, COALESCE(service_lines.order_total, 0) - {rewardDeduction}), 2) AS order_total
            FROM bookings b
            JOIN booking_statuses status ON status.booking_status_id = b.current_status_id
            LEFT JOIN (
                SELECT bs.booking_id,
                       GROUP_CONCAT(s.service_name ORDER BY s.service_id SEPARATOR ', ') AS service_names,
                       SUM(bs.quantity * bs.unit_price) AS order_total
                FROM booking_services bs
                JOIN services s ON s.service_id = bs.service_id
                GROUP BY bs.booking_id
            ) service_lines ON service_lines.booking_id = b.booking_id
            {rewardJoin}
            WHERE b.customer_id = @customerId
            ORDER BY b.booked_at DESC
            """;
        using (var command = new MySqlCommand(historyQuery, conn))
        {
            command.Parameters.AddWithValue("@customerId", customerId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                model.History.Add(new CustomerBooking
                {
                    RefId = reader["booking_code"]?.ToString() ?? "",
                    BookedAt = Convert.ToDateTime(reader["booked_at"]),
                    Date = Convert.ToDateTime(reader["booked_at"]).ToString("MMM dd, yyyy"),
                    Service = reader["service_names"]?.ToString() ?? "",
                    PaymentAmount = Convert.ToDecimal(reader["order_total"]),
                    Payment = $"₱{Convert.ToDecimal(reader["order_total"]):N2}",
                    Status = reader["status_name"]?.ToString() ?? ""
                });
            }
        }

        const string activeBookingQuery = """
            SELECT b.booking_code, b.scheduled_pickup_at, b.booked_at, status.status_name
            FROM bookings b
            JOIN booking_statuses status ON status.booking_status_id = b.current_status_id
            WHERE b.customer_id = @customerId AND status.is_terminal = FALSE
            ORDER BY b.booked_at DESC
            """;
        using (var command = new MySqlCommand(activeBookingQuery, conn))
        {
            command.Parameters.AddWithValue("@customerId", customerId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                model.ActiveBookings.Add(new CustomerActiveBooking
                {
                    BookingCode = reader["booking_code"]?.ToString() ?? "",
                    Status = reader["status_name"]?.ToString() ?? "",
                    ScheduledAt = reader["scheduled_pickup_at"] == DBNull.Value
                        ? null : Convert.ToDateTime(reader["scheduled_pickup_at"]),
                    BookedAt = Convert.ToDateTime(reader["booked_at"])
                });
            }

            model.ActiveOrderCount = model.ActiveBookings.Count;
        }

        using (var command = new MySqlCommand(
                   "SELECT service_name, current_price FROM services WHERE is_active = TRUE",
                   conn))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                model.Pricing[reader["service_name"]?.ToString() ?? ""] = Convert.ToDecimal(reader["current_price"]);
            }
        }
        model.KgPerLoad = LoadPricing.ReadKgPerLoad(conn);
        model.Products = ProductCatalog.Read(conn);

        const string notificationsQuery = """
            SELECT notification_id, notification_type, title, message, is_read, sent_at
            FROM notifications
            WHERE user_id = @customerId
            ORDER BY sent_at DESC, notification_id DESC
            LIMIT 100
            """;
        using (var command = new MySqlCommand(notificationsQuery, conn))
        {
            command.Parameters.AddWithValue("@customerId", customerId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                model.Notifications.Add(new CustomerNotification
                {
                    Id = Convert.ToInt64(reader["notification_id"]),
                    Type = reader["notification_type"]?.ToString() ?? "System",
                    Title = reader["title"]?.ToString() ?? "",
                    Message = reader["message"]?.ToString() ?? "",
                    IsRead = Convert.ToBoolean(reader["is_read"]),
                    SentAt = Convert.ToDateTime(reader["sent_at"])
                });
            }
        }

        model.AvailableSlots = BookingSchedule.ReadUpcomingSlots(conn, DateTime.Now);
        model.OpenBookingForm = editBooking || openBooking;
        model.OpenNotifications = openNotifications;
        model.Page = model.OpenBookingForm ? BookPage
            : openNotifications ? NotificationsPage
            : openRewards ? RewardsPage
            : page is OrdersPage or AccountPage ? page
            : HomePage;
        if (editBooking && GetBookingDraft() is { } draft)
        {
            model.DraftScheduledAt = draft.ScheduledAt;
            model.DraftServices = draft.Services.Select(service => service.ServiceName).ToList();
            model.DraftServiceMode = draft.Services.Any(service => service.ServiceName == ServiceRules.SelfService)
                ? ServiceRules.SelfService : ServiceRules.FullService;
            model.DraftWeight = draft.Weight;
            model.DraftPaymentMethod = draft.PaymentMethod;
            model.DraftUseReward = draft.UseReward;
            CustomerBookingDraftService? powder = draft.Services.FirstOrDefault(service => service.ProductType == ProductCatalog.Detergent);
            CustomerBookingDraftService? downy = draft.Services.FirstOrDefault(service => service.ProductType == ProductCatalog.FabricConditioner);
            model.DraftDetergentProductId = powder?.ServiceId ?? 0;
            model.DraftSoftenerProductId = downy?.ServiceId ?? 0;
            // An old checked add-on proves a shop purchase; an unchecked one does
            // not establish that the customer will supply their own product.
            model.DraftDetergentSource = LaundrySupplyChoices.IsValid(draft.DetergentSource)
                ? draft.DetergentSource : powder != null ? LaundrySupplyChoices.Shop : "";
            model.DraftSoftenerSource = LaundrySupplyChoices.IsValid(draft.SoftenerSource)
                ? draft.SoftenerSource : downy != null ? LaundrySupplyChoices.Shop : "";
            model.DraftIncludeRushFee = draft.Services.Any(service => service.ServiceName == "Rush Fee");
            if (powder != null) model.DraftPowderQuantity = Convert.ToInt32(powder.Quantity);
            if (downy != null) model.DraftDownyQuantity = Convert.ToInt32(downy.Quantity);
        }

        return View(nameof(Index), model);
    }

    [HttpPost]
    public IActionResult ConfirmBooking(
        string? serviceType,
        string serviceMode,
        decimal weight,
        string paymentMethod,
        string detergentSource = "",
        int powderQuantity = 0,
        string softenerSource = "",
        int downyQuantity = 0,
        int detergentProductId = 0,
        int softenerProductId = 0,
        string packageSupplies = "",
        int packageDetergentId = 0,
        int packageFabconId = 0,
        bool includeRushFee = false,
        bool useReward = false,
        string appointment = "")
    {
        // Full-Service is EZ-K's wash-dry-fold package with the customer's own supplies or
        // the shop's (one detergent and one fabric conditioner per load, for a package fee).
        bool fullService = serviceMode == ServiceRules.FullService;
        bool packageShop = fullService && packageSupplies == LaundrySupplyChoices.Shop;
        if (fullService)
        {
            serviceType = "Washing, Drying, Folding";
            detergentSource = softenerSource = packageShop ? LaundrySupplyChoices.Shop : LaundrySupplyChoices.Customer;
            powderQuantity = downyQuantity = 1;
        }
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");
        if (!BookingSchedule.TryParseSlot(appointment, out DateTime scheduledAt)
            || !BookingSchedule.IsBookableTime(scheduledAt, DateTime.Now))
        {
            TempData["Msg"] = "Choose an available drop-off schedule.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Book));
        }

        List<string> selectedServices = (serviceType ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        string[] allowedCoreServices = fullService ? ["Washing", "Drying", "Folding"] : ["Washing", "Drying"];
        bool invalidService = selectedServices.Any(service =>
            !allowedCoreServices.Contains(service, StringComparer.OrdinalIgnoreCase));
        bool invalidSupplyChoice = !LaundrySupplyChoices.IsValidQuantity(detergentSource, powderQuantity)
                                   || !LaundrySupplyChoices.IsValidQuantity(softenerSource, downyQuantity);
        if (!ModelState.IsValid || selectedServices.Count == 0 || invalidService || weight is < 1m or > 20m
            || weight != decimal.Round(weight, 2)
            || paymentMethod is not ("GCash" or "Cash") || invalidSupplyChoice || !ServiceRules.IsValidMode(serviceMode))
        {
            TempData["Msg"] = "Choose how detergent and fabric softener will be provided, and complete valid booking details. Shop quantities must be 1 to 20 pieces; weight must be 1 to 20 kg with at most two decimal places.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Book));
        }
        if (ServiceRules.ServiceModeConflict(serviceMode, includeRushFee) is string selfServiceError)
        {
            TempData["Msg"] = selfServiceError;
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Book));
        }

        using var conn = _db.GetConnection();
        conn.Open();

        var itemQuantities = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var priceOverrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        int packageLoads = LoadPricing.LoadsFor(weight, LoadPricing.ReadKgPerLoad(conn));
        decimal packageExtra = 0m;
        foreach (var (source, productId, quantity, type) in new[]
                 {
                     (detergentSource, packageShop ? packageDetergentId : detergentProductId, powderQuantity, ProductCatalog.Detergent),
                     (softenerSource, packageShop ? packageFabconId : softenerProductId, downyQuantity, ProductCatalog.FabricConditioner)
                 })
        {
            if (source != LaundrySupplyChoices.Shop) continue;
            if (ProductCatalog.FindActive(conn, null, productId, type) is not var (productName, extra))
            {
                TempData["Msg"] = $"Choose which {type.ToLowerInvariant()} the shop should use.";
                TempData["Type"] = "error";
                return RedirectToAction(nameof(Book));
            }
            selectedServices.Add(productName);
            if (packageShop)
            {
                // Package supplies: one piece per load, included in the package fee.
                itemQuantities[productName] = packageLoads;
                priceOverrides[productName] = 0m;
                packageExtra += extra;
            }
            else
                itemQuantities[productName] = quantity;
        }
        if (packageShop)
        {
            selectedServices.Add(ProductCatalog.PackageFee);
            if (packageExtra > 0m)
            {
                selectedServices.Add(ProductCatalog.PremiumFee);
                priceOverrides[ProductCatalog.PremiumFee] = packageExtra;
            }
        }
        if (includeRushFee) selectedServices.Add("Rush Fee");
        if (serviceMode == ServiceRules.SelfService) selectedServices.Add(ServiceRules.SelfService);

        if (BookingSchedule.CountBookingsInSlot(conn, null, scheduledAt) >= BookingSchedule.SlotCapacity)
        {
            TempData["Msg"] = $"The {scheduledAt:MMM dd, h:mm tt} schedule is already full. Please choose another time.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Book));
        }
        CustomerBookingDraft? draft = CreateBookingDraft(
            conn, selectedServices.Distinct(StringComparer.OrdinalIgnoreCase), itemQuantities,
            weight, paymentMethod, out string unavailableService, priceOverrides);
        if (draft == null)
        {
            TempData["Msg"] = $"{unavailableService} is not currently available. Please update your selection.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Book));
        }

        draft.DetergentSource = detergentSource;
        draft.SoftenerSource = softenerSource;
        draft.CustomerId = customerId;
        draft.ScheduledAt = scheduledAt;
        if (useReward)
        {
            RewardSnapshot rewards = RewardStore.ReadSnapshot(conn, customerId);
            if (!rewards.CanRedeem || ToReviewModel(draft).Subtotal < RewardStore.DiscountAmount)
            {
                TempData["Msg"] = rewards.CanRedeem
                    ? "A reward discount requires an estimated order total of at least PHP 50."
                    : "You need 100 available points to use the PHP 50 discount.";
                TempData["Type"] = "error";
                return RedirectToAction(nameof(Book));
            }
        }
        draft.UseReward = useReward;
        HttpContext.Session.Remove(RewardSelectionSessionKey);
        SaveBookingDraft(draft);
        return RedirectToAction(nameof(ReviewBooking));
    }

    [HttpGet]
    public IActionResult ReviewBooking()
    {
        if (!TryGetCustomerId(out _)) return RedirectToAction("Login", "Account");
        CustomerBookingDraft? draft = GetBookingDraft();
        if (draft == null) return RedirectToAction(nameof(Book));
        if (!HasValidSupplyChoices(draft)) return RequestSupplyChoices();
        return View(ToReviewModel(draft));
    }

    [HttpPost]
    public IActionResult ConfirmReviewedBooking()
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");
        CustomerBookingDraft? draft = GetBookingDraft();
        if (draft == null)
        {
            TempData["Msg"] = "Your booking review expired. Please enter the details again.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Book));
        }

        if (!HasValidSupplyChoices(draft)) return RequestSupplyChoices();
        if (draft.PaymentMethod == "GCash" && ToReviewModel(draft).EstimatedTotal > 0m)
        {
            if (!CanContinueRewardBooking(customerId, draft)) return RedirectToAction(nameof(ReviewBooking));
            return RedirectToAction(nameof(PaymentQR));
        }

        return FinalizeBooking(customerId, draft);
    }

    [HttpGet]
    public IActionResult PaymentQR()
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");
        CustomerBookingDraft? draft = GetBookingDraft();
        if (draft == null || draft.PaymentMethod != "GCash")
            return RedirectToAction(nameof(Book));
        if (!HasValidSupplyChoices(draft)) return RequestSupplyChoices();
        if (!CanContinueRewardBooking(customerId, draft)) return RedirectToAction(nameof(ReviewBooking));
        if (ToReviewModel(draft).EstimatedTotal <= 0m)
            return RedirectToAction(nameof(ReviewBooking));
        return View(ToReviewModel(draft));
    }

    [HttpPost]
    public IActionResult ConfirmGCashPayment()
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");
        CustomerBookingDraft? draft = GetBookingDraft();
        if (draft == null || draft.PaymentMethod != "GCash")
        {
            TempData["Msg"] = "Your payment session expired. No booking was created.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Book));
        }

        if (!HasValidSupplyChoices(draft)) return RequestSupplyChoices();
        return FinalizeBooking(customerId, draft, paymentDeclared: true);
    }

    [HttpPost]
    public IActionResult CancelMyBooking(string bookingCode)
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");

        using var conn = _db.GetConnection();
        conn.Open();
        int bookingId = 0;
        DateTime bookedAt = DateTime.MinValue;
        using (var command = new MySqlCommand(
                   "SELECT booking_id, booked_at FROM bookings WHERE booking_code = @code AND customer_id = @customerId",
                   conn))
        {
            command.Parameters.AddWithValue("@code", bookingCode ?? "");
            command.Parameters.AddWithValue("@customerId", customerId);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                bookingId = Convert.ToInt32(reader["booking_id"]);
                bookedAt = Convert.ToDateTime(reader["booked_at"]);
            }
        }

        if (bookingId == 0 || DateTime.Now > bookedAt.AddMinutes(CancelWindowMinutes))
        {
            TempData["Msg"] = $"This booking can no longer be cancelled online. Bookings can be cancelled only within {CancelWindowMinutes} minutes after booking. Please contact the shop.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Index));
        }

        bool rewardsReady = RewardStore.IsReady(conn);
        using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            decimal? refunded = BookingSchedule.CancelPending(conn, transaction, bookingId, rewardsReady, customerId,
                _ => "Cancelled by the customer within the 15-minute cancellation window.",
                "Booking cancelled",
                booking => $"You cancelled booking {booking.Code}.");
            if (refunded == null)
            {
                transaction.Rollback();
                TempData["Msg"] = "This booking was already accepted by the staff and can no longer be cancelled online. Please contact the shop.";
                TempData["Type"] = "error";
                return RedirectToAction(nameof(Index));
            }
            transaction.Commit();
            TempData["Msg"] = "Your booking was cancelled." + (refunded > 0m ? $" PHP {refunded:N2} will be refunded by the shop." : "");
            TempData["Type"] = "success";
        }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            transaction.Rollback();
            TempData["Msg"] = "Another request is updating this booking. Please try again.";
            TempData["Type"] = "error";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult CancelBookingDraft()
    {
        if (!TryGetCustomerId(out _)) return RedirectToAction("Login", "Account");
        ClearBookingDraft();
        TempData["Msg"] = "Booking cancelled. No booking was created.";
        TempData["Type"] = "info";
        return RedirectToAction(nameof(Book));
    }

    [HttpGet]
    public IActionResult BookingReceipt()
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");
        int? bookingId = HttpContext.Session.GetInt32("LastBookingId");
        if (!bookingId.HasValue) return RedirectToAction(nameof(Index));

        using var conn = _db.GetConnection();
        conn.Open();
        const string bookingQuery = """
            SELECT b.booking_code, b.booked_at, b.customer_notes, b.scheduled_pickup_at, status.status_name,
                   COALESCE(SUM(items.weight_kg), 0) AS weight_kg
            FROM bookings b
            JOIN booking_statuses status ON status.booking_status_id = b.current_status_id
            LEFT JOIN booking_items items ON items.booking_id = b.booking_id
            WHERE b.booking_id = @bookingId AND b.customer_id = @customerId
            GROUP BY b.booking_id, b.booking_code, b.booked_at, b.customer_notes, b.scheduled_pickup_at, status.status_name
            """;
        var model = new BookingReceiptVM();
        using (var command = new MySqlCommand(bookingQuery, conn))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId.Value);
            command.Parameters.AddWithValue("@customerId", customerId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return RedirectToAction(nameof(Index));
            model.BookingCode = reader["booking_code"]?.ToString() ?? "";
            model.BookedAt = Convert.ToDateTime(reader["booked_at"]);
            model.Status = reader["status_name"]?.ToString() ?? "";
            model.Weight = Convert.ToDecimal(reader["weight_kg"]);
            model.SupplyInstructions = reader["customer_notes"]?.ToString() ?? "";
            model.ScheduledAt = reader["scheduled_pickup_at"] == DBNull.Value
                ? null : Convert.ToDateTime(reader["scheduled_pickup_at"]);
        }

        const string servicesQuery = """
            SELECT service.service_name, COALESCE(booked.charging_basis, service.charging_basis) AS charging_basis,
                   booked.quantity, booked.unit_price
            FROM booking_services booked
            JOIN services service ON service.service_id = booked.service_id
            WHERE booked.booking_id = @bookingId
            ORDER BY booked.booking_service_id
            """;
        using (var command = new MySqlCommand(servicesQuery, conn))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId.Value);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string serviceName = reader["service_name"]?.ToString() ?? "";
                if (serviceName == ServiceRules.SelfService)
                {
                    model.ServiceMode = ServiceRules.SelfService;
                    continue;
                }
                model.Services.Add(new BookingReviewLineVM
                {
                    ServiceName = serviceName,
                    ChargingBasis = reader["charging_basis"]?.ToString() ?? "",
                    Quantity = Convert.ToDecimal(reader["quantity"]),
                    UnitPrice = Convert.ToDecimal(reader["unit_price"])
                });
            }
        }

        model.RewardDiscount = Math.Min(model.Subtotal, RewardStore.GetBookingDiscount(conn, null, bookingId.Value));
        model.PaymentMethod = HttpContext.Session.GetString("LastBookingPaymentMethod") ?? "Not specified";
        return View(model);
    }

    // Live Tracking auto-update: the home page polls this every 15 seconds and reloads only when
    // the signature differs from the one it was rendered with.
    public const int TrackingPollSeconds = 15;

    public static string TrackingSignature(IEnumerable<CustomerActiveBooking> bookings, int unreadNotifications) =>
        string.Join("|", bookings.Select(booking => $"{booking.BookingCode}:{booking.Status}")) + "#" + unreadNotifications;

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult TrackingStatus()
    {
        if (!TryGetCustomerId(out int customerId)) return Unauthorized();

        using var conn = _db.GetConnection();
        conn.Open();
        // Same filter and order as the home page's active-booking query, so an unchanged state gives the same signature.
        var bookings = new List<CustomerActiveBooking>();
        const string activeBookingQuery = """
            SELECT b.booking_code, status.status_name
            FROM bookings b
            JOIN booking_statuses status ON status.booking_status_id = b.current_status_id
            WHERE b.customer_id = @customerId AND status.is_terminal = FALSE
            ORDER BY b.booked_at DESC
            """;
        using (var command = new MySqlCommand(activeBookingQuery, conn))
        {
            command.Parameters.AddWithValue("@customerId", customerId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                bookings.Add(new CustomerActiveBooking
                {
                    BookingCode = reader["booking_code"]?.ToString() ?? "",
                    Status = reader["status_name"]?.ToString() ?? ""
                });
            }
        }

        // The home page counts unread items among the latest 100 notifications, so count the same window here.
        const string unreadQuery = """
            SELECT COUNT(*) FROM (
                SELECT is_read FROM notifications
                WHERE user_id = @customerId
                ORDER BY sent_at DESC, notification_id DESC
                LIMIT 100
            ) latest
            WHERE latest.is_read = FALSE
            """;
        int unread;
        using (var command = new MySqlCommand(unreadQuery, conn))
        {
            command.Parameters.AddWithValue("@customerId", customerId);
            unread = Convert.ToInt32(command.ExecuteScalar());
        }

        return Json(new { signature = TrackingSignature(bookings, unread) });
    }

    [HttpPost]
    public IActionResult MarkAllNotificationsRead()
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");

        using var conn = _db.GetConnection();
        conn.Open();
        const string query = """
            UPDATE notifications
            SET is_read = TRUE, read_at = COALESCE(read_at, NOW())
            WHERE user_id = @customerId AND is_read = FALSE
            """;
        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@customerId", customerId);
        int updated = command.ExecuteNonQuery();

        TempData["Msg"] = updated > 0
            ? $"{updated} notification(s) marked as read."
            : "All notifications are already read.";
        TempData["Type"] = "info";
        return RedirectToAction(nameof(Notifications));
    }

    [HttpPost]
    public IActionResult UpdateProfile(string FullName, string ContactNumber)
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(ContactNumber)) return BadRequest();

        var (firstName, lastName) = AccountController.SplitFullName(FullName);
        using var conn = _db.GetConnection();
        conn.Open();
        const string query = "UPDATE users SET first_name = @firstName, last_name = @lastName, phone = @phone WHERE user_id = @id";
        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@firstName", firstName);
        command.Parameters.AddWithValue("@lastName", lastName);
        command.Parameters.AddWithValue("@phone", ContactNumber.Trim());
        command.Parameters.AddWithValue("@id", customerId);
        command.ExecuteNonQuery();
        HttpContext.Session.SetString("UserName", $"{firstName} {lastName}".Trim());

        TempData["Msg"] = "Account information updated successfully.";
        TempData["Type"] = "success";
        return RedirectToAction(nameof(Account));
    }

    [HttpPost]
    public IActionResult ChangePassword(string currentPass, string newPass, string confirmPass)
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");
        if (newPass != confirmPass)
        {
            TempData["Msg"] = "New password and confirmation do not match.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Account));
        }

        if (!PasswordSecurity.IsStrongPassword(newPass))
        {
            TempData["Msg"] = PasswordSecurity.StrengthError;
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Account));
        }

        using var conn = _db.GetConnection();
        conn.Open();
        string storedHash;
        using (var find = new MySqlCommand("SELECT password_hash FROM users WHERE user_id = @id", conn))
        {
            find.Parameters.AddWithValue("@id", customerId);
            storedHash = find.ExecuteScalar()?.ToString() ?? "";
        }

        if (!PasswordSecurity.VerifyPassword(currentPass ?? "", storedHash))
        {
            TempData["Msg"] = "Current password is incorrect.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Account));
        }

        if (PasswordSecurity.VerifyPassword(newPass, storedHash))
        {
            TempData["Msg"] = "New password must be different from the current password.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Account));
        }

        using (var update = new MySqlCommand("UPDATE users SET password_hash = @hash WHERE user_id = @id", conn))
        {
            update.Parameters.AddWithValue("@hash", PasswordSecurity.HashPassword(newPass));
            update.Parameters.AddWithValue("@id", customerId);
            update.ExecuteNonQuery();
        }

        TempData["Msg"] = "Password changed successfully.";
        TempData["Type"] = "success";
        return RedirectToAction(nameof(Account));
    }

    [HttpPost]
    public IActionResult RedeemReward()
    {
        if (!TryGetCustomerId(out int customerId)) return RedirectToAction("Login", "Account");
        using var conn = _db.GetConnection();
        conn.Open();
        RewardSnapshot rewards = RewardStore.ReadSnapshot(conn, customerId);
        if (!rewards.CanRedeem)
        {
            TempData["Msg"] = rewards.IsReady
                ? "You need 100 available points to redeem the PHP 50 discount."
                : "Rewards are not set up yet. Please contact the shop owner.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Rewards));
        }

        // This selects the reward for a future order; it does not spend points yet.
        HttpContext.Session.SetString(RewardSelectionSessionKey, customerId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        TempData["Msg"] = "PHP 50 discount selected. Create an order of at least PHP 50; 100 points will be deducted only when you confirm the booking.";
        TempData["Type"] = "info";
        return RedirectToAction(nameof(Book));
    }

    private bool TryGetCustomerId(out int customerId)
    {
        int? id = HttpContext.Session.GetInt32("UserId");
        customerId = id ?? 0;
        return id.HasValue && HttpContext.Session.GetString("UserRole") == "Customer";
    }

    private static CustomerBookingDraft? CreateBookingDraft(
        MySqlConnection conn,
        IEnumerable<string> selectedServices,
        IReadOnlyDictionary<string, decimal> itemQuantities,
        decimal weight,
        string paymentMethod,
        out string unavailableService,
        IReadOnlyDictionary<string, decimal>? priceOverrides = null)
    {
        unavailableService = "The selected service";
        var draft = new CustomerBookingDraft
        {
            BookingCode = CreateBookingCode(),
            Weight = weight,
            PaymentMethod = paymentMethod
        };

        decimal kgPerLoad = LoadPricing.ReadKgPerLoad(conn);
        foreach (string serviceName in selectedServices)
        {
            using var command = new MySqlCommand(
                "SELECT service_id, service_name, current_price, charging_basis, COALESCE(product_type, '') AS product_type FROM services WHERE service_name = @name AND is_active = TRUE",
                conn);
            command.Parameters.AddWithValue("@name", serviceName);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                unavailableService = serviceName;
                return null;
            }

            string chargingBasis = reader["charging_basis"]?.ToString() ?? "Flat";
            decimal unitPrice = Convert.ToDecimal(reader["current_price"]);
            if (serviceName == ServiceRules.SelfService)
            {
                chargingBasis = "Flat";
                unitPrice = 0m;
            }
            if (priceOverrides != null && priceOverrides.TryGetValue(serviceName, out decimal overridePrice))
                unitPrice = overridePrice;
            decimal quantity = chargingBasis switch
            {
                "PerItem" when itemQuantities.TryGetValue(serviceName, out decimal itemQuantity) => itemQuantity,
                "PerItem" => 1m,
                _ => LoadPricing.QuantityFor(chargingBasis, weight, 1m, kgPerLoad)
            };
            draft.Services.Add(new CustomerBookingDraftService
            {
                ServiceId = Convert.ToInt32(reader["service_id"]),
                ServiceName = reader["service_name"]?.ToString() ?? serviceName,
                ChargingBasis = chargingBasis,
                Quantity = quantity,
                UnitPrice = unitPrice,
                ProductType = reader["product_type"]?.ToString() ?? ""
            });
        }

        return draft;
    }

    private static BookingReviewVM ToReviewModel(CustomerBookingDraft draft) => new()
    {
        Weight = draft.Weight,
        UseReward = draft.UseReward,
        PaymentMethod = draft.PaymentMethod,
        SupplyInstructions = GetSupplyInstructions(draft),
        ScheduledAt = draft.ScheduledAt,
        ServiceMode = draft.Services.Any(service => service.ServiceName == ServiceRules.SelfService)
            ? ServiceRules.SelfService : ServiceRules.FullService,
        Services = draft.Services.Where(service => service.ServiceName != ServiceRules.SelfService).Select(service => new BookingReviewLineVM
        {
            ServiceName = service.ServiceName,
            ChargingBasis = service.ChargingBasis,
            Quantity = service.Quantity,
            UnitPrice = service.UnitPrice
        }).ToList()
    };

    private int CreateBookingFromDraft(int customerId, CustomerBookingDraft draft)
    {
        if (draft.CustomerId != customerId)
            throw new RewardValidationException("This booking draft belongs to a different customer. Please start a new booking.");
        if (!HasValidSupplyChoices(draft))
            throw new InvalidOperationException("Please review the detergent and fabric-softener choices first.");
        using var conn = _db.GetConnection();
        conn.Open();
        using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            // Re-check the schedule at confirmation: the draft may be old or the slot filled meanwhile.
            if (draft.ScheduledAt is not DateTime scheduledAt || !BookingSchedule.IsBookableTime(scheduledAt, DateTime.Now))
                throw new BookingSlotException("Your drop-off schedule has already passed. Please choose a new schedule.");
            if (BookingSchedule.CountBookingsInSlot(conn, transaction, scheduledAt) >= BookingSchedule.SlotCapacity)
                throw new BookingSlotException($"The {scheduledAt:MMM dd, h:mm tt} schedule was just filled. Please choose another time.");

            const string bookingInsert = """
                INSERT INTO bookings (booking_code, customer_id, current_status_id, is_rush, customer_notes, scheduled_pickup_at)
                SELECT @code, @customerId, booking_status_id, @isRush, @customerNotes, @scheduledAt
                FROM booking_statuses WHERE status_name = 'Pending';
                SELECT LAST_INSERT_ID();
                """;
            // A booking is a rush order when the customer selected the Rush Fee, same as walk-ins.
            bool isRush = draft.Services.Any(service => service.ServiceName == "Rush Fee");
            int bookingId;
            using (var command = new MySqlCommand(bookingInsert, conn, transaction))
            {
                command.Parameters.AddWithValue("@code", draft.BookingCode);
                command.Parameters.AddWithValue("@customerId", customerId);
                command.Parameters.AddWithValue("@isRush", isRush);
                command.Parameters.AddWithValue("@customerNotes", GetSupplyInstructions(draft));
                command.Parameters.AddWithValue("@scheduledAt", scheduledAt);
                bookingId = Convert.ToInt32(command.ExecuteScalar());
            }

            const string itemInsert = """
                INSERT INTO booking_items (booking_id, item_type_id, weight_kg)
                SELECT @bookingId, item_type_id, @weight
                FROM item_types WHERE item_name = 'Clothes' AND is_active = TRUE
                """;
            using (var command = new MySqlCommand(itemInsert, conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.Parameters.AddWithValue("@weight", draft.Weight);
                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("The Clothes item type is unavailable.");
            }

            const string serviceInsert = """
                INSERT INTO booking_services (booking_id, service_id, quantity, unit_price, charging_basis)
                VALUES (@bookingId, @serviceId, @quantity, @unitPrice, @basis)
                """;
            foreach (CustomerBookingDraftService service in draft.Services)
            {
                using var command = new MySqlCommand(serviceInsert, conn, transaction);
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.Parameters.AddWithValue("@serviceId", service.ServiceId);
                command.Parameters.AddWithValue("@quantity", service.Quantity);
                command.Parameters.AddWithValue("@unitPrice", service.UnitPrice);
                command.Parameters.AddWithValue("@basis", string.IsNullOrEmpty(service.ChargingBasis) ? DBNull.Value : service.ChargingBasis);
                command.ExecuteNonQuery();
            }

            decimal discount = draft.UseReward
                ? RewardStore.ApplyBookingDiscount(conn, transaction, bookingId, customerId) : 0m;
            decimal savedSubtotal;
            using (var totals = new MySqlCommand("SELECT COALESCE(SUM(quantity * unit_price), 0) FROM booking_services WHERE booking_id = @bookingId", conn, transaction))
            {
                totals.Parameters.AddWithValue("@bookingId", bookingId);
                savedSubtotal = Convert.ToDecimal(totals.ExecuteScalar());
            }
            // The customer declares the full estimated GCash amount. Staff still verifies
            // the actual payment; this is not an automatic GCash gateway confirmation.
            decimal declaredAmount = RewardStore.NetTotal(savedSubtotal, discount);
            if (draft.PaymentMethod.Equals("GCash", StringComparison.OrdinalIgnoreCase) && declaredAmount > 0m)
            {
                const string pendingPaymentInsert = """
                    INSERT INTO payments
                        (booking_id, payment_method_id, payment_status_id, amount, notes)
                    SELECT @bookingId, method.payment_method_id, status.payment_status_id,
                           @amount, 'Customer reported GCash payment; awaiting Staff verification.'
                    FROM payment_methods method
                    CROSS JOIN payment_statuses status
                    WHERE method.method_name = 'GCash'
                      AND method.is_active = TRUE
                      AND status.status_name = 'Pending'
                    """;
                using var paymentCommand = new MySqlCommand(pendingPaymentInsert, conn, transaction);
                paymentCommand.Parameters.AddWithValue("@bookingId", bookingId);
                paymentCommand.Parameters.AddWithValue("@amount", declaredAmount);
                if (paymentCommand.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("GCash payment verification is unavailable.");
            }

            const string historyInsert = """
                INSERT INTO booking_status_history (booking_id, booking_status_id, notes)
                SELECT @bookingId, booking_status_id, 'Customer booking created after final confirmation.'
                FROM booking_statuses WHERE status_name = 'Pending'
                """;
            using (var command = new MySqlCommand(historyInsert, conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            return bookingId;
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            transaction.Rollback();
            using var findExisting = new MySqlCommand(
                "SELECT booking_id FROM bookings WHERE booking_code = @code AND customer_id = @customerId",
                conn);
            findExisting.Parameters.AddWithValue("@code", draft.BookingCode);
            findExisting.Parameters.AddWithValue("@customerId", customerId);
            object? existingId = findExisting.ExecuteScalar();
            if (existingId != null) return Convert.ToInt32(existingId);
            throw;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void SaveBookingDraft(CustomerBookingDraft draft) =>
        HttpContext.Session.SetString(BookingDraftSessionKey, JsonSerializer.Serialize(draft));

    private IActionResult RequestSupplyChoices()
    {
        TempData["Msg"] = "Please review your order and choose how detergent and fabric softener will be provided.";
        TempData["Type"] = "info";
        return RedirectToAction(nameof(Book), new { edit = true });
    }

    private static bool HasValidSupplyChoices(CustomerBookingDraft draft) =>
        SupplyMatchesLine(draft, draft.DetergentSource, ProductCatalog.Detergent)
        && SupplyMatchesLine(draft, draft.SoftenerSource, ProductCatalog.FabricConditioner);

    private static bool SupplyMatchesLine(CustomerBookingDraft draft, string source, string productType)
    {
        if (!LaundrySupplyChoices.IsValid(source)) return false;
        List<CustomerBookingDraftService> lines = draft.Services
            .Where(service => service.ProductType == productType).ToList();
        return source == LaundrySupplyChoices.Shop
            ? lines.Count == 1 && lines[0].Quantity is >= 1m and <= 20m
              && lines[0].Quantity == decimal.Truncate(lines[0].Quantity)
            : lines.Count == 0;
    }

    private static string GetSupplyInstructions(CustomerBookingDraft draft)
    {
        var detergent = draft.Services.FirstOrDefault(service => service.ProductType == ProductCatalog.Detergent);
        var softener = draft.Services.FirstOrDefault(service => service.ProductType == ProductCatalog.FabricConditioner);
        return LaundrySupplyChoices.BuildInstructions(
            draft.DetergentSource, detergent?.Quantity ?? 0m,
            draft.SoftenerSource, softener?.Quantity ?? 0m,
            detergent?.ServiceName ?? "", softener?.ServiceName ?? "");
    }

    private CustomerBookingDraft? GetBookingDraft()
    {
        string? json = HttpContext.Session.GetString(BookingDraftSessionKey);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            CustomerBookingDraft? draft = JsonSerializer.Deserialize<CustomerBookingDraft>(json);
            if (draft == null || draft.Services.Count == 0 || string.IsNullOrWhiteSpace(draft.BookingCode)
                || draft.CustomerId != HttpContext.Session.GetInt32("UserId"))
            {
                ClearBookingDraft();
                return null;
            }

            return draft;
        }
        catch (JsonException)
        {
            ClearBookingDraft();
            return null;
        }
    }

    private void SetLastBooking(int bookingId, CustomerBookingDraft draft)
    {
        HttpContext.Session.SetInt32("LastBookingId", bookingId);
        HttpContext.Session.SetString("LastBookingCode", draft.BookingCode);
        HttpContext.Session.SetString("LastBookingPaymentMethod", draft.PaymentMethod);
    }

    private bool CanContinueRewardBooking(int customerId, CustomerBookingDraft draft)
    {
        if (!draft.UseReward) return true;
        using var conn = _db.GetConnection();
        conn.Open();
        RewardSnapshot rewards = RewardStore.ReadSnapshot(conn, customerId);
        if (rewards.CanRedeem && ToReviewModel(draft).Subtotal >= RewardStore.DiscountAmount) return true;
        TempData["Msg"] = "The selected reward is no longer available. No booking or points change was saved. Edit your order before proceeding with payment.";
        TempData["Type"] = "error";
        return false;
    }

    private IActionResult FinalizeBooking(int customerId, CustomerBookingDraft draft, bool paymentDeclared = false)
    {
        try
        {
            int bookingId = CreateBookingFromDraft(customerId, draft);
            SetLastBooking(bookingId, draft);
            ClearBookingDraft();
            if (paymentDeclared)
            {
                TempData["Msg"] = "Payment submission noted. Staff must verify the GCash transaction before recording it as paid.";
                TempData["Type"] = "info";
            }
            return RedirectToAction(nameof(BookingReceipt));
        }
        catch (BookingSlotException error)
        {
            TempData["Msg"] = error.Message + " No booking was saved.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(Book), new { edit = true });
        }
        catch (RewardValidationException error)
        {
            TempData["Msg"] = error.Message + " No new booking was saved. Edit your order to continue without the discount.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(ReviewBooking));
        }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            TempData["Msg"] = "Another request is updating your booking or points. No partial change was saved. Please review and try again.";
            TempData["Type"] = "error";
            return RedirectToAction(nameof(ReviewBooking));
        }
    }

    private void ClearBookingDraft()
    {
        HttpContext.Session.Remove(BookingDraftSessionKey);
        HttpContext.Session.Remove(RewardSelectionSessionKey);
    }

    private static string CreateBookingCode() => $"EZK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
