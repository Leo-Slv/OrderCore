using OrderCore.Api.Modules.Orders;
using OrderCore.Api.Modules.Payments;

var builder = WebApplication.CreateBuilder(args);

// Endpoints stay thin and delegate to the Application layer (section 39).
// Each module registers its own services through a dedicated extension
// method (section 5.1), the same way CourseCore composes
// AddCoursesModule() etc. in its own Program.cs — this keeps Program.cs
// from growing with individual service registrations as more modules gain
// Application/Infrastructure wiring.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOrdersModule();
builder.Services.AddPaymentsModule();

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { service = "OrderCore.Api", status = "ok" }));

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
