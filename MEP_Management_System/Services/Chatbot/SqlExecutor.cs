using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MEP_Management_System.Models;
using Npgsql;

namespace MEP_Management_System.Services.Chatbot;

/// <summary>
/// Rate limit đơn giản theo fixed window (in-memory, reset khi restart app).
/// Đăng ký singleton trong DI để chia sẻ bucket giữa mọi request - chặn spam
/// gọi AI API (tốn phí token) từ một user.
/// </summary>
public class ChatbotRateLimiter
{
    private readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> _buckets = new();
    private readonly int _maxRequestsPerWindow;
    private readonly int _windowSeconds;

    public ChatbotRateLimiter(int maxRequestsPerWindow = 15, int windowSeconds = 60)
    {
        _maxRequestsPerWindow = maxRequestsPerWindow;
        _windowSeconds = windowSeconds;
    }

    /// <summary>
    /// Kiểm tra user có được phép thực hiện request tiếp theo trong cửa sổ hiện tại không.
    /// </summary>
    public bool Allow(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) key = "anonymous";

        var now = DateTime.UtcNow;
        var bucket = _buckets.AddOrUpdate(
            key,
            _ => (now, 1),
            (_, existing) =>
                (now - existing.WindowStart).TotalSeconds >= _windowSeconds
                    ? (now, 1)
                    : (existing.WindowStart, existing.Count + 1));

        // Dọn dẹp bucket cũ định kỳ để tránh rò rỉ bộ nhớ khi có nhiều user.
        if (_buckets.Count > 10_000)
        {
            foreach (var kvp in _buckets)
            {
                if ((now - kvp.Value.WindowStart).TotalSeconds >= _windowSeconds)
                {
                    _buckets.TryRemove(kvp.Key, out _);
                }
            }
        }

        return bucket.Count <= _maxRequestsPerWindow;
    }
}

public class SqlExecutor
{
    private readonly MEPDatabaseContext _context;
    private readonly ILogger<SqlExecutor> _logger;
    private readonly string? _chatbotConnectionString;

