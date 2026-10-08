namespace OrderCore.Api.Shared.Presentation.OpenApi;

/// <summary>
/// Whether the OpenAPI document and the Scalar UI are mapped: always in
/// Development, and elsewhere only when <c>OpenApi:Expose</c> is true — off by
/// default, so a deployed API's surface isn't public unless a deployment (the
/// public demo, Docs/specs/operations/aws-demo-deployment.md) opts in.
/// </summary>
public static class OpenApiExposure
{
    public const string SettingName = "OpenApi:Expose";

    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() || configuration.GetValue<bool>(SettingName);
}
