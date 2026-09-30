using System.Text.Json.Serialization;
using OrderCore.Api.Modules.AuditLogs;
using OrderCore.Api.Modules.Catalog;
using OrderCore.Api.Modules.Customers;
using OrderCore.Api.Modules.Identity;
using OrderCore.Api.Modules.Inventory;
using OrderCore.Api.Modules.Messaging;
using OrderCore.Api.Modules.Orders;
using OrderCore.Api.Modules.Orders.Presentation.Realtime;
using OrderCore.Api.Modules.Payments;
using OrderCore.Api.Shared;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Conventions;
using OrderCore.Api.Shared.Presentation.Cors;
using OrderCore.Api.Shared.Infrastructure.Observability;
using OrderCore.Api.Shared.Infrastructure.Persistence;
using OrderCore.Api.Shared.Presentation.ExceptionHandling;
using OrderCore.Api.Shared.Presentation.Hosting;
using OrderCore.Api.Shared.Presentation.Observability;
using OrderCore.Api.Shared.Presentation.OpenApi;
using OrderCore.Api.Shared.Presentation.Security;
using Scalar.AspNetCore;

// `dotnet OrderCore.Api.dll migrate` applies every module's migrations and
// exits (Docs/operations/deployment.md) — the only way they run outside
// development; starting the API normally never migrates.
var migrate = args.Length > 0 && args[0] == "migrate";

var builder = WebApplication.CreateBuilder(migrate ? args[1..] : args);

// Traces, metrics and logs (OpenTelemetry, exported over OTLP when an
// endpoint is configured) — Docs/specs/observability/observability.md.
builder.AddOrderCoreObservability();

// Endpoints stay thin and delegate to the Application layer (section 39).
// Each module registers its own services through a dedicated extension
// method (section 5.1), the same way CourseCore composes
// AddCoursesModule() etc. in its own Program.cs — this keeps Program.cs
// from growing with individual service registrations as more modules gain
// Application/Infrastructure wiring.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSharedKernel();
builder.Services.AddCustomersModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddAuditLogsModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);

// Every controller declares only its own segment (e.g. [Route("orders")])
// — this convention prepends "api" once, instead of every module's
// controller repeating "api/" in its own [Route] attribute.
//
// Enums are (de)serialized by name, so request enums are accepted — and
// documented in OpenAPI — as "Card"/"Pix" instead of integers.
//
// SuppressAsyncSuffixInActionNames = false keeps action names as declared
// (e.g. "GetByIdAsync"), so CreatedAtAction(nameof(GetByIdAsync), ...) can
// find the action. With the framework's default (true) the name becomes
// "GetById", link generation fails, and every create endpoint answered
// 500 after having already saved.
builder.Services
    .AddControllers(options =>
    {
        options.Conventions.Add(new ApiRoutePrefixConvention("api"));
        options.SuppressAsyncSuffixInActionNames = false;
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Business failures (not found, rule violated, conflict) reach the client
// as ProblemDetails with a stable "code" — see ApiExceptionHandler — and
// the trace id to look the request up with.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = TraceResponseExtensions.Customize);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddStorefrontCors(builder.Configuration);
builder.Services.AddOrderCoreForwardedHeaders(builder.Configuration);
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
builder.Services.AddOrderCoreHealthChecks();
builder.Services.AddEndpointsApiExplorer();

// Deny by default: every endpoint needs a signed-in user unless it is
// marked [AllowAnonymous]; admin/customer endpoints add a policy. JWT
// bearer authentication itself is registered by the Identity module.
builder.Services.AddOrderCoreAuthorization();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecurityTransformer>();
    options.AddOperationTransformer<BearerSecurityTransformer>();
});

var app = builder.Build();

// Built but never started: no web server, no jobs, no broker connection.
if (migrate)
{
    return await app.Services.GetRequiredService<DatabaseMigrator>().RunAsync(CancellationToken.None);
}

// First: the client's real address and scheme from a trusted proxy in front
// of the API (ForwardedHeaders section), which everything after relies on.
app.UseForwardedHeaders();

// Outside development the API is served over HTTPS (terminated by the
// proxy): browsers are told to stay on it, and a plain-HTTP request is
// redirected — except the health checks, which platforms probe over HTTP
// inside their own network.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments(HealthEndpoints.PathPrefix),
        branch => branch.UseHttpsRedirection());
}

app.UseSecurityHeaders();

// Every response names its trace (W3C traceparent header).
app.UseTraceResponseHeader();
app.UseExceptionHandler();

// Empty-bodied error responses (unknown route 404, the authorization
// middleware's 401/403) become ProblemDetails too, with a default code.
app.UseStatusCodePages();
app.UseStorefrontCors();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI ("swagger") document + Scalar UI, Development-only (same
// posture as the ASP.NET Core templates' own SwaggerUI-in-Development
// default): the API surface isn't something to expose unauthenticated in
// a deployed environment by default.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapOrderCoreHealthChecks();

app.MapGet("/", () => Results.Ok(new { service = "OrderCore.Api", status = "ok" })).AllowAnonymous();

app.MapControllers();
app.MapOrderUpdatesHub();

await app.RunAsync();
return 0;

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