    // ĐỒNG BỘ với danh sách 32 bảng đã duyệt (GRANT SELECT cho role mep_chatbot_ro).
    // Đây là whitelist duy nhất - mọi bảng xuất hiện trong FROM/JOIN của SQL do AI
    // sinh ra PHẢI nằm trong danh sách này, nếu không sẽ bị từ chối (fail-closed).
    // public để unit test verify toàn bộ whitelist.
    public static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "branches", "branch_products", "products", "product_attributes",
        "product_conversion_units", "customers", "orders", "order_items",
        "order_discount_tiers", "invoices", "invoice_items", "payments",
        "refund_records", "return_orders", "return_items",
        "suppliers", "SupplierPayments", "supplier_payments", "\"SupplierPayments\"",
        "purchase_orders", "purchase_order_items", "stock_tickets", "stock_ticket_items",
        "inventory_check_tickets", "inventory_check_details",
        "inventory_transfer_tickets", "inventory_transfer_details",
        "expense_vouchers", "expense_categories",
        "shifts", "staff_assignments", "users", "user_roles", "roles"
    };

    // Các schema/bảng hệ thống PostgreSQL tuyệt đối không cho phép động vào,
    // dù không nằm trong AllowedTables ở trên (phòng trường hợp AI cố dò schema
    // hoặc đọc thông tin nhạy cảm qua các view hệ thống)
    private static readonly string[] ForbiddenPatterns =
    {
        @"\binformation_schema\b",
        @"\bpg_catalog\b",
        @"\bpg_[a-z_]+\b",          // pg_shadow, pg_roles, pg_authid, pg_proc, pg_read_file, pg_ls_dir...
        @"\bdblink[a-z_]*\b",
        @"\blo_import\b", @"\blo_export\b",
        @"\bcopy\b",
        @"\bcurrent_setting\b", @"\bset_config\b",
        @"\bpg_sleep\b",
        @"\bDROP\b", @"\bDELETE\b", @"\bINSERT\b", @"\bUPDATE\b",
        @"\bTRUNCATE\b", @"\bALTER\b", @"\bCREATE\b", @"\bGRANT\b", @"\bREVOKE\b",
        @"\bEXEC\b", @"\bEXECUTE\b", @"\bCALL\b", @"\bDO\b",
        @"\bUNION\b\s+\bSELECT\b",
        @"\bSELECT\s+\*",           // chặn SELECT * - buộc AI liệt kê cột cụ thể (tránh lộ PII diện rộng)
        @"--", @"/\*"
    };

    // Bắt TOÀN BỘ danh sách bảng sau FROM/JOIN, kể cả danh sách phân tách dấu phẩy
    // (FROM a, b). Mỗi bảng trong list đều phải nằm trong whitelist - trước đây chỉ
    // bắt được bảng đầu tiên, để lọt bảng đứng sau dấu phẩy.
    private static readonly Regex TableListRegex = new(
        @"(?:FROM|JOIN)\s+(?<tables>""?[A-Za-z_][A-Za-z0-9_]*""?(?:\s+[A-Za-z_][A-Za-z0-9_]*)?(?:\s*,\s*""?[A-Za-z_][A-Za-z0-9_]*""?(?:\s+[A-Za-z_][A-Za-z0-9_]*)?)*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SqlExecutor(MEPDatabaseContext context, ILogger<SqlExecutor> logger, IConfiguration config)
    {
        _context = context;
        _logger = logger;
        // Connection string RIÊNG cho role read-only (mep_chatbot_ro) - xem
        // setup_chatbot_readonly_role.sql. Nếu chưa cấu hình, fallback về
        // connection string của app chính (KHÔNG an toàn bằng, chỉ để không crash
        // khi bạn chưa kịp cấu hình - hãy thêm "ChatbotDbConnection" vào
        // appsettings.json càng sớm càng tốt).
        _chatbotConnectionString = config.GetConnectionString("ChatbotDbConnection");
        if (string.IsNullOrWhiteSpace(_chatbotConnectionString))
        {
            _logger.LogWarning(
                "ChatbotDbConnection chưa được cấu hình trong appsettings.json - " +
                "chatbot đang dùng chung connection string với app chính (KHÔNG khuyến nghị). " +
                "Hãy thêm connection string dùng role mep_chatbot_ro để có bảo vệ DB-level thực sự.");
        }
    }

    /// <summary>
    /// Validate SQL do AI sinh ra - fail-closed: bất kỳ điều gì không khớp whitelist
    /// đều bị từ chối. Tách public static để unit-test trực tiếp không cần DB.
    /// </summary>
    public static (bool IsValid, string Message) ValidateSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return (false, "SQL query trống.");
        }

        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Chỉ hỗ trợ câu lệnh SELECT.");
        }

        // Chặn multi-statement injection: chỉ cho phép TỐI ĐA 1 dấu ; và phải là
        // ký tự cuối cùng (sau khi trim khoảng trắng), không cho phép có nội dung
        // sau dấu ;
        var withoutTrailingSemicolon = trimmed.TrimEnd().TrimEnd(';');
        if (withoutTrailingSemicolon.Contains(';'))
        {
            return (false, "SQL query không được chứa nhiều câu lệnh (phát hiện dấu ';' ở giữa).");
        }

        // Chặn các pattern/từ khóa nguy hiểm và các schema hệ thống
        foreach (var pattern in ForbiddenPatterns)
        {
            if (Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase))
            {
                return (false, "SQL query chứa từ khóa hoặc schema không được phép.");
            }
        }

        var tableMatches = TableListRegex.Matches(sql);
        if (tableMatches.Count == 0)
        {
            return (false, "Không xác định được bảng nào trong câu SQL - từ chối để an toàn.");
        }

        foreach (Match m in tableMatches)
        {
            foreach (var part in m.Groups["tables"].Value.Split(','))
            {
                // Lấy token đầu tiên của mỗi phần (tên bảng), bỏ qua alias phía sau
                var nameMatch = Regex.Match(part.Trim(), @"^""?([A-Za-z_][A-Za-z0-9_]*)""?");
                if (!nameMatch.Success)
                {
                    // Phần không phải tên bảng trần (vd: subquery) - các bảng bên trong
                    // subquery sẽ được quét riêng bởi FROM/JOIN bên trong nó.
                    continue;
                }

                var tableName = nameMatch.Groups[1].Value;
                if (!AllowedTables.Contains(tableName))
                {
                    // FAIL-CLOSED: bất kỳ bảng nào không nằm trong whitelist đều bị từ
                    // chối ngay lập tức, thay vì chỉ bỏ qua như code cũ.
                    return (false, $"Bảng '{tableName}' không được phép truy cập.");
                }
            }
        }

        return (true, "OK");
    }

    /// <summary>
    /// Nhận SQL đã được ChatController extract sẵn (không còn marker --QUERY_START--...).
    /// Đây là nơi DUY NHẤT validate + thực thi SQL - không parse marker lại lần nữa
    /// để tránh lệch pha giữa ChatController và SqlExecutor.
    ///
    /// branchId: branch_id (dạng string, ví dụ Guid.ToString()) của user đang chat,
    /// lấy từ claim trong JWT ở ChatController. Được set vào session variable
    /// Postgres "app.current_branch_id" TRƯỚC khi chạy SQL do AI sinh ra, để Row
    /// Level Security (RLS) tự động lọc theo đúng chi nhánh của user - không phụ
    /// thuộc vào việc AI có nhớ filter branch_id trong câu SQL hay không.
    ///
    /// virtual để unit-test vòng lặp chatbot có thể fake kết quả query.
    /// </summary>
    public virtual async Task<(bool success, string message, object? data)> ExecuteQueryAsync(string sql, string? branchId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return (false, "Không tìm thấy SQL query hợp lệ.", null);
            }

            sql = sql.Trim();

            var validation = ValidateSql(sql);
            if (!validation.IsValid)
            {
                _logger.LogWarning("SQL validation failed: {Reason} | SQL: {Sql}", validation.Message, sql);
                return (false, validation.Message, null);
            }

            // Inject branch_id filter vào SQL để đảm bảo AI luôn query đúng chi nhánh
            sql = InjectBranchIdFilter(sql, branchId);

            _logger.LogInformation("Executing SQL (branchId={BranchId}): {Sql}", branchId, sql);

            var result = await ExecuteSelectAsync(sql, branchId);

            return (true, "Query thành công", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL query: {Sql}", sql);
            return (false, $"Lỗi khi thực thi query: {ex.Message}", null);
        }
    }

    private async Task<object?> ExecuteSelectAsync(string sql, string? branchId)
    {
        // Luôn mở connection RIÊNG và tự đóng - KHÔNG dùng chung connection đang mở
        // của EF context (tránh nhiễu session variable giữa các request). Ưu tiên
        // connection string read-only (role mep_chatbot_ro) nếu đã cấu hình
        // ChatbotDbConnection; nếu chưa thì fallback về connection string của app
        // chính (vẫn là connection riêng, không dùng chung với EF).
        var connectionString = !string.IsNullOrWhiteSpace(_chatbotConnectionString)
            ? _chatbotConnectionString
            : _context.Database.GetDbConnection().ConnectionString;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        try
        {
            // 🔒 Set session variable NGAY TRÊN CONNECTION NÀY trước khi chạy SQL
            // do AI sinh ra. Đây là điều kiện để các RLS policy (USING
            // (branch_id::text = current_setting('app.current_branch_id', true)))
            // tự động lọc đúng chi nhánh của user, bất kể AI có viết WHERE
            // branch_id hay không.
            //
            // is_local = false (tham số thứ 3 của set_config) vì mỗi request
            // dùng 1 connection riêng biệt (mở/đóng ngay trong hàm này), không
            // nằm trong 1 transaction dài hạn nên không cần set theo transaction.
            //
            // Nếu branchId null/rỗng (không xác định được chi nhánh của user),
            // set thành chuỗi rỗng để RLS mặc định KHÔNG khớp bất kỳ branch nào
            // (fail-closed: không trả dữ liệu nào thay vì lộ hết dữ liệu).
            await using (var setCmd = connection.CreateCommand())
            {
                setCmd.CommandText = "SELECT set_config('app.current_branch_id', @branchId, false);";
                var param = setCmd.CreateParameter();
                param.ParameterName = "branchId";
                param.Value = (object?)branchId ?? string.Empty;
                setCmd.Parameters.Add(param);
                await setCmd.ExecuteNonQueryAsync();
            }

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 15; // giây - đủ cho query aggregate lớn, tránh AI lặp 4 lần vì timeout

            using var reader = await command.ExecuteReaderAsync();

            var results = new List<Dictionary<string, object?>>();
            var columns = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            const int maxRows = 500; // an toàn cho trường hợp AI quên LIMIT
            while (await reader.ReadAsync() && results.Count < maxRows)
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columns[i]] = value;
                }
                results.Add(row);
            }

            // Kiểm tra còn dòng nữa không để báo truncated CHÍNH XÁC: chỉ báo cắt
            // khi thật sự có dòng bị bỏ lại sau maxRows, không báo nhầm khi số
            // dòng = đúng maxRows.
            var truncated = results.Count >= maxRows && await reader.ReadAsync();

            return new
            {
                columns = columns,
                rows = results,
                totalRows = results.Count,
                truncated = truncated
            };
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Inject branch_id filter vào SQL query tự động.
    /// Khi query các bảng có branch_id (orders, customers, expense_vouchers...),
    /// tự động thêm filter branch_id = 'xxx'.
    /// Khi query payments/invoices không có branch_id, phải JOIN với orders để lọc.
    /// </summary>
    private static string InjectBranchIdFilter(string sql, string? branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId) || string.IsNullOrWhiteSpace(sql))
            return sql;

        var sqlUpper = sql.ToUpperInvariant();

        // Tìm vị trí để insert branch_id filter - phải insert TRƯỚC GROUP BY, ORDER BY, LIMIT
        var insertPos = sql.Length;
        var keywords = new[] { "GROUP BY", "ORDER BY", "LIMIT", ";", "--" };
        foreach (var kw in keywords)
        {
            var pos = sqlUpper.LastIndexOf(kw);
            if (pos >= 0 && pos < insertPos)
                insertPos = pos;
        }

        // 1. Query trực tiếp bảng orders - thêm branch_id filter
        if ((sqlUpper.Contains("FROM ORDERS") || sqlUpper.Contains("JOIN ORDERS ")) && !sqlUpper.Contains("BRANCH_ID"))
        {
            // Tìm alias của orders
            var ordersAlias = "orders";
            if (sqlUpper.Contains(" ORDERS O ") || sqlUpper.Contains("ORDERS AS O") || sqlUpper.Contains("ORDERS O\n") || sqlUpper.Contains("ORDERS O\r\n"))
                ordersAlias = "o";

            var branchFilter = $" AND {ordersAlias}.branch_id = '{branchId}'";
            sql = sql.Insert(insertPos, branchFilter);
        }

        // 2. Bảng customers KHÔNG có branch_id - RLS lọc thông qua orders nên KHÔNG
        // inject branch_id filter (sẽ gây lỗi "column customers.branch_id does not exist").
        // Các query JOIN customers sẽ được RLS tự lọc qua orders.branch_id trong
        // policy hoặc qua JOIN với orders đã có filter branch_id.

        // 3. Query payments/invoices - cần JOIN với orders để lọc
        if ((sqlUpper.Contains("PAYMENTS") || sqlUpper.Contains("INVOICES")) && !sqlUpper.Contains("JOIN ORDERS"))
        {
            if (sqlUpper.Contains("JOIN INVOICES") && !sqlUpper.Contains("JOIN ORDERS O"))
            {
                var oldLen = sql.Length;
                sql = sql.Replace("JOIN invoices i", "JOIN invoices i JOIN orders o ON i.order_id = o.order_id");
                insertPos += (sql.Length - oldLen);
                sql = sql.Insert(insertPos, $" AND o.branch_id = '{branchId}'");
            }
            else if (sqlUpper.Contains("JOIN PAYMENTS") && !sqlUpper.Contains("JOIN ORDERS"))
            {
                var oldLen = sql.Length;
                sql = sql.Replace("JOIN payments p", "JOIN payments p JOIN invoices i ON p.invoice_id = i.invoice_id JOIN orders o ON i.order_id = o.order_id");
                insertPos += (sql.Length - oldLen);
                sql = sql.Insert(insertPos, $" AND o.branch_id = '{branchId}'");
            }
        }

        return sql;
    }
}
