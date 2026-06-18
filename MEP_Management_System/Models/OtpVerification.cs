using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class OtpVerification
{
    public Guid OtpId { get; set; }

    public string Email { get; set; } = null!;

    public string OtpCode { get; set; } = null!;

    public string Purpose { get; set; } = null!;

    public DateTime ExpiredAt { get; set; }

    public short? IsUsed { get; set; }

    public DateTime? CreatedAt { get; set; }
}
