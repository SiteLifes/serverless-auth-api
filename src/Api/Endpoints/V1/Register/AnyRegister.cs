using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Domains;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Register;

public class AnyRegister : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] List<string> phones,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {

        var list = new List<string>();
        
        foreach (var item in phones)
        {
            var userId = await authService.FindUserByPhone(item, cancellationToken);
            var isRegistered = !string.IsNullOrEmpty(userId);
            
            if(!isRegistered)
                list.Add(item);
        }

        return Results.Ok(list);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/anyregister", Handler)
            .Produces200<List<string>>()
            .WithTags("Register");
    }
}