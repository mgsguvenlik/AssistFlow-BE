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

            // Birleşik anahtar tanımı

        }
    }
}
