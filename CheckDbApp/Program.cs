using Npgsql;

var connStr = "Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@";
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

Console.WriteLine("=== MARKING ALL UNREAD NOTIFICATIONS AS READ IN DB ===");
await using (var cmd = new NpgsqlCommand(@"
    UPDATE notifications
    SET is_read = true
    WHERE is_read = false", conn))
{
    var rows = await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Updated {rows} unread notifications to is_read = true.");
}

Console.WriteLine("\n=== REMAINING UNREAD NOTIFICATIONS ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT COUNT(*) FROM notifications WHERE is_read = false", conn))
{
    var count = await cmd.ExecuteScalarAsync();
    Console.WriteLine($"Unread count: {count}");
}
