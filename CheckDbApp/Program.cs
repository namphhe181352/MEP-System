using Npgsql;

var connStr = "Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@";
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

Console.WriteLine("=== UPDATING MISMATCHED SALE STOCK TICKET ITEMS ===");
await using (var cmd = new NpgsqlCommand(@"
    UPDATE stock_ticket_items
    SET unit_price = ROUND(ii.sell_price / COALESCE(NULLIF(ii.""ConvertValue"", 0), 1), 4)
    FROM stock_tickets st, invoices i, invoice_items ii
    WHERE stock_ticket_items.stock_ticket_id = st.stock_ticket_id
      AND st.ticket_code = i.invoice_code
      AND i.invoice_id = ii.invoice_id 
      AND stock_ticket_items.branch_product_id = ii.branch_product_id
      AND st.ticket_type = 'SALE'
      AND ii.""ConvertValue"" IS NOT NULL AND ii.""ConvertValue"" > 1
      AND stock_ticket_items.unit_price = ii.sell_price", conn))
{
    var rows = await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Updated {rows} stock_ticket_items rows.");
}

Console.WriteLine("\n=== VERIFYING INV-2026-0535 ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT st.ticket_code, sti.discrepancy, sti.unit_price, sti.unit_name,
           (sti.discrepancy * sti.unit_price) as total_amount
    FROM stock_tickets st
    JOIN stock_ticket_items sti ON st.stock_ticket_id = sti.stock_ticket_id
    WHERE st.ticket_code = 'INV-2026-0535'", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"Ticket: {reader["ticket_code"]} | Discrepancy: {reader["discrepancy"]} | UnitPrice: {reader["unit_price"]} | UnitName: '{reader["unit_name"]}' | Total: {reader["total_amount"]}");
    }
}
