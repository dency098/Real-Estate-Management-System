using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class PropertyCategory
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public virtual ICollection<Property> Properties { get; set; } = new List<Property>();
}
