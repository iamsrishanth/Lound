using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lound.Api.Data;
using Lound.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Lound.Api.IntegrationTests;

public sealed class ApiContractTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Test!Passw0rd-A3";

    [SqlServerFact]
    public async Task Standard_users_cannot_access_admin_routes_and_pending_reports_are_private()
    {
        await using var run = await fixture.ResetAsync();
        var submitter = await run.RegisterAsync("submitter");
        var other = await run.RegisterAsync("other");

        using var forbidden = await other.Client.GetAsync("/v1/admin/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var adminDashboard = await run.Admin.GetAsync("/v1/admin/dashboard");
        Assert.Equal(HttpStatusCode.OK, adminDashboard.StatusCode);

        var created = await PostItemAsync(submitter.Client, "Pending blue umbrella", "Lost");
        var itemId = created.GetProperty("id").GetString()!;

        using var search = await other.Client.GetAsync("/v1/items?keyword=Pending%20blue%20umbrella");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using var searchJson = await JsonDocument.ParseAsync(await search.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(ReadArray(searchJson.RootElement), x => x.GetProperty("id").GetString() == itemId);
        using var hiddenDetail = await other.Client.GetAsync($"/v1/items/{itemId}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenDetail.StatusCode);

        using var ownReports = await submitter.Client.GetAsync("/v1/me/items");
        Assert.Equal(HttpStatusCode.OK, ownReports.StatusCode);
        using var ownJson = await JsonDocument.ParseAsync(await ownReports.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(ownJson.RootElement), x => x.GetProperty("id").GetString() == itemId
            && x.GetProperty("moderationStatus").GetString() == "Pending");

        using var verify = await run.Admin.PostAsync($"/v1/admin/items/{itemId}/verify", JsonContent.Create(new { }));
        Assert.True(verify.IsSuccessStatusCode, await verify.Content.ReadAsStringAsync());
        using var visibleSearch = await other.Client.GetAsync("/v1/items?keyword=Pending%20blue%20umbrella");
        using var visibleJson = await JsonDocument.ParseAsync(await visibleSearch.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(visibleJson.RootElement), x => x.GetProperty("id").GetString() == itemId);
    }

    [SqlServerFact]
    public async Task Claim_evidence_is_private_and_rejecting_a_claim_keeps_the_item_found()
    {
        await using var run = await fixture.ResetAsync();
        var owner = await run.RegisterAsync("founder");
        var claimant = await run.RegisterAsync("claimant");
        var unrelated = await run.RegisterAsync("unrelated");

        var item = await PostItemAsync(owner.Client, "Found green lunch bag", "Found");
        var itemId = item.GetProperty("id").GetString()!;
        using var verify = await run.Admin.PostAsync($"/v1/admin/items/{itemId}/verify", JsonContent.Create(new { }));
        Assert.True(verify.IsSuccessStatusCode, await verify.Content.ReadAsStringAsync());

        const string privateProof = "Claim proof cobalt zipper and stitched initials Q7-41";
        var claim = await PostClaimAsync(claimant.Client, itemId, privateProof);
        var claimId = claim.GetProperty("id").GetString()!;

        using var claimantClaims = await claimant.Client.GetAsync("/v1/me/claims");
        using var claimantJson = await JsonDocument.ParseAsync(await claimantClaims.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(claimantJson.RootElement), x => x.GetProperty("id").GetString() == claimId
            && x.GetProperty("evidence").GetString() == privateProof);

        using var unrelatedClaims = await unrelated.Client.GetAsync("/v1/me/claims");
        using var unrelatedJson = await JsonDocument.ParseAsync(await unrelatedClaims.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(ReadArray(unrelatedJson.RootElement), x => x.GetProperty("evidence").GetString() == privateProof);

        using var itemDetail = await unrelated.Client.GetAsync($"/v1/items/{itemId}");
        var detailText = await itemDetail.Content.ReadAsStringAsync();
        Assert.DoesNotContain(privateProof, detailText, StringComparison.Ordinal);

        using var adminClaims = await run.Admin.GetAsync("/v1/admin/claims");
        using var adminJson = await JsonDocument.ParseAsync(await adminClaims.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(adminJson.RootElement), x => x.GetProperty("id").GetString() == claimId
            && x.GetProperty("evidence").GetString() == privateProof);

        using var reject = await run.Admin.PostAsync($"/v1/admin/claims/{claimId}/reject",
            JsonContent.Create(new { reason = "The submitted proof does not match." }));
        Assert.True(reject.IsSuccessStatusCode, await reject.Content.ReadAsStringAsync());
        using var itemAfterReject = await run.Admin.GetAsync($"/v1/items/{itemId}");
        using var itemAfterRejectJson = await JsonDocument.ParseAsync(await itemAfterReject.Content.ReadAsStreamAsync());
        Assert.Equal("Found", itemAfterRejectJson.RootElement.GetProperty("itemStatus").GetString());
    }

    [SqlServerFact]
    public async Task Rejected_and_removed_reports_keep_history_but_stay_out_of_shared_search()
    {
        await using var run = await fixture.ResetAsync();
        var owner = await run.RegisterAsync("report-owner");
        var other = await run.RegisterAsync("report-searcher");
        var item = await PostItemAsync(owner.Client, "Synthetic rejected red scarf", "Lost");
        var itemId = item.GetProperty("id").GetString()!;

        using (var reject = await run.Admin.PostAsync($"/v1/admin/items/{itemId}/reject",
            JsonContent.Create(new { reason = "Synthetic test rejection." })))
            Assert.True(reject.IsSuccessStatusCode, await reject.Content.ReadAsStringAsync());

        using var ownRejected = await owner.Client.GetAsync("/v1/me/items");
        using var ownRejectedJson = await JsonDocument.ParseAsync(await ownRejected.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(ownRejectedJson.RootElement), x => x.GetProperty("id").GetString() == itemId
            && x.GetProperty("moderationStatus").GetString() == "Rejected");

        using (var remove = await run.Admin.PostAsync($"/v1/admin/items/{itemId}/remove",
            JsonContent.Create(new { reason = "Synthetic test removal." })))
            Assert.True(remove.IsSuccessStatusCode, await remove.Content.ReadAsStringAsync());

        using var search = await other.Client.GetAsync("/v1/items?keyword=rejected%20red%20scarf");
        using var searchJson = await JsonDocument.ParseAsync(await search.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(ReadArray(searchJson.RootElement), x => x.GetProperty("id").GetString() == itemId);
        using var hiddenDetail = await other.Client.GetAsync($"/v1/items/{itemId}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenDetail.StatusCode);

        using var ownerHistory = await owner.Client.GetAsync("/v1/me/items");
        using var ownerHistoryJson = await JsonDocument.ParseAsync(await ownerHistory.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(ownerHistoryJson.RootElement), x => x.GetProperty("id").GetString() == itemId
            && x.GetProperty("moderationStatus").GetString() == "Removed");
        using var adminHistory = await run.Admin.GetAsync("/v1/admin/items?moderationStatus=Removed");
        using var adminHistoryJson = await JsonDocument.ParseAsync(await adminHistory.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(adminHistoryJson.RootElement), x => x.GetProperty("id").GetString() == itemId);
    }

    [SqlServerFact]
    public async Task Concurrent_approvals_have_one_winner_and_handover_is_visible_in_history_and_stats()
    {
        await using var run = await fixture.ResetAsync();
        var owner = await run.RegisterAsync("owner");
        var claimantA = await run.RegisterAsync("claimant-a");
        var claimantB = await run.RegisterAsync("claimant-b");

        var item = await PostItemAsync(owner.Client, "Found silver water bottle", "Found");
        var itemId = item.GetProperty("id").GetString()!;
        using (var verify = await run.Admin.PostAsync($"/v1/admin/items/{itemId}/verify", JsonContent.Create(new { })))
            Assert.True(verify.IsSuccessStatusCode, await verify.Content.ReadAsStringAsync());

        var claimA = await PostClaimAsync(claimantA.Client, itemId, "Synthetic proof A beads near the lid.");
        var claimB = await PostClaimAsync(claimantB.Client, itemId, "Synthetic proof B scratch beneath base.");
        var claimAId = claimA.GetProperty("id").GetString()!;
        var claimBId = claimB.GetProperty("id").GetString()!;

        using var adminA = run.CreateAdminClient();
        using var adminB = run.CreateAdminClient();
        var outcomes = await Task.WhenAll(
            adminA.PostAsync($"/v1/admin/claims/{claimAId}/approve", JsonContent.Create(new { })),
            adminB.PostAsync($"/v1/admin/claims/{claimBId}/approve", JsonContent.Create(new { })));
        try
        {
            Assert.Single(outcomes, x => x.IsSuccessStatusCode);
            Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var outcome in outcomes) outcome.Dispose();
        }

        using var claimsResponse = await run.Admin.GetAsync("/v1/admin/claims");
        using var claimsJson = await JsonDocument.ParseAsync(await claimsResponse.Content.ReadAsStreamAsync());
        var claims = ReadArray(claimsJson.RootElement).ToArray();
        Assert.Single(claims, x => x.GetProperty("status").GetString() == "Approved");
        Assert.Single(claims, x => x.GetProperty("status").GetString() == "Rejected");

        using var itemResponse = await run.Admin.GetAsync($"/v1/items/{itemId}");
        using var itemJson = await JsonDocument.ParseAsync(await itemResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Claimed", itemJson.RootElement.GetProperty("itemStatus").GetString());

        var approved = Assert.Single(claims, x => x.GetProperty("status").GetString() == "Approved");
        var approvedId = approved.GetProperty("id").GetString()!;
        var winningClient = approved.GetProperty("claimantDisplayName").GetString()!.EndsWith("a", StringComparison.Ordinal)
            ? claimantA.Client : claimantB.Client;

        using var recordReturn = await run.Admin.PostAsync($"/v1/admin/claims/{approvedId}/record-return", JsonContent.Create(new { }));
        Assert.True(recordReturn.IsSuccessStatusCode, await recordReturn.Content.ReadAsStringAsync());
        using var returnedClaimResponse = await winningClient.GetAsync("/v1/me/claims");
        using var returnedClaimsJson = await JsonDocument.ParseAsync(await returnedClaimResponse.Content.ReadAsStreamAsync());
        var returnedClaim = Assert.Single(ReadArray(returnedClaimsJson.RootElement));
        Assert.Equal("Returned", returnedClaim.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, returnedClaim.GetProperty("returnedAt").ValueKind);

        using var returnedItemResponse = await run.Admin.GetAsync($"/v1/items/{itemId}");
        using var returnedItemJson = await JsonDocument.ParseAsync(await returnedItemResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Returned", returnedItemJson.RootElement.GetProperty("itemStatus").GetString());

        using var dashboard = await run.Admin.GetAsync("/v1/admin/dashboard");
        using var dashboardJson = await JsonDocument.ParseAsync(await dashboard.Content.ReadAsStreamAsync());
        Assert.True(dashboardJson.RootElement.GetProperty("returnedItems").GetInt32() >= 1);
        Assert.True(dashboardJson.RootElement.GetProperty("returnedClaims").GetInt32() >= 1);

        using var summary = await run.Admin.GetAsync("/v1/admin/reports/summary?from=2020-01-01&to=2035-12-31");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        using var summaryJson = await JsonDocument.ParseAsync(await summary.Content.ReadAsStreamAsync());
        Assert.True(summaryJson.RootElement.GetProperty("returns").GetProperty("total").GetInt32() >= 1);
    }

    [SqlServerFact]
    public async Task Deactivation_and_directory_removal_revoke_existing_tokens_without_deleting_history()
    {
        await using var run = await fixture.ResetAsync();
        var deactivated = await run.RegisterAsync("deactivated");
        var removed = await run.RegisterAsync("removed");
        var report = await PostItemAsync(deactivated.Client, "History stays linked", "Lost");
        var itemId = report.GetProperty("id").GetString()!;
        var deactivatedUserId = await GetMyIdAsync(deactivated.Client);
        var removedUserId = await GetMyIdAsync(removed.Client);

        using var deactivate = await run.Admin.PostAsync($"/v1/admin/users/{deactivatedUserId}/deactivate", JsonContent.Create(new { }));
        Assert.True(deactivate.IsSuccessStatusCode, await deactivate.Content.ReadAsStringAsync());
        using var deniedDeactivated = await deactivated.Client.GetAsync("/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, deniedDeactivated.StatusCode);

        using var remove = await run.Admin.PostAsync($"/v1/admin/users/{removedUserId}/remove-from-directory", JsonContent.Create(new { }));
        Assert.True(remove.IsSuccessStatusCode, await remove.Content.ReadAsStringAsync());
        using var deniedRemoved = await removed.Client.GetAsync("/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, deniedRemoved.StatusCode);

        using var activeDirectory = await run.Admin.GetAsync("/v1/admin/users");
        using var activeDirectoryJson = await JsonDocument.ParseAsync(await activeDirectory.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(ReadArray(activeDirectoryJson.RootElement), x => x.GetProperty("id").GetString() == removedUserId);

        using var allUsers = await run.Admin.GetAsync("/v1/admin/users?includeInactive=true");
        using var allUsersJson = await JsonDocument.ParseAsync(await allUsers.Content.ReadAsStreamAsync());
        var removedAccount = Assert.Single(ReadArray(allUsersJson.RootElement), x => x.GetProperty("id").GetString() == removedUserId);
        Assert.False(removedAccount.GetProperty("isDirectoryVisible").GetBoolean());
        var inactiveAccount = Assert.Single(ReadArray(allUsersJson.RootElement), x => x.GetProperty("id").GetString() == deactivatedUserId);
        Assert.False(inactiveAccount.GetProperty("isActive").GetBoolean());

        using var history = await run.Admin.GetAsync("/v1/admin/items?moderationStatus=Pending");
        using var historyJson = await JsonDocument.ParseAsync(await history.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(historyJson.RootElement), x => x.GetProperty("id").GetString() == itemId);
    }

    [SqlServerFact]
    public async Task Invalid_input_is_rejected_and_database_state_survives_host_restart()
    {
        await using var run = await fixture.ResetAsync();
        var user = await run.RegisterAsync("persistent");

        using var invalid = await user.Client.PostAsJsonAsync("/v1/items", new
        {
            title = " ", category = "Bags", description = "Missing title should fail", location = "West hall",
            incidentDate = "2026-11-26", reportType = "Lost"
        });
        Assert.True(invalid.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity);

        using var badRange = await run.Admin.GetAsync("/v1/admin/reports/summary?from=2026-12-01&to=2026-11-01");
        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);

        using var profileUpdate = await user.Client.PatchAsJsonAsync("/v1/auth/me", new { displayName = "Persisted user profile" });
        Assert.True(profileUpdate.IsSuccessStatusCode, await profileUpdate.Content.ReadAsStringAsync());
        var report = await PostItemAsync(user.Client, "Persisted black notebook", "Found");
        var itemId = report.GetProperty("id").GetString()!;
        using (var verify = await run.Admin.PostAsync($"/v1/admin/items/{itemId}/verify", JsonContent.Create(new { })))
            Assert.True(verify.IsSuccessStatusCode, await verify.Content.ReadAsStringAsync());

        var claimant = await run.RegisterAsync("persistent-claimant");
        var claim = await PostClaimAsync(claimant.Client, itemId, "Synthetic proof survives restart.");
        var claimId = claim.GetProperty("id").GetString()!;
        using (var approve = await run.Admin.PostAsync($"/v1/admin/claims/{claimId}/approve", JsonContent.Create(new { })))
            Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());
        using (var recordReturn = await run.Admin.PostAsync($"/v1/admin/claims/{claimId}/record-return", JsonContent.Create(new { })))
            Assert.True(recordReturn.IsSuccessStatusCode, await recordReturn.Content.ReadAsStringAsync());

        await fixture.RestartHostAsync();
        using var restartedClient = fixture.CreateAuthenticatedClient(await fixture.LoginAsync(user.Email, user.Password));
        using var ownReports = await restartedClient.GetAsync("/v1/me/items");
        using var ownJson = await JsonDocument.ParseAsync(await ownReports.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(ownJson.RootElement), x => x.GetProperty("id").GetString() == itemId
            && x.GetProperty("moderationStatus").GetString() == "Verified"
            && x.GetProperty("itemStatus").GetString() == "Returned");
        using var restartedProfile = await restartedClient.GetAsync("/v1/auth/me");
        using var profileJson = await JsonDocument.ParseAsync(await restartedProfile.Content.ReadAsStreamAsync());
        Assert.Equal("Persisted user profile", profileJson.RootElement.GetProperty("displayName").GetString());

        using var restartedClaimant = fixture.CreateAuthenticatedClient(await fixture.LoginAsync(claimant.Email, claimant.Password));
        using var ownClaims = await restartedClaimant.GetAsync("/v1/me/claims");
        using var ownClaimsJson = await JsonDocument.ParseAsync(await ownClaims.Content.ReadAsStreamAsync());
        Assert.Contains(ReadArray(ownClaimsJson.RootElement), x => x.GetProperty("id").GetString() == claimId
            && x.GetProperty("status").GetString() == "Returned");
    }

    private static async Task<JsonElement> PostItemAsync(HttpClient client, string title, string reportType)
    {
        using var response = await client.PostAsJsonAsync("/v1/items", new
        {
            title,
            category = "Bags",
            description = "Synthetic item description for automated integration testing.",
            location = "West campus library",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).ToString("yyyy-MM-dd"),
            reportType
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> PostClaimAsync(HttpClient client, string itemId, string evidence)
    {
        using var response = await client.PostAsJsonAsync($"/v1/items/{itemId}/claims", new { evidence });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private static async Task<string> GetMyIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/v1/auth/me");
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    private static IEnumerable<JsonElement> ReadArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array) return element.EnumerateArray().Select(x => x.Clone());
        foreach (var property in new[] { "items", "claims", "users" })
        {
            if (element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
                return array.EnumerateArray().Select(x => x.Clone());
        }
        return Array.Empty<JsonElement>();
    }
}

public sealed class ApiFixture : IAsyncLifetime
{
    private const string AdminEmail = "integration-admin@example.test";
    private const string AdminPassword = "Admin!Passw0rd-A3";
    private const string TestJwtKey = "test-only-signing-key-at-least-sixty-four-characters-long-2026";
    private readonly string? _baseConnectionString = Environment.GetEnvironmentVariable("LOUND_TEST_CONNECTION");
    private WebApplicationFactory<Program>? _factory;
    private string? _databaseName;
    private string? _databaseConnectionString;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task<TestRun> ResetAsync()
    {
        await EnsureDatabaseAsync();
        var factory = EnsureFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();

            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { RoleNames.User, RoleNames.Admin })
            {
                if (!await roles.RoleExistsAsync(role))
                {
                    var roleResult = await roles.CreateAsync(new IdentityRole(role));
                    Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(x => x.Description)));
                }
            }

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                DisplayName = "Synthetic Admin"
            };
            var create = await users.CreateAsync(admin, AdminPassword);
            Assert.True(create.Succeeded, string.Join("; ", create.Errors.Select(x => x.Description)));
            var assign = await users.AddToRoleAsync(admin, RoleNames.Admin);
            Assert.True(assign.Succeeded, string.Join("; ", assign.Errors.Select(x => x.Description)));
        }

        using var anonymous = factory.CreateClient();
        var token = await LoginAsync(anonymous, AdminEmail, AdminPassword);
        return new TestRun(this, factory, token);
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        var client = EnsureFactory().CreateClient();
        return await LoginAsync(client, email, password);
    }

    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = EnsureFactory().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task RestartHostAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        _factory = null;
        var factory = EnsureFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    private async Task EnsureDatabaseAsync()
    {
        if (_databaseName is not null) return;
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
            throw new InvalidOperationException("BLOCKED: SQL Server integration tests require LOUND_TEST_CONNECTION.");

        try
        {
            var builder = new SqlConnectionStringBuilder(_baseConnectionString)
            {
                InitialCatalog = "master",
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            _databaseName = $"LoundTests_{Guid.NewGuid():N}";
            await using var command = new SqlCommand($"CREATE DATABASE [{_databaseName}]", connection);
            await command.ExecuteNonQueryAsync();

            builder.InitialCatalog = _databaseName;
            _databaseConnectionString = builder.ConnectionString;
        }
        catch (Exception error) when (error is SqlException or InvalidOperationException or ArgumentException)
        {
            _databaseName = null;
            throw new Xunit.Sdk.XunitException($"BLOCKED: SQL Server test database setup failed ({error.GetType().Name}). Check LocalDB availability and CREATE/DROP DATABASE permissions.");
        }
    }

    private WebApplicationFactory<Program> EnsureFactory()
    {
        if (_factory is not null) return _factory;
        if (_databaseConnectionString is null) throw new InvalidOperationException("The test SQL database has not been initialized.");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "Lound.IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "Lound.IntegrationTests");
        _factory = new TestApiFactory(_databaseConnectionString, TestJwtKey);
        return _factory;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        _factory = null;
        if (_databaseName is null || string.IsNullOrWhiteSpace(_baseConnectionString)) return;

        SqlConnection.ClearAllPools();
        try
        {
            var builder = new SqlConnectionStringBuilder(_baseConnectionString)
            {
                InitialCatalog = "master",
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END",
                connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Cleanup failure must not hide the test outcome; the database name is generated by this fixture.
        }
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private sealed class TestApiFactory(string connectionString, string signingKey) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Jwt:SigningKey"] = signingKey,
                    ["Jwt:Issuer"] = "Lound.IntegrationTests",
                    ["Jwt:Audience"] = "Lound.IntegrationTests",
                    ["Jwt:AccessTokenMinutes"] = "30"
                }));
        }
    }
}

public sealed class TestRun(ApiFixture fixture, WebApplicationFactory<Program> factory, string adminToken) : IAsyncDisposable
{
    private const string Password = "Test!Passw0rd-A3";
    private readonly List<HttpClient> _clients = new();
    private HttpClient? _adminClient;

    public HttpClient Admin => _adminClient ??= Track(fixture.CreateAuthenticatedClient(adminToken));
    public HttpClient CreateAdminClient() => Track(fixture.CreateAuthenticatedClient(adminToken));

    public async Task<IdentitySession> RegisterAsync(string localPart)
    {
        var email = $"{localPart}-{Guid.NewGuid():N}@example.test";
        return await RegisterAsyncWithEmail(email, $"Synthetic {localPart}");
    }

    public async Task<IdentitySession> RegisterAsyncWithEmail(string email, string displayName)
    {
        using var anonymous = factory.CreateClient();
        using var response = await anonymous.PostAsJsonAsync("/v1/auth/register", new { email, password = Password, displayName });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var token = await LoginAsync(anonymous, email, Password);
        return new IdentitySession(email, Password, Track(fixture.CreateAuthenticatedClient(token)));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private HttpClient Track(HttpClient client)
    {
        _clients.Add(client);
        return client;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var client in _clients) client.Dispose();
        _clients.Clear();
        return ValueTask.CompletedTask;
    }
}

public sealed record IdentitySession(string Email, string Password, HttpClient Client);

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOUND_TEST_CONNECTION")))
            Skip = "BLOCKED: SQL Server integration tests are skipped. Set LOUND_TEST_CONNECTION to an isolated SQL Server instance with CREATE DATABASE permission.";
    }
}
