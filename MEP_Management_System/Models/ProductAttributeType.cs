using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.Models
{
    /// <summary>
    /// Bảng lưu các loại thuộc tính sản phẩm (phù hợp ngành kim khí điện nước).
    /// VD: Kích thước, Chất liệu, Điện áp, Công suất, Màu sắc, Quy cách
    /// </summary>
    public class ProductAttributeType
    {
        [Key]
        public Guid TypeId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string TypeName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? TypeCode { get; set; }

        public short IsActive { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public Guid? UpdatedBy { get; set; }
    }
}
