using Amazon;
using Amazon.SimpleNotificationService;
using core_banking_lite.Interfaces;
using core_banking_lite.Repositories;
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

// Register Dapper repository — resolved connection string at registration time
builder.Services.AddScoped<IBankAccountRepository>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
    return new BankAccountRepository(opts.ConnectionStrings.DefaultConnection);
});

// Register SNS client as singleton — it is thread-safe and expensive to construct
builder.Services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
    return new AmazonSimpleNotificationServiceClient(
        RegionEndpoint.GetBySystemName(opts.Region));
});

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();