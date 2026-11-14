using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MarksApi.Contracts;

namespace MarksApi.SmokeTests;

public sealed class SmokeTestRunner(IConfiguration configuration) : IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private int _passed;
    private int _failed;

    public async Task<bool> RunAsync(Uri baseAddress, CancellationToken cancellationToken = default)
    {
        _client.BaseAddress = baseAddress;
        string teacherName;
        string teacherPassword;
        string studentName;
        string studentPassword;
        try
        {
            teacherName = RequiredSetting("DemoAccounts:TeacherUsername");
            teacherPassword = RequiredSetting("DemoAccounts:TeacherPassword");
            studentName = RequiredSetting("DemoAccounts:Student1Username");
            studentPassword = RequiredSetting("DemoAccounts:Student1Password");
        }
        catch (InvalidOperationException exception)
        {
            RecordContract("Smoke test configuration", "all demo account settings", "missing", false);
            Console.Error.WriteLine(exception.Message);
            Console.WriteLine($"Smoke test summary: {_passed} passed, {_failed} failed.");
            return false;
        }
        string? teacherToken = null;
        string? studentToken = null;
        int? smokeStudentId = null;
        int? smokeCourseId = null;
        int? smokeSecondCourseId = null;
        int? smokeEnrollmentId = null;
        int? smokeSecondEnrollmentId = null;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var studentBody = new
        {
            registrationNo = $"SMOKE-{suffix}",
            fullName = "Smoke Test Student",
            email = $"smoke-{suffix}@example.test"
        };
        var courseBody = new
        {
            code = $"SM{suffix}",
            title = "Smoke Test Course",
            creditHours = 3
        };

        try
        {
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, "/health", null, null, StatusCodes.Status200OK, cancellationToken))
            {
                if (response is null)
                {
                    return Finish();
                }
            }

            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/auth/login",
                new { username = teacherName, password = teacherPassword },
                null, StatusCodes.Status200OK, cancellationToken))
            {
                teacherToken = await ReadStringAsync(response, "token", cancellationToken);
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/auth/login",
                new { username = "smoke-invalid-user", password = "invalid-password" },
                null, StatusCodes.Status401Unauthorized, cancellationToken))
            {
            }
            if (string.IsNullOrWhiteSpace(teacherToken))
            {
                return Finish();
            }

            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/auth/login",
                new { username = studentName, password = studentPassword },
                null, StatusCodes.Status200OK, cancellationToken))
            {
                studentToken = await ReadStringAsync(response, "token", cancellationToken);
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, "/api/students", null, null,
                StatusCodes.Status401Unauthorized, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, "/api/students?page=1&pageSize=10", null, teacherToken,
                StatusCodes.Status200OK, cancellationToken))
            {
            }

            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/students", studentBody, teacherToken,
                StatusCodes.Status201Created, cancellationToken))
            {
                smokeStudentId = await ReadIntAsync(response, "id", cancellationToken);
            }
            if (smokeStudentId is null)
            {
                return Finish();
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/students",
                new
                {
                    registrationNo = $"SMOKE-DUP-{suffix}",
                    fullName = "Duplicate Email",
                    email = studentBody.email
                },
                teacherToken, StatusCodes.Status409Conflict, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/students",
                new
                {
                    registrationNo = studentBody.registrationNo,
                    fullName = "Duplicate Registration",
                    email = $"smoke-duplicate-{suffix}@example.test"
                },
                teacherToken, StatusCodes.Status409Conflict, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/students",
                new { registrationNo = $"SMOKE-BAD-{suffix}", fullName = "", email = "not-an-email" },
                teacherToken, StatusCodes.Status400BadRequest, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/students/{smokeStudentId}", null, teacherToken,
                StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Put, $"/api/students/{smokeStudentId}",
                new
                {
                    registrationNo = studentBody.registrationNo,
                    fullName = "Smoke Updated Student",
                    email = studentBody.email
                },
                teacherToken, StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/students/{int.MaxValue}", null, teacherToken,
                StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Put, $"/api/students/{int.MaxValue}",
                studentBody, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Delete, $"/api/students/{int.MaxValue}",
                null, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/students/{int.MaxValue}/transcript",
                null, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }

            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/courses", courseBody, teacherToken,
                StatusCodes.Status201Created, cancellationToken))
            {
                smokeCourseId = await ReadIntAsync(response, "id", cancellationToken);
            }
            if (smokeCourseId is null)
            {
                return Finish();
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, "/api/courses", null, studentToken,
                StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, "/api/courses", null, null,
                StatusCodes.Status401Unauthorized, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{smokeCourseId}", null, teacherToken,
                StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{int.MaxValue}", null, teacherToken,
                StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Put, $"/api/courses/{smokeCourseId}",
                new { courseBody.code, title = "Smoke Updated Course", courseBody.creditHours },
                teacherToken, StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Put, $"/api/courses/{int.MaxValue}",
                courseBody, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Delete, $"/api/courses/{int.MaxValue}",
                null, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/courses",
                new { code = courseBody.code, title = "Duplicate Code", creditHours = 2 },
                teacherToken, StatusCodes.Status409Conflict, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/courses",
                new { code = $"BAD{suffix}", title = "Invalid Credit", creditHours = 5 },
                teacherToken, StatusCodes.Status400BadRequest, cancellationToken))
            {
            }

            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, $"/api/courses/{smokeCourseId}/enrollments",
                new { studentId = smokeStudentId, mark = (int?)null },
                teacherToken, StatusCodes.Status201Created, cancellationToken))
            {
                smokeEnrollmentId = await ReadIntAsync(response, "id", cancellationToken);
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/courses",
                new { code = $"SC{suffix}", title = "Smoke Weighted Course", creditHours = 2 },
                teacherToken, StatusCodes.Status201Created, cancellationToken))
            {
                smokeSecondCourseId = await ReadIntAsync(response, "id", cancellationToken);
            }
            if (smokeSecondCourseId.HasValue)
            {
                using var response = await SendAndCheckAsync(
                    HttpMethod.Post, $"/api/courses/{smokeSecondCourseId}/enrollments",
                    new { studentId = smokeStudentId, mark = 60 },
                    teacherToken, StatusCodes.Status201Created, cancellationToken);
                smokeSecondEnrollmentId = await ReadIntAsync(response, "id", cancellationToken);
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, $"/api/courses/{int.MaxValue}/enrollments",
                new { studentId = smokeStudentId, mark = (int?)null },
                teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, $"/api/courses/{smokeCourseId}/enrollments",
                new { studentId = int.MaxValue, mark = (int?)null },
                teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, $"/api/courses/{smokeCourseId}/enrollments",
                new { studentId = smokeStudentId, mark = 101 },
                teacherToken, StatusCodes.Status400BadRequest, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{int.MaxValue}/enrollments",
                null, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            if (smokeEnrollmentId is null)
            {
                return Finish();
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, $"/api/courses/{smokeCourseId}/enrollments",
                new { studentId = smokeStudentId, mark = 70 },
                teacherToken, StatusCodes.Status409Conflict, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{smokeCourseId}/enrollments",
                null, teacherToken, StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{smokeCourseId}/enrollments",
                null, studentToken, StatusCodes.Status403Forbidden, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/enrollments/{smokeEnrollmentId}",
                null, teacherToken, StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/enrollments/{int.MaxValue}",
                null, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{smokeCourseId}/stats",
                null, teacherToken, StatusCodes.Status200OK, cancellationToken))
            {
                await AssertNullableDecimalAsync(
                    response, "/api/courses/{courseId}/stats", "average", null, cancellationToken);
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Put, $"/api/enrollments/{smokeEnrollmentId}/mark",
                new { mark = 80 }, teacherToken, StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Put, $"/api/enrollments/{int.MaxValue}/mark",
                new { mark = 80 }, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Put, $"/api/enrollments/{smokeEnrollmentId}/mark",
                new { mark = 101 }, teacherToken, StatusCodes.Status400BadRequest, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Put, $"/api/enrollments/{smokeEnrollmentId}/mark",
                new { mark = -1 }, teacherToken, StatusCodes.Status400BadRequest, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{smokeCourseId}/stats",
                null, teacherToken, StatusCodes.Status200OK, cancellationToken))
            {
                await AssertNullableDecimalAsync(
                    response, "/api/courses/{courseId}/stats", "average", 80m, cancellationToken);
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{smokeCourseId}/stats",
                null, studentToken, StatusCodes.Status403Forbidden, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/courses/{int.MaxValue}/stats",
                null, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/students/{smokeStudentId}/transcript",
                null, teacherToken, StatusCodes.Status200OK, cancellationToken))
            {
                await AssertNullableDecimalAsync(
                    response, "/api/students/{id}/transcript", "weightedAverage", 72m, cancellationToken);
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Delete, $"/api/students/{smokeStudentId}",
                null, teacherToken, StatusCodes.Status409Conflict, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Delete, $"/api/courses/{smokeCourseId}",
                null, teacherToken, StatusCodes.Status409Conflict, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Delete, $"/api/enrollments/{int.MaxValue}",
                null, teacherToken, StatusCodes.Status404NotFound, cancellationToken))
            {
            }

            if (string.IsNullOrWhiteSpace(studentToken))
            {
                return Finish();
            }
            var studentId = ReadStudentId(studentToken);
            if (studentId is null)
            {
                RecordContract("Student login token", "student_id claim", "missing or invalid", false);
                return Finish();
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/students/{studentId}", null, studentToken,
                StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/students/{smokeStudentId}/transcript", null, studentToken,
                StatusCodes.Status403Forbidden, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, $"/api/students/{studentId}/transcript", null, studentToken,
                StatusCodes.Status200OK, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Get, "/api/students", null, studentToken,
                StatusCodes.Status403Forbidden, cancellationToken))
            {
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Post, "/api/courses",
                new { code = $"ST{suffix}", title = "Forbidden Course", creditHours = 1 },
                studentToken, StatusCodes.Status403Forbidden, cancellationToken))
            {
            }

            using (var response = await SendAndCheckAsync(
                HttpMethod.Delete, $"/api/enrollments/{smokeEnrollmentId}",
                null, teacherToken, StatusCodes.Status204NoContent, cancellationToken))
            {
                if (response?.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    smokeEnrollmentId = null;
                }
            }
            if (smokeSecondEnrollmentId.HasValue)
            {
                using var response = await SendAndCheckAsync(
                    HttpMethod.Delete, $"/api/enrollments/{smokeSecondEnrollmentId}",
                    null, teacherToken, StatusCodes.Status204NoContent, cancellationToken);
                if (response?.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    smokeSecondEnrollmentId = null;
                }
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Delete, $"/api/courses/{smokeCourseId}",
                null, teacherToken, StatusCodes.Status204NoContent, cancellationToken))
            {
                if (response?.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    smokeCourseId = null;
                }
            }
            if (smokeSecondCourseId.HasValue)
            {
                using var response = await SendAndCheckAsync(
                    HttpMethod.Delete, $"/api/courses/{smokeSecondCourseId}",
                    null, teacherToken, StatusCodes.Status204NoContent, cancellationToken);
                if (response?.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    smokeSecondCourseId = null;
                }
            }
            using (var response = await SendAndCheckAsync(
                HttpMethod.Delete, $"/api/students/{smokeStudentId}",
                null, teacherToken, StatusCodes.Status204NoContent, cancellationToken))
            {
                if (response?.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    smokeStudentId = null;
                }
            }

            return Finish();
        }
        finally
        {
            if (teacherToken is not null)
            {
                if (smokeEnrollmentId.HasValue)
                {
                    await CleanupAsync($"/api/enrollments/{smokeEnrollmentId}", teacherToken, cancellationToken);
                }
                if (smokeSecondEnrollmentId.HasValue)
                {
                    await CleanupAsync($"/api/enrollments/{smokeSecondEnrollmentId}", teacherToken, cancellationToken);
                }
                if (smokeCourseId.HasValue)
                {
                    await CleanupAsync($"/api/courses/{smokeCourseId}", teacherToken, cancellationToken);
                }
                if (smokeSecondCourseId.HasValue)
                {
                    await CleanupAsync($"/api/courses/{smokeSecondCourseId}", teacherToken, cancellationToken);
                }
                if (smokeStudentId.HasValue)
                {
                    await CleanupAsync($"/api/students/{smokeStudentId}", teacherToken, cancellationToken);
                }
            }
            Console.WriteLine($"Smoke test summary: {_passed} passed, {_failed} failed.");
        }
    }

    private async Task<HttpResponseMessage?> SendAndCheckAsync(
        HttpMethod method,
        string path,
        object? body,
        string? token,
        int expectedStatus,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        try
        {
            var response = await _client.SendAsync(request, cancellationToken);
            var actualStatus = (int)response.StatusCode;
            Record(method, path, expectedStatus, actualStatus);
            if (actualStatus == expectedStatus && expectedStatus == StatusCodes.Status201Created)
            {
                RecordContract(
                    $"{method.Method} {path}",
                    "Location header",
                    response.Headers.Location?.ToString() ?? "missing",
                    response.Headers.Location is not null);
            }
            return response;
        }
        catch (HttpRequestException exception)
        {
            Record(method, path, expectedStatus, "HTTP ERROR");
            Console.Error.WriteLine(exception.Message);
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Record(method, path, expectedStatus, "TIMEOUT");
            return null;
        }
    }

    private async Task CleanupAsync(string path, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await _client.SendAsync(request, cancellationToken);
            var actual = (int)response.StatusCode;
            var accepted = actual is StatusCodes.Status204NoContent or StatusCodes.Status404NotFound;
            RecordAlternative(HttpMethod.Delete, path, "204 or 404", actual, accepted);
        }
        catch (HttpRequestException exception)
        {
            RecordAlternative(HttpMethod.Delete, path, "204 or 404", "HTTP ERROR", false);
            Console.Error.WriteLine(exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RecordAlternative(HttpMethod.Delete, path, "204 or 404", "TIMEOUT", false);
        }
    }

    private void Record(HttpMethod method, string path, int expected, object actual)
    {
        var passed = actual is int code && code == expected;
        if (passed)
        {
            _passed++;
        }
        else
        {
            _failed++;
        }
        Console.WriteLine(
            $"{(passed ? "PASS" : "FAIL")} {method.Method} {path} expected={expected} actual={actual}");
    }

    private void RecordAlternative(HttpMethod method, string path, string expected, object actual, bool passed)
    {
        if (passed)
        {
            _passed++;
        }
        else
        {
            _failed++;
        }
        Console.WriteLine(
            $"{(passed ? "PASS" : "FAIL")} {method.Method} {path} expected={expected} actual={actual}");
    }

    private void RecordContract(string path, string expected, object actual, bool passed)
    {
        if (passed)
        {
            _passed++;
        }
        else
        {
            _failed++;
        }
        Console.WriteLine(
            $"{(passed ? "PASS" : "FAIL")} {path} expected={expected} actual={actual}");
    }

    private async Task AssertNullableDecimalAsync(
        HttpResponseMessage? response,
        string path,
        string property,
        decimal? expected,
        CancellationToken cancellationToken)
    {
        decimal? actual = null;
        var found = false;
        if (response is not null && response.IsSuccessStatusCode)
        {
            try
            {
                using var document = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(property, out var value))
                {
                    found = true;
                    if (value.ValueKind != JsonValueKind.Null && value.TryGetDecimal(out var number))
                    {
                        actual = number;
                    }
                    else if (value.ValueKind != JsonValueKind.Null)
                    {
                        found = false;
                    }
                }
            }
            catch (JsonException)
            {
                found = false;
            }
        }

        RecordContract(
            $"{path} {property}",
            expected?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null",
            actual?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null",
            found && actual == expected);
    }

    private bool Finish() => _failed == 0;

    private string RequiredSetting(string key) =>
        configuration[key] ?? throw new InvalidOperationException(
            $"Smoke test configuration value {key} is required.");

    private async Task<string?> ReadStringAsync(
        HttpResponseMessage? response, string property, CancellationToken cancellationToken)
    {
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
        }
        RecordContract("Response body", $"string property '{property}'", "missing or invalid", false);
        return null;
    }

    private async Task<int?> ReadIntAsync(
        HttpResponseMessage? response, string property, CancellationToken cancellationToken)
    {
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(property, out var value)
                && value.TryGetInt32(out var id))
            {
                return id;
            }
        }
        catch (JsonException)
        {
        }
        RecordContract("Response body", $"integer property '{property}'", "missing or invalid", false);
        var locationSegment = response.Headers.Location?.Segments.LastOrDefault()?.TrimEnd('/');
        return int.TryParse(locationSegment, out var locationId) ? locationId : null;
    }

    private static int? ReadStudentId(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var value = jwt.Claims.SingleOrDefault(claim => claim.Type == "student_id")?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public void Dispose() => _client.Dispose();
}
