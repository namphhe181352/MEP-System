using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;

namespace MEP_Management_System.Services.Chatbot;

/// <summary>
/// Dispatcher chuyển tiếp tool calls từ Groq tới đúng Service method.
/// </summary>
public class ToolDispatcher
{
    private readonly IReportService _reportService;
    private readonly IProductService _productService;
    private readonly IInvoiceService _invoiceService;
    private readonly ISupplierDebtService _supplierDebtService;
    private readonly IInwardInventoryQueryService _inwardInventoryQueryService;

    public ToolDispatcher(
        IReportService reportService,
        IProductService productService,
        IInvoiceService invoiceService,
        ISupplierDebtService supplierDebtService,
        IInwardInventoryQueryService inwardInventoryQueryService)
    {
        _reportService = reportService;
        _productService = productService;
        _invoiceService = invoiceService;
        _supplierDebtService = supplierDebtService;
        _inwardInventoryQueryService = inwardInventoryQueryService;
    }

    /// <summary>
    /// Dispatch tool call tới đúng service method.
    /// </summary>
    /// <param name="toolName">Tên tool (theo định nghĩa trong ToolDefinitions)</param>
    /// <param name="arguments">JSON string chứa arguments từ Groq</param>
    /// <param name="currentUser">User hiện tại để lấy branch context</param>
    /// <returns>Kết quả dạng JSON string</returns>
    public async Task<string> DispatchAsync(string toolName, string arguments, ClaimsPrincipal currentUser)
    {
        JsonElement args;
        try
        {
            args = JsonSerializer.Deserialize<JsonElement>(arguments);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid JSON arguments: {arguments}", ex);
        }

        return toolName switch
        {
            #region Report Tools

            "get_daily_end_report" => await HandleGetDailyEndReport(args, currentUser),
            "get_stock_movement_report" => await HandleGetStockMovementReport(args, currentUser),
            "get_revenue_by_time_report" => await HandleGetRevenueByTimeReport(args, currentUser),
            "get_low_stock_report" => await HandleGetLowStockReport(args, currentUser),
            "get_product_profit_report" => await HandleGetProductProfitReport(args, currentUser),
            "get_supplier_detail_report" => await HandleGetSupplierDetailReport(args, currentUser),

            #endregion

            #region Quick Lookup Tools

            "get_product_by_id" => await HandleGetProductById(args, currentUser),
            "get_product_by_code" => await HandleGetProductByCode(args, currentUser),
            "search_products" => await HandleSearchProducts(args, currentUser),
            "get_product_price" => await HandleGetProductPrice(args, currentUser),
            "get_product_stock" => await HandleGetProductStock(args, currentUser),
            "get_invoice_by_id" => await HandleGetInvoiceById(args, currentUser),
            "get_supplier_debt_detail" => await HandleGetSupplierDebtDetail(args, currentUser),
            "get_inward_inventory" => await HandleGetInwardInventory(args, currentUser),

            #endregion

            _ => throw new ArgumentException($"Tool không được hỗ trợ: {toolName}")
        };
    }

    #region Report Tool Handlers

    private async Task<string> HandleGetDailyEndReport(JsonElement args, ClaimsPrincipal currentUser)
    {
        var date = args.TryGetProperty("date", out var dateEl)
            ? DateTime.Parse(dateEl.GetString()!)
            : DateTime.UtcNow.Date;

        var request = new DailyEndReportRequestDto { Date = date };
        var result = await _reportService.GetDailyEndReportAsync(request, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetStockMovementReport(JsonElement args, ClaimsPrincipal currentUser)
    {
        var now = DateTime.UtcNow.Date;
        var defaultFromDate = now.AddDays(-30);

        DateTime fromDate;
        DateTime toDate;

        try
        {
            fromDate = args.TryGetProperty("fromDate", out var fromEl) && fromEl.ValueKind != JsonValueKind.Null
                ? DateTime.Parse(fromEl.GetString()!)
                : defaultFromDate;
            toDate = args.TryGetProperty("toDate", out var toEl) && toEl.ValueKind != JsonValueKind.Null
                ? DateTime.Parse(toEl.GetString()!)
                : now;
        }
        catch
        {
            fromDate = defaultFromDate;
            toDate = now;
        }

        // Validate and clamp dates to reasonable range
        if (fromDate > now) fromDate = now.AddDays(-1);
        if (toDate > now) toDate = now;
        if (fromDate > toDate) fromDate = toDate.AddDays(-30);

        var categoryName = args.TryGetProperty("categoryName", out var catEl) && catEl.ValueKind != JsonValueKind.Null
            ? catEl.GetString()
            : null;
        var productId = args.TryGetProperty("productId", out var pidEl) && pidEl.ValueKind != JsonValueKind.Null
            ? Guid.Parse(pidEl.GetString()!)
            : (Guid?)null;

        var request = new StockMovementReportRequestDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            CategoryName = categoryName,
            ProductId = productId
        };
        var result = await _reportService.GetStockMovementReportAsync(request, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetRevenueByTimeReport(JsonElement args, ClaimsPrincipal currentUser)
    {
        var now = DateTime.UtcNow.Date;
        DateTime fromDate;
        DateTime toDate;

        try
        {
            fromDate = args.TryGetProperty("fromDate", out var fromEl) && fromEl.ValueKind != JsonValueKind.Null
                ? DateTime.Parse(fromEl.GetString()!)
                : now.AddDays(-30);
            toDate = args.TryGetProperty("toDate", out var toEl) && toEl.ValueKind != JsonValueKind.Null
                ? DateTime.Parse(toEl.GetString()!)
                : now;
        }
        catch
        {
            fromDate = now.AddDays(-30);
            toDate = now;
        }

        if (fromDate > now) fromDate = now;
        if (toDate > now) toDate = now;
        if (fromDate > toDate) fromDate = toDate.AddDays(-30);

        var timeGrouping = args.TryGetProperty("timeGrouping", out var tgEl) && tgEl.ValueKind != JsonValueKind.Null
            ? tgEl.GetString()
            : "day";

        var request = new RevenueByTimeRequestDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimeGrouping = timeGrouping ?? "day"
        };
        var result = await _reportService.GetRevenueByTimeReportAsync(request, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetLowStockReport(JsonElement args, ClaimsPrincipal currentUser)
    {
        var includeZeroStock = args.TryGetProperty("includeZeroStock", out var izsEl) && izsEl.ValueKind == JsonValueKind.True;

        var request = new LowStockReportRequestDto { IncludeZeroStock = includeZeroStock };
        var result = await _reportService.GetLowStockReportAsync(request, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetProductProfitReport(JsonElement args, ClaimsPrincipal currentUser)
    {
        var now = DateTime.UtcNow.Date;
        DateTime fromDate;
        DateTime toDate;

        try
        {
            fromDate = args.TryGetProperty("fromDate", out var fromEl) && fromEl.ValueKind != JsonValueKind.Null
                ? DateTime.Parse(fromEl.GetString()!)
                : now.AddDays(-30);
            toDate = args.TryGetProperty("toDate", out var toEl) && toEl.ValueKind != JsonValueKind.Null
                ? DateTime.Parse(toEl.GetString()!)
                : now;
        }
        catch
        {
            fromDate = now.AddDays(-30);
            toDate = now;
        }

        if (fromDate > now) fromDate = now;
        if (toDate > now) toDate = now;
        if (fromDate > toDate) fromDate = toDate.AddDays(-30);

        var categoryName = args.TryGetProperty("categoryName", out var catEl) && catEl.ValueKind != JsonValueKind.Null
            ? catEl.GetString()
            : null;
        var sortBy = args.TryGetProperty("sortBy", out var sortEl) && sortEl.ValueKind != JsonValueKind.Null
            ? sortEl.GetString()
            : "revenue";

        var request = new ProductProfitReportRequestDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            CategoryName = categoryName,
            SortBy = sortBy ?? "revenue"
        };
        var result = await _reportService.GetProductProfitReportAsync(request, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetSupplierDetailReport(JsonElement args, ClaimsPrincipal currentUser)
    {
        var supplierId = args.TryGetProperty("supplierId", out var sidEl)
            ? Guid.Parse(sidEl.GetString()!)
            : throw new ArgumentException("Thiếu supplierId");
        var year = args.TryGetProperty("year", out var yearEl) && yearEl.ValueKind != JsonValueKind.Null
            ? yearEl.GetInt32()
            : (int?)null;
        var month = args.TryGetProperty("month", out var monthEl) && monthEl.ValueKind != JsonValueKind.Null
            ? monthEl.GetInt32()
            : (int?)null;

        var request = new SupplierDetailRequestDto
        {
            SupplierId = supplierId,
            Year = year,
            Month = month
        };
        var result = await _reportService.GetSupplierDetailReportAsync(request, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    #endregion

    #region Quick Lookup Tool Handlers

    private async Task<string> HandleGetProductById(JsonElement args, ClaimsPrincipal currentUser)
    {
        var productId = args.TryGetProperty("productId", out var pidEl)
            ? Guid.Parse(pidEl.GetString()!)
            : throw new ArgumentException("Thiếu productId");

        var result = await _productService.GetProductByIdAsync(productId, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetProductByCode(JsonElement args, ClaimsPrincipal currentUser)
    {
        var productCode = args.TryGetProperty("productCode", out var codeEl)
            ? codeEl.GetString()
            : throw new ArgumentException("Thiếu productCode");

        if (string.IsNullOrWhiteSpace(productCode))
            throw new ArgumentException("Thiếu productCode");

        var result = await _productService.GetProductByCodeAsync(productCode.Trim(), currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleSearchProducts(JsonElement args, ClaimsPrincipal currentUser)
    {
        var keyword = args.TryGetProperty("keyword", out var kwEl)
            ? kwEl.GetString()
            : throw new ArgumentException("Thiếu keyword");

        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("Thiếu keyword");

        var result = await _productService.SearchProductsAsync(keyword.Trim(), currentUser);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Log for debugging
        Console.WriteLine($"[ToolDispatcher] search_products keyword='{keyword}' result count={result?.Count ?? 0}");

        return json;
    }

    private async Task<string> HandleGetProductPrice(JsonElement args, ClaimsPrincipal currentUser)
    {
        var productId = args.TryGetProperty("productId", out var pidEl)
            ? Guid.Parse(pidEl.GetString()!)
            : throw new ArgumentException("Thiếu productId");

        var result = await _productService.GetProductPriceAsync(productId, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetProductStock(JsonElement args, ClaimsPrincipal currentUser)
    {
        var productId = args.TryGetProperty("productId", out var pidEl)
            ? Guid.Parse(pidEl.GetString()!)
            : throw new ArgumentException("Thiếu productId");

        var result = await _productService.GetProductStockAsync(productId, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetInvoiceById(JsonElement args, ClaimsPrincipal currentUser)
    {
        var invoiceId = args.TryGetProperty("invoiceId", out var iidEl)
            ? Guid.Parse(iidEl.GetString()!)
            : throw new ArgumentException("Thiếu invoiceId");

        var result = await _invoiceService.GetInvoiceByIdAsync(invoiceId, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetSupplierDebtDetail(JsonElement args, ClaimsPrincipal currentUser)
    {
        var supplierId = args.TryGetProperty("supplierId", out var sidEl)
            ? Guid.Parse(sidEl.GetString()!)
            : throw new ArgumentException("Thiếu supplierId");

        var result = await _supplierDebtService.GetSupplierDebtDetailAsync(supplierId, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private async Task<string> HandleGetInwardInventory(JsonElement args, ClaimsPrincipal currentUser)
    {
        var status = args.TryGetProperty("status", out var statusEl) && statusEl.ValueKind != JsonValueKind.Null
            ? statusEl.GetString()
            : null;

        var result = await _inwardInventoryQueryService.GetInwardInventoryAsync(status, currentUser);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    #endregion
}
