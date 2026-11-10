# MarksApi

## Development-only sample accounts

When running in the `Development` environment against an empty, migrated database, the API inserts:

| Role | Username | Password |
|---|---|---|
| Teacher | `teacher` | `Teacher123!` |
| Student | `student1` | `Student123!` |
| Student | `student2` | `Student123!` |

These credentials are for local development and testing only. Passwords are stored as hashes. Do not use these credentials or development seeding in production.

The development seeder creates two students, three courses, and six enrollments. It skips seeding if the database already contains any of those records.
