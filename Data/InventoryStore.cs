using EZKLaundrySystem.Models;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Data;

/// <summary>
/// Piece-based stock for the shop's products (detergent and fabric conditioner brands,
/// services with category Consumable). Booking service lines are the source of issued
/// quantities; customer notes and estimated weight are not.
/// </summary>
public static class InventoryStore
{
    private const int MaximumEntryQuantity = 1_000_000;
    // Every stock-tracked product: a per-piece entry in the Consumable category.
    private const string ProductFilter = "s.service_category = 'Consumable' AND s.charging_basis = 'PerItem'";

    // Run explicitly during setup, outside a business transaction: MySQL DDL commits.
    // Existing stock is never reset, and historical bookings are never backfilled.
    public static void EnsureSchema(MySqlConnection connection)
    {
        string[] statements =
        [
            """
            CREATE TABLE IF NOT EXISTS inventory_stock (
                service_id SMALLINT UNSIGNED NOT NULL,
                quantity INT UNSIGNED NOT NULL DEFAULT 0,
                updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (service_id),
                CONSTRAINT fk_inventory_stock_service FOREIGN KEY (service_id)
                    REFERENCES services (service_id) ON UPDATE CASCADE ON DELETE RESTRICT,
                CONSTRAINT ck_inventory_stock_quantity CHECK (quantity <= 2147483647)
            ) ENGINE = InnoDB
            """,
            """
            CREATE TABLE IF NOT EXISTS inventory_booking_issues (
                booking_id BIGINT UNSIGNED NOT NULL,
                issued_by_user_id BIGINT UNSIGNED NOT NULL,
                issued_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (booking_id),
                CONSTRAINT fk_inventory_issue_booking FOREIGN KEY (booking_id)
                    REFERENCES bookings (booking_id) ON UPDATE CASCADE ON DELETE RESTRICT,
                CONSTRAINT fk_inventory_issue_user FOREIGN KEY (issued_by_user_id)
                    REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT
            ) ENGINE = InnoDB
            """,
            """
            CREATE TABLE IF NOT EXISTS inventory_movements (
                movement_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                service_id SMALLINT UNSIGNED NOT NULL,
                quantity_change INT NOT NULL,
                balance_after INT UNSIGNED NOT NULL,
                movement_kind VARCHAR(12) NOT NULL,
                booking_id BIGINT UNSIGNED NULL,
                recorded_by_user_id BIGINT UNSIGNED NOT NULL,
                reason VARCHAR(255) NOT NULL,
                request_key VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                requested_quantity INT UNSIGNED NOT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (movement_id),
                CONSTRAINT uq_inventory_movement_request UNIQUE (request_key),
                KEY ix_inventory_movement_service (service_id, movement_id),
                KEY ix_inventory_movement_booking (booking_id, service_id),
                CONSTRAINT fk_inventory_movement_service FOREIGN KEY (service_id)
                    REFERENCES services (service_id) ON UPDATE CASCADE ON DELETE RESTRICT,
                CONSTRAINT fk_inventory_movement_booking FOREIGN KEY (booking_id)
                    REFERENCES bookings (booking_id) ON UPDATE CASCADE ON DELETE RESTRICT,
                CONSTRAINT fk_inventory_movement_user FOREIGN KEY (recorded_by_user_id)
                    REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT,
                CONSTRAINT ck_inventory_movement_kind CHECK (movement_kind IN ('Restock', 'Count', 'Issue', 'Return')),
                CONSTRAINT ck_inventory_movement_sign CHECK (
                    (movement_kind IN ('Restock', 'Return') AND quantity_change > 0)
                    OR (movement_kind = 'Issue' AND quantity_change < 0)
                    OR movement_kind = 'Count'),
                CONSTRAINT ck_inventory_movement_booking CHECK (
                    (movement_kind IN ('Issue', 'Return') AND booking_id IS NOT NULL)
                    OR (movement_kind IN ('Restock', 'Count') AND booking_id IS NULL)),
                CONSTRAINT ck_inventory_movement_balance CHECK (balance_after <= 2147483647)
            ) ENGINE = InnoDB
            """,
            """
            INSERT INTO inventory_stock (service_id, quantity)
            SELECT s.service_id, 0 FROM services s
            WHERE s.service_category = 'Consumable' AND s.charging_basis = 'PerItem'
            ON DUPLICATE KEY UPDATE service_id = VALUES(service_id)
            """
        ];

        foreach (string statement in statements)
        {
            using var command = new MySqlCommand(statement, connection);
            command.ExecuteNonQuery();
        }
    }

