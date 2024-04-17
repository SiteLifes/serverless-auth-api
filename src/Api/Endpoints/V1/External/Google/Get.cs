using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.External.Google;

public class Get : IEndpoint
{
    private static IResult Handler([FromServices] IOptionsSnapshot<GoogleAuthOptions> googleAuthOptions)
    {
        var optionValue = googleAuthOptions.Value;
        var googleAuthUrl = $"{optionValue.OauthUrl}?response_type=code&client_id={optionValue.ClientId}&scope={optionValue.Scope}&redirect_uri={optionValue.RedirectUri}";
        return Results.Ok(googleAuthUrl);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("v1/auth/external/google", Handler)
            .Produces200<string>()
            .WithTags("External");
    }
}