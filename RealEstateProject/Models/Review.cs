using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class Review
{
    public int ReviewId { get; set; }

    public int PropertyId { get; set; }

    public int UserId { get; set; }

    public int Rating { get; set; }

    public string Comment { get; set; } = null!;

    public byte[] CreatedAt { get; set; } = null!;

    public virtual Property Property { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
