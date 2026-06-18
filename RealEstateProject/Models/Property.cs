using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace RealEstateProject.Models;

public partial class Property
{
    public int ProperyId { get; set; }

    public int UserId { get; set; }

    public int CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal Price { get; set; }

    public string PropertyType { get; set; } = null!;

    public int AreaSqft { get; set; }

    public int Bedroom { get; set; }

    public int Bathrooms { get; set; }

    public string Furnishing { get; set; } = null!;

    public string Address { get; set; } = null!;

    public int CityId { get; set; }

    public string Pincode { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? VideoUrl { get; set; }

    public virtual PropertyCategory Category { get; set; } = null!;

    public virtual City City { get; set; } = null!;

    public virtual ICollection<ContactSeller> ContactSellers { get; set; } = new List<ContactSeller>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();

    public virtual ICollection<ProperyImage> ProperyImages { get; set; } = new List<ProperyImage>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Transection> Transections { get; set; } = new List<Transection>();

    public virtual User User { get; set; } = null!;
}
