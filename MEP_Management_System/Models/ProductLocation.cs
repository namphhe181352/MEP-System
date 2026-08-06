using System;

namespace MEP_Management_System.Models
{
    public class ProductLocation
    {
        public Guid LocationId { get; set; }
        public Guid? BranchId { get; set; }
        public string LocationName { get; set; } = null!;
        public string? LocationCode { get; set; }
        public string? Description { get; set; }
        public short IsActive { get; set; } = 1;
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}
