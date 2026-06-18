using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class Favorite
{
    public int FavoriteId { get; set; }

    public int UserId { get; set; }

    public int PropertyId { get; set; }

    public virtual Property Property { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
