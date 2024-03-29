using System.Reflection;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
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

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
    app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(async context => await Results.Problem().ExecuteAsync(context)));
}
app.UseMiddleware<ApiKeyValidatorMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapEndpointsCore(AppDomain.CurrentDomain.GetAssemblies());

app.Run();
