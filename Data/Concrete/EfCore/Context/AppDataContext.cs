using Microsoft.EntityFrameworkCore;
using Model.Concrete;

namespace Data.Concrete.EfCore.Context
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions<AppDataContext> options) : base(options)
        {

        }
        public DbSet<User> Users { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            /// ProgressApprover Entity Configuration
            modelBuilder.Entity<ProgressApprover>(b =>
            {
                b.HasIndex(x => x.CustomerId);
                b.HasIndex(x => new { x.CustomerId, x.Email }).IsUnique();

                b.HasOne(x => x.Customer)
                 .WithMany()              // eğer Customer tarafında koleksiyon ekleyeceksen .WithMany(c => c.ProgressApprovers)
                 .HasForeignKey(x => x.CustomerId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            /// User Entity Configuration
            modelBuilder.Entity<UserRole>(e =>
            {
                e.ToTable("UserRole");

                // Aynı (UserId, RoleId) çifti bir kez bulunabilsin
                e.HasIndex(x => new { x.UserId, x.RoleId })
                 .IsUnique();

                // İlişkiler
                e.HasOne(x => x.User)
                 .WithMany(u => u.UserRoles)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Role)
                 .WithMany(r => r.UserRoles)
                 .HasForeignKey(x => x.RoleId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            /// ProductType Entity Configuration
            modelBuilder.Entity<ProductType>(e =>
            {
                e.ToTable("ProductType");
                e.Property(x => x.Type).HasMaxLength(100).IsRequired();
                e.Property(x => x.Code).HasMaxLength(50);
                e.HasIndex(x => x.Code).IsUnique(false);
            });

            /// CurrencyType Entity Configuration
            modelBuilder.Entity<CurrencyType>(e =>
            {
                e.ToTable("CurrencyType");
                e.Property(x => x.Code).HasMaxLength(10).IsRequired();
                e.Property(x => x.Name).HasMaxLength(100);
                e.HasIndex(x => x.Code).IsUnique(); // USD/EUR gibi benzersiz
            });

            /// Brand Entity Configuration
            modelBuilder.Entity<Brand>(e =>
            {
                e.ToTable("Brand");
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Desc).HasMaxLength(500);
                e.HasIndex(x => x.Name).IsUnique(false);
            });

            /// Model Entity Configuration
            modelBuilder.Entity<Model.Concrete.Model>(e =>
            {
                e.ToTable("Model");
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Desc).HasMaxLength(500);
                e.HasOne(x => x.Brand)
                 .WithMany(b => b.Models)
                 .HasForeignKey(x => x.BrandId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.BrandId, x.Name }).IsUnique(); // aynı markada aynı model tek
            });

            /// Product Entity Configuration
            modelBuilder.Entity<Product>(e =>
            {
                e.ToTable("Product");
                e.Property(x => x.ProductCode).HasMaxLength(100);
                e.Property(x => x.OracleProductCode).HasMaxLength(100);
                e.Property(x => x.SystemType).HasMaxLength(100);
                e.Property(x => x.Description).HasMaxLength(1000);
                e.Property(x => x.PriceCurrency).HasMaxLength(10);
                e.Property(x => x.Price).HasPrecision(18, 2);
                e.Property(x => x.CorporateCustomerShortCode).HasMaxLength(50);
                e.Property(x => x.OracleCustomerCode).HasMaxLength(100);

                e.HasOne(x => x.Brand)
                 .WithMany(b => b.Products)
                 .HasForeignKey(x => x.BrandId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Model)
                 .WithMany(m => m.Products)
                 .HasForeignKey(x => x.ModelId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.CurrencyType)
                 .WithMany(c => c.Products)
                 .HasForeignKey(x => x.CurrencyTypeId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.ProductType)
                 .WithMany(pt => pt.Products)
                 .HasForeignKey(x => x.ProductTypeId)
                 .OnDelete(DeleteBehavior.SetNull);

                // Tipik aramalar için akıllı indeksler
                e.HasIndex(x => x.ProductCode).IsUnique(false);
                e.HasIndex(x => x.OracleProductCode).IsUnique(false);
                e.HasIndex(x => new { x.BrandId, x.ModelId });
                e.HasIndex(x => new { x.ProductTypeId, x.CurrencyTypeId });
            });

            /// SystemType Entity Configuration
            modelBuilder.Entity<SystemType>(e =>
            {
                e.ToTable("SystemType");
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.Code).HasMaxLength(50);
                e.HasIndex(x => x.Code).IsUnique(false);
            });

            /// ServiceType Entity Configuration
            modelBuilder.Entity<ServiceType>(e =>
            {
                e.ToTable("ServiceType");
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.ContractNumber).HasMaxLength(50);
            });
        }
    }
}
