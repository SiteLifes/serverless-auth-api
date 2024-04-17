using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.External.Microsoft;

public class Get : IEndpoint
{
    private static IResult Handler(
        [FromServices] IOptionsSnapshot<MicrosoftAuthOptions> microsoftAuthOptions)
    {
        var authorizationEndpoint =
            microsoftAuthOptions.Value.OauthUrl.Replace("{tenantId}", microsoftAuthOptions.Value.TenantId);

        var authorizationUrl =
            $"{authorizationEndpoint}?client_id={microsoftAuthOptions.Value.ClientId}&response_type=code&redirect_uri={microsoftAuthOptions.Value.RedirectUri}&scope=openid profile offline_access User.Read User.ReadBasic.All&response_mode=query&state=12345";

        return Results.Ok(authorizationUrl);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("v1/auth/external/microsoft", Handler)
            .Produces200()
            .WithTags("External");
    }
}