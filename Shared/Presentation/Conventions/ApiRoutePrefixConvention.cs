using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace OrderCore.Api.Shared.Presentation.Conventions;

/// <summary>
/// Prepends a fixed prefix (e.g. "api") to every controller's route,
/// applied once in <c>Program.cs</c> (<c>AddControllers(options =>
/// options.Conventions.Add(...))</c>) instead of every module's controller
/// repeating it in its own <c>[Route("api/...")]</c> attribute. A module's
/// controller only declares its own segment, e.g. <c>[Route("orders")]</c>
/// — this convention combines it into <c>api/orders</c>.
/// </summary>
public sealed class ApiRoutePrefixConvention : IApplicationModelConvention
{
    private readonly AttributeRouteModel _prefix;

    public ApiRoutePrefixConvention(string prefix)
    {
        _prefix = new AttributeRouteModel(new RouteAttribute(prefix));
    }

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            foreach (var selector in controller.Selectors)
            {
                selector.AttributeRouteModel = selector.AttributeRouteModel is null
                    ? _prefix
                    : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
            }
        }
    }
}
