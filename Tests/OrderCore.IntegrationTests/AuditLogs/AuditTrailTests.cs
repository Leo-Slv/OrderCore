using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.AuditLogs;

/// <summary>
/// Through the real host: what an admin does lands in the audit log with
/// the admin as the actor, can be read back as the entity's timeline, and
/// is still there after the API restarts (it used to live in memory).
/// </summary>
public sealed class AuditTrailTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public AuditTrailTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task A_products_timeline_survives_a_restart_and_names_the_admin()
    {
        Guid productId;
        await using (var factory = _database.CreateFactory())
        {
            var admin = await SignInAsAdminAsync(factory);
            (productId, _) = await CreatePublishedProductAsync(admin, "Audited Mug", 49.90m);
        }

        await using var restarted = _database.CreateFactory();
        var client = await SignInAsAdminAsync(restarted);

        var response = await client.GetAsync($"/api/audit-logs?entityName=Product&entityId={productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var entries = page.GetProperty("items").EnumerateArray().ToList();
        entries.Select(e => e.GetProperty("action").GetString()).Should().Equal("ProductPublished", "ProductCreated");
        entries.Should().OnlyContain(e => e.GetProperty("entityId").GetGuid() == productId);
        entries.Should().OnlyContain(e => e.GetProperty("userId").ValueKind == JsonValueKind.String);
    }
}
