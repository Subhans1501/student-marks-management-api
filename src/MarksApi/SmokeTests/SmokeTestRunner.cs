using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MarksApi.Dtos.Auth;
using MarksApi.Dtos.Courses;
using MarksApi.Dtos.Enrollments;
using MarksApi.Dtos.Students;

namespace MarksApi.SmokeTests;

public static class SmokeTestRunner
{
    private sealed record CheckResult(bool Passed, string Method, string Path, int ExpectedStatus, int ActualStatus, string Note);

    public static async Task<int> RunAsync(string baseUrl, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var results = new List<CheckResult>();

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };

        var teacherUsername = configuration["SeedUsers:TeacherUsername"];
        var teacherPassword = configuration["SeedUsers:TeacherPassword"];
        var studentUsername = configuration["SeedUsers:Student1Username"];
        var studentPassword = configuration["SeedUsers:Student1Password"];

        if (string.IsNullOrWhiteSpace(teacherUsername) ||
            string.IsNullOrWhiteSpace(teacherPassword) ||
            string.IsNullOrWhiteSpace(studentUsername) ||
            string.IsNullOrWhiteSpace(studentPassword))
        {
            Console.WriteLine("FAIL | CONFIG | SeedUsers credentials must be set in user-secrets or environment variables before running --smoke-test.");
            return 1;
        }

        int? createdStudentId = null;
        int? createdCourseId = null;
        int? createdEnrollmentId = null;
        string? teacherToken = null;
        string? studentToken = null;

