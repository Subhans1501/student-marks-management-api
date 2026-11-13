using MarksApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("students");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedOnAdd();

            entity.Property(s => s.RegistrationNo)
                .IsRequired()
                .HasMaxLength(20);
            entity.HasIndex(s => s.RegistrationNo)
                .IsUnique()
                .HasDatabaseName("ix_students_registration_no");

            entity.Property(s => s.FullName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(s => s.Email)
                .IsRequired()
                .HasMaxLength(150);
            entity.HasIndex(s => s.Email)
                .IsUnique()
                .HasDatabaseName("ix_students_email");

            entity.Property(s => s.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamptz");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses", t =>
            {
                t.HasCheckConstraint("ck_courses_credit_hours", "credit_hours >= 1 AND credit_hours <= 4");
            });
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedOnAdd();

            entity.Property(c => c.Code)
                .IsRequired()
                .HasMaxLength(10);
            entity.HasIndex(c => c.Code)
                .IsUnique()
                .HasDatabaseName("ix_courses_code");

            entity.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(c => c.CreditHours)
                .IsRequired();

            entity.Property(c => c.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamptz");
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("enrollments", t =>
            {
                t.HasCheckConstraint("ck_enrollments_mark", "mark IS NULL OR (mark >= 0 AND mark <= 100)");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.CourseId).IsRequired();
            entity.Property(e => e.Mark).IsRequired(false);
            entity.Property(e => e.EnrolledAtUtc)
                .IsRequired()
                .HasColumnType("timestamptz");

            // Use PostgreSQL's xmin system column for optimistic concurrency on mark updates
            if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                entity.Property(e => e.Version)
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
            else
            {
                entity.Property(e => e.Version).IsConcurrencyToken();
            }

            entity.HasOne(e => e.Student)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.StudentId, e.CourseId })
                .IsUnique()
                .HasDatabaseName("ix_enrollments_student_id_course_id");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", t =>
            {
                t.HasCheckConstraint("ck_users_role", "role IN ('Teacher', 'Student')");
            });
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).ValueGeneratedOnAdd();

            entity.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);
            entity.HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("ix_users_username");

            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasColumnType("text");

            entity.Property(u => u.Role)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(u => u.StudentId).IsRequired(false);

            entity.HasOne(u => u.Student)
                .WithOne(s => s.User)
                .HasForeignKey<User>(u => u.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

