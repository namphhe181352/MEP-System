namespace MEP_Management_System.DTOs.InventoryDTOs
{
    // ========== PRODUCT DTO (Request/Response) ==========
    // Lưu ý: ApiResponse<T> và PaginatedResponse<T> được định nghĩa ở
    // MEP_Management_System.DTOs (ApiResponse.cs) để dùng chung cho toàn project.
    // KHÔNG định nghĩa lại ở đây để tránh xung đột tên khi build.

    public class ProductUpsertDto
    {
        // Identity
        public Guid? ProductId { get; set; }
        public string? Id { get; set; } // For Frontend compatibility
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;

        // Basic Info
        public string? Barcode { get; set; }
        public string Unit { get; set; } = null!; // Maps to BaseUnit
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? ItemType { get; set; }

        // Pricing
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }

        // Stock
        public decimal ActualStock { get; set; } // Current quantity
        public decimal ReservedStock { get; set; }
        public decimal AvailableStock { get; set; }
        public decimal? MinimumStock { get; set; }
        public decimal? MaximumStock { get; set; }

        // Location & Dimensions
        public string? ShelfLocation { get; set; }
        public List<string> ShelfLocations { get; set; } = new();
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Width { get; set; }
        public decimal? Length { get; set; }
        public decimal? Height { get; set; }
        public string? Specification { get; set; }
        public string? SpecificationDetail { get; set; }

        // Media
        public string? ImageUrl { get; set; }
        public List<string> Images { get; set; } = new();

        // Sales & Status
        public bool DirectSale { get; set; }
        public bool SalesChannelLinked { get; set; }
        public bool IsActive { get; set; }

        // Nested
        public List<ProductAttributeDto> Attributes { get; set; } = new();
        public List<ConversionUnitDto> ConversionUnits { get; set; } = new();

        // Timestamps
        public DateTime? EstimatedOutAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Frontend display fields
        public string? Status { get; set; } // Sẵn hàng, Hết hàng, Sắp hết
        public string? StatusTone { get; set; } // green, red, amber
        public string? ProductStatus { get; set; } // active, inactive, draft
    }

    public class ProductAttributeDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = null!;
        public string Value { get; set; } = null!;
    }

    public class ConversionUnitDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Rate { get; set; } // e.g. 10 (1 Thùng = 10 Bao)
        public string? Note { get; set; }

        // Legacy fields for backward compatibility
        public decimal ConvertValue { get; set; }
        public string? ConvertFrom { get; set; }
        public decimal Price { get; set; }
        public bool DirectSale { get; set; }
    }

    // ========== PRODUCT PARAMETERS (Query Filters) ==========
    public class ProductParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public string? SearchTerm { get; set; }
        public string? Sort { get; set; }
        public string? Order { get; set; } = "asc";
        public string? CategoryName { get; set; }
        public string? BrandName { get; set; }
        public string? Status { get; set; } // active, inactive, draft
        public Guid? SupplierId { get; set; }
    }
}
