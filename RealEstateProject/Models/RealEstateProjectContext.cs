using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace RealEstateProject.Models;

public partial class RealEstateProjectContext : DbContext
{
    public RealEstateProjectContext()
    {
    }

    public RealEstateProjectContext(DbContextOptions<RealEstateProjectContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminCommission> AdminCommissions { get; set; }

    public virtual DbSet<Amenity> Amenities { get; set; }

    public virtual DbSet<City> Cities { get; set; }

    public virtual DbSet<ContactSeller> ContactSellers { get; set; }

    public virtual DbSet<Enquiry> Enquiries { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Property> Properties { get; set; }

    public virtual DbSet<PropertyAmenity> PropertyAmenities { get; set; }

    public virtual DbSet<PropertyCategory> PropertyCategories { get; set; }

    public virtual DbSet<ProperyImage> ProperyImages { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<State> States { get; set; }

    public virtual DbSet<Transection> Transections { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Data Source=LAPTOP-6LU5IHV3;Initial Catalog=RealEstateProject;Integrated Security=True;Encrypt=False");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<Enquiry>(entity =>
        {
            entity.Property(e => e.PropertyPrice).HasPrecision(18, 2);
            entity.Property(e => e.TokenAmount).HasPrecision(18, 2);
            entity.Property(e => e.CommissionAmount).HasPrecision(18, 2);
            entity.Property(e => e.AdminCommission).HasPrecision(18, 2);
            entity.Property(e => e.SecurityDepositAmount).HasPrecision(18, 2);
            entity.Property(e => e.FirstMonthRentAmount).HasPrecision(18, 2);
            entity.Property(e => e.BrokerageChargesAmount).HasPrecision(18, 2);
            entity.Property(e => e.AgreementChargesAmount).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyRentAmount).HasPrecision(18, 2);
        });
    
    modelBuilder.Entity<AdminCommission>(entity =>
        {
            entity.HasKey(e => e.CommissionId);

            entity.ToTable("Admin_commission");

            entity.Property(e => e.CommissionId).HasColumnName("commission_id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 0)")
                .HasColumnName("amount");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.TransectionId).HasColumnName("transectionId");

            entity.HasOne(d => d.Transection).WithMany(p => p.AdminCommissions)
                .HasForeignKey(d => d.TransectionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Admin_commission_Transection");
        });

        modelBuilder.Entity<Amenity>(entity =>
        {
            entity.ToTable("Amenity");

            entity.Property(e => e.AmenityId).HasColumnName("amenityId");
            entity.Property(e => e.AmenityName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("amenity_name");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("description");
        });

        modelBuilder.Entity<City>(entity =>
        {
            entity.ToTable("City");

            entity.Property(e => e.CityName)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.State).WithMany(p => p.Cities)
                .HasForeignKey(d => d.StateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_City_States");
        });

        modelBuilder.Entity<ContactSeller>(entity =>
        {
            entity.HasKey(e => e.ContactId);

            entity.ToTable("Contact_Seller");

            entity.Property(e => e.ContactId).HasColumnName("contactId");
            entity.Property(e => e.ContactDate)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("contact_date");
            entity.Property(e => e.Message)
                .HasColumnType("text")
                .HasColumnName("message");
            entity.Property(e => e.PropertyId).HasColumnName("propertyId");

            entity.HasOne(d => d.Property).WithMany(p => p.ContactSellers)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contact_Seller_Property");

            entity.HasOne(d => d.User).WithMany(p => p.ContactSellers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contact_Seller_User");
        });

        modelBuilder.Entity<Enquiry>(entity =>
        {
            entity.HasKey(e => e.EnquiryId).HasName("PK__Enquirie__0A019B7D4E4AF15A");

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.ToTable("Favorite");

            entity.Property(e => e.FavoriteId).HasColumnName("favoriteId");

            entity.HasOne(d => d.Property).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Favorite_Property");

            entity.HasOne(d => d.User).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Favorite_User");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Message).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasDefaultValue("Info");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Enquiry).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.EnquiryId)
                .HasConstraintName("FK_Notifications_Enquiries");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Notifications_User");
        });

        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasKey(e => e.ProperyId);

            entity.ToTable("Property");

            entity.Property(e => e.Address)
                .HasColumnType("text")
                .HasColumnName("address");
            entity.Property(e => e.AreaSqft).HasColumnName("area_sqft");
            entity.Property(e => e.Bathrooms).HasColumnName("bathrooms");
            entity.Property(e => e.Bedroom).HasColumnName("bedroom");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.Furnishing)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("furnishing");
            entity.Property(e => e.Pincode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("pincode");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(18, 0)")
                .HasColumnName("price");
            entity.Property(e => e.PropertyType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("property_type");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("title");

            entity.HasOne(d => d.Category).WithMany(p => p.Properties)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Property_Property_Categories");

            entity.HasOne(d => d.City).WithMany(p => p.Properties)
                .HasForeignKey(d => d.CityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Property_City");

            entity.HasOne(d => d.User).WithMany(p => p.Properties)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Property_User");
        });

        modelBuilder.Entity<PropertyAmenity>(entity =>
        {
            entity.HasKey(e => e.ProperyAmenityId);

            entity.ToTable("Property_amenity");

            entity.Property(e => e.ProperyAmenityId).HasColumnName("Propery_amenity_Id");
            entity.Property(e => e.AmenityId).HasColumnName("amenityId");

            entity.HasOne(d => d.Amenity).WithMany(p => p.PropertyAmenities)
                .HasForeignKey(d => d.AmenityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Property_amenity_Amenity");

            entity.HasOne(d => d.Property).WithMany(p => p.PropertyAmenities)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Property_amenity_Property");
        });

        modelBuilder.Entity<PropertyCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId);

            entity.ToTable("Property_Categories");

            entity.Property(e => e.CategoryName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ProperyImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("PK__Propery___7516F70CBBA8697E");

            entity.ToTable("Propery_Image");

            entity.Property(e => e.ImagePath)
                .HasMaxLength(500)
                .IsUnicode(false);

            entity.HasOne(d => d.Property).WithMany(p => p.ProperyImages)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Propery_I__Prope__29221CFB");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.Property(e => e.ReviewId).HasColumnName("reviewId");
            entity.Property(e => e.Comment)
                .HasColumnType("text")
                .HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("created_at");
            entity.Property(e => e.Rating).HasColumnName("rating");

            entity.HasOne(d => d.Property).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reviews_Property");

            entity.HasOne(d => d.User).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reviews_User");
        });

        modelBuilder.Entity<State>(entity =>
        {
            entity.Property(e => e.StateName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Transection>(entity =>
        {
            entity.ToTable("Transection");

            entity.Property(e => e.CommissionAmount)
                .HasColumnType("decimal(18, 0)")
                .HasColumnName("commission_amount");
            entity.Property(e => e.CommissionPercentage)
                .HasColumnType("decimal(18, 0)")
                .HasColumnName("commission_percentage");
            entity.Property(e => e.FinalPrice)
                .HasColumnType("decimal(18, 0)")
                .HasColumnName("final_price");
            //entity.Property(e => e.TrasectionDate)
            entity.Property(e => e.TransactionDate)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("TransactionDate");

            entity.HasOne(d => d.Property).WithMany(p => p.Transections)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Transection_Property");

            entity.HasOne(d => d.User).WithMany(p => p.Transections)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Transection_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime")
                .HasColumnName("Created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FullName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.Phone)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ProfileImage)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
