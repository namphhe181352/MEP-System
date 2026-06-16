namespace MEP.Inventory.Api.DTOs
{
    public class ProductMediaDto
    {
        public string MediaUrl { get; set; } = null!;
        public string MediaType { get; set; } = "IMAGE"; // IMAGE hoặc VIDEO
        public short IsPrimary { get; set; } = 0; // 1 là ảnh đại diện, 0 là ảnh phụ
    }

    public class CreateProductDto
    {
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string? Barcode { get; set; }
        public string BaseUnit { get; set; } = null!;
        public string? Specification { get; set; }
        public string? ItemType { get; set; }
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public Guid? SupplierId { get; set; }

        // THÊM VÀO: Danh sách ảnh đi kèm khi tạo mới
        public List<ProductMediaDto> Medias { get; set; } = new();
    }

    public class UpdateProductDto
    {
        public string ProductName { get; set; } = null!;
        public string? Barcode { get; set; }
        public string? Specification { get; set; }
        public string? ItemType { get; set; }
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public Guid? SupplierId { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Width { get; set; }
        public decimal? Length { get; set; }
        public decimal? Height { get; set; }
        public string? SizeUnit { get; set; }

        // THÊM VÀO: Danh sách ảnh mới để đè/cập nhật lại ảnh cũ
        public List<ProductMediaDto> Medias { get; set; } = new();
    }

    // DTO cấu hình tham số Tìm kiếm & Phân trang
    public class ProductParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; } // Tìm kiếm theo mã hoặc tên sản phẩm
        public string? BrandName { get; set; }
        public Guid? SupplierId { get; set; }
    }


}
