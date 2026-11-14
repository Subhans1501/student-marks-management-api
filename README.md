# Student Marks Management API

**Student:** Muhammad Subhan Shahid  
**Registration No:** 23F-6109  
**Course:** Advanced Programming (C# and .NET)  
**Campus:** FAST NUCES, Chiniot-Faisalabad Campus  

## Overview

This is an ASP.NET Core 8 Web API for managing university students, courses, enrollments, and marks, backed by PostgreSQL and Entity Framework Core 8. Teachers can manage student records, courses, enrollments, and marks, while students can view courses and check only their own profile and weighted transcript.

Main features:
- Layered structure with Controllers, Services (behind interfaces), EF Core `AppDbContext`, and DTOs.
- PostgreSQL schema with `snake_case` naming (`EFCore.NamingConventions`), foreign keys (`ON DELETE RESTRICT`), unique indexes, and SQL check constraints.
- RFC 7807 `ProblemDetails` responses for all `4xx` and `5xx` errors via `GlobalExceptionHandler`.
- JWT Bearer authentication with hashed passwords (`PasswordHasher<User>`) and role/ownership checks.
- On-demand startup smoke test runner (`--smoke-test`) that tests every endpoint against PostgreSQL and removes its temporary `SMOKE-` data afterward.
- Additional implemented features: Docker Compose setup, 10 xUnit service-layer tests, login rate limiting, and optimistic concurrency on mark updates using PostgreSQL `xmin`.

---

## Prerequisites

| Tool | Version | Check Command |
| :--- | :--- | :--- |
| .NET SDK | 8.0 | `dotnet --version` |
| PostgreSQL | 16 or 17 (or Docker Desktop) | `psql --version` or `docker --version` |
| EF Core CLI | `dotnet-ef` 8.x+ | `dotnet ef --version` |
| Postman | Desktop or Web | Import files from `postman/` |

---

## Setup Guide (From a Clean Clone)

### 1. Create the PostgreSQL Database and User

Using Docker:
```bash
docker run -d --name marks-postgres -e POSTGRES_USER=marks_user -e POSTGRES_PASSWORD=<your_db_password> -e POSTGRES_DB=marks_db -p 5432:5432 postgres:17
```

Or using local `psql`:
```sql
psql -U postgres
CREATE USER marks_user WITH PASSWORD '<your_db_password>';
CREATE DATABASE marks_db OWNER marks_user;
\q
```

### 2. Set Local Secrets

Secrets are kept out of Git using `dotnet user-secrets`. From `src/MarksApi`:

```bash
cd src/MarksApi

dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=marks_db;Username=marks_user;Password=<your_db_password>"
dotnet user-secrets set "Jwt:Key" "<replace-with-your-own-random-secret-key-32-chars-or-more>"
dotnet user-secrets set "SeedUsers:TeacherUsername" "teacher1"
dotnet user-secrets set "SeedUsers:TeacherPassword" "Teacher@123"
dotnet user-secrets set "SeedUsers:Student1Username" "student1"
dotnet user-secrets set "SeedUsers:Student1Password" "Student1@123"
dotnet user-secrets set "SeedUsers:Student2Username" "student2"
dotnet user-secrets set "SeedUsers:Student2Password" "Student2@123"
```

On a server or in Docker, pass these as environment variables using double underscores (for example `ConnectionStrings__Database` and `Jwt__Key`). See `appsettings.Example.json` for all configuration keys.

### 3. Run Migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update
```

The API also runs `MigrateAsync()` and seeds demo development data on startup if the `users` table is empty.

### 4. Start the API

```bash
dotnet run
```

The server listens on `http://localhost:5123`. Opening `http://localhost:5123/` in a browser redirects to Swagger UI.
- Swagger UI: `http://localhost:5123/swagger`
- Health check: `http://localhost:5123/health`

---

## Demo Accounts (Development Only)

These accounts are seeded for local development and testing only. Passwords are stored hashed using `PasswordHasher<User>`, never in plain text.

| Username | Password (Dev Only) | Role | Linked Student |
| :--- | :--- | :--- | :--- |
| `teacher1` | `Teacher@123` | `Teacher` | `null` |
| `student1` | `Student1@123` | `Student` | Student `1` (`23F-6109`, Muhammad Subhan Shahid) |
| `student2` | `Student2@123` | `Student` | Student `2` (`CS-2022-002`, Sara Ahmed) |

Seeded records include 3 students, 3 courses (`CS401`, `AI302`, `MA201`), and 6 enrollments (5 graded, 1 ungraded).

---

## API Endpoints

| Method | Path | Allowed Role(s) | Success | Error Codes |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Anyone (rate limited) | `200` | `400, 401, 429` |
| `GET` | `/api/students` | Teacher | `200` | `401, 403` |
| `GET` | `/api/students/{id}` | Teacher, or matching Student | `200` | `401, 403, 404` |
| `POST` | `/api/students` | Teacher | `201` | `400, 401, 403, 409` |
| `PUT` | `/api/students/{id}` | Teacher | `200` | `400, 401, 403, 404, 409` |
| `DELETE` | `/api/students/{id}` | Teacher | `204` | `401, 403, 404, 409` |
| `GET` | `/api/courses` | Teacher, Student | `200` | `401` |
| `GET` | `/api/courses/{id}` | Teacher, Student | `200` | `401, 404` |
| `POST` | `/api/courses` | Teacher | `201` | `400, 401, 403, 409` |
| `PUT` | `/api/courses/{id}` | Teacher | `200` | `400, 401, 403, 404, 409` |
| `DELETE` | `/api/courses/{id}` | Teacher | `204` | `401, 403, 404, 409` |
| `POST` | `/api/courses/{courseId}/enrollments` | Teacher | `201` | `400, 401, 403, 404, 409` |
| `GET` | `/api/courses/{courseId}/enrollments` | Teacher | `200` | `401, 403, 404` |
| `PUT` | `/api/enrollments/{id}/mark` | Teacher | `200` | `400, 401, 403, 404, 409` |
| `DELETE` | `/api/enrollments/{id}` | Teacher | `204` | `401, 403, 404` |
| `GET` | `/api/courses/{courseId}/stats` | Teacher | `200` | `401, 403, 404` |
| `GET` | `/api/students/{id}/transcript` | Teacher, or matching Student | `200` | `401, 403, 404` |
| `GET` | `/health` | Anyone | `200` | `503` |

---

## Running the Startup Smoke Test

The smoke test module (`src/MarksApi/SmokeTests/SmokeTestRunner.cs`) runs only when `--smoke-test` or `SMOKE_TEST=true` is passed. It executes 41 live HTTP requests against the real PostgreSQL database, verifies expected status codes (`200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`), deletes all `SMOKE-` records, and exits with code `0` if all checks pass.

```bash
dotnet run --project src/MarksApi -- --smoke-test
```

---

## Running the Postman Collection

1. Import `postman/MarksApi.postman_collection.json` and `postman/Local.postman_environment.json` into Postman.
2. Select the **Local** environment (`baseUrl` is `http://localhost:5123`; `token` is blank until login runs).
3. Start the API (`dotnet run --project src/MarksApi`).
4. Run **Marks API** in Collection Runner. Requests `02` and `03` save the Student and Teacher JWT tokens, create requests save `studentId`/`courseId`/`enrollmentId`, and cleanup requests at the end delete the temporary records.

---

## Unit Tests and Docker Compose

### xUnit Service Layer Tests
```bash
dotnet test tests/MarksApi.Tests
```

### Docker Compose (API + PostgreSQL 17)
Set the required secrets in your shell or a git-ignored `.env` file:
```bash
POSTGRES_PASSWORD=<your_db_password>
JWT_KEY=<your_32_plus_char_random_jwt_secret>
SEED_TEACHER_PASSWORD=Teacher@123
SEED_STUDENT1_PASSWORD=Student1@123
SEED_STUDENT2_PASSWORD=Student2@123
```
Then run:
```bash
docker compose up --build
```

---

## Design Decisions

1. **Returning `409 Conflict` instead of `400 Bad Request` for duplicates and active enrollments:** `400` means the request body failed field validation (for example an empty name or mark above 100). `409` means the payload is valid, but it clashes with existing database records (duplicate roll number, duplicate course code, enrolling twice, or deleting a student/course that still has enrollments).
2. **Database-level duplicate handling instead of check-then-insert:** Checking `AnyAsync()` before calling `SaveChangesAsync()` can allow two simultaneous requests to both pass the check before either commits. Instead, PostgreSQL unique indexes enforce uniqueness at commit time, and services catch `DbUpdateException` (SQLSTATE `23505`) to return `409 Conflict`.
3. **Optimistic concurrency for mark updates:** `Enrollment` maps PostgreSQL's `xmin` system column as a concurrency token (`Version`). If a caller passes `version` and another teacher changed the mark first, EF Core throws `DbUpdateConcurrencyException`, which returns `409 Conflict`.
4. **Non-tracking reads and DTO projection:** Read queries use `.AsNoTracking()` and `.Select(...)` into response DTOs so entities are never returned directly or tracked unnecessarily.