    public static bool IsReady(MySqlConnection connection)
    {
        using var tables = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE() AND table_name IN
                ('inventory_stock', 'inventory_booking_issues', 'inventory_movements')
            """, connection);
        if (Convert.ToInt32(tables.ExecuteScalar()) != 3)
            return false;

        using var items = new MySqlCommand($"""
            SELECT COUNT(*) FROM inventory_stock i
            JOIN services s ON s.service_id = i.service_id
            WHERE {ProductFilter}
            """, connection);
        return Convert.ToInt32(items.ExecuteScalar()) > 0;
    }

    /// <summary>Products are sold one stocked piece at a time; add a stock row for each new product.</summary>
    public static void EnsureStockRow(MySqlConnection connection, int serviceId)
    {
        using var command = new MySqlCommand(
            "INSERT IGNORE INTO inventory_stock (service_id, quantity) VALUES (@service, 0)", connection);
        command.Parameters.AddWithValue("@service", serviceId);
        command.ExecuteNonQuery();
    }

    public static InventorySnapshot ReadSnapshot(MySqlConnection connection)
    {
        var snapshot = new InventorySnapshot { IsReady = IsReady(connection) };
        if (!snapshot.IsReady)
            return snapshot;

        using (var command = new MySqlCommand($"""
            SELECT s.service_id, s.service_name, COALESCE(s.product_type, '') AS product_type, i.quantity, s.current_price
            FROM inventory_stock i JOIN services s ON s.service_id = i.service_id
            WHERE {ProductFilter} AND s.is_active = TRUE
            ORDER BY s.product_type, s.service_name
            """, connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                snapshot.Items.Add(new InventoryItem
                {
                    ServiceId = Convert.ToInt32(reader["service_id"]),
                    Name = reader.GetString("service_name"),
                    ProductType = reader.GetString("product_type"),
                    Quantity = Convert.ToInt32(reader["quantity"]),
                    UnitPrice = reader.GetDecimal("current_price")
                });
        }

        using (var command = new MySqlCommand("""
            SELECT s.service_name, m.quantity_change, m.balance_after, m.movement_kind,
                COALESCE(b.booking_code, '') AS booking_code, m.reason,
                CONCAT_WS(' ', u.first_name, u.last_name) AS recorded_by, m.created_at
            FROM inventory_movements m
            JOIN services s ON s.service_id = m.service_id
            JOIN users u ON u.user_id = m.recorded_by_user_id
            LEFT JOIN bookings b ON b.booking_id = m.booking_id
            ORDER BY m.movement_id DESC LIMIT 100
            """, connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                snapshot.Movements.Add(new InventoryMovement
                {
                    Name = reader.GetString("service_name"),
                    Change = reader.GetInt32("quantity_change"),
                    BalanceAfter = Convert.ToInt32(reader["balance_after"]),
                    Kind = reader.GetString("movement_kind"),
                    BookingReference = reader.GetString("booking_code"),
                    Reason = reader.GetString("reason"),
                    RecordedBy = reader.GetString("recorded_by"),
                    CreatedAt = reader.GetDateTime("created_at")
                });
        }
        return snapshot;
    }

    /// <summary>
    /// Reads every movement in an inclusive calendar-date range. This report does
    /// not change stock, initialize inventory, or use the page's latest-100 limit.
    /// </summary>
    public static List<InventoryMovement> ReadMovementsForReport(MySqlConnection connection,
        DateOnly fromDate, DateOnly toDate)
    {
        using var command = CreateReportMovementsCommand(connection, fromDate, toDate);
        using var reader = command.ExecuteReader();
        var movements = new List<InventoryMovement>();
        while (reader.Read())
            movements.Add(new InventoryMovement
            {
                Name = reader.GetString("service_name"),
                Change = reader.GetInt32("quantity_change"),
                BalanceAfter = Convert.ToInt32(reader["balance_after"]),
                Kind = reader.GetString("movement_kind"),
                BookingReference = reader.GetString("booking_code"),
                Reason = reader.GetString("reason"),
                RecordedBy = reader.GetString("recorded_by"),
                CreatedAt = reader.GetDateTime("created_at")
            });
        return movements;
    }

    private static MySqlCommand CreateReportMovementsCommand(MySqlConnection connection,
        DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate.Year < 1000 || fromDate > toDate)
            throw new ArgumentOutOfRangeException(nameof(fromDate), "Use a valid MySQL date range in chronological order.");

        // DATETIME cannot extend past 9999-12-31. Omitting its upper bound avoids
        // overflowing DateOnly while still including every time on that last day.
        bool hasExclusiveEnd = toDate < DateOnly.MaxValue;
        var command = new MySqlCommand("""
            SELECT s.service_name, m.quantity_change, m.balance_after, m.movement_kind,
                COALESCE(b.booking_code, '') AS booking_code, m.reason,
                CONCAT_WS(' ', u.first_name, u.last_name) AS recorded_by, m.created_at
            FROM inventory_movements m
            JOIN services s ON s.service_id = m.service_id
            JOIN users u ON u.user_id = m.recorded_by_user_id
            LEFT JOIN bookings b ON b.booking_id = m.booking_id
            WHERE m.created_at >= @fromDate
            """ + (hasExclusiveEnd ? " AND m.created_at < @toExclusive" : "") + """

            ORDER BY m.created_at DESC, m.movement_id DESC
            """, connection);
        command.Parameters.Add("@fromDate", MySqlDbType.DateTime).Value = fromDate.ToDateTime(TimeOnly.MinValue);
        if (hasExclusiveEnd)
            command.Parameters.Add("@toExclusive", MySqlDbType.DateTime).Value = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return command;
    }

    public static bool ReceiveStock(MySqlConnection connection, int serviceId, int quantity,
        int actor, string reason, string requestKey) =>
        AddStock(connection, serviceId, quantity, actor, reason, requestKey);

    /// <summary>
    /// Called after locking the booking row in the staff processing transaction.
    /// Own/no-product bookings need no inventory setup. Never infer usage from notes.
    /// </summary>
    public static bool RequiresBookingIssue(MySqlConnection connection, MySqlTransaction transaction, int bookingId)
    {
        using (var supplies = new MySqlCommand("""
            SELECT COUNT(*) FROM booking_services bs
            JOIN services s ON s.service_id = bs.service_id
            WHERE bs.booking_id = @booking AND s.service_category = 'Consumable'
            """, connection, transaction))
        {
            supplies.Parameters.AddWithValue("@booking", bookingId);
            if (Convert.ToInt32(supplies.ExecuteScalar()) == 0) return false;
        }
        if (!IsReady(connection))
            throw new InventoryValidationException("Inventory is not ready. Ask the owner to initialize inventory and record the available products before processing this order.");
        using var issued = new MySqlCommand(
            "SELECT booking_id FROM inventory_booking_issues WHERE booking_id = @booking", connection, transaction);
        issued.Parameters.AddWithValue("@booking", bookingId);
        return issued.ExecuteScalar() == null;
    }

    /// <summary>
    /// Issue all selected shop consumables once, in the caller's booking transaction.
    /// The caller MUST roll back its whole transaction if this method throws.
    /// False means already issued, or the booking contains no shop consumables.
    /// No automatic issuance of old bookings or automatic cancellation returns occurs.
    /// Callers use READ COMMITTED; all booking-service edits must first lock the
    /// booking row, as the existing actual-weight update already does.
    /// </summary>
    public static bool IssueBooking(MySqlConnection connection, MySqlTransaction transaction,
        int bookingId, int actor)
    {
        if (bookingId <= 0 || actor <= 0)
            throw new InventoryValidationException("A valid booking and staff member are required.");

        string status = LockBooking(connection, transaction, bookingId);
        // The booking row already serializes issuance for this booking.
        // Do not gap-lock a missing marker shared by unrelated new bookings.
        using (var existing = new MySqlCommand(
            "SELECT booking_id FROM inventory_booking_issues WHERE booking_id = @booking",
            connection, transaction))
        {
            existing.Parameters.AddWithValue("@booking", bookingId);
            if (existing.ExecuteScalar() != null)
                return false;
        }
        if (status is not ("Washing" or "Drying" or "Folding" or "Ready"))
            throw new InventoryValidationException("Shop supplies can only be issued after staff accepts an active booking.");

        var supplies = new List<(int ServiceId, string Name, int Quantity)>();
        using (var command = new MySqlCommand("""
            SELECT bs.service_id, s.service_name, s.charging_basis, bs.quantity
            FROM booking_services bs JOIN services s ON s.service_id = bs.service_id
            WHERE bs.booking_id = @booking AND s.service_category = 'Consumable'
            ORDER BY bs.service_id
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("@booking", bookingId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString("charging_basis") != "PerItem")
                    throw new InventoryValidationException("Products must be configured per piece before processing this order.");
                decimal quantity = reader.GetDecimal("quantity");
                if (quantity <= 0 || quantity > MaximumEntryQuantity || quantity != decimal.Truncate(quantity))
                    throw new InventoryValidationException("Shop supplies must use a positive, whole number of pieces.");
                supplies.Add((Convert.ToInt32(reader["service_id"]), reader.GetString("service_name"), (int)quantity));
            }
        }
        if (supplies.Count == 0)
            return false;

        // Lock every item in a stable order and validate all balances BEFORE writing.
        var balances = new Dictionary<int, int>();
        foreach (var supply in supplies)
        {
            int available = LockStock(connection, transaction, supply.ServiceId);
            if (available < supply.Quantity)
                throw new InventoryValidationException(
                    $"Not enough {supply.Name} stock: {available} piece(s) available, {supply.Quantity} needed. Restock or correct the selected shop supplies.");
            balances[supply.ServiceId] = available - supply.Quantity;
        }

        using (var issue = new MySqlCommand("""
            INSERT INTO inventory_booking_issues (booking_id, issued_by_user_id)
            VALUES (@booking, @actor)
            """, connection, transaction))
        {
            issue.Parameters.AddWithValue("@booking", bookingId);
            issue.Parameters.AddWithValue("@actor", actor);
            issue.ExecuteNonQuery();
        }
        foreach (var supply in supplies)
        {
            using var decrement = new MySqlCommand("""
                UPDATE inventory_stock SET quantity = quantity - @quantity
                WHERE service_id = @service AND quantity >= @quantity
                """, connection, transaction);
            decrement.Parameters.AddWithValue("@quantity", supply.Quantity);
            decrement.Parameters.AddWithValue("@service", supply.ServiceId);
            if (decrement.ExecuteNonQuery() != 1)
                throw new InventoryValidationException("Stock changed before the supplies were issued. Please try again.");

            InsertMovement(connection, transaction, supply.ServiceId, -supply.Quantity,
                balances[supply.ServiceId], "Issue", bookingId, actor,
                "Shop supplies issued for this booking.", $"issue:{bookingId}:{supply.ServiceId}", supply.Quantity);
        }
        return true;
    }

    private static bool AddStock(MySqlConnection connection, int serviceId, int requestedQuantity,
        int actor, string reason, string requestKey)
    {
        if (serviceId <= 0 || actor <= 0)
            throw new InventoryValidationException("Select a valid supply item and staff member.");
        if (requestedQuantity < 1 || requestedQuantity > MaximumEntryQuantity)
            throw new InventoryValidationException("The quantity must be between 1 and 1,000,000 whole pieces.");
        reason = (reason ?? "").Trim();
        if (reason.Length is < 3 or > 255)
            throw new InventoryValidationException("Enter a reason between 3 and 255 characters for this stock movement.");
        requestKey = (requestKey ?? "").Trim();
        if (requestKey.Length is < 16 or > 80 || requestKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new InventoryValidationException("This stock form has expired or has an invalid request key. Reload the inventory page.");
        string storedKey = "manual:" + requestKey;

        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            int previous = LockStock(connection, transaction, serviceId);
            if (WasAlreadyApplied(connection, transaction, storedKey, serviceId, requestedQuantity, actor, reason))
            {
                transaction.Commit();
                return false;
            }

            long next = (long)previous + requestedQuantity;
            if (next > int.MaxValue)
                throw new InventoryValidationException("The stock total is too large. Please check the quantity.");
            int change = (int)next - previous;
            using (var update = new MySqlCommand(
                "UPDATE inventory_stock SET quantity = @quantity WHERE service_id = @service", connection, transaction))
            {
                update.Parameters.AddWithValue("@quantity", next);
                update.Parameters.AddWithValue("@service", serviceId);
                update.ExecuteNonQuery();
            }
            InsertMovement(connection, transaction, serviceId, change, (int)next, "Restock",
                null, actor, reason, storedKey, requestedQuantity);
            transaction.Commit();
            return true;
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            // A globally repeated request may race while locking a different item.
            // Roll back before inspecting the winner; never leave the changed balance.
            transaction.Rollback();
            if (WasAlreadyApplied(connection, null, storedKey, serviceId, requestedQuantity, actor, reason))
                return false;
            throw new InventoryValidationException("This request was already submitted. Reload the inventory page before trying again.");
        }
    }

    private static string LockBooking(MySqlConnection connection, MySqlTransaction transaction, int bookingId)
    {
        using var command = new MySqlCommand(
            "SELECT current_status_id FROM bookings WHERE booking_id = @booking FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("@booking", bookingId);
        object? statusId = command.ExecuteScalar();
        if (statusId == null)
            throw new InventoryValidationException("The booking could not be found.");
        using var status = new MySqlCommand(
            "SELECT status_name FROM booking_statuses WHERE booking_status_id = @status", connection, transaction);
        status.Parameters.AddWithValue("@status", statusId);
        return status.ExecuteScalar() as string
            ?? throw new InventoryValidationException("The booking status could not be found.");
    }

    private static int LockStock(MySqlConnection connection, MySqlTransaction transaction, int serviceId)
    {
        // Catalogue identities/bases are not edited by stock operations. Avoid
        // exclusive locks on service rows shared by concurrent booking inserts.
        using var catalogue = new MySqlCommand($"""
            SELECT s.service_id FROM services s WHERE s.service_id = @service AND {ProductFilter}
            """, connection, transaction);
        catalogue.Parameters.AddWithValue("@service", serviceId);
        if (catalogue.ExecuteScalar() == null)
            throw new InventoryValidationException("This supply is not configured for piece-based inventory.");

        using var command = new MySqlCommand(
            "SELECT quantity FROM inventory_stock WHERE service_id = @service FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("@service", serviceId);
        object? value = command.ExecuteScalar();
        if (value == null)
            throw new InventoryValidationException("This supply is not configured for piece-based inventory.");
        return Convert.ToInt32(value);
    }

    private static bool WasAlreadyApplied(MySqlConnection connection, MySqlTransaction? transaction,
        string requestKey, int serviceId, int requestedQuantity, int actor, string reason)
    {
        using var command = new MySqlCommand("""
            SELECT service_id, requested_quantity, recorded_by_user_id, reason, movement_kind, booking_id
            FROM inventory_movements WHERE request_key = @key
            """ + (transaction == null ? "" : " FOR UPDATE"), connection, transaction);
        command.Parameters.AddWithValue("@key", requestKey);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return false;
        int? recordedBooking = reader.IsDBNull(reader.GetOrdinal("booking_id"))
            ? null : Convert.ToInt32(reader["booking_id"]);
        if (Convert.ToInt32(reader["service_id"]) != serviceId
            || Convert.ToInt32(reader["requested_quantity"]) != requestedQuantity
            || Convert.ToInt32(reader["recorded_by_user_id"]) != actor
            || reader.GetString("reason") != reason
            || reader.GetString("movement_kind") != "Restock" || recordedBooking != null)
            throw new InventoryValidationException("This request key was already used for different stock details. Reload the inventory page.");
        return true;
    }

    private static void InsertMovement(MySqlConnection connection, MySqlTransaction transaction,
        int serviceId, int change, int balance, string kind, int? bookingId, int actor,
        string reason, string requestKey, int requestedQuantity)
    {
        using var command = new MySqlCommand("""
            INSERT INTO inventory_movements (service_id, quantity_change, balance_after, movement_kind,
                booking_id, recorded_by_user_id, reason, request_key, requested_quantity)
            VALUES (@service, @change, @balance, @kind, @booking, @actor, @reason, @key, @requested)
            """, connection, transaction);
        command.Parameters.AddWithValue("@service", serviceId);
        command.Parameters.AddWithValue("@change", change);
        command.Parameters.AddWithValue("@balance", balance);
        command.Parameters.AddWithValue("@kind", kind);
        command.Parameters.AddWithValue("@booking", bookingId.HasValue ? bookingId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@actor", actor);
        command.Parameters.AddWithValue("@reason", reason);
        command.Parameters.AddWithValue("@key", requestKey);
        command.Parameters.AddWithValue("@requested", requestedQuantity);
        command.ExecuteNonQuery();
    }
}
