using Microsoft.EntityFrameworkCore;
using TheJourney.Api.Modules.Admin.Auth.Models;
using TheJourney.Api.Modules.Mobile.Auth.Models;

namespace TheJourney.Api.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<Admin> Admins { get; set; }
    public DbSet<LoginAttempt> LoginAttempts { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }
    public DbSet<StudentPasswordResetToken> StudentPasswordResetTokens { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
        });
        
        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.AdminId);
            entity.HasIndex(e => e.AttemptedAt);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            
            entity.HasOne(e => e.Admin)
                .WithMany()
                .HasForeignKey(e => e.AdminId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            entity.HasIndex(e => e.PhoneNumber).IsUnique().HasFilter("\"PhoneNumber\" IS NOT NULL");
        });

        modelBuilder.Entity<VerificationCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Purpose).IsRequired().HasMaxLength(12);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CodeHash).IsRequired();
            entity.Property(e => e.DeliveryTarget).HasMaxLength(255);
            entity.HasIndex(e => new { e.StudentId, e.Purpose });
            entity.HasIndex(e => new { e.StudentId, e.Channel, e.Purpose });

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudentPasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => e.ExpiresAt);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

