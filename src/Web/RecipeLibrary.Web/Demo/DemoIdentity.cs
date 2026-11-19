using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Web.Demo;

/// <summary>Captures circuit identity without relying on a live HttpContext during events.</summary>
public sealed class DemoIdentity(IHttpContextAccessor http) : ICurrentUser, IDemoContext
{
    public const string DemoClaim = "recipelibrary:demo";
    public const string ExpiresClaim = "recipelibrary:demo-expires";
    public DateTimeOffset ExpiresAt => DateTimeOffset.TryParse(User.FindFirstValue(ExpiresClaim), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var expires) ? expires : DateTimeOffset.MinValue;
    public const string RevisionClaim = "recipelibrary:demo-revision";
    private ClaimsPrincipal? circuitUser;
    private ClaimsPrincipal User => circuitUser ?? http.HttpContext?.User ?? new ClaimsPrincipal();
    public void SetCircuitUser(ClaimsPrincipal user) => circuitUser = user;
    public string? UserId => IsAuthenticated ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    public string? UserName => IsDemo ? "Demo" : User.Identity?.Name;
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public bool IsInRole(string roleName) => !IsDemo && User.IsInRole(roleName);
    public bool IsDemo => User.HasClaim(DemoClaim, "true");
    public int Revision => int.TryParse(User.FindFirstValue(RevisionClaim), out var revision) ? revision : 0;
}

public sealed class DemoCircuitHandler(DemoIdentity identity, AuthenticationStateProvider authentication)
    : CircuitHandler, IDisposable
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        identity.SetCircuitUser((await authentication.GetAuthenticationStateAsync()).User);
        authentication.AuthenticationStateChanged += Changed;
    }

    private async void Changed(Task<AuthenticationState> state)
    {
        try { identity.SetCircuitUser((await state).User); }
        catch { identity.SetCircuitUser(new ClaimsPrincipal()); }
    }

    public void Dispose() => authentication.AuthenticationStateChanged -= Changed;
}
