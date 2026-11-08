using EZKLaundrySystem.Models;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Data;

/// <summary>
/// Basic booking-linked rewards. The existing reward_transactions ledger is the
/// balance source; no starting points or historical booking awards are inferred.
/// Mutations join the caller's READ COMMITTED transaction. Callers must roll back
/// the entire transaction on failure. Lock order: booking, reward account, marker.
/// </summary>
public static class RewardStore
{
    public const int RedemptionCost = 100;
    public const decimal DiscountAmount = 50m;
    public const decimal PesosPerPoint = 10m;

    // Run explicitly during setup, never inside a business transaction: DDL commits.
    public static void EnsureSchema(MySqlConnection connection)
    {
        string[] statements =
        [
            """
            CREATE TABLE IF NOT EXISTS reward_accounts (
                user_id BIGINT UNSIGNED NOT NULL,
                PRIMARY KEY (user_id),
                CONSTRAINT fk_reward_accounts_user FOREIGN KEY (user_id)
                    REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT
            ) ENGINE = InnoDB
            """,
            """
            CREATE TABLE IF NOT EXISTS booking_rewards (
                booking_id BIGINT UNSIGNED NOT NULL,
                redeemed_points INT UNSIGNED NOT NULL DEFAULT 0,
                discount_amount DECIMAL(10, 2) NOT NULL DEFAULT 0.00,
                redeemed_at DATETIME NULL,
                points_awarded INT UNSIGNED NOT NULL DEFAULT 0,
                earned_at DATETIME NULL,
                paid_amount_at_earning DECIMAL(14, 2) NULL,
                returned_at DATETIME NULL,
                PRIMARY KEY (booking_id),
                CONSTRAINT fk_booking_rewards_booking FOREIGN KEY (booking_id)
                    REFERENCES bookings (booking_id) ON UPDATE CASCADE ON DELETE RESTRICT,
                CONSTRAINT ck_booking_rewards_discount CHECK (
                    (redeemed_points = 0 AND discount_amount = 0 AND redeemed_at IS NULL)
                    OR (redeemed_points = 100 AND discount_amount = 50 AND redeemed_at IS NOT NULL)),
                CONSTRAINT ck_booking_rewards_award CHECK (
                    points_awarded <= 2147483647 AND
                    ((earned_at IS NULL AND points_awarded = 0 AND paid_amount_at_earning IS NULL)
                    OR (earned_at IS NOT NULL AND paid_amount_at_earning IS NOT NULL
                        AND paid_amount_at_earning >= 0))),
                CONSTRAINT ck_booking_rewards_return CHECK (
                    returned_at IS NULL OR redeemed_points = 100)
            ) ENGINE = InnoDB
            """,
            """
            CREATE OR REPLACE VIEW v_booking_totals AS
            SELECT b.booking_id, b.booking_code,
                ROUND(GREATEST(0.00, COALESCE(SUM(bs.quantity * bs.unit_price), 0.00)
                    - COALESCE(br.discount_amount, 0.00)), 2) AS order_total
            FROM bookings b
            LEFT JOIN booking_services bs ON bs.booking_id = b.booking_id
            LEFT JOIN booking_rewards br ON br.booking_id = b.booking_id AND br.returned_at IS NULL
            GROUP BY b.booking_id, b.booking_code, br.discount_amount
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
        using var command = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE() AND table_name IN
                ('reward_accounts', 'booking_rewards', 'reward_transactions')
            """, connection);
        return Convert.ToInt32(command.ExecuteScalar()) == 3;
    }

