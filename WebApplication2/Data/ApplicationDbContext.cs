using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;
namespace WebApplication2.Data
{





    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
              .HasIndex(u => u.Email)
              .IsUnique();
        }
    }





    //public class ApplicationDbContext(DbContextOptions options) : DbContext(options)
    //{

    //    public DbSet<Villa> Villa { get; set; }
    //    public DbSet<User> Users { get; set; }

    //    protected override void OnModelCreating(ModelBuilder modelBuilder)
    //    {

    //        base.OnModelCreating(modelBuilder);
    //        modelBuilder.Entity<Villa>().HasData(
    //            new Villa
    //            {
    //                Id = 1,
    //                Name = "Royal Villa",
    //                Details = "This is the Royal Villa",
    //                Rate = 200.0,
    //                Sqft = 550,
    //                Occupancy = 4,
    //                ImgUrl = "https://dotnetmasteryimages.blob.core.windows.net/bluevillaimages/villa3.jpg"
    //            },
    //            new Villa
    //            {
    //                Id = 2,
    //                Name = "Premium Pool Villa",
    //                Details = "This is the Premium Pool Villa",
    //                Rate = 300.0,
    //                Sqft = 550,
    //                Occupancy = 4,
    //                ImgUrl = "https://dotnetmasteryimages.blob.core.windows.net/bluevillaimages/villa1.jpg"
    //            }
    //        );
    //    }
    //}

}
