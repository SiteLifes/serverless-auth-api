using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Domains;
using Domain.Entities;
using Domain.Services;
using FluentValidation;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Login;

public class AnyLogin : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] AnyLoginRequestModel request,
        [FromServices] AuthRepository repository,
        [FromServices] IValidator<AnyLoginRequestModel> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());
        
        var phone = request.phone.Replace("+90","");

        var result = await repository.GetLoginAsync(phone, cancellationToken);
        bool exists = result is not null;
        return Results.Ok(exists);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/login/any-login", Handler)
            .Produces200<bool>()
            .WithTags("Login");
    }

    public record AnyLoginRequestModel(string phone);

    public class AnyLoginRequestModelValidator : AbstractValidator<AnyLoginRequestModel>
    {
        public AnyLoginRequestModelValidator()
        {
            RuleFor(q => q.phone).NotEmpty();
        }
    }
}