    public static RewardSnapshot ReadSnapshot(MySqlConnection connection, int customerId)
    {
        var snapshot = new RewardSnapshot { IsReady = IsReady(connection) };
        if (!snapshot.IsReady || customerId <= 0) return snapshot;
        using (var command = new MySqlCommand("""
            SELECT COALESCE(SUM(points), 0) AS balance,
                COALESCE(SUM(CASE WHEN transaction_type = 'Earn' THEN points ELSE 0 END), 0) AS earned
            FROM reward_transactions WHERE user_id = @customer
            """, connection))
        {
            command.Parameters.AddWithValue("@customer", customerId);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                snapshot.Balance = Convert.ToInt64(reader["balance"]);
                snapshot.EarnedPoints = Convert.ToInt64(reader["earned"]);
            }
        }
        using (var command = new MySqlCommand("""
            SELECT r.reward_transaction_id, r.points, r.transaction_type,
                COALESCE(r.description, '') AS description,
                COALESCE(b.booking_code, '') AS booking_code, r.created_at
            FROM reward_transactions r LEFT JOIN bookings b ON b.booking_id = r.booking_id
            WHERE r.user_id = @customer ORDER BY r.reward_transaction_id DESC LIMIT 30
            """, connection))
        {
            command.Parameters.AddWithValue("@customer", customerId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                snapshot.History.Add(new RewardHistoryItem
                {
                    Id = Convert.ToInt64(reader["reward_transaction_id"]),
                    Points = Convert.ToInt32(reader["points"]),
                    Type = reader.GetString("transaction_type"),
                    Description = reader.GetString("description"),
                    BookingReference = reader.GetString("booking_code"),
                    CreatedAt = reader.GetDateTime("created_at")
                });
        }
        return snapshot;
    }

    public static decimal GetBookingDiscount(MySqlConnection connection, MySqlTransaction? transaction,
        int bookingId)
    {
        if (!IsReady(connection)) return 0m;
        using var command = new MySqlCommand(
            "SELECT discount_amount FROM booking_rewards WHERE booking_id = @booking AND returned_at IS NULL", connection, transaction);
        command.Parameters.AddWithValue("@booking", bookingId);
        object? amount = command.ExecuteScalar();
        return amount == null ? 0m : Convert.ToDecimal(amount);
    }

