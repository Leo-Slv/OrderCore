using OrderCore.Api.Modules.Payments.Domain.Enums;

namespace OrderCore.Api.Modules.Payments.Application.DTOs;

/// <summary>
/// The backoffice payment list. Every criterion is optional; the created
/// range is <c>[CreatedFrom, CreatedTo)</c>.
/// </summary>
public sealed class ListPaymentsFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public PaymentStatus? Status { get; init; }

    public PaymentMethod? Method { get; init; }

    public DateTimeOffset? CreatedFrom { get; init; }

    public DateTimeOffset? CreatedTo { get; init; }

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;
}
