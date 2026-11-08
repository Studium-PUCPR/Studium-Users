using System.Text.Json.Serialization;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Studium_Users.Api.Extensions;
using Studium_Users.Api.HostedServices;
using Studium_Users.Api.Middleware;
using Studium_Users.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=studium_users;Username=postgres;Password=postgres";

builder.Services.AddDbContext<UsersDbContext>(
    options => options.UseNpgsql(connectionString),
    optionsLifetime: ServiceLifetime.Singleton);

builder.Services.AddHostedService<DatabaseMigrationHostedService>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.CritterStackDefaults(options =>
{
    options.Development.GeneratedCodeMode = TypeLoadMode.Dynamic;
    options.Production.GeneratedCodeMode = TypeLoadMode.Static;
    options.Production.AssertAllPreGeneratedTypesExist = true;
});

builder.AddStudiumMessaging();
builder.AddStudiumAuthentication();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
    options.AddPolicy("StudiumCors", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors("StudiumCors");
app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Studium-Users",
    pod = Environment.MachineName
}));

return await app.RunJasperFxCommands(args);
