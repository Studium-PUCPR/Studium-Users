using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Studium_Users.Data;
using Studium_Users.Extensions;
using Studium_Users.Middleware;
using Studium_Users.Services.UserEventsPublishers;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=studium_users;Username=postgres;Password=postgres";

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUserEventsPublisher, UserEventsPublisher>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

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

app.Run();
