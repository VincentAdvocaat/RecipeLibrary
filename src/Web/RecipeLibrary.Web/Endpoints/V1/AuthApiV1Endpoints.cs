using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RecipeLibrary.Infrastructure.Identity;

namespace RecipeLibrary.Web.Endpoints.V1;

public static class AuthApiV1Endpoints
{
    public static RouteGroupBuilder MapAuthApiV1(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("RegisterUser");

        return group;
    }

    private static async Task<Results<Created<RegisterUserResponse>, ValidationProblem, ProblemHttpResult>> RegisterAsync(
        [FromBody] RegisterUserRequest request,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.UserName)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["Email, UserName, and Password are required."],
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => string.IsNullOrWhiteSpace(e.Code) ? "identity" : e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Created(
            $"/api/v1/auth/register/{user.Id}",
            new RegisterUserResponse(user.Id!, user.UserName!, user.Email!));
    }
}

public sealed record RegisterUserRequest(string Email, string UserName, string Password);

public sealed record RegisterUserResponse(string UserId, string UserName, string Email);
