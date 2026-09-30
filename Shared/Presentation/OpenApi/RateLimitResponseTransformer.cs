using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace OrderCore.Api.Shared.Presentation.OpenApi;

/// <summary>
/// Declares the <c>429</c> of every rate-limited action (one with
/// <c>[EnableRateLimiting]</c>, on itself or its controller), the way
/// <see cref="BearerSecurityTransformer"/> declares 401/403 — so actions don't
/// repeat it and the document can't forget it.
/// </summary>
public sealed class RateLimitResponseTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("429", new OpenApiResponse
            {
                Description = "Too many requests (code too_many_requests); retry after the Retry-After header's seconds.",
            });
        }

        return Task.CompletedTask;
    }
}