    /// <summary>
    /// Apply only while saving a new pending booking, after its service lines and
    /// before its payment declaration. Selection in a form never spends points.
    /// Returning an existing discount does not spend points again.
    /// </summary>
    public static decimal ApplyBookingDiscount(MySqlConnection connection, MySqlTransaction transaction,
        int bookingId, int customerId)
    {
        if (!IsReady(connection))
            throw new RewardValidationException("Rewards are not set up yet. No points were spent; remove the reward selection or ask the owner to initialize rewards.");
        RequireReadCommitted(transaction);
        var booking = LockBooking(connection, transaction, bookingId);
        if (booking.CustomerId != customerId)
            throw new RewardValidationException("This reward does not belong to the booking customer.");
        if (booking.Status != "Pending")
            throw new RewardValidationException("A reward discount can only be applied when creating a new pending booking.");

        LockAccount(connection, transaction, customerId);
        var state = ReadState(connection, transaction, bookingId);
        if (state.Returned)
            throw new RewardValidationException("The reward for this booking was already returned and cannot be applied again.");
        if (state.RedeemedPoints > 0) return state.Discount;
        decimal gross = ReadGrossTotal(connection, transaction, bookingId);
        if (gross < DiscountAmount)
            throw new RewardValidationException("The booking subtotal must be at least PHP 50 to use 100 reward points.");
        if (ReadPaidAmount(connection, transaction, bookingId) > 0m)
            throw new RewardValidationException("A reward cannot be added after a payment has been recorded.");
        long balance = ReadBalance(connection, transaction, customerId);
        if (balance < RedemptionCost)
            throw new RewardValidationException("You need 100 available points for this discount. No booking or points change was saved.");

        EnsureBookingState(connection, transaction, bookingId);
        using (var command = new MySqlCommand("""
            UPDATE booking_rewards SET redeemed_points = @points,
                discount_amount = @discount, redeemed_at = NOW()
            WHERE booking_id = @booking AND redeemed_points = 0 AND returned_at IS NULL
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("@points", RedemptionCost);
            command.Parameters.AddWithValue("@discount", DiscountAmount);
            command.Parameters.AddWithValue("@booking", bookingId);
            if (command.ExecuteNonQuery() != 1)
                throw new RewardValidationException("The booking reward changed. Reload and try again.");
        }
        InsertLedger(connection, transaction, customerId, bookingId, "Redeem", -RedemptionCost,
            "100 points applied as a booking discount of up to PHP 50; no cash change.");
        return DiscountAmount;
    }

    /// <summary>
    /// Invoke only after a successful completion or confirmed payment mutation,
    /// before committing it. Never invoke while reading pages or scanning history.
    /// A zero-point award still records a marker, so it cannot be awarded later.
    /// </summary>
    public static int AwardCompletedBooking(MySqlConnection connection, MySqlTransaction transaction,
        int bookingId)
    {
        if (!IsReady(connection)) return 0;
        RequireReadCommitted(transaction);
        var booking = LockBooking(connection, transaction, bookingId);
        if (booking.Status != "Completed") return 0;
        LockAccount(connection, transaction, booking.CustomerId);
        var state = ReadState(connection, transaction, bookingId);
        if (state.Earned) return 0;
        // Respect an existing real ledger entry without fabricating a replacement.
        using (var existing = new MySqlCommand("""
            SELECT reward_transaction_id FROM reward_transactions
            WHERE booking_id = @booking AND transaction_type = 'Earn' LIMIT 1
            """, connection, transaction))
        {
            existing.Parameters.AddWithValue("@booking", bookingId);
            if (existing.ExecuteScalar() != null) return 0;
        }
        decimal net = NetTotal(ReadGrossTotal(connection, transaction, bookingId), state.Returned ? 0m : state.Discount);
        decimal paid = ReadPaidAmount(connection, transaction, bookingId);
        if (paid < net) return 0;
        decimal qualifyingPaid = Math.Min(paid, net);
        int points = PointsForPaidAmount(qualifyingPaid);
        EnsureBookingState(connection, transaction, bookingId);
        using (var command = new MySqlCommand("""
            UPDATE booking_rewards SET points_awarded = @points, earned_at = NOW(),
                paid_amount_at_earning = @paid WHERE booking_id = @booking AND earned_at IS NULL
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("@points", points);
            command.Parameters.AddWithValue("@paid", qualifyingPaid);
            command.Parameters.AddWithValue("@booking", bookingId);
            if (command.ExecuteNonQuery() != 1) return 0;
        }
        if (points > 0)
            InsertLedger(connection, transaction, booking.CustomerId, bookingId, "Earn", points,
                $"Earned on completed, fully paid booking: PHP {qualifyingPaid:0.00} paid; 1 point per full PHP 10.");
        return points;
    }

    /// <summary>Return a cancelled booking's spent 100 points exactly once.</summary>
    public static bool RestoreCancelledBooking(MySqlConnection connection, MySqlTransaction transaction,
        int bookingId)
    {
        if (!IsReady(connection)) return false;
        RequireReadCommitted(transaction);
        var booking = LockBooking(connection, transaction, bookingId);
        if (booking.Status != "Cancelled") return false;
        LockAccount(connection, transaction, booking.CustomerId);
        var state = ReadState(connection, transaction, bookingId);
        if (state.RedeemedPoints == 0 || state.Returned) return false;
        using (var command = new MySqlCommand("""
            UPDATE booking_rewards SET returned_at = NOW()
            WHERE booking_id = @booking AND redeemed_points = 100 AND returned_at IS NULL
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("@booking", bookingId);
            if (command.ExecuteNonQuery() != 1) return false;
        }
        InsertLedger(connection, transaction, booking.CustomerId, bookingId, "Adjustment", state.RedeemedPoints,
            "Reward points returned because the discounted booking was cancelled.");
        return true;
    }

    public static decimal NetTotal(decimal grossTotal, decimal discount) =>
        decimal.Round(Math.Max(0m, grossTotal - Math.Max(0m, discount)), 2,
            MidpointRounding.AwayFromZero);

    public static int PointsForPaidAmount(decimal amount)
    {
        decimal points = decimal.Floor(Math.Max(0m, amount) / PesosPerPoint);
        if (points > int.MaxValue)
            throw new RewardValidationException("The payment total is too large to calculate reward points safely.");
        return (int)points;
    }

    private static void RequireReadCommitted(MySqlTransaction transaction)
    {
        if (transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new RewardValidationException("Rewards require a fresh booking transaction. Reload and try again.");
    }

    private static (int CustomerId, string Status) LockBooking(MySqlConnection connection,
        MySqlTransaction transaction, int bookingId)
    {
        int customerId;
        int statusId;
        // Do not lock the shared status catalogue or the user row. Booking inserts
        // already hold a shared user FK lock; upgrading it could deadlock callers.
        using (var command = new MySqlCommand("""
            SELECT customer_id, current_status_id FROM bookings
            WHERE booking_id = @booking FOR UPDATE
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("@booking", bookingId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) throw new RewardValidationException("The booking could not be found.");
            customerId = Convert.ToInt32(reader["customer_id"]);
            statusId = Convert.ToInt32(reader["current_status_id"]);
        }
        using var status = new MySqlCommand(
            "SELECT status_name FROM booking_statuses WHERE booking_status_id = @status", connection, transaction);
        status.Parameters.AddWithValue("@status", statusId);
        return (customerId, status.ExecuteScalar()?.ToString() ?? "");
    }

    private static void LockAccount(MySqlConnection connection, MySqlTransaction transaction, int customerId)
    {
        // A separate mutex row, not a cached balance. On first use, the unique key
        // serializes concurrent creation; no points are ever seeded or backfilled.
        using (var insert = new MySqlCommand("""
            INSERT INTO reward_accounts (user_id) VALUES (@customer)
            ON DUPLICATE KEY UPDATE user_id = VALUES(user_id)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("@customer", customerId);
            insert.ExecuteNonQuery();
        }
        using var command = new MySqlCommand(
            "SELECT user_id FROM reward_accounts WHERE user_id = @customer FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("@customer", customerId);
        command.ExecuteScalar();
    }

    private static (int RedeemedPoints, decimal Discount, bool Earned, bool Returned) ReadState(
        MySqlConnection connection, MySqlTransaction transaction, int bookingId)
    {
        // The booking lock serializes marker access, including missing rows.
        using var command = new MySqlCommand("""
            SELECT redeemed_points, discount_amount, earned_at, returned_at
            FROM booking_rewards WHERE booking_id = @booking
            """, connection, transaction);
        command.Parameters.AddWithValue("@booking", bookingId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return (0, 0m, false, false);
        return (Convert.ToInt32(reader["redeemed_points"]), reader.GetDecimal("discount_amount"),
            !reader.IsDBNull(reader.GetOrdinal("earned_at")), !reader.IsDBNull(reader.GetOrdinal("returned_at")));
    }

    private static void EnsureBookingState(MySqlConnection connection, MySqlTransaction transaction, int bookingId)
    {
        using var command = new MySqlCommand("""
            INSERT INTO booking_rewards (booking_id) VALUES (@booking)
            ON DUPLICATE KEY UPDATE booking_id = VALUES(booking_id)
            """, connection, transaction);
        command.Parameters.AddWithValue("@booking", bookingId);
        command.ExecuteNonQuery();
    }

    private static long ReadBalance(MySqlConnection connection, MySqlTransaction transaction, int customerId)
    {
        using var command = new MySqlCommand(
            "SELECT COALESCE(SUM(points), 0) FROM reward_transactions WHERE user_id = @customer", connection, transaction);
        command.Parameters.AddWithValue("@customer", customerId);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static decimal ReadGrossTotal(MySqlConnection connection, MySqlTransaction transaction, int bookingId)
    {
        using var command = new MySqlCommand("""
            SELECT ROUND(COALESCE(SUM(quantity * unit_price), 0), 2)
            FROM booking_services WHERE booking_id = @booking
            """, connection, transaction);
        command.Parameters.AddWithValue("@booking", bookingId);
        return Convert.ToDecimal(command.ExecuteScalar());
    }

    private static decimal ReadPaidAmount(MySqlConnection connection, MySqlTransaction transaction, int bookingId)
    {
        using var command = new MySqlCommand("""
            SELECT COALESCE(SUM(p.amount), 0) FROM payments p
            JOIN payment_statuses s ON s.payment_status_id = p.payment_status_id
            WHERE p.booking_id = @booking AND s.status_name = 'Paid'
            """, connection, transaction);
        command.Parameters.AddWithValue("@booking", bookingId);
        return Convert.ToDecimal(command.ExecuteScalar());
    }

    private static void InsertLedger(MySqlConnection connection, MySqlTransaction transaction,
        int customerId, int bookingId, string type, int points, string description)
    {
        using var command = new MySqlCommand("""
            INSERT INTO reward_transactions (user_id, booking_id, transaction_type, points, description)
            VALUES (@customer, @booking, @type, @points, @description)
            """, connection, transaction);
        command.Parameters.AddWithValue("@customer", customerId);
        command.Parameters.AddWithValue("@booking", bookingId);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@points", points);
        command.Parameters.AddWithValue("@description", description);
        command.ExecuteNonQuery();
    }
}
