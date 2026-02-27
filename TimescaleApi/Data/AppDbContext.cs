using Microsoft.EntityFrameworkCore;
using TimescaleApi.Models;

namespace TimescaleApi.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Values> Values { get; set; }
        public DbSet<Result> Results { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Values>()
                .Property(v => v.FileName)
                .IsRequired();

            modelBuilder.Entity<Values>()
                .HasIndex(v => v.FileName);

            modelBuilder.Entity<Result>()
                .HasIndex(v => v.FileName)
                .IsUnique();

            modelBuilder.Entity<Values>()
                .Property(v => v.Date)
                .HasColumnType("timestamp with time zone");

            modelBuilder.Entity<Result>()
                .Property(r => r.MinDate)
                .HasColumnType("timestamp with time zone");
        }
    }
}
