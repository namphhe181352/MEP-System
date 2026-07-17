using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MEP_Management_System.Models;

[Table("product_taxonomies")]
public class ProductTaxonomy
{
    [Key]
    [Column("taxonomy_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid TaxonomyId { get; set; }

    /// <summary>
    /// Branch ID để phân biệt taxonomy theo từng chi nhánh.
    /// Null = taxonomy toàn hệ thống (legacy).
    /// </summary>
    [Column("branch_id")]
    public Guid? BranchId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("type")]
    public string Type { get; set; } = null!; // "Category" or "Brand"

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BranchId")]
    public virtual Branch? Branch { get; set; }
}
