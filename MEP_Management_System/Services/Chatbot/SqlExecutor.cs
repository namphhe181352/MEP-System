using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MEP_Management_System.Models;
using Npgsql;

namespace MEP_Management_System.Services.Chatbot;

public class SqlExecutor
{
    private readonly MEPDatabaseContext _context;
    private readonly ILogger<SqlExecutor> _logger;
    private readonly string? _chatbotConnectionString;

    // ĐỒNG BỘ với danh sách 32 bảng đã duyệt (GRANT SELECT cho role mep_chatbot_ro).
    // Đây là whitelist duy nhất - mọi bảng xuất hiện trong FROM/JOIN của SQL do AI
    // sinh ra PHẢI nằm trong danh sách này, nếu không sẽ bị từ chối (fail-closed).
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "branches", "branch_products", "products", "product_attributes",
        "product_conversion_units", "customers", "orders", "order_items",
        "order_discount_tiers", "invoices", "invoice_items", "payments",
        "refund_records", "return_orders", "return_items", "promotions",
        "promotion_usages", "suppliers", "supplier_ratings", "SupplierPayments",
        "purchase_orders", "purchase_order_items", "stock_tickets", "stock_ticket_items",
        "inventory_check_tickets", "inventory_check_details",
        "inventory_transfer_tickets", "inventory_transfer_details",
        "expense_vouchers", "expense_categories", "daily_summaries", "shifts",
        "loyalty_transactions", "staff_assignments"
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
        @"--", @"/\*"
    };

    public SqlExecutor(MEPDatabaseContext context, ILogger<SqlExecutor> logger, IConfiguration config)
    {
        _context = context;
        _logger = logger;
        // Connection string RIÊNG cho role read-only (mep_chatbot_ro) - xem
        // setup_chatbot_readonly_role.sql. Nếu chưa cấu hình, fallback về
        // connection của app chính (KHÔNG an toàn bằng, chỉ để không crash khi
        // bạn chưa kịp cấu hình - hãy thêm "ChatbotDbConnection" vào appsettings.json
        // càng sớm càng tốt).
        _chatbotConnectionString = config.GetConnectionString("ChatbotDbConnection");
        if (string.IsNullOrWhiteSpace(_chatbotConnectionString))
        {
            _logger.LogWarning(
                "ChatbotDbConnection chưa được cấu hình trong appsettings.json - " +
                "chatbot đang dùng chung connection với app chính (KHÔNG khuyến nghị). " +
                "Hãy thêm connection string dùng role mep_chatbot_ro để có bảo vệ DB-level thực sự.");
        }
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
    /// </summary>
    public async Task<(bool success, string message, object? data)> ExecuteQueryAsync(string sql, string? branchId)
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

    private (bool IsValid, string Message) ValidateSql(string sql)
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

        // Trích xuất TẤT CẢ tên bảng xuất hiện sau FROM/JOIN, hỗ trợ cả identifier
        // có ngoặc kép (PascalCase như "SupplierPayments") lẫn snake_case thường.
        var tableMatches = Regex.Matches(
            sql,
            @"(?:FROM|JOIN)\s+""?([A-Za-z_][A-Za-z0-9_]*)""?",
            RegexOptions.IgnoreCase);

        if (tableMatches.Count == 0)
        {
            return (false, "Không xác định được bảng nào trong câu SQL - từ chối để an toàn.");
        }

        foreach (Match m in tableMatches)
        {
            var tableName = m.Groups[1].Value;
            if (!AllowedTables.Contains(tableName))
            {
                // FAIL-CLOSED: bất kỳ bảng nào không nằm trong whitelist đều bị từ
                // chối ngay lập tức, thay vì chỉ bỏ qua như code cũ.
                return (false, $"Bảng '{tableName}' không được phép truy cập.");
            }
        }

        return (true, "OK");
    }

    private async Task<object?> ExecuteSelectAsync(string sql, string? branchId)
    {
        // Dùng connection RIÊNG (role mep_chatbot_ro, chỉ SELECT) thay vì connection
        // full-quyền của app chính, nếu đã cấu hình ChatbotDbConnection.
        // Đây là lớp bảo vệ DB-level thực sự - kể cả nếu ValidateSql phía trên có
        // sót lỗ hổng, DB vẫn tự chặn INSERT/UPDATE/DELETE hay bảng ngoài whitelist.
        await using var connection = !string.IsNullOrWhiteSpace(_chatbotConnectionString)
            ? new NpgsqlConnection(_chatbotConnectionString)
            : (NpgsqlConnection)_context.Database.GetDbConnection();

        var ownsConnection = !string.IsNullOrWhiteSpace(_chatbotConnectionString);
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

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

            // 🔍 DEBUG TẠM THỜI: log ra current_user và giá trị session variable
            // thực tế đang được DB nhìn thấy, để xác nhận app có đang connect
            // đúng role mep_chatbot_ro và branchId có được set đúng hay không.
            // XÓA đoạn debug này sau khi xác nhận RLS hoạt động đúng.
            await using (var debugCmd = connection.CreateCommand())
            {
                debugCmd.CommandText = "SELECT current_user, current_setting('app.current_branch_id', true);";
                await using var debugReader = await debugCmd.ExecuteReaderAsync();
                if (await debugReader.ReadAsync())
                {
                    _logger.LogWarning(
                        "DEBUG RLS: current_user={CurrentUser}, app.current_branch_id={BranchSetting}",
                        debugReader.GetValue(0), debugReader.GetValue(1));
                }
            }

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 5; // giây - khớp với statement_timeout đã set cho role trong SQL script

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

            return new
            {
                columns = columns,
                rows = results,
                totalRows = results.Count,
                truncated = results.Count >= maxRows
            };
        }
        finally
        {
            if (ownsConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}