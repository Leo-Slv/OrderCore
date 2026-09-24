using System.Text.Json.Serialization;
using OrderCore.Api.Modules.AuditLogs;
using OrderCore.Api.Modules.Catalog;
using OrderCore.Api.Modules.Customers;
using OrderCore.Api.Modules.Inventory;
using OrderCore.Api.Modules.Orders;
using OrderCore.Api.Modules.Payments;
using OrderCore.Api.Shared;
using OrderCore.Api.Shared.Presentation.Conventions;
using OrderCore.Api.Shared.Presentation.Cors;
using OrderCore.Api.Shared.Presentation.ExceptionHandling;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddAuditLogsModule();

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
// as ProblemDetails with a stable "code" — see ApiExceptionHandler.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddStorefrontCors(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStorefrontCors();

// OpenAPI ("swagger") document + Scalar UI, Development-only (same
// posture as the ASP.NET Core templates' own SwaggerUI-in-Development
// default): the API surface isn't something to expose unauthenticated in
// a deployed environment by default.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { service = "OrderCore.Api", status = "ok" }));

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
