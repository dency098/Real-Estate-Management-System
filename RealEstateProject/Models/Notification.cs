using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RealEstateProject.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int? UserId { get; set; }

    public int? EnquiryId { get; set; }
    [StringLength(500)]
    public string Message { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string Type { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Enquiry? Enquiry { get; set; }

    public virtual User? User { get; set; }
}
