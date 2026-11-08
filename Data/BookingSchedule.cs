using System.Globalization;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Data;

public class BookingSlotException(string message) : Exception(message);

public class BookingSlot
{
    public DateTime Start { get; set; }
    public int Remaining { get; set; }
    public bool IsFull => Remaining <= 0;
    public string Value => Start.ToString(BookingSchedule.SlotFormat, CultureInfo.InvariantCulture);
}

/// <summary>
/// Drop-off time slots for online bookings (Objective 1) and the 15-minute grace period
/// (Scope): a booking still Pending 15 minutes after its slot is cancelled automatically
/// to free the slot for other customers. The slot is stored in bookings.scheduled_pickup_at.
/// </summary>
public static class BookingSchedule
{
    public const int OpeningHour = 8;        // first slot 8:00 AM
    public const int ClosingHour = 20;       // shop closes 8:00 PM; last slot 7:00 PM
    public const int SlotCapacity = 3;       // bookings allowed per slot, to avoid overbooking
    public const int DaysAhead = 7;
    public const int GraceMinutes = 15;
    public const string SlotFormat = "yyyy-MM-ddTHH:mm";

    public static bool TryParseSlot(string? value, out DateTime slot) =>
        DateTime.TryParseExact(value, SlotFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out slot);

    public static bool IsBookableTime(DateTime slot, DateTime now) =>
        slot.Minute == 0 && slot.Second == 0
        && slot.Hour >= OpeningHour && slot.Hour < ClosingHour
        && slot > now
        && slot.Date <= now.Date.AddDays(DaysAhead);

    /// <summary>All upcoming slots with remaining capacity; cancelled bookings free their slot.</summary>
    public static List<BookingSlot> ReadUpcomingSlots(MySqlConnection connection, DateTime now)
    {
        var booked = new Dictionary<DateTime, int>();
        using (var command = new MySqlCommand("""
                   SELECT b.scheduled_pickup_at, COUNT(*) AS bookings
                   FROM bookings b
                   JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
                   WHERE b.scheduled_pickup_at >= @from AND b.scheduled_pickup_at < @to
                     AND bs.status_name <> 'Cancelled'
                   GROUP BY b.scheduled_pickup_at
                   """, connection))
        {
            command.Parameters.AddWithValue("@from", now.Date);
            command.Parameters.AddWithValue("@to", now.Date.AddDays(DaysAhead + 1));
            using var reader = command.ExecuteReader();
            while (reader.Read())
                booked[Convert.ToDateTime(reader["scheduled_pickup_at"])] = Convert.ToInt32(reader["bookings"]);
        }

        var slots = new List<BookingSlot>();
        for (int day = 0; day <= DaysAhead; day++)
        {
            for (int hour = OpeningHour; hour < ClosingHour; hour++)
            {
                DateTime start = now.Date.AddDays(day).AddHours(hour);
                if (!IsBookableTime(start, now)) continue;
                slots.Add(new BookingSlot
                {
                    Start = start,
                    Remaining = SlotCapacity - booked.GetValueOrDefault(start)
                });
            }
        }
        return slots;
    }

