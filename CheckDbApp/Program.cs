using Npgsql;

var connStr = "Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@";
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
Console.WriteLine("=== REMAINING INVENTORY CHECK TICKETS ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT ticket_code, status, created_at
    FROM stock_tickets
    WHERE ticket_type = 'INVENTORY_CHECK'
    ORDER BY created_at DESC
    LIMIT 20", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"Ticket: {reader["ticket_code"]} | Status: {reader["status"]} | CreatedAt: {reader["created_at"]}");
    }
}

Console.WriteLine("\n=== INVENTORY CHECK TICKETS WITH CHK ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT ticket_id, ticket_code, status, created_at, notes
    FROM inventory_check_tickets
    WHERE ticket_code LIKE 'CHK-%'
    ORDER BY created_at DESC", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"CheckTicket: {reader["ticket_code"]} | Status: {reader["status"]} | CreatedAt: {reader["created_at"]} | Notes: {reader["notes"]}");
    }
}
