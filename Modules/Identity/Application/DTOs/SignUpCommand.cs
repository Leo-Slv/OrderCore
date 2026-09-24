namespace OrderCore.Api.Modules.Identity.Application.DTOs;

public sealed record SignUpCommand(string Name, string Email, string Password, string? Phone);

public sealed record SignInCommand(string Email, string Password);