    /// <summary>Counts live bookings in a slot inside the caller's transaction.</summary>
    public static int CountBookingsInSlot(MySqlConnection connection, MySqlTransaction? transaction, DateTime slot)
    {
        using var command = new MySqlCommand("""
            SELECT COUNT(*)
            FROM bookings b
            JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
            WHERE b.scheduled_pickup_at = @slot AND bs.status_name <> 'Cancelled'
            """, connection, transaction);
        command.Parameters.AddWithValue("@slot", slot);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <summary>
    /// Cancels Pending online bookings whose customer did not arrive within the grace period.
    /// Paid down payments become Refunded, unverified GCash declarations become Failed,
    /// redeemed reward points are restored, and the customer is notified.
    /// </summary>
    public static int CancelNoShows(MySqlConnection connection, DateTime now)
    {
        var due = new List<int>();
        using (var command = new MySqlCommand("""
                   SELECT b.booking_id
                   FROM bookings b
                   JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
                   WHERE bs.status_name = 'Pending'
                     AND b.scheduled_pickup_at IS NOT NULL
                     AND b.scheduled_pickup_at < @cutoff
                   """, connection))
        {
            command.Parameters.AddWithValue("@cutoff", now.AddMinutes(-GraceMinutes));
            using var reader = command.ExecuteReader();
            while (reader.Read()) due.Add(Convert.ToInt32(reader["booking_id"]));
        }

        bool rewardsReady = RewardStore.IsReady(connection);
        int cancelled = 0;
        foreach (int bookingId in due)
        {
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                if (CancelNoShow(connection, transaction, bookingId, rewardsReady)) cancelled++;
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        return cancelled;
    }

    private static bool CancelNoShow(MySqlConnection connection, MySqlTransaction transaction, int bookingId, bool rewardsReady) =>
        CancelPending(connection, transaction, bookingId, rewardsReady, changedByUserId: null,
            booking => $"Automatically cancelled: customer did not arrive within the {GraceMinutes}-minute grace period of the {booking.Slot:MMM dd, h:mm tt} drop-off schedule.",
            "Booking cancelled (no-show)",
            booking => $"Your booking {booking.Code} was cancelled because you did not arrive within {GraceMinutes} minutes of your {booking.Slot:MMM dd, h:mm tt} drop-off schedule. Please book a new schedule.") != null;

    /// <summary>
    /// Cancels a booking that is still Pending (not yet accepted by staff): refunds paid down
    /// payments, fails unverified GCash declarations, restores redeemed points, and notifies the
    /// customer. Returns the refunded amount, or null when the booking is no longer Pending.
    /// </summary>
    public static decimal? CancelPending(MySqlConnection connection, MySqlTransaction transaction, int bookingId,
        bool rewardsReady, int? changedByUserId, Func<(string Code, DateTime? Slot), string> historyNote,
        string notificationTitle, Func<(string Code, DateTime? Slot), string> customerMessage)
    {
        string bookingCode;
        int customerId;
        DateTime? slot;
        using (var command = new MySqlCommand("""
                   SELECT b.booking_code, b.customer_id, b.scheduled_pickup_at, bs.status_name
                   FROM bookings b
                   JOIN booking_statuses bs ON bs.booking_status_id = b.current_status_id
                   WHERE b.booking_id = @bookingId
                   FOR UPDATE
                   """, connection, transaction))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId);
            using var reader = command.ExecuteReader();
            // Staff may have accepted the booking meanwhile; leave it alone then.
            if (!reader.Read() || reader["status_name"]?.ToString() != "Pending") return null;
            bookingCode = reader["booking_code"]?.ToString() ?? $"BK-{bookingId}";
            customerId = Convert.ToInt32(reader["customer_id"]);
            slot = reader["scheduled_pickup_at"] == DBNull.Value ? null : Convert.ToDateTime(reader["scheduled_pickup_at"]);
        }

        string reason = historyNote((bookingCode, slot));
        Execute(connection, transaction, """
            UPDATE bookings
            SET current_status_id = (SELECT booking_status_id FROM booking_statuses WHERE status_name = 'Cancelled')
            WHERE booking_id = @bookingId
            """, bookingId);
        using (var command = new MySqlCommand("""
                   INSERT INTO booking_status_history (booking_id, booking_status_id, changed_by_user_id, notes)
                   SELECT @bookingId, booking_status_id, @changedBy, @notes
                   FROM booking_statuses WHERE status_name = 'Cancelled'
                   """, connection, transaction))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId);
            command.Parameters.AddWithValue("@changedBy", changedByUserId.HasValue ? changedByUserId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@notes", reason);
            command.ExecuteNonQuery();
        }

        decimal refunded;
        using (var command = new MySqlCommand("""
                   SELECT COALESCE(SUM(p.amount), 0)
                   FROM payments p
                   JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
                   WHERE p.booking_id = @bookingId AND ps.status_name = 'Paid'
                   """, connection, transaction))
        {
            command.Parameters.AddWithValue("@bookingId", bookingId);
            refunded = Convert.ToDecimal(command.ExecuteScalar());
        }
        Execute(connection, transaction, """
            UPDATE payments p
            JOIN payment_statuses paid ON paid.payment_status_id = p.payment_status_id AND paid.status_name = 'Paid'
            JOIN payment_statuses refunded ON refunded.status_name = 'Refunded'
            SET p.payment_status_id = refunded.payment_status_id,
                p.notes = LEFT(CONCAT_WS(' ', p.notes, 'Refunded: booking cancelled before it was accepted.'), 500)
            WHERE p.booking_id = @bookingId
            """, bookingId);
        Execute(connection, transaction, """
            UPDATE payments p
            JOIN payment_statuses pending ON pending.payment_status_id = p.payment_status_id AND pending.status_name = 'Pending'
            JOIN payment_statuses failed ON failed.status_name = 'Failed'
            SET p.payment_status_id = failed.payment_status_id,
                p.notes = LEFT(CONCAT_WS(' ', p.notes, 'Booking cancelled before verification; return any amount received.'), 500)
            WHERE p.booking_id = @bookingId
            """, bookingId);

        if (rewardsReady) RewardStore.RestoreCancelledBooking(connection, transaction, bookingId);

        string message = customerMessage((bookingCode, slot))
                         + (refunded > 0m ? $" PHP {refunded:N2} of your payment will be refunded." : "");
        using (var command = new MySqlCommand("""
                   INSERT INTO notifications (user_id, booking_id, notification_type, title, message)
                   VALUES (@customerId, @bookingId, 'Booking', @title, @message)
                   """, connection, transaction))
        {
            command.Parameters.AddWithValue("@customerId", customerId);
            command.Parameters.AddWithValue("@bookingId", bookingId);
            command.Parameters.AddWithValue("@title", notificationTitle);
            command.Parameters.AddWithValue("@message", message);
            command.ExecuteNonQuery();
        }
        return refunded;
    }

    private static void Execute(MySqlConnection connection, MySqlTransaction transaction, string sql, int bookingId, string? notes = null)
    {
        using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@bookingId", bookingId);
        if (notes != null) command.Parameters.AddWithValue("@notes", notes);
        command.ExecuteNonQuery();
    }
}
