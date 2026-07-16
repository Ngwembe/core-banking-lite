using Amazon;
using Amazon.SimpleNotificationService;
using core_banking_lite.Infrastructure;
using core_banking_lite.Interfaces;
using core_banking_lite.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<InfrastructureOptions>(
    builder.Configuration.GetSection(InfrastructureOptions.SectionName));

// ── Persistence ──────────────────────────────────────────────────────────────
// Swap this one call to change the entire data layer (e.g. AddEfCorePersistence).
const string connectionString = "Data Source=banking;Mode=Memory;Cache=Shared";
builder.Services.AddSqlitePersistence(connectionString);

var initializer = new SqliteDatabaseInitializer(connectionString);
await initializer.InitializeAsync();
// ─────────────────────────────────────────────────────────────────────────────

// Register SNS client as singleton — it is thread-safe and expensive to construct
builder.Services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
    return new AmazonSimpleNotificationServiceClient(
        RegionEndpoint.GetBySystemName(opts.Region));
});

// Use a fake publisher in Development so no real AWS credentials are needed
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<ISnsPublisher, FakeSnsPublisher>();
else
    builder.Services.AddSingleton<ISnsPublisher, SnsPublisher>();

builder.Services.AddHostedService<OutboxPublisherService>();

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();