using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class User
{
    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string ProfileImage { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ContactSeller> ContactSellers { get; set; } = new List<ContactSeller>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Property> Properties { get; set; } = new List<Property>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Transection> Transections { get; set; } = new List<Transection>();
}
