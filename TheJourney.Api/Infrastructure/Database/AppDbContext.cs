using Microsoft.EntityFrameworkCore;
using TheJourney.Api.Modules.Admin.Auth.Models;
using TheJourney.Api.Modules.Admin.CareerFramework.Models;
using TheJourney.Api.Modules.Mobile.Assessment.Models;
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
    public DbSet<Industry> Industries { get; set; }
    public DbSet<Major> Majors { get; set; }
    public DbSet<JobRole> JobRoles { get; set; }
    public DbSet<AssessmentTemplate> AssessmentTemplates { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<AssessmentTemplateSkill> AssessmentTemplateSkills { get; set; }
    public DbSet<RoleSpecificQuestion> RoleSpecificQuestions { get; set; }
    public DbSet<TrainingResource> TrainingResources { get; set; }
    public DbSet<AssessmentTemplateSkillTraining> AssessmentTemplateSkillTrainings { get; set; }
    public DbSet<MajorIndustryMapping> MajorIndustryMappings { get; set; }
    public DbSet<StudentAssessment> StudentAssessments { get; set; }
    public DbSet<AssessmentAnswer> AssessmentAnswers { get; set; }
    
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
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
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

        modelBuilder.Entity<Industry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Major>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.Industry)
                .WithMany(i => i.Majors)
                .HasForeignKey(e => e.IndustryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobRole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(150);
            entity.Property(e => e.ShortDescription).HasMaxLength(255);
            entity.Property(e => e.RequiredQualification).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.Major)
                .WithMany(m => m.JobRoles)
                .HasForeignKey(e => e.MajorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssessmentTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.JobRole)
                .WithMany(r => r.AssessmentTemplates)
                .HasForeignKey(e => e.JobRoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.JobRoleId, e.Version }).IsUnique();
        });

        modelBuilder.Entity<Skill>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<AssessmentTemplateSkill>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequiredProficiencyLevel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Weight).HasColumnType("numeric(5,4)").HasDefaultValue(0.1m);
            entity.Property(e => e.IsRequired).HasDefaultValue(true);

            entity.HasOne(e => e.AssessmentTemplate)
                .WithMany(t => t.SkillMatrix)
                .HasForeignKey(e => e.AssessmentTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Skill)
                .WithMany(s => s.TemplateSkills)
                .HasForeignKey(e => e.SkillId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.AssessmentTemplateId, e.SkillId }).IsUnique();
        });

        modelBuilder.Entity<RoleSpecificQuestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuestionText).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.QuestionType).HasMaxLength(50);

            entity.HasOne(e => e.AssessmentTemplate)
                .WithMany(t => t.Questions)
                .HasForeignKey(e => e.AssessmentTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingResource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Duration).HasMaxLength(50);
            entity.Property(e => e.ResourceType).HasMaxLength(50);
            entity.Property(e => e.ExternalUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<AssessmentTemplateSkillTraining>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Priority).HasDefaultValue(1);

            entity.HasOne(e => e.AssessmentTemplateSkill)
                .WithMany(s => s.TrainingRecommendations)
                .HasForeignKey(e => e.AssessmentTemplateSkillId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TrainingResource)
                .WithMany(r => r.TrainingMappings)
                .HasForeignKey(e => e.TrainingResourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.AssessmentTemplateSkillId, e.TrainingResourceId }).IsUnique();
        });

        modelBuilder.Entity<MajorIndustryMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Priority).HasDefaultValue(1);

            entity.HasOne(e => e.Major)
                .WithMany(m => m.SuggestedIndustries)
                .HasForeignKey(e => e.MajorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Industry)
                .WithMany()
                .HasForeignKey(e => e.IndustryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MajorId, e.IndustryId }).IsUnique();
        });

        modelBuilder.Entity<StudentAssessment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.JobRole)
                .WithMany()
                .HasForeignKey(e => e.JobRoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AssessmentTemplate)
                .WithMany()
                .HasForeignKey(e => e.AssessmentTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssessmentAnswer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProficiencyLevel).HasMaxLength(50);

            entity.HasOne(e => e.StudentAssessment)
                .WithMany(a => a.Answers)
                .HasForeignKey(e => e.StudentAssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RoleSpecificQuestion)
                .WithMany()
                .HasForeignKey(e => e.RoleSpecificQuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Skill)
                .WithMany()
                .HasForeignKey(e => e.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

