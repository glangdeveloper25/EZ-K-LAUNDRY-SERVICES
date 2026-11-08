using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Data;

public class LaundryMachine
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public class MachineSnapshot
{
    public bool IsReady { get; set; }
    public List<LaundryMachine> Machines { get; set; } = [];

    public int CountOf(string type) => Machines.Count(machine => machine.Type == type);
    public int AvailableOf(string type) =>
        Machines.Count(machine => machine.Type == type && machine.Status == MachineStore.Available);
}

/// <summary>
/// Self-Service (DIY) machine monitoring. Staff record each machine's status;
/// customers only read the snapshot to avoid physical queues at the shop.
/// </summary>
public static class MachineStore
{
    public const string Available = "Available";
    public const string InUse = "In Use";
    public const string Maintenance = "Maintenance";
    public static readonly string[] Statuses = [Available, InUse, Maintenance];

    public static bool IsReady(MySqlConnection connection)
    {
        using var command = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE() AND table_name = 'laundry_machines'
            """, connection);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public static MachineSnapshot ReadSnapshot(MySqlConnection connection)
    {
        var snapshot = new MachineSnapshot { IsReady = IsReady(connection) };
        if (!snapshot.IsReady) return snapshot;

        using var command = new MySqlCommand("""
            SELECT machine_id, machine_name, machine_type, machine_status, updated_at
            FROM laundry_machines
            ORDER BY machine_type DESC, machine_name
            """, connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            snapshot.Machines.Add(new LaundryMachine
            {
                Id = Convert.ToInt32(reader["machine_id"]),
                Name = reader["machine_name"]?.ToString() ?? "",
                Type = reader["machine_type"]?.ToString() ?? "",
                Status = reader["machine_status"]?.ToString() ?? "",
                UpdatedAt = Convert.ToDateTime(reader["updated_at"])
            });
        }
        return snapshot;
    }

    /// <returns>False when the machine does not exist or the status is not allowed.</returns>
    public static bool UpdateStatus(MySqlConnection connection, int machineId, string status, int staffId)
    {
        if (!Statuses.Contains(status)) return false;

        using var command = new MySqlCommand("""
            UPDATE laundry_machines
            SET machine_status = @status, updated_by_user_id = @staffId
            WHERE machine_id = @machineId
            """, connection);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@staffId", staffId);
        command.Parameters.AddWithValue("@machineId", machineId);
        return command.ExecuteNonQuery() == 1;
    }
}
