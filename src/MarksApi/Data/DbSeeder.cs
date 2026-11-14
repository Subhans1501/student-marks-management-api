using MarksApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        logger.LogInformation("Seeding initial development data into database...");

        var now = DateTime.UtcNow;

        var student1 = new Student
        {
            RegistrationNo = "23F-6109",
            FullName = "Muhammad Subhan Shahid",
            Email = "f236109@cfd.nu.edu.pk",
            CreatedAtUtc = now
        };

        var student2 = new Student
        {
            RegistrationNo = "CS-2022-002",
            FullName = "Sara Ahmed",
            Email = "sara.ahmed@example.com",
            CreatedAtUtc = now
        };

        var student3 = new Student
        {
            RegistrationNo = "CS-2022-003",
            FullName = "Bilal Khan",
            Email = "bilal.khan@example.com",
            CreatedAtUtc = now
        };

        dbContext.Students.AddRange(student1, student2, student3);

        var course1 = new Course
        {
            Code = "CS401",
            Title = "Advanced Programming",
            CreditHours = 3,
            CreatedAtUtc = now
        };

        var course2 = new Course
        {
            Code = "AI302",
            Title = "Machine Learning",
            CreditHours = 4,
            CreatedAtUtc = now
        };

        var course3 = new Course
        {
            Code = "MA201",
            Title = "Linear Algebra",
            CreditHours = 2,
            CreatedAtUtc = now
        };

        dbContext.Courses.AddRange(course1, course2, course3);
        await dbContext.SaveChangesAsync(cancellationToken);

        var enrollments = new List<Enrollment>
        {
            new() { StudentId = student1.Id, CourseId = course1.Id, Mark = 85, EnrolledAtUtc = now },
            new() { StudentId = student1.Id, CourseId = course2.Id, Mark = 90, EnrolledAtUtc = now },
            new() { StudentId = student1.Id, CourseId = course3.Id, Mark = 75, EnrolledAtUtc = now },
            new() { StudentId = student2.Id, CourseId = course1.Id, Mark = 70, EnrolledAtUtc = now },
            new() { StudentId = student2.Id, CourseId = course2.Id, Mark = 45, EnrolledAtUtc = now },
            new() { StudentId = student3.Id, CourseId = course1.Id, Mark = null, EnrolledAtUtc = now }
        };

        dbContext.Enrollments.AddRange(enrollments);

        var teacherUsername = configuration["SeedUsers:TeacherUsername"] ?? "teacher1";
        var teacherPassword = configuration["SeedUsers:TeacherPassword"] ?? "Teacher@123";
        var student1Username = configuration["SeedUsers:Student1Username"] ?? "student1";
        var student1Password = configuration["SeedUsers:Student1Password"] ?? "Student1@123";
        var student2Username = configuration["SeedUsers:Student2Username"] ?? "student2";
        var student2Password = configuration["SeedUsers:Student2Password"] ?? "Student2@123";

        var teacherUser = new User
        {
            Username = teacherUsername,
            Role = "Teacher",
            StudentId = null
        };
        teacherUser.PasswordHash = passwordHasher.HashPassword(teacherUser, teacherPassword);

        var studentUser1 = new User
        {
            Username = student1Username,
            Role = "Student",
            StudentId = student1.Id
        };
        studentUser1.PasswordHash = passwordHasher.HashPassword(studentUser1, student1Password);

        var studentUser2 = new User
        {
            Username = student2Username,
            Role = "Student",
            StudentId = student2.Id
        };
        studentUser2.PasswordHash = passwordHasher.HashPassword(studentUser2, student2Password);

        dbContext.Users.AddRange(teacherUser, studentUser1, studentUser2);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded 3 students, 3 courses, 6 enrollments, and 3 users successfully.");
    }
}

