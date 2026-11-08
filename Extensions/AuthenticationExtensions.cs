using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Studium_Users.Domain.Entities.Users;
using Studium_Users.Domain.Exceptions;

namespace Studium_Users.Extensions;

public static class AuthenticationExtensions
{
    public static void AddStudiumAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Authority = builder.Configuration["AWS:Cognito:Authority"];
                options.Audience = builder.Configuration["AWS:Cognito:ClientId"];

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "sub",
                    RoleClaimType = "cognito:groups"
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy =>
                policy.RequireRole(nameof(UserRole.Administrator)));
    }

    public static string GetUserId(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("UNAUTHENTICATED_USER", "Authenticated user ID claim is missing.");

        return userId;
    }

    public static bool IsSelfOrAdmin(this ClaimsPrincipal principal, string targetUserId) =>
        principal.GetUserId() == targetUserId || principal.IsInRole(nameof(UserRole.Administrator));

    public static User ToUserEntity(this ClaimsPrincipal principal)
    {
        var id = principal.GetUserId();
        var email = principal.FindFirstValue("email") ?? $"{id}@studium.edu.br";
        var fullName = principal.FindFirstValue("name") ?? email.Split('@')[0];
        var registrationNumber = principal.FindFirstValue("custom:registration_number") ?? $"REG-{id}";

        var role = Enum.TryParse<UserRole>(principal.FindFirstValue("cognito:groups"), ignoreCase: true, out var parsedRole)
            ? parsedRole
            : UserRole.Student;

        return new User(id, fullName, email, registrationNumber, role);
    }
}
