using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class ContactSeller
{
    public int ContactId { get; set; }

    public int PropertyId { get; set; }

    public int UserId { get; set; }

    public string Message { get; set; } = null!;

    public byte[] ContactDate { get; set; } = null!;

    public virtual Property Property { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
