# MarksApi

## Requirements

- .NET 10 SDK
- PostgreSQL 16 or later

## Local setup

From the `MarksApi` directory:

1. Start PostgreSQL 16 or later. For a local Docker database, replace the password placeholder and run:

   ```powershell
   docker run --name marks-postgres --detach -e POSTGRES_USER=marks_user -e POSTGRES_PASSWORD=YOUR_DATABASE_PASSWORD -e POSTGRES_DB=marks_db -p 5432:5432 postgres:17
   ```

   For an existing PostgreSQL server, create the `marks_user` role and `marks_db` database there instead.

2. Install the EF CLI if needed and initialize user-secrets:

   ```powershell
   dotnet tool install --global dotnet-ef
   dotnet user-secrets init
   dotnet dev-certs https --trust
   ```

3. Keep database passwords and signing keys in user-secrets, not in an appsettings file. Use the same database password from step 1:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=marks_db;Username=marks_user;Password=YOUR_DATABASE_PASSWORD"
   dotnet user-secrets set "Jwt:Key" "YOUR_RANDOM_SECRET_KEY_OF_AT_LEAST_32_CHARACTERS"
   dotnet user-secrets set "DemoAccounts:TeacherUsername" "teacher"
   dotnet user-secrets set "DemoAccounts:TeacherPassword" "Teacher123!"
   dotnet user-secrets set "DemoAccounts:Student1Username" "student1"
   dotnet user-secrets set "DemoAccounts:Student1Password" "Student123!"
   dotnet user-secrets set "DemoAccounts:Student2Username" "student2"
   dotnet user-secrets set "DemoAccounts:Student2Password" "Student123!"
   ```

4. Apply migrations and run:

   ```powershell
   dotnet ef database update
   dotnet run
   ```

   The default launch profile listens on HTTP and HTTPS and redirects HTTP requests to HTTPS. Trust the local development certificate as above.

5. Configure Postman environment variable `baseUrl` to the URL printed by `dotnet run`, then request `GET {{baseUrl}}/health`.

The project targets .NET 10. PostgreSQL must be running before migration and runtime checks.
`appsettings.Example.json` documents all supported settings using fake values. Keep actual credentials in user-secrets locally and environment variables on servers.

## API documentation and endpoints

In Development, interactive API documentation is available at `/scalar/v1` and the OpenAPI JSON is at `/openapi/v1.json`.

| Method | Path | Access |
|---|---|---|
| POST | `/api/auth/login` | Anyone |
| GET, POST | `/api/students` | Teacher |
| GET, PUT, DELETE | `/api/students/{id}` | Teacher; GET also permits the same student |
| GET | `/api/students/{id}/transcript` | Teacher or same student |
| GET | `/api/courses`, `/api/courses/{id}` | Teacher or Student |
| POST, PUT, DELETE | `/api/courses`, `/api/courses/{id}` | Teacher |
| POST, GET | `/api/courses/{courseId}/enrollments` | Teacher |
| GET | `/api/courses/{courseId}/stats` | Teacher |
| GET | `/api/enrollments/{id}` | Teacher (resource lookup for create `Location`) |
| PUT | `/api/enrollments/{id}/mark` | Teacher |
| DELETE | `/api/enrollments/{id}` | Teacher |
| GET | `/health` | Anyone |

List routes support `page`, `pageSize` (capped at 50), `search`, and allow-listed `sort` values. A Student token contains its linked `student_id` claim and cannot read another student's record or transcript.

## Development-only sample accounts

When running in the `Development` environment against an empty, migrated database, the API inserts two students, three courses and six enrollments, plus these accounts:

| Role | Username | Password |
|---|---|---|
| Teacher | `teacher` | `Teacher123!` |
| Student | `student1` | `Student123!` |
| Student | `student2` | `Student123!` |

These sample credentials are read from user-secrets, are for local development and testing only, and are not defaults in source code. Passwords are stored as hashes. Do not use these credentials or development seeding in production.

The development seeder skips seeding if the database already contains students, courses, users or enrollments.

## Postman

Import `postman/MarksApi.postman_collection.json` and `postman/Local.postman_environment.json`, select the `Local` environment, and run the `Marks API` collection in order against a fresh migrated database. The login requests set the teacher and student tokens automatically. The environment file intentionally exports empty token values. The collection includes 41 requests and collection-level status/body assertions.

## Startup smoke test

In a Development environment with PostgreSQL migrated and the demo account user-secrets configured, run:

```powershell
dotnet run -- --smoke-test
```

The test starts the API, makes real HTTP requests, prints each expected/actual status and exits nonzero on failure. It creates uniquely prefixed `SMOKE-` data and deletes it afterward. The seeded account names and passwords come from user-secrets (`DemoAccounts:*`), not smoke-test source code. Alternatively, set `SMOKE_TEST=true` for a Development run. Smoke tests are rejected outside Development.

## Database and submission files

- The EF Core migrations are in `Migrations/`.
- The latest migration also enforces that a user with the `Student` role has a linked `student_id`.
- `db/schema.sql` is generated from the committed EF migrations. The local PostgreSQL server was unavailable when this file was prepared, so it is not represented as a live `pg_dump` export.
- The ER diagram is at `Documentation/ERD.png`.
- After applying the migrations to PostgreSQL, replace that migration script with a live schema-only export from the project directory:

  ```powershell
  New-Item -ItemType Directory -Force db
  pg_dump --host=localhost --port=5432 --username=marks_user --dbname=marks_db --schema-only --no-owner --no-privileges --file=db\schema.sql --password
  ```

- Postman run screenshots, smoke-test screenshots, and the short PDF report must be captured/generated after running the system locally; they are not simulated by this repository.

## Design notes

- Unique indexes, not pre-insert existence checks, resolve duplicate registrations, emails, course codes, and student/course enrollments under concurrent requests.
- Deletions with existing enrollments return `409 Conflict`, preserving the restricted foreign-key relationships.
- Course averages use graded marks only; transcripts use the sum of mark × credit hours divided by graded credit hours, rounded to two decimal places. The pass threshold is read from `PassMark`.
