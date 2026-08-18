using Npgsql;

var connStr = "Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@";
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
Console.WriteLine("=== TESTING UPDATE WITH QUOTED ConvertValue ===");
await using (var cmd = new NpgsqlCommand(@"
    UPDATE stock_ticket_items sti
    SET unit_price = ROUND((sti.unit_price / COALESCE(NULLIF(ii.""ConvertValue"", 0), 1.0)), 4),
        unit_name = COALESCE(p.base_unit, sti.unit_name)
    FROM stock_tickets st,
         invoices inv,
         invoice_items ii,
         branch_products bp,
         products p
    WHERE sti.stock_ticket_id = st.stock_ticket_id
      AND inv.invoice_code = st.ticket_code
      AND ii.invoice_id = inv.invoice_id
      AND ii.branch_product_id = sti.branch_product_id
      AND bp.branch_product_id = sti.branch_product_id
      AND p.product_id = bp.product_id
      AND st.ticket_type = 'SALE'
      AND COALESCE(ii.""ConvertValue"", 1.0) > 1.0
      AND NOT EXISTS (SELECT 1 FROM system_configs WHERE config_key = 'SALE_UOM_CONVERSION_FIX_V1');", conn))
{
    var rows = await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Affected rows: {rows}");
}

await using (var cmd = new NpgsqlCommand(@"
    INSERT INTO system_configs (config_id, branch_id, config_key, config_value)
    SELECT gen_random_uuid(), NULL, 'SALE_UOM_CONVERSION_FIX_V1', 'done'
    WHERE NOT EXISTS (SELECT 1 FROM system_configs WHERE config_key = 'SALE_UOM_CONVERSION_FIX_V1');", conn))
{
    var sentinel = await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Sentinel rows: {sentinel}");
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
