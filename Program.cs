using OrderCore.Api.Modules.AuditLogs;
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
builder.Services.AddOrdersModule();
builder.Services.AddPaymentsModule();
builder.Services.AddAuditLogsModule();

// AuditLogsController is the first controller-based endpoint in the
// project (every other module is still minimal-API/no endpoints yet), so
// this is where MVC controllers get wired in.
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
