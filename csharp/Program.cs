using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Configure Application Insights
builder.Services.AddApplicationInsightsTelemetryWorkerService();

builder.ConfigureFunctionsWebApplication();

builder.Build().Run();
