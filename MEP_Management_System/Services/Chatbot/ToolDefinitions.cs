using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace MEP_Management_System.Services.Chatbot;

/// <summary>
/// Định nghĩa tool cho Groq function calling - theo chuẩn OpenAI/Groq JSON schema.
/// </summary>
public static class ToolDefinitions
{
    /// <summary>
    /// Lấy danh sách tất cả tool definitions cho Groq.
    /// </summary>
    public static List<ToolDefinition> GetAllTools()
    {
        return new List<ToolDefinition>
        {
            #region Report Tools

            new ToolDefinition
            {
                Name = "get_daily_end_report",
                Description = "Lấy báo cáo cuối ngày: tổng doanh thu, số đơn hàng, chi tiết thanh toán (tiền mặt, chuyển khoản, nợ), số phiếu nhập/xuất trong ngày. Dùng khi user hỏi 'báo cáo hôm nay', 'doanh thu hôm nay', 'tổng kết ngày'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["date"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Ngày cần lấy báo cáo, định dạng YYYY-MM-DD. Mặc định là hôm nay.",
                            Format = "date"
                        }
                    },
                    Required = new List<string>()
                }
            },

            new ToolDefinition
            {
                Name = "get_stock_movement_report",
                Description = "Lấy báo cáo xuất nhập tồn kho: số lượng tồn đầu, nhập, xuất, tồn cuối và giá trị tồn kho theo từng sản phẩm. Dùng khi user hỏi 'báo cáo tồn kho', 'nhập xuất tồn', 'biến động kho', 'tồn kho bao nhiêu'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["fromDate"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Từ ngày, định dạng YYYY-MM-DD.",
                            Format = "date"
                        },
                        ["toDate"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Đến ngày, định dạng YYYY-MM-DD.",
                            Format = "date"
                        },
                        ["categoryName"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Tên nhóm sản phẩm để lọc (tùy chọn). Ví dụ: 'Điện thoại', 'Laptop', 'Phụ kiện'."
                        },
                        ["productId"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "ID sản phẩm cụ thể để lọc (tùy chọn). Định dạng GUID."
                        }
                    },
                    Required = new List<string> { "fromDate", "toDate" }
                }
            },

            new ToolDefinition
            {
                Name = "get_revenue_by_time_report",
                Description = "Lấy báo cáo doanh thu theo thời gian: biểu đồ và bảng doanh thu theo ngày/tuần/tháng. Dùng khi user hỏi 'doanh thu tháng này', 'doanh thu tuần trước', 'biểu đồ doanh thu', 'so sánh doanh thu'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["fromDate"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Từ ngày, định dạng YYYY-MM-DD.",
                            Format = "date"
                        },
                        ["toDate"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Đến ngày, định dạng YYYY-MM-DD.",
                            Format = "date"
                        },
                        ["timeGrouping"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Nhóm theo: 'day' (ngày), 'week' (tuần), 'month' (tháng). Mặc định là 'day'.",
                            Enum = new List<string> { "day", "week", "month" }
                        }
                    },
                    Required = new List<string> { "fromDate", "toDate" }
                }
            },

            new ToolDefinition
            {
                Name = "get_low_stock_report",
                Description = "Lấy báo cáo tồn kho sắp hết: danh sách sản phẩm có tồn kho thấp hơn mức tối thiểu (MinStock). Dùng khi user hỏi 'sản phẩm sắp hết hàng', 'tồn kho thấp', 'cần nhập thêm', 'hàng cần bổ sung'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["includeZeroStock"] = new ToolProperty
                        {
                            Type = "boolean",
                            Description = "Bao gồm cả sản phẩm tồn = 0. Mặc định là true.",
                            Default = true
                        }
                    },
                    Required = new List<string>()
                }
            },

            new ToolDefinition
            {
                Name = "get_product_profit_report",
                Description = "Lấy báo cáo doanh thu và lợi nhuận theo từng sản phẩm: số lượng bán, doanh thu, giá vốn, lợi nhuận, tỷ lệ lợi nhuận. Dùng khi user hỏi 'lợi nhuận sản phẩm', 'doanh thu theo sản phẩm', 'sản phẩm nào bán chạy', 'top sản phẩm'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["fromDate"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Từ ngày, định dạng YYYY-MM-DD.",
                            Format = "date"
                        },
                        ["toDate"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Đến ngày, định dạng YYYY-MM-DD.",
                            Format = "date"
                        },
                        ["categoryName"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Tên nhóm sản phẩm để lọc (tùy chọn)."
                        },
                        ["sortBy"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Sắp xếp theo: 'revenue' (doanh thu), 'profit' (lợi nhuận), 'quantity' (số lượng). Mặc định là 'revenue'.",
                            Enum = new List<string> { "revenue", "profit", "quantity" }
                        }
                    },
                    Required = new List<string> { "fromDate", "toDate" }
                }
            },

            new ToolDefinition
            {
                Name = "get_supplier_detail_report",
                Description = "Lấy chi tiết nhà cung cấp: thông tin liên hệ, công nợ tổng cộng, công nợ theo tháng, lịch sử mua hàng, lịch sử thanh toán. Dùng khi user hỏi 'công nợ nhà cung cấp', 'thông tin NCC', 'mua hàng từ NCC', 'trả tiền NCC'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["supplierId"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "ID nhà cung cấp. Định dạng GUID.",
                            Format = "uuid"
                        },
                        ["year"] = new ToolProperty
                        {
                            Type = "integer",
                            Description = "Năm để lọc công nợ theo năm (tùy chọn)."
                        },
                        ["month"] = new ToolProperty
                        {
                            Type = "integer",
                            Description = "Tháng để lọc công nợ theo tháng (1-12, tùy chọn)."
                        }
                    },
                    Required = new List<string> { "supplierId" }
                }
            },

            #endregion

            #region Quick Lookup Tools

            new ToolDefinition
            {
                Name = "get_product_by_id",
                Description = "Tra cứu thông tin chi tiết một sản phẩm theo ID: tên, mã, giá, tồn kho, nhóm sản phẩm, nhà cung cấp. Dùng khi user hỏi 'sản phẩm SP028', 'thông tin sản phẩm', 'chi tiết sản phẩm'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["productId"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "ID sản phẩm. Định dạng GUID.",
                            Format = "uuid"
                        }
                    },
                    Required = new List<string> { "productId" }
                }
            },

            new ToolDefinition
            {
                Name = "get_product_by_code",
                Description = "Tra cứu thông tin chi tiết một sản phẩm theo MÃ sản phẩm (ví dụ: SP016, SP028): tên, mã, giá, tồn kho, nhóm sản phẩm. Dùng khi user hỏi 'sản phẩm SP016 còn bao nhiêu', 'tồn kho SP028', 'thông tin sản phẩm SP016'. ĐÂY LÀ TOOL ƯU TIÊN khi user cung cấp mã sản phẩm thay vì ID.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["productCode"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Mã sản phẩm (ví dụ: SP016, SP028, ĐT001)."
                        }
                    },
                    Required = new List<string> { "productCode" }
                }
            },

            new ToolDefinition
            {
                Name = "search_products",
                Description = "Tìm kiếm sản phẩm theo từ khóa (tên sản phẩm, mã sản phẩm, thương hiệu, hoặc danh mục). LUÔN LUÔN dùng tool này khi user hỏi: 'có những loại [tên] nào', 'tìm sản phẩm điện', 'danh sách sản phẩm Aptomat', 'trong kho có những loại sản phẩm nào', 'xem sản phẩm [tên]'. ĐÂY LÀ TOOL ƯU TIÊN khi user muốn xem danh sách hoặc tìm sản phẩm theo từ khóa (không cần ID).",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["keyword"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Từ khóa tìm kiếm: tên sản phẩm, mã sản phẩm, thương hiệu, hoặc danh mục. Ví dụ: 'Aptomat', 'Điện thoại', 'Laptop', 'Samsung'."
                        }
                    },
                    Required = new List<string> { "keyword" }
                }
            },

            new ToolDefinition
            {
                Name = "get_product_price",
                Description = "Lấy giá bán của một sản phẩm: giá bán lẻ, giá bán buôn (nếu có). Dùng khi user hỏi 'giá sản phẩm', 'bao nhiêu tiền', 'giá SP028'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["productId"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "ID sản phẩm. Định dạng GUID.",
                            Format = "uuid"
                        }
                    },
                    Required = new List<string> { "productId" }
                }
            },

            new ToolDefinition
            {
                Name = "get_product_stock",
                Description = "Lấy số lượng tồn kho của một sản phẩm. Dùng khi user hỏi 'còn bao nhiêu', 'tồn kho bao nhiêu', 'hết hàng chưa', 'còn hàng không'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["productId"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "ID sản phẩm. Định dạng GUID.",
                            Format = "uuid"
                        }
                    },
                    Required = new List<string> { "productId" }
                }
            },

            new ToolDefinition
            {
                Name = "get_invoice_by_id",
                Description = "Tra cứu thông tin hóa đơn bán hàng theo ID: khách hàng, sản phẩm, tổng tiền, phương thức thanh toán, trạng thái. Dùng khi user hỏi 'hóa đơn này', 'chi tiết đơn hàng', 'xem đơn'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["invoiceId"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "ID hóa đơn. Định dạng GUID.",
                            Format = "uuid"
                        }
                    },
                    Required = new List<string> { "invoiceId" }
                }
            },

            new ToolDefinition
            {
                Name = "get_supplier_debt_detail",
                Description = "Lấy chi tiết công nợ của một nhà cung cấp: tổng nợ, đã trả, còn nợ, số đơn chưa thanh toán. Dùng khi user hỏi 'nợ NCC bao nhiêu', 'công nợ của', 'trả bao nhiêu rồi'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["supplierId"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "ID nhà cung cấp. Định dạng GUID.",
                            Format = "uuid"
                        }
                    },
                    Required = new List<string> { "supplierId" }
                }
            },

            new ToolDefinition
            {
                Name = "get_inward_inventory",
                Description = "Lấy danh sách phiếu nhập kho (phiếu nhập hàng): mã phiếu, ngày nhập, nhà cung cấp, tổng tiền, trạng thái. Dùng khi user hỏi 'phiếu nhập kho', 'danh sách nhập hàng', 'nhập kho'.",
                Parameters = new ToolParameter
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["status"] = new ToolProperty
                        {
                            Type = "string",
                            Description = "Lọc theo trạng thái: 'Pending' (chờ duyệt), 'Approved' (đã duyệt), 'Rejected' (từ chối). Để trống lấy tất cả."
                        }
                    },
                    Required = new List<string>()
                }
            }

            #endregion
        };
    }

    /// <summary>
    /// Chuyển đổi tool definitions sang định dạng Groq/OpenAI.
    /// </summary>
    public static List<GroqTool> GetGroqTools()
    {
        var tools = GetAllTools();
        var result = new List<GroqTool>();

        foreach (var tool in tools)
        {
            result.Add(new GroqTool
            {
                Type = "function",
                Function = new GroqFunction
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = ConvertToOpenAiSchema(tool.Parameters)
                }
            });
        }

        return result;
    }

    /// <summary>
    /// Chuyển đổi ToolParameter sang JSON Schema chuẩn OpenAI.
    /// </summary>
    private static JsonElement ConvertToOpenAiSchema(ToolParameter param)
    {
        var json = JsonSerializer.Serialize(param, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    #region JSON Schema Models

    public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ToolParameter Parameters { get; set; } = new();
}

public class ToolParameter
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, ToolProperty> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

public class ToolProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("default")]
    public object? Default { get; set; }
}
    #endregion

    #region Groq/OpenAI Format

public class GroqTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public GroqFunction Function { get; set; } = new();
}

public class GroqFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }
}
    #endregion
}
