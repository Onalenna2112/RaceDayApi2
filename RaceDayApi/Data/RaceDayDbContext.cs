using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceDayApi.Models;

namespace RaceDayApi.Data
{
    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

        public DbSet<Organiser> Organisers { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Result> Results { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Fix decimal precision for payments
            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            // Fix multiple cascade paths for Enrollments (or related entities)
            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Event)
                .WithMany(ev => ev.Enrollments)
                .HasForeignKey(e => e.EventID)
                .OnDelete(DeleteBehavior.Restrict); // Prevents cascade delete conflict

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Participant)
                .WithMany(p => p.Enrollments)
                .HasForeignKey(e => e.ParticipantID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}