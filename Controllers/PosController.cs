using EZKLaundrySystem.Data;
using EZKLaundrySystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Controllers;

/// <summary>
/// Walk-in point of sale (panel suggestion): staff search a registered or returning
/// customer, tap per-load services and shop products, and record a Cash or GCash
/// payment in one step. The order then joins the active queue like any walk-in.
/// </summary>
[AutoValidateAntiforgeryToken]
public class PosController : Controller
{
    private const string RequestTokenKey = "PosRequestToken";
    private static readonly string[] LaundryServices = ["Washing", "Drying", "Folding"];
    private readonly DbConnection _db = new();

    public IActionResult Index()
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");

        string token = HttpContext.Session.GetString(RequestTokenKey) ?? Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString(RequestTokenKey, token);

        using var conn = _db.GetConnection();
        conn.Open();
        var model = new PosPageVM
        {
            KgPerLoad = LoadPricing.ReadKgPerLoad(conn),
            InventoryReady = InventoryStore.IsReady(conn),
            RequestToken = token
        };

        string stockColumn = model.InventoryReady ? "stock.quantity" : "NULL";
        string stockJoin = model.InventoryReady ? "LEFT JOIN inventory_stock stock ON stock.service_id = s.service_id" : "";
        using var command = new MySqlCommand($"""
            SELECT s.service_id, s.service_name, s.service_category, COALESCE(s.product_type, '') AS product_type,
                   s.current_price, s.package_extra, {stockColumn} AS stock
            FROM services s {stockJoin}
            WHERE s.is_active = TRUE
            ORDER BY s.product_type, s.service_name
            """, conn);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var item = new PosCatalogItem
            {
                ServiceId = Convert.ToInt32(reader["service_id"]),
                Name = reader["service_name"]?.ToString() ?? "",
                ProductType = reader["product_type"]?.ToString() ?? "",
                Price = Convert.ToDecimal(reader["current_price"]),
                PackageExtra = Convert.ToDecimal(reader["package_extra"]),
                Stock = reader["stock"] == DBNull.Value ? null : Convert.ToInt32(reader["stock"])
            };
            string category = reader["service_category"]?.ToString() ?? "";
            if (LaundryServices.Contains(item.Name))
                model.Services.Add(item);
            else if (category == "Consumable")
                model.Products.Add(item);
            else if (item.Name == "Rush Fee")
                model.RushFee = item.Price;
            else if (item.Name == ProductCatalog.PackageFee)
                model.PackageFee = item.Price;
        }
        model.Services = [.. model.Services.OrderBy(service => Array.IndexOf(LaundryServices, service.Name))];
        return View(model);
    }

    /// <summary>Finds returning customers (suki) by name or contact number.</summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult SearchCustomers(string q)
    {
        if (!IsStaff()) return Unauthorized();
        string term = (q ?? "").Trim();
        if (term.Length is < 2 or > 60) return Json(Array.Empty<object>());
        string digits = new(term.Where(char.IsDigit).ToArray());

        using var conn = _db.GetConnection();
        conn.Open();
        using var command = new MySqlCommand("""
            SELECT u.user_id, CONCAT(u.first_name, ' ', u.last_name) AS full_name, COALESCE(u.phone, '') AS phone,
                   (SELECT COUNT(*) FROM bookings b
                    JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
                    WHERE b.customer_id = u.user_id AND bs.status_name = 'Completed') AS completed_orders,
                   (SELECT MAX(b.booked_at) FROM bookings b WHERE b.customer_id = u.user_id) AS last_visit
            FROM users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.user_status_id = u.user_status_id
            WHERE r.role_name = 'Customer' AND us.status_name = 'Active'
              AND (CONCAT(u.first_name, ' ', u.last_name) LIKE @name
                   OR (@digits <> '' AND REPLACE(REPLACE(COALESCE(u.phone, ''), ' ', ''), '-', '') LIKE @digits))
            ORDER BY last_visit IS NULL, last_visit DESC, full_name
            LIMIT 8
            """, conn);
        command.Parameters.AddWithValue("@name", "%" + EscapeLike(term) + "%");
        command.Parameters.AddWithValue("@digits", digits.Length >= 3 ? "%" + digits + "%" : "");
        using var reader = command.ExecuteReader();
        var results = new List<object>();
        while (reader.Read())
        {
            int completed = Convert.ToInt32(reader["completed_orders"]);
            results.Add(new
            {
                id = Convert.ToInt32(reader["user_id"]),
                name = reader["full_name"]?.ToString() ?? "",
                phone = reader["phone"]?.ToString() ?? "",
                completedOrders = completed,
                isRegular = CustomerCategory.Classify(completed) == CustomerCategory.Regular,
                lastVisit = reader["last_visit"] == DBNull.Value ? "" : Convert.ToDateTime(reader["last_visit"]).ToString("MMM d")
            });
        }
        return Json(results);
    }

    [HttpPost]
    public IActionResult Checkout(PosOrderForm form)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        form.Products ??= [];
        if (!Guid.TryParseExact(form.RequestToken, "N", out Guid requestId) ||
            form.RequestToken != HttpContext.Session.GetString(RequestTokenKey))
            return Error("This order form expired or was already saved. Check the Active Queue before trying again.");
        if (form.Validate() is string validationError)
            return Error(validationError);

        using var conn = _db.GetConnection();
        conn.Open();
        bool usesPackageSupplies = form.IsFullService && form.ShopSupplies;
        if ((form.Products.Count > 0 || usesPackageSupplies) && !InventoryStore.IsReady(conn))
            return Error("Inventory is not set up yet. Ask the owner to finish the inventory setup before using shop products.");

        int staffId = HttpContext.Session.GetInt32("UserId")!.Value;
        // A deterministic code stops a double-click from saving or deducting stock twice.
        string bookingCode = $"EZK-P-{requestId:N}"[..30].ToUpperInvariant();
        using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var products = ReadProducts(conn, transaction, form.Products);
            decimal packageExtra = 0m;
            if (usesPackageSupplies)
            {
                // One detergent and one fabric conditioner per load, included in the package price.
                foreach (var (productId, type) in new[] { (form.PackageDetergentId, ProductCatalog.Detergent), (form.PackageFabconId, ProductCatalog.FabricConditioner) })
                {
                    var product = ProductCatalog.FindActive(conn, transaction, productId, type)
                        ?? throw new InventoryValidationException($"The selected {type.ToLowerInvariant()} is no longer available. Please choose another brand.");
                    products.Add((product.Name, type, form.PackageLoads));
                    packageExtra += product.PackageExtra;
                }
            }
            int customerId = form.IsNewCustomer
                ? FindOrCreateCustomer(conn, transaction, form.NewFirstName, form.NewLastName, form.NewPhone)
                : RequireCustomer(conn, transaction, form.CustomerId!.Value);

            var detergents = products.Where(p => p.Type != ProductCatalog.FabricConditioner).ToList();
            var softeners = products.Where(p => p.Type == ProductCatalog.FabricConditioner).ToList();
            // The PHP 155 Full-Service package means the customer brings both supplies.
            bool ownPackageSupplies = form.IsFullService && !form.ShopSupplies;
            string detergentSource = detergents.Count > 0 ? LaundrySupplyChoices.Shop
                : form.OwnDetergent || ownPackageSupplies ? LaundrySupplyChoices.Customer : LaundrySupplyChoices.None;
            string softenerSource = softeners.Count > 0 ? LaundrySupplyChoices.Shop
                : form.OwnSoftener || ownPackageSupplies ? LaundrySupplyChoices.Customer : LaundrySupplyChoices.None;

            int bookingId;
            using (var command = new MySqlCommand("""
                INSERT INTO bookings
                    (booking_code, customer_id, current_status_id, created_by_user_id, is_rush, customer_notes)
                SELECT @code, @customerId, bs.booking_status_id, @staffId, @isRush, @notes
                FROM booking_statuses bs WHERE bs.status_name = 'Washing';
                SELECT LAST_INSERT_ID();
                """, conn, transaction))
            {
                command.Parameters.AddWithValue("@code", bookingCode);
                command.Parameters.AddWithValue("@customerId", customerId);
                command.Parameters.AddWithValue("@staffId", staffId);
                command.Parameters.AddWithValue("@isRush", form.IsRush);
                command.Parameters.AddWithValue("@notes", LaundrySupplyChoices.BuildInstructions(
                    detergentSource, detergents.Sum(p => p.Quantity), softenerSource, softeners.Sum(p => p.Quantity),
                    string.Join(", ", detergents.Select(p => p.Name)), string.Join(", ", softeners.Select(p => p.Name))));
                bookingId = Convert.ToInt32(command.ExecuteScalar());
            }

            using (var command = new MySqlCommand("""
                INSERT INTO booking_items (booking_id, item_type_id, weight_kg)
                SELECT @bookingId, item_type_id, @weight FROM item_types WHERE item_name = 'Clothes'
                """, conn, transaction))
            {
                command.Parameters.AddWithValue("@bookingId", bookingId);
                command.Parameters.AddWithValue("@weight", form.Weight);
                command.ExecuteNonQuery();
            }

            decimal total = 0m;
            // Full-Service is the wash-dry-fold package, so every step uses the package loads.
            var steps = form.IsFullService
                ? new[] { ("Washing", form.PackageLoads), ("Drying", form.PackageLoads), ("Folding", form.PackageLoads) }
                : new[] { ("Washing", form.WashLoads), ("Drying", form.DryLoads) };
            foreach (var (service, loads) in steps)
                if (loads > 0)
                    total += StaffController.AddBookingService(conn, transaction, bookingId, service, form.Weight, quantityOverride: loads);
            if (form.ServiceMode == ServiceRules.SelfService)
                StaffController.AddBookingService(conn, transaction, bookingId, ServiceRules.SelfService, form.Weight);
            foreach (var product in products)
                total += StaffController.AddBookingService(conn, transaction, bookingId, product.Name, form.Weight, product.Quantity,
                    priceOverride: usesPackageSupplies ? 0m : null);
            if (usesPackageSupplies)
            {
                total += StaffController.AddBookingService(conn, transaction, bookingId, ProductCatalog.PackageFee, form.Weight,
                    quantityOverride: form.PackageLoads);
                if (packageExtra > 0m)
                    total += StaffController.AddBookingService(conn, transaction, bookingId, ProductCatalog.PremiumFee, form.Weight,
                        quantityOverride: form.PackageLoads, priceOverride: packageExtra);
            }
            if (form.IsRush)
                total += StaffController.AddBookingService(conn, transaction, bookingId, "Rush Fee", form.Weight);
            if (products.Count > 0)
                InventoryStore.IssueBooking(conn, transaction, bookingId, staffId);

            total = decimal.Round(total, 2, MidpointRounding.AwayFromZero);
            decimal change = 0m;
            if (form.PaymentMethod == "Cash")
            {
                if (form.CashReceived + 0.005m < total)
                    throw new InventoryValidationException($"The cash received (PHP {form.CashReceived:N2}) is less than the total of PHP {total:N2}.");
                change = form.CashReceived - total;
            }
            if (total > 0m)
                InsertPayment(conn, transaction, bookingId, staffId, form, total, change);

            AddStatusHistory(conn, transaction, bookingId, staffId, "Walk-in order created and paid through the POS.");
            transaction.Commit();
            HttpContext.Session.Remove(RequestTokenKey);

            TempData["PosMessage"] = form.PaymentMethod == "Cash"
                ? $"Order saved and paid. Total PHP {total:N2}, cash PHP {form.CashReceived:N2}, change PHP {change:N2}."
                : $"Order saved and paid through GCash. Total PHP {total:N2}.";
            return RedirectToAction(nameof(Receipt), new { id = bookingId });
        }
        catch (InventoryValidationException error)
        {
            transaction.Rollback();
            return Error(error.Message);
        }
        catch (MySqlException error) when (error.Number is 1205 or 1213)
        {
            transaction.Rollback();
            return Error("Another request is updating the same order or stock. Nothing was saved. Please try again.");
        }
        catch (MySqlException error) when (error.Number == 1062)
        {
            transaction.Rollback();
            using var existing = new MySqlCommand("SELECT booking_id FROM bookings WHERE booking_code = @code", conn);
            existing.Parameters.AddWithValue("@code", bookingCode);
            if (existing.ExecuteScalar() is object id)
            {
                HttpContext.Session.Remove(RequestTokenKey);
                return RedirectToAction(nameof(Receipt), new { id = Convert.ToInt32(id) });
            }
            return Error("The GCash reference number was already used for another payment. Please check it.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public IActionResult Receipt(int id)
    {
        if (!IsStaff()) return RedirectToAction("StaffLogin", "Account");
        using var conn = _db.GetConnection();
        conn.Open();
        var model = new PosReceiptVM();
        using (var command = new MySqlCommand("""
            SELECT b.booking_id, b.booking_code, b.booked_at, b.is_rush, COALESCE(b.customer_notes, '') AS notes,
                   CONCAT(c.first_name, ' ', c.last_name) AS customer_name, COALESCE(c.phone, '') AS phone,
                   COALESCE(CONCAT(s.first_name, ' ', s.last_name), '') AS staff_name,
                   COALESCE((SELECT SUM(weight_kg) FROM booking_items WHERE booking_id = b.booking_id), 0) AS weight_kg
            FROM bookings b
            JOIN users c ON c.user_id = b.customer_id
            LEFT JOIN users s ON s.user_id = b.created_by_user_id
            WHERE b.booking_id = @id
            """, conn))
        {
            command.Parameters.AddWithValue("@id", id);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return RedirectToAction(nameof(Index));
            model.BookingId = id;
            model.BookingCode = reader["booking_code"]?.ToString() ?? "";
            model.BookedAt = Convert.ToDateTime(reader["booked_at"]);
            model.IsRush = Convert.ToBoolean(reader["is_rush"]);
            model.Notes = reader["notes"]?.ToString() ?? "";
            model.CustomerName = reader["customer_name"]?.ToString() ?? "";
            model.CustomerPhone = reader["phone"]?.ToString() ?? "";
            model.StaffName = reader["staff_name"]?.ToString() ?? "";
            model.Weight = Convert.ToDecimal(reader["weight_kg"]);
        }

        using (var command = new MySqlCommand("""
            SELECT s.service_name, COALESCE(bs.charging_basis, s.charging_basis) AS charging_basis, bs.quantity, bs.unit_price
            FROM booking_services bs JOIN services s ON s.service_id = bs.service_id
            WHERE bs.booking_id = @id ORDER BY bs.booking_service_id
            """, conn))
        {
            command.Parameters.AddWithValue("@id", id);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string name = reader["service_name"]?.ToString() ?? "";
                if (name == ServiceRules.SelfService)
                {
                    model.ServiceMode = ServiceRules.SelfService;
                    continue;
                }
                model.Lines.Add(new PosReceiptLine
                {
                    Name = name,
                    ChargingBasis = reader["charging_basis"]?.ToString() ?? "",
                    Quantity = Convert.ToDecimal(reader["quantity"]),
                    UnitPrice = Convert.ToDecimal(reader["unit_price"])
                });
            }
        }

        using (var command = new MySqlCommand("""
            SELECT pm.method_name, p.amount, COALESCE(p.transaction_reference, '') AS reference,
                   COALESCE(p.notes, '') AS notes, p.paid_at
            FROM payments p
            JOIN payment_methods pm ON pm.payment_method_id = p.payment_method_id
            JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
            WHERE p.booking_id = @id AND ps.status_name = 'Paid'
            ORDER BY p.paid_at, p.payment_id
            """, conn))
        {
            command.Parameters.AddWithValue("@id", id);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                model.Payments.Add(new PosReceiptPayment
                {
                    Method = reader["method_name"]?.ToString() ?? "",
                    Amount = Convert.ToDecimal(reader["amount"]),
                    Reference = reader["reference"]?.ToString() ?? "",
                    Notes = reader["notes"]?.ToString() ?? "",
                    PaidAt = reader["paid_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["paid_at"])
                });
        }
        return View(model);
    }

    private IActionResult Error(string message)
    {
        TempData["PosError"] = message;
        return RedirectToAction(nameof(Index));
    }

    private bool IsStaff() => HttpContext.Session.GetString("UserRole") == "Staff"
                              && HttpContext.Session.GetInt32("UserId").HasValue;

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static List<(string Name, string Type, int Quantity)> ReadProducts(MySqlConnection conn,
        MySqlTransaction transaction, List<PosProductLine> lines)
    {
        var products = new List<(string Name, string Type, int Quantity)>();
        foreach (var line in lines)
        {
            using var command = new MySqlCommand("""
                SELECT service_name, COALESCE(product_type, '') AS product_type FROM services
                WHERE service_id = @id AND service_category = 'Consumable' AND is_active = TRUE
                """, conn, transaction);
            command.Parameters.AddWithValue("@id", line.ServiceId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                throw new InventoryValidationException("A selected product is no longer available. Please review the order.");
            products.Add((reader.GetString("service_name"), reader.GetString("product_type"), line.Quantity));
        }
        return products;
    }

    private static int RequireCustomer(MySqlConnection conn, MySqlTransaction transaction, int customerId)
    {
        using var command = new MySqlCommand("""
            SELECT u.user_id FROM users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.user_status_id = u.user_status_id
            WHERE u.user_id = @id AND r.role_name = 'Customer' AND us.status_name = 'Active'
            """, conn, transaction);
        command.Parameters.AddWithValue("@id", customerId);
        return command.ExecuteScalar() is null
            ? throw new InventoryValidationException("The selected customer was not found. Please search again.")
            : customerId;
    }

    /// <summary>A contact number that is already registered reuses that customer.</summary>
    private static int FindOrCreateCustomer(MySqlConnection conn, MySqlTransaction transaction,
        string firstName, string lastName, string phone)
    {
        using (var find = new MySqlCommand("SELECT user_id FROM users WHERE phone = @phone LIMIT 1", conn, transaction))
        {
            find.Parameters.AddWithValue("@phone", phone.Trim());
            if (find.ExecuteScalar() is object existing) return Convert.ToInt32(existing);
        }

        using var command = new MySqlCommand("""
            INSERT INTO users (role_id, user_status_id, first_name, last_name, phone, is_walk_in)
            SELECT r.role_id, us.user_status_id, @firstName, @lastName, @phone, TRUE
            FROM roles r CROSS JOIN user_statuses us
            WHERE r.role_name = 'Customer' AND us.status_name = 'Active';
            SELECT LAST_INSERT_ID();
            """, conn, transaction);
        command.Parameters.AddWithValue("@firstName", firstName.Trim());
        command.Parameters.AddWithValue("@lastName", lastName.Trim());
        command.Parameters.AddWithValue("@phone", phone.Trim());
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void InsertPayment(MySqlConnection conn, MySqlTransaction transaction, int bookingId,
        int staffId, PosOrderForm form, decimal total, decimal change)
    {
        string reference = form.PaymentMethod == "GCash" ? form.GCashReference.Trim() : "";
        string notes = form.PaymentMethod == "Cash"
            ? $"POS cash payment. Received PHP {form.CashReceived:N2}, change PHP {change:N2}."
            : "POS GCash payment of the exact amount through the shop QR code.";
        using var command = new MySqlCommand("""
            INSERT INTO payments
                (booking_id, payment_method_id, payment_status_id, recorded_by_user_id, amount, transaction_reference, paid_at, notes)
            SELECT @bookingId, method.payment_method_id, status.payment_status_id, @staffId, @amount, @reference, NOW(), @notes
            FROM payment_methods method CROSS JOIN payment_statuses status
            WHERE method.method_name = @method AND method.is_active = TRUE AND status.status_name = 'Paid'
            """, conn, transaction);
        command.Parameters.AddWithValue("@bookingId", bookingId);
        command.Parameters.AddWithValue("@staffId", staffId);
        command.Parameters.AddWithValue("@amount", total);
        command.Parameters.AddWithValue("@reference", reference.Length > 0 ? reference : DBNull.Value);
        command.Parameters.AddWithValue("@notes", notes);
        command.Parameters.AddWithValue("@method", form.PaymentMethod);
        if (command.ExecuteNonQuery() != 1)
            throw new InventoryValidationException("The selected payment method is unavailable.");
    }

    private static void AddStatusHistory(MySqlConnection conn, MySqlTransaction transaction,
        int bookingId, int staffId, string notes)
    {
        using var command = new MySqlCommand("""
            INSERT INTO booking_status_history (booking_id, booking_status_id, changed_by_user_id, notes)
            SELECT @bookingId, booking_status_id, @staffId, @notes FROM booking_statuses WHERE status_name = 'Washing'
            """, conn, transaction);
        command.Parameters.AddWithValue("@bookingId", bookingId);
        command.Parameters.AddWithValue("@staffId", staffId);
        command.Parameters.AddWithValue("@notes", notes);
        command.ExecuteNonQuery();
    }
}
