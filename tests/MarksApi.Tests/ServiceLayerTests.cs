using MarksApi.Data;
using MarksApi.Data.Entities;
using MarksApi.Dtos.Auth;
using MarksApi.Dtos.Courses;
using MarksApi.Dtos.Enrollments;
using MarksApi.Dtos.Students;
using MarksApi.Exceptions;
using MarksApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarksApi.Tests;

public class ServiceLayerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly StudentService _studentService;
    private readonly CourseService _courseService;
    private readonly EnrollmentService _enrollmentService;
    private readonly AuthService _authService;

    public ServiceLayerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        var configValues = new Dictionary<string, string?>
        {
            ["DepartmentRules:PassMark"] = "50",
            ["Jwt:Key"] = "unit-test-super-secret-signing-key-32-chars-minimum",
            ["Jwt:Issuer"] = "MarksApi",
            ["Jwt:Audience"] = "MarksApiClients",
            ["Jwt:LifetimeMinutes"] = "60"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _studentService = new StudentService(_dbContext, _configuration, NullLogger<StudentService>.Instance);
        _courseService = new CourseService(_dbContext, _configuration, NullLogger<CourseService>.Instance);
        _enrollmentService = new EnrollmentService(_dbContext, NullLogger<EnrollmentService>.Instance);
        _authService = new AuthService(_dbContext, new PasswordHasher<User>(), _configuration, NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Test01_CreateStudent_NormalizesEmailToLowerCase_AndSetsUtcTime()
    {
        var created = await _studentService.CreateStudentAsync(new CreateStudentRequest
        {
            RegistrationNo = "23F-6109",
            FullName = "Muhammad Subhan Shahid",
            Email = "Subhan.Shahid@FAST.edu.pk"
        });

        Assert.True(created.Id > 0);
        Assert.Equal("23F-6109", created.RegistrationNo);
        Assert.Equal("subhan.shahid@fast.edu.pk", created.Email);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task Test02_CreateStudent_DuplicateRegistrationNo_ThrowsConflict409()
    {
        await _studentService.CreateStudentAsync(new CreateStudentRequest
        {
            RegistrationNo = "CS-2022-001",
            FullName = "Ali Raza",
            Email = "ali.raza@example.com"
        });

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            _studentService.CreateStudentAsync(new CreateStudentRequest
            {
                RegistrationNo = "CS-2022-001",
                FullName = "Another Student",
                Email = "another@example.com"
            }));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task Test03_GetStudents_EnforcesMaxPageSize50_AndSupportsSearch()
    {
        await _studentService.CreateStudentAsync(new CreateStudentRequest
        {
            RegistrationNo = "23F-6109",
            FullName = "Muhammad Subhan Shahid",
            Email = "subhan@example.com"
        });
        await _studentService.CreateStudentAsync(new CreateStudentRequest
        {
            RegistrationNo = "CS-2022-002",
            FullName = "Sara Ahmed",
            Email = "sara@example.com"
        });

        var paged = await _studentService.GetStudentsAsync(page: 0, pageSize: 500, search: "Subhan", sort: "name_asc");

        Assert.Equal(1, paged.Page);
        Assert.Equal(50, paged.PageSize);
        Assert.Equal(1, paged.TotalCount);
        Assert.Single(paged.Items);
        Assert.Equal("23F-6109", paged.Items[0].RegistrationNo);
    }

    [Fact]
    public async Task Test04_DeleteStudent_WithActiveEnrollment_ThrowsConflict409()
    {
        var student = await _studentService.CreateStudentAsync(new CreateStudentRequest
        {
            RegistrationNo = "CS-2022-010",
            FullName = "Enrolled Student",
            Email = "enrolled@example.com"
        });

        var course = await _courseService.CreateCourseAsync(new CreateCourseRequest
        {
            Code = "CS401",
            Title = "Advanced Programming",
            CreditHours = 3
        });

        await _enrollmentService.EnrollStudentAsync(course.Id, new CreateEnrollmentRequest
        {
            StudentId = student.Id
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _studentService.DeleteStudentAsync(student.Id));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Test05_EnrollStudent_SecondAttemptInSameCourse_ThrowsConflict409()
    {
        var student = await _studentService.CreateStudentAsync(new CreateStudentRequest
        {
            RegistrationNo = "CS-2022-011",
            FullName = "Double Enroll Student",
            Email = "double@example.com"
        });

        var course = await _courseService.CreateCourseAsync(new CreateCourseRequest
        {
            Code = "AI302",
            Title = "Machine Learning",
            CreditHours = 4
        });

        await _enrollmentService.EnrollStudentAsync(course.Id, new CreateEnrollmentRequest { StudentId = student.Id });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _enrollmentService.EnrollStudentAsync(course.Id, new CreateEnrollmentRequest { StudentId = student.Id }));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Test06_GetCourseStats_UngradedCourse_ReturnsNullAverageNotZero()
    {
        var student = await _studentService.CreateStudentAsync(new CreateStudentRequest
        {
            RegistrationNo = "CS-2022-012",
            FullName = "Ungraded Student",
            Email = "ungraded@example.com"
        });

        var course = await _courseService.CreateCourseAsync(new CreateCourseRequest
        {
            Code = "SE201",
            Title = "Software Engineering",
            CreditHours = 3
        });

        await _enrollmentService.EnrollStudentAsync(course.Id, new CreateEnrollmentRequest { StudentId = student.Id });

        var stats = await _courseService.GetCourseStatsAsync(course.Id);

        Assert.Equal(1, stats.Enrolled);
        Assert.Equal(0, stats.Graded);
        Assert.Null(stats.Average);
        Assert.Null(stats.Highest);
        Assert.Null(stats.Lowest);
        Assert.Equal(0, stats.Passed);
    }

    [Fact]
    public async Task Test07_GetCourseStats_ComputesGradedOnlyAverageHighestLowestAndPassed()
    {
        var s1 = await _studentService.CreateStudentAsync(new CreateStudentRequest { RegistrationNo = "S-01", FullName = "S1", Email = "s1@example.com" });
        var s2 = await _studentService.CreateStudentAsync(new CreateStudentRequest { RegistrationNo = "S-02", FullName = "S2", Email = "s2@example.com" });
        var s3 = await _studentService.CreateStudentAsync(new CreateStudentRequest { RegistrationNo = "S-03", FullName = "S3", Email = "s3@example.com" });

        var course = await _courseService.CreateCourseAsync(new CreateCourseRequest { Code = "CS401", Title = "Advanced Programming", CreditHours = 3 });

        var e1 = await _enrollmentService.EnrollStudentAsync(course.Id, new CreateEnrollmentRequest { StudentId = s1.Id });
        var e2 = await _enrollmentService.EnrollStudentAsync(course.Id, new CreateEnrollmentRequest { StudentId = s2.Id });
        await _enrollmentService.EnrollStudentAsync(course.Id, new CreateEnrollmentRequest { StudentId = s3.Id }); // ungraded

        await _enrollmentService.UpdateMarkAsync(e1.Id, new UpdateMarkRequest { Mark = 70 });
        await _enrollmentService.UpdateMarkAsync(e2.Id, new UpdateMarkRequest { Mark = 80 });

        var stats = await _courseService.GetCourseStatsAsync(course.Id);

        Assert.Equal(3, stats.Enrolled);
        Assert.Equal(2, stats.Graded);
        Assert.Equal(75.0, stats.Average);
        Assert.Equal(80, stats.Highest);
        Assert.Equal(70, stats.Lowest);
        Assert.Equal(2, stats.Passed);
    }

    [Fact]
    public async Task Test08_GetTranscript_CalculatesCreditHourWeightedAverage_ExactFormula()
    {
        // Matches Section 8.4 specification example:
        // CS401 (3 credits, mark 80) + MA201 (2 credits, mark 60) => (80*3 + 60*2)/(3+2) = 360/5 = 72.0
        var student = await _studentService.CreateStudentAsync(new CreateStudentRequest
        {
            RegistrationNo = "CS-2022-001",
            FullName = "Ali Raza",
            Email = "ali.raza@example.com"
        });

        var cs401 = await _courseService.CreateCourseAsync(new CreateCourseRequest { Code = "CS401", Title = "Advanced Programming", CreditHours = 3 });
        var ma201 = await _courseService.CreateCourseAsync(new CreateCourseRequest { Code = "MA201", Title = "Linear Algebra", CreditHours = 2 });

        var e1 = await _enrollmentService.EnrollStudentAsync(cs401.Id, new CreateEnrollmentRequest { StudentId = student.Id });
        var e2 = await _enrollmentService.EnrollStudentAsync(ma201.Id, new CreateEnrollmentRequest { StudentId = student.Id });

        await _enrollmentService.UpdateMarkAsync(e1.Id, new UpdateMarkRequest { Mark = 80 });
        await _enrollmentService.UpdateMarkAsync(e2.Id, new UpdateMarkRequest { Mark = 60 });

        var transcript = await _studentService.GetTranscriptAsync(student.Id);

        Assert.Equal(72.0, transcript.WeightedAverage);
        Assert.Equal(2, transcript.Courses.Count);
        Assert.All(transcript.Courses, c => Assert.True(c.Passed));
    }

    [Fact]
    public async Task Test09_UpdateMark_WithStaleOptimisticConcurrencyVersion_ThrowsConflict409()
    {
        var student = await _studentService.CreateStudentAsync(new CreateStudentRequest { RegistrationNo = "S-99", FullName = "Concurrency Student", Email = "conc@example.com" });
        var course = await _courseService.CreateCourseAsync(new CreateCourseRequest { Code = "CS499", Title = "Distributed Systems", CreditHours = 3 });
        var enrollment = await _enrollmentService.EnrollStudentAsync(course.Id, new CreateEnrollmentRequest { StudentId = student.Id });

        // First teacher updates mark (advances Version from 0 to 1)
        var firstUpdate = await _enrollmentService.UpdateMarkAsync(enrollment.Id, new UpdateMarkRequest
        {
            Mark = 75,
            Version = enrollment.Version
        });

        Assert.NotEqual(enrollment.Version, firstUpdate.Version);

        // Second teacher tries to update using stale initial Version token (0)
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _enrollmentService.UpdateMarkAsync(enrollment.Id, new UpdateMarkRequest
            {
                Mark = 95,
                Version = enrollment.Version
            }));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Test10_AuthService_VerifiesHashedPassword_AndReturnsValidJwtToken()
    {
        var hasher = new PasswordHasher<User>();
        var teacher = new User
        {
            Username = "teacher1",
            Role = "Teacher"
        };
        teacher.PasswordHash = hasher.HashPassword(teacher, "Teacher@123");
        _dbContext.Users.Add(teacher);
        await _dbContext.SaveChangesAsync();

        var loginResult = await _authService.LoginAsync(new LoginRequest
        {
            Username = "teacher1",
            Password = "Teacher@123"
        });

        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token));
        Assert.Equal("Teacher", loginResult.Role);
        Assert.True(loginResult.ExpiresAtUtc > DateTime.UtcNow);

        await Assert.ThrowsAsync<UnauthorizedApiException>(() =>
            _authService.LoginAsync(new LoginRequest
            {
                Username = "teacher1",
                Password = "WrongPassword!"
            }));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}

