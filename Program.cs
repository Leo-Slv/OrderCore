using OrderCore.Api.Modules.AuditLogs;
using OrderCore.Api.Modules.Catalog;
using OrderCore.Api.Modules.Customers;
using OrderCore.Api.Modules.Inventory;
using OrderCore.Api.Modules.Orders;
using OrderCore.Api.Modules.Payments;
using OrderCore.Api.Shared;

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

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { service = "OrderCore.Api", status = "ok" }));

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
