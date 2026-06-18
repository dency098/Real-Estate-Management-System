using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class Amenity
{
    public int AmenityId { get; set; }

    public string AmenityName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();
}
