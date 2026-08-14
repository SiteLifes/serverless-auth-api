using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Domains;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Register;

public class Register : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] RegisterRequest request,
        [FromServices] IAuthService authService,
        [FromServices] IJwtService jwtService,
        [FromServices] IValidator<RegisterRequest> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());
        
        if (!string.IsNullOrEmpty(request.Phone))
        {
            var phoneMappingCreated = await authService.CreatePhoneUserMapping(
                request.Phone,
                request.UserId,
                cancellationToken);
            if (!phoneMappingCreated)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Telefon numarası kullanımda",
                    Detail = "Bu telefon numarası başka bir kullanıcı hesabına bağlı."
                });
            }
        }

        if (!string.IsNullOrEmpty(request.Email))
        {
            await authService.CreateEmailUserMapping(request.Email.ToLower(), request.UserId, cancellationToken);
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            await authService.CreatePasswordUserMapping(request.UserId, request.Password, cancellationToken);
        }

        var jwt = await jwtService.CreateJwtAsync(request.UserId, cancellationToken);

        return Results.Ok(jwt);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/register", Handler)
            .Produces200<JwtDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Register");
    }

    public record RegisterRequest(string Phone, string? Email, string Password, string UserId);

    public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(q => q.Password).NotEmpty().When(q => !string.IsNullOrEmpty(q.Email));
            RuleFor(q => q.Email).NotEmpty().When(q => string.IsNullOrEmpty(q.Phone));
            RuleFor(q => q.Phone).NotEmpty().When(q => string.IsNullOrEmpty(q.Email));
        }
    }
}
