using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ClintonFrankland.Services;

/// <summary>Applies antiforgery validation to JSON minimal API mutation endpoints.</summary>
public static class AntiforgeryEndpointExtensions
{
    public static RouteHandlerBuilder RequireAntiforgery(this RouteHandlerBuilder builder) =>
        builder
            .WithMetadata(new RequireAntiforgeryTokenAttribute(true))
            .AddEndpointFilter(async (context, next) =>
            {
                var validation = context.HttpContext.Features.Get<IAntiforgeryValidationFeature>();
                if (validation is { IsValid: false })
                    return Results.BadRequest();

                try
                {
                    await context.HttpContext.RequestServices
                        .GetRequiredService<IAntiforgery>()
                        .ValidateRequestAsync(context.HttpContext);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.BadRequest();
                }

                return await next(context);
            });
}
