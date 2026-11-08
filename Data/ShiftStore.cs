using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Data;

public class StaffShift
{
    public DateTime TimeIn { get; set; }
    public DateTime? TimeOut { get; set; }
    public TimeSpan Worked(DateTime now) => (TimeOut ?? now) - TimeIn;
}

public class ShiftSnapshot
{
    public bool IsReady { get; set; }
    public StaffShift? OpenShift { get; set; }
    public bool IsOnDuty => OpenShift != null;
    public List<StaffShift> TodayShifts { get; set; } = [];
    public int OrdersHandledToday { get; set; }
    public decimal PaymentsRecordedToday { get; set; }

    public TimeSpan WorkedToday(DateTime now) =>
        TimeSpan.FromTicks(TodayShifts.Sum(shift => shift.Worked(now).Ticks));
}

/// <summary>
/// My Shift (Staff Dashboard storyboard): Time In / Time Out records and a daily summary of
/// the staff member's work, so the owner can see who is on duty and track staff performance.
/// </summary>
public static class ShiftStore
{
    public static bool IsReady(MySqlConnection connection)
    {
        using var command = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE() AND table_name = 'staff_shifts'
            """, connection);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public static ShiftSnapshot ReadSnapshot(MySqlConnection connection, int staffId, DateTime now)
    {
        var snapshot = new ShiftSnapshot { IsReady = IsReady(connection) };
        if (!snapshot.IsReady) return snapshot;

        using (var command = new MySqlCommand("""
                   SELECT time_in, time_out FROM staff_shifts
                   WHERE staff_id = @staffId AND (time_in >= @today OR time_out IS NULL)
                   ORDER BY time_in
                   """, connection))
        {
            command.Parameters.AddWithValue("@staffId", staffId);
            command.Parameters.AddWithValue("@today", now.Date);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var shift = new StaffShift
                {
                    TimeIn = Convert.ToDateTime(reader["time_in"]),
                    TimeOut = reader["time_out"] == DBNull.Value ? null : Convert.ToDateTime(reader["time_out"])
                };
                snapshot.TodayShifts.Add(shift);
                if (shift.TimeOut == null) snapshot.OpenShift = shift;
            }
        }

        using (var command = new MySqlCommand("""
                   SELECT COUNT(DISTINCT booking_id) FROM booking_status_history
                   WHERE changed_by_user_id = @staffId AND changed_at >= @today
                   """, connection))
        {
            command.Parameters.AddWithValue("@staffId", staffId);
            command.Parameters.AddWithValue("@today", now.Date);
            snapshot.OrdersHandledToday = Convert.ToInt32(command.ExecuteScalar());
        }

        using (var command = new MySqlCommand("""
                   SELECT COALESCE(SUM(p.amount), 0) FROM payments p
                   JOIN payment_statuses ps ON ps.payment_status_id = p.payment_status_id
                   WHERE p.recorded_by_user_id = @staffId AND ps.status_name = 'Paid' AND p.paid_at >= @today
                   """, connection))
        {
            command.Parameters.AddWithValue("@staffId", staffId);
            command.Parameters.AddWithValue("@today", now.Date);
            snapshot.PaymentsRecordedToday = Convert.ToDecimal(command.ExecuteScalar());
        }
        return snapshot;
    }

    /// <returns>False when the staff member is already timed in.</returns>
    public static bool TimeIn(MySqlConnection connection, int staffId, DateTime now)
    {
        using var command = new MySqlCommand("""
            INSERT INTO staff_shifts (staff_id, time_in)
            SELECT @staffId, @now FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM staff_shifts WHERE staff_id = @staffId AND time_out IS NULL)
            """, connection);
        command.Parameters.AddWithValue("@staffId", staffId);
        command.Parameters.AddWithValue("@now", now);
        return command.ExecuteNonQuery() == 1;
    }

    /// <returns>False when the staff member has no open shift.</returns>
    public static bool TimeOut(MySqlConnection connection, int staffId, DateTime now)
    {
        using var command = new MySqlCommand(
            "UPDATE staff_shifts SET time_out = @now WHERE staff_id = @staffId AND time_out IS NULL", connection);
        command.Parameters.AddWithValue("@staffId", staffId);
        command.Parameters.AddWithValue("@now", now);
        return command.ExecuteNonQuery() > 0;
    }

    public static HashSet<int> OnDutyStaffIds(MySqlConnection connection)
    {
        var ids = new HashSet<int>();
        if (!IsReady(connection)) return ids;
        using var command = new MySqlCommand("SELECT DISTINCT staff_id FROM staff_shifts WHERE time_out IS NULL", connection);
        using var reader = command.ExecuteReader();
        while (reader.Read()) ids.Add(Convert.ToInt32(reader["staff_id"]));
        return ids;
    }
}
