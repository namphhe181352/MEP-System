using Npgsql;

var connStr = "Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@";
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

Console.WriteLine("=== INVOICE ITEMS FOR INV-2026-0536 ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT ii.*
    FROM invoices i
    JOIN invoice_items ii ON i.invoice_id = ii.invoice_id
    WHERE i.invoice_code = 'INV-2026-0536'", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    for (int i = 0; i < reader.FieldCount; i++)
    {
        Console.Write($"{reader.GetName(i)}\t");
    }
    Console.WriteLine();
    while (await reader.ReadAsync())
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            Console.Write($"{reader.GetValue(i)}\t");
        }
        Console.WriteLine();
    }
}

Console.WriteLine("\n=== PRODUCT CONVERSION UNITS FOR SP001 ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT pcu.*, p.product_code, p.product_name, p.base_unit
    FROM product_conversion_units pcu
    JOIN products p ON pcu.product_id = p.product_id
    WHERE p.product_code = 'SP001'", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"PCU: UnitName='{reader["unit_name"]}' | ConvertValue={reader["convert_value"]} | Price={reader["price"]} | DirectSale={reader["direct_sale"]}");
    }
}

Console.WriteLine("\n=== STOCK TICKET FOR INV-2026-0536 ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT st.stock_ticket_id, st.ticket_code, st.ticket_type, st.notes, st.total_amount,
           sti.ticket_item_id, sti.unit_name, sti.system_quantity, sti.actual_quantity, sti.unit_price
    FROM stock_tickets st
    JOIN stock_ticket_items sti ON st.stock_ticket_id = sti.stock_ticket_id
    WHERE st.ticket_code = 'INV-2026-0536' OR st.notes LIKE '%INV-2026-0536%'", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"StockTicket: {reader["ticket_code"]} | Notes: {reader["notes"]} | TicketTotal: {reader["total_amount"]}");
        Console.WriteLine($"StockTicketItem: UnitName='{reader["unit_name"]}' | SystemQty={reader["system_quantity"]} | ActualQty={reader["actual_quantity"]} | UnitPrice={reader["unit_price"]}");
    }
}