        try
        {
            // 1. Health Check (200)
            await CheckAsync(httpClient, results, HttpMethod.Get, "/health", null, null, 200, "Health endpoint open", cancellationToken);

            // 2. Auth Login - Negative (400 Bad Body)
            await CheckAsync(httpClient, results, HttpMethod.Post, "/api/auth/login", new { username = "", password = "" }, null, 400, "Login empty credentials", cancellationToken);

            // 3. Auth Login - Negative (401 Wrong Password)
            await CheckAsync(httpClient, results, HttpMethod.Post, "/api/auth/login", new { username = teacherUsername, password = "wrong-password-999" }, null, 401, "Login invalid password", cancellationToken);

            // 4. Auth Login - Valid Teacher (200)
            var teacherLoginResponse = await CheckAndReadAsync<LoginResponse>(
                httpClient, results, HttpMethod.Post, "/api/auth/login",
                new LoginRequest { Username = teacherUsername, Password = teacherPassword },
                null, 200, "Teacher login", cancellationToken);
            teacherToken = teacherLoginResponse?.Token;

            // 5. Auth Login - Valid Student (200)
            var studentLoginResponse = await CheckAndReadAsync<LoginResponse>(
                httpClient, results, HttpMethod.Post, "/api/auth/login",
                new LoginRequest { Username = studentUsername, Password = studentPassword },
                null, 200, "Student login", cancellationToken);
            studentToken = studentLoginResponse?.Token;

            // 6. GET /api/students - Negative (401 No Token)
            await CheckAsync(httpClient, results, HttpMethod.Get, "/api/students", null, null, 401, "List students without token", cancellationToken);

            // 7. GET /api/students - Negative (403 Student Token)
            await CheckAsync(httpClient, results, HttpMethod.Get, "/api/students", null, studentToken, 403, "List students as Student role", cancellationToken);

            // 8. GET /api/students - Valid Teacher (200)
            await CheckAsync(httpClient, results, HttpMethod.Get, "/api/students?page=1&pageSize=10", null, teacherToken, 200, "List students as Teacher", cancellationToken);

            // 9. POST /api/students - Negative (400 Bad Body)
            await CheckAsync(httpClient, results, HttpMethod.Post, "/api/students",
                new { registrationNo = "SMOKE-BAD", fullName = "", email = "not-an-email" },
                teacherToken, 400, "Create student invalid body", cancellationToken);

            // 10. POST /api/students - Valid Teacher (201)
            var createdStudent = await CheckAndReadAsync<StudentResponse>(
                httpClient, results, HttpMethod.Post, "/api/students",
                new CreateStudentRequest
                {
                    RegistrationNo = "SMOKE-STU-01",
                    FullName = "SMOKE Test Student",
                    Email = "smoke.student01@example.com"
                },
                teacherToken, 201, "Create SMOKE student", cancellationToken);
            createdStudentId = createdStudent?.Id;

            // 11. POST /api/students - Negative (409 Duplicate RegistrationNo)
            await CheckAsync(httpClient, results, HttpMethod.Post, "/api/students",
                new CreateStudentRequest
                {
                    RegistrationNo = "SMOKE-STU-01",
                    FullName = "SMOKE Duplicate Student",
                    Email = "smoke.duplicate@example.com"
                },
                teacherToken, 409, "Create duplicate student", cancellationToken);

            if (createdStudentId.HasValue)
            {
                // 12. GET /api/students/{id} - Valid Teacher (200)
                await CheckAsync(httpClient, results, HttpMethod.Get, $"/api/students/{createdStudentId.Value}", null, teacherToken, 200, "Get student by id", cancellationToken);

                // 13. GET /api/students/{id} - Negative (403 Student reading another student)
                await CheckAsync(httpClient, results, HttpMethod.Get, $"/api/students/{createdStudentId.Value}", null, studentToken, 403, "Student reading another student", cancellationToken);

                // 14. GET /api/students/{id} - Negative (404 Not Found)
                await CheckAsync(httpClient, results, HttpMethod.Get, "/api/students/999999", null, teacherToken, 404, "Get unknown student", cancellationToken);

                // 15. PUT /api/students/{id} - Valid Teacher (200)
                await CheckAsync(httpClient, results, HttpMethod.Put, $"/api/students/{createdStudentId.Value}",
                    new UpdateStudentRequest
                    {
                        RegistrationNo = "SMOKE-STU-01",
                        FullName = "SMOKE Test Student Updated",
                        Email = "smoke.student01@example.com"
                    },
                    teacherToken, 200, "Update SMOKE student", cancellationToken);

                // 16. PUT /api/students/{id} - Negative (404 Not Found)
                await CheckAsync(httpClient, results, HttpMethod.Put, "/api/students/999999",
                    new UpdateStudentRequest
                    {
                        RegistrationNo = "SMOKE-NONE",
                        FullName = "Unknown",
                        Email = "smoke.unknown@example.com"
                    },
                    teacherToken, 404, "Update unknown student", cancellationToken);
            }

            // 17. GET /api/courses - Valid Student (200)
            await CheckAsync(httpClient, results, HttpMethod.Get, "/api/courses?page=1&pageSize=10", null, studentToken, 200, "List courses", cancellationToken);

            // 18. GET /api/courses - Negative (401 No Token)
            await CheckAsync(httpClient, results, HttpMethod.Get, "/api/courses", null, null, 401, "List courses without token", cancellationToken);

            // 19. POST /api/courses - Negative (400 CreditHours out of range)
            await CheckAsync(httpClient, results, HttpMethod.Post, "/api/courses",
                new { code = "SMK99", title = "SMOKE Bad Course", creditHours = 6 },
                teacherToken, 400, "Create course invalid creditHours", cancellationToken);

            // 20. POST /api/courses - Valid Teacher (201)
            var createdCourse = await CheckAndReadAsync<CourseResponse>(
                httpClient, results, HttpMethod.Post, "/api/courses",
                new CreateCourseRequest
                {
                    Code = "SMK101",
                    Title = "SMOKE Test Course",
                    CreditHours = 3
                },
                teacherToken, 201, "Create SMOKE course", cancellationToken);
            createdCourseId = createdCourse?.Id;

            // 21. POST /api/courses - Negative (409 Duplicate Code)
            await CheckAsync(httpClient, results, HttpMethod.Post, "/api/courses",
                new CreateCourseRequest
                {
                    Code = "SMK101",
                    Title = "SMOKE Duplicate Course",
                    CreditHours = 3
                },
                teacherToken, 409, "Create duplicate course", cancellationToken);

            if (createdCourseId.HasValue)
            {
                // 22. GET /api/courses/{id} - Valid (200)
                await CheckAsync(httpClient, results, HttpMethod.Get, $"/api/courses/{createdCourseId.Value}", null, studentToken, 200, "Get course by id", cancellationToken);

                // 23. GET /api/courses/{id} - Negative (404)
                await CheckAsync(httpClient, results, HttpMethod.Get, "/api/courses/999999", null, teacherToken, 404, "Get unknown course", cancellationToken);

                // 24. PUT /api/courses/{id} - Valid (200)
                await CheckAsync(httpClient, results, HttpMethod.Put, $"/api/courses/{createdCourseId.Value}",
                    new UpdateCourseRequest
                    {
                        Code = "SMK101",
                        Title = "SMOKE Test Course Updated",
                        CreditHours = 4
                    },
                    teacherToken, 200, "Update SMOKE course", cancellationToken);

                // 25. PUT /api/courses/{id} - Negative (404)
                await CheckAsync(httpClient, results, HttpMethod.Put, "/api/courses/999999",
                    new UpdateCourseRequest
                    {
                        Code = "SMK404",
                        Title = "Unknown Course",
                        CreditHours = 3
                    },
                    teacherToken, 404, "Update unknown course", cancellationToken);
            }

            if (createdCourseId.HasValue && createdStudentId.HasValue)
            {
                // 26. POST /api/courses/{courseId}/enrollments - Valid (201)
                var createdEnrollment = await CheckAndReadAsync<EnrollmentResponse>(
                    httpClient, results, HttpMethod.Post, $"/api/courses/{createdCourseId.Value}/enrollments",
                    new CreateEnrollmentRequest { StudentId = createdStudentId.Value },
                    teacherToken, 201, "Enroll SMOKE student in SMOKE course", cancellationToken);
                createdEnrollmentId = createdEnrollment?.Id;

                // 27. POST /api/courses/{courseId}/enrollments - Negative (409 Double Enrollment)
                await CheckAsync(httpClient, results, HttpMethod.Post, $"/api/courses/{createdCourseId.Value}/enrollments",
                    new CreateEnrollmentRequest { StudentId = createdStudentId.Value },
                    teacherToken, 409, "Duplicate enrollment rejected", cancellationToken);

                // 28. GET /api/courses/{courseId}/enrollments - Valid (200)
                await CheckAsync(httpClient, results, HttpMethod.Get, $"/api/courses/{createdCourseId.Value}/enrollments",
                    null, teacherToken, 200, "List course enrollments", cancellationToken);

                // 29. GET /api/courses/{courseId}/enrollments - Negative (404 Unknown Course)
                await CheckAsync(httpClient, results, HttpMethod.Get, "/api/courses/999999/enrollments",
                    null, teacherToken, 404, "List enrollments for unknown course", cancellationToken);

                // 30. DELETE /api/students/{id} - Negative (409 Student has enrollments)
                await CheckAsync(httpClient, results, HttpMethod.Delete, $"/api/students/{createdStudentId.Value}",
                    null, teacherToken, 409, "Delete student with active enrollment rejected", cancellationToken);

                // 31. DELETE /api/courses/{id} - Negative (409 Course has enrollments)
                await CheckAsync(httpClient, results, HttpMethod.Delete, $"/api/courses/{createdCourseId.Value}",
                    null, teacherToken, 409, "Delete course with active enrollment rejected", cancellationToken);
            }

            if (createdEnrollmentId.HasValue)
            {
                // 32. PUT /api/enrollments/{id}/mark - Negative (400 Mark = 101)
                await CheckAsync(httpClient, results, HttpMethod.Put, $"/api/enrollments/{createdEnrollmentId.Value}/mark",
                    new { mark = 101 }, teacherToken, 400, "Record mark > 100 rejected", cancellationToken);

                // 33. PUT /api/enrollments/{id}/mark - Valid (200 Mark = 88)
                await CheckAsync(httpClient, results, HttpMethod.Put, $"/api/enrollments/{createdEnrollmentId.Value}/mark",
                    new UpdateMarkRequest { Mark = 88 }, teacherToken, 200, "Record valid mark", cancellationToken);
            }

            if (createdCourseId.HasValue)
            {
                // 34. GET /api/courses/{courseId}/stats - Valid (200)
                await CheckAsync(httpClient, results, HttpMethod.Get, $"/api/courses/{createdCourseId.Value}/stats",
                    null, teacherToken, 200, "Get course stats", cancellationToken);

                // 35. GET /api/courses/{courseId}/stats - Negative (404)
                await CheckAsync(httpClient, results, HttpMethod.Get, "/api/courses/999999/stats",
                    null, teacherToken, 404, "Get stats for unknown course", cancellationToken);
            }

            if (createdStudentId.HasValue)
            {
                // 36. GET /api/students/{id}/transcript - Valid Teacher (200)
                await CheckAsync(httpClient, results, HttpMethod.Get, $"/api/students/{createdStudentId.Value}/transcript",
                    null, teacherToken, 200, "Get student transcript", cancellationToken);

                // 37. GET /api/students/{id}/transcript - Negative (403 Student reading another student's transcript)
                await CheckAsync(httpClient, results, HttpMethod.Get, $"/api/students/{createdStudentId.Value}/transcript",
                    null, studentToken, 403, "Student reading another student's transcript", cancellationToken);
            }

            // Cleanup step by step via DELETE endpoints (also verifies 204 and 404 on DELETE endpoints)
            if (createdEnrollmentId.HasValue)
            {
                // 38. DELETE /api/enrollments/{id} - Valid (204)
                await CheckAsync(httpClient, results, HttpMethod.Delete, $"/api/enrollments/{createdEnrollmentId.Value}",
                    null, teacherToken, 204, "Delete SMOKE enrollment", cancellationToken);
                createdEnrollmentId = null;

                // 39. DELETE /api/enrollments/{id} - Negative (404)
                await CheckAsync(httpClient, results, HttpMethod.Delete, "/api/enrollments/999999",
                    null, teacherToken, 404, "Delete unknown enrollment", cancellationToken);
            }

            if (createdCourseId.HasValue)
            {
                // 40. DELETE /api/courses/{id} - Valid (204)
                await CheckAsync(httpClient, results, HttpMethod.Delete, $"/api/courses/{createdCourseId.Value}",
                    null, teacherToken, 204, "Delete SMOKE course", cancellationToken);
                createdCourseId = null;
            }

            if (createdStudentId.HasValue)
            {
                // 41. DELETE /api/students/{id} - Valid (204)
                await CheckAsync(httpClient, results, HttpMethod.Delete, $"/api/students/{createdStudentId.Value}",
                    null, teacherToken, 204, "Delete SMOKE student", cancellationToken);
                createdStudentId = null;
            }
        }
        finally
        {
            // Fallback cleanup if any intermediate failure left test data behind
            if (!string.IsNullOrWhiteSpace(teacherToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                if (createdEnrollmentId.HasValue)
                {
                    await httpClient.DeleteAsync($"/api/enrollments/{createdEnrollmentId.Value}", cancellationToken);
                }
                if (createdCourseId.HasValue)
                {
                    await httpClient.DeleteAsync($"/api/courses/{createdCourseId.Value}", cancellationToken);
                }
                if (createdStudentId.HasValue)
                {
                    await httpClient.DeleteAsync($"/api/students/{createdStudentId.Value}", cancellationToken);
                }
            }
        }

        var passedCount = results.Count(r => r.Passed);
        var failedCount = results.Count - passedCount;

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"SMOKE TEST SUMMARY: {passedCount}/{results.Count} checks passed, {failedCount} failed.");
        Console.WriteLine("--------------------------------------------------------------------------------");

        return failedCount == 0 ? 0 : 1;
    }

    private static async Task CheckAsync(
        HttpClient client,
        List<CheckResult> results,
        HttpMethod method,
        string path,
        object? body,
        string? bearerToken,
        int expectedStatus,
        string note,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var actualStatus = (int)response.StatusCode;
        var passed = actualStatus == expectedStatus;

        results.Add(new CheckResult(passed, method.Method, path, expectedStatus, actualStatus, note));
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} | {method.Method,-6} | {path,-38} | Expected: {expectedStatus} | Actual: {actualStatus} | {note}");
    }

    private static async Task<T?> CheckAndReadAsync<T>(
        HttpClient client,
        List<CheckResult> results,
        HttpMethod method,
        string path,
        object? body,
        string? bearerToken,
        int expectedStatus,
        string note,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var actualStatus = (int)response.StatusCode;
        var passed = actualStatus == expectedStatus;

        results.Add(new CheckResult(passed, method.Method, path, expectedStatus, actualStatus, note));
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} | {method.Method,-6} | {path,-38} | Expected: {expectedStatus} | Actual: {actualStatus} | {note}");

        if (!passed)
        {
            return default;
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<T>(options, cancellationToken);
    }
}

