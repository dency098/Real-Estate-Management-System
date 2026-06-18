using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class PropertyAmenity
{
    public int ProperyAmenityId { get; set; }

    public int PropertyId { get; set; }

    public int AmenityId { get; set; }

    public virtual Amenity Amenity { get; set; } = null!;

    public virtual Property Property { get; set; } = null!;
}
