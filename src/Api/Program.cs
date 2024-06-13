using System.Reflection;
using Api.Infrastructure.Extensions;
using Api.Infrastructure.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Extensions;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Logging.ClearProviders();
// Serilog configuration        
var logger = new LoggerConfiguration()
    .Enrich.WithProperty("Application", "Auth")
    .WriteTo.Console(new JsonFormatter())
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("AWSSDK", LogEventLevel.Warning)
    .MinimumLevel.Override("System.", LogEventLevel.Warning)
    .CreateLogger();
// Register Serilog
builder.Logging.AddSerilog(logger);

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.CustomOperationIds(e =>
    {
        // Extract and join route values
        var routeValues = string.Join(
            "_",
            e.ActionDescriptor.RouteValues.OrderByDescending(o => o.Key)
                .Select(i => i.Value)
        );

        // Extract the namespace from the MethodInfo in EndpointMetadata
        var methodInfo = e
            .ActionDescriptor.EndpointMetadata.OfType<MethodInfo>()
            .FirstOrDefault();
        var namespaceName =
            methodInfo?.DeclaringType?.Namespace?.Split('.').Last() ?? "Default";

        // Return the custom operation ID including the namespace and route values
        return $"{namespaceName}_{routeValues}";
    });
});
builder.Services.AddScoped<ApiKeyValidatorMiddleware>();


var option = builder.Configuration.GetAWSOptions();
builder.Services.AddDefaultAWSOptions(option);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(exceptionHandlerApp =>
        exceptionHandlerApp.Run(async context => await Results.Problem().ExecuteAsync(context)));
}

app.UseMiddleware<ApiKeyValidatorMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapEndpointsCore(AppDomain.CurrentDomain.GetAssemblies());

app.Run();