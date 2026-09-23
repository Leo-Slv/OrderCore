namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

public sealed record UpdateProductCommand(string Name, string? ShortDescription, string? Description, string? Brand);
