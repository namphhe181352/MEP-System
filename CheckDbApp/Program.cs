using Npgsql;

var connStr = "Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@";
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

Console.WriteLine("=== ROLE PERMISSIONS FOR INVENTORYSTAFF ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT r.role_name, p.permission_code, p.permission_name
    FROM role_permissions rp
    JOIN roles r ON rp.role_id = r.role_id
    JOIN permissions p ON rp.permission_id = p.permission_id
    WHERE r.role_name LIKE '%Inventory%'
    ORDER BY r.role_name, p.permission_code", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"Role: {reader["role_name"]} | Perm: {reader["permission_code"]} ({reader["permission_name"]})");
    }
}
