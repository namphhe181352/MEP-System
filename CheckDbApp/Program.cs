using Npgsql;

var connStr = "Host=103.75.182.211;Port=5432;Database=M.E.P Database;Username=postgres;Password=Vuatrom154321@";
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
Console.WriteLine("=== ALL PRODUCT CONVERSION UNITS ===");
await using (var cmd = new NpgsqlCommand(@"
    SELECT p.product_code, p.product_name, p.base_unit, bp.sell_price as base_price,
           pcu.unit_name, pcu.convert_value, pcu.price, pcu.direct_sale
    FROM product_conversion_units pcu
    JOIN products p ON pcu.product_id = p.product_id
    LEFT JOIN branch_products bp ON p.product_id = bp.product_id
    ORDER BY p.product_code, pcu.convert_value", conn))
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"Product: {reader["product_code"]} | Base: {reader["base_unit"]} (Price: {reader["base_price"]:N0}) | ConvUnit: {reader["unit_name"]} (Convert: {reader["convert_value"]}, Price: {reader["price"]:N0}, DirectSale: {reader["direct_sale"]})");
    }
}
