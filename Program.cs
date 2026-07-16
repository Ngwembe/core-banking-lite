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

// Keep one connection open for the lifetime of the app so the named
// in-memory database is not destroyed between requests.
const string sqliteConnectionString = "Data Source=banking;Mode=Memory;Cache=Shared";

var keepAlive = new SqliteConnection(sqliteConnectionString);
keepAlive.Open();
builder.Services.AddSingleton(keepAlive);

// Initialise schema + seed data before the app starts serving requests.
var initializer = new SqliteDatabaseInitializer(sqliteConnectionString);
await initializer.InitializeAsync();

// Register Dapper repository
builder.Services.AddScoped<IBankAccountRepository>(_ =>
    new BankAccountRepository(sqliteConnectionString));

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

builder.Services.AddSingleton<IHostedService>(sp =>
    new OutboxPublisherService(
        sqliteConnectionString,
        sp.GetRequiredService<IOptions<InfrastructureOptions>>(),
        sp.GetRequiredService<ISnsPublisher>(),
        sp.GetRequiredService<ILogger<OutboxPublisherService>>()));

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();

keepAlive.Dispose();