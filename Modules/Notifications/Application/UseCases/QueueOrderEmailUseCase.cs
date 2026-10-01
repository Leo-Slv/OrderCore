using System.Globalization;
using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Notifications.Application.UseCases;

public enum OrderEmailKind
{
    Confirmed,
    Shipped,
    Cancelled,
    PaymentFailed,
}

/// <summary>What an order e-mail says; <see cref="Reason"/> and the shipment fields only for the kinds that have them.</summary>
public sealed record OrderEmail(
    OrderEmailKind Kind,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    string? Reason = null,
    string? Carrier = null,
    string? TrackingCode = null,
    string? TrackingUrl = null);

/// <summary>
/// Queues the e-mail telling a customer their order was confirmed, shipped,
/// cancelled or its payment failed (password-recovery spec, item 6) — driven
/// by Orders' integration events, never by a use case. Reasons are shown as
/// friendly Portuguese text, never as the raw code nor as a note someone
/// typed in the backoffice. A customer that no longer exists is skipped and
/// logged.
/// </summary>
public sealed class QueueOrderEmailUseCase
{
    private readonly ICustomerContacts _customers;
    private readonly QueueEmailUseCase _queueEmail;
    private readonly ILogger<QueueOrderEmailUseCase> _logger;

    public QueueOrderEmailUseCase(ICustomerContacts customers, QueueEmailUseCase queueEmail, ILogger<QueueOrderEmailUseCase> logger)
    {
        _customers = customers;
        _queueEmail = queueEmail;
        _logger = logger;
    }

    public async Task ExecuteAsync(OrderEmail email, CancellationToken cancellationToken)
    {
        Observed.Order(email.OrderId);
        Observed.Customer(email.CustomerId);

        var contact = await _customers.GetAsync(email.CustomerId, cancellationToken);
        if (contact is null)
        {
            _logger.LogWarning("No e-mail for order {OrderId}: its customer no longer exists.", email.OrderId);
            return;
        }

        var values = new Dictionary<string, string>
        {
            ["greeting"] = $"Olá, {contact.Name.Trim()}.",
            ["orderNumber"] = email.OrderNumber,
            ["total"] = Money(email.TotalAmount, email.Currency),
        };

        string template;
        switch (email.Kind)
        {
            case OrderEmailKind.Confirmed:
                template = EmailTemplateNames.OrderConfirmed;
                break;
            case OrderEmailKind.Shipped:
                values["shipment"] = Shipment(email.Carrier, email.TrackingCode);
                if (Uri.TryCreate(email.TrackingUrl, UriKind.Absolute, out var trackingUrl)
                    && (trackingUrl.Scheme == Uri.UriSchemeHttps || trackingUrl.Scheme == Uri.UriSchemeHttp))
                {
                    template = EmailTemplateNames.OrderShippedWithTracking;
                    values["trackingUrl"] = trackingUrl.AbsoluteUri;
                }
                else
                {
                    template = EmailTemplateNames.OrderShipped;
                }

                break;
            case OrderEmailKind.Cancelled:
                template = EmailTemplateNames.OrderCancelled;
                values["reason"] = CancellationReason(email.Reason);
                break;
            default:
                template = EmailTemplateNames.OrderPaymentFailed;
                values["reason"] = PaymentFailureReason(email.Reason);
                break;
        }

        await _queueEmail.ExecuteAsync(contact.Email, template, values, cancellationToken);
    }

    /// <summary><c>R$ 1.234,56</c> for BRL; <c>1.234,56 USD</c> otherwise. No culture data needed.</summary>
    public static string Money(decimal amount, string currency)
    {
        var number = amount.ToString("#,0.00", CultureInfo.InvariantCulture).Replace(',', '_').Replace('.', ',').Replace('_', '.');
        return string.Equals(currency, "BRL", StringComparison.OrdinalIgnoreCase) ? $"R$ {number}" : $"{number} {currency.ToUpperInvariant()}";
    }

    public static string Shipment(string? carrier, string? trackingCode) =>
        (string.IsNullOrWhiteSpace(carrier), string.IsNullOrWhiteSpace(trackingCode)) switch
        {
            (false, false) => $"Transportadora: {carrier!.Trim()}. Código de rastreio: {trackingCode!.Trim()}.",
            (false, true) => $"Transportadora: {carrier!.Trim()}.",
            (true, false) => $"Código de rastreio: {trackingCode!.Trim()}.",
            _ => "Ele já está a caminho do endereço de entrega.",
        };

    /// <summary>
    /// The default customer cancellation and the payment deadline are
    /// explained; any other reason is free text someone typed (the customer
    /// or the backoffice) and is not shown.
    /// </summary>
    public static string CancellationReason(string? reason) => reason switch
    {
        "Cancelled by the customer" => "O pedido foi cancelado a seu pedido.",
        "authorization_expired" => "O prazo para concluir o pagamento terminou e o valor reservado no cartão foi liberado.",
        _ => "O pedido foi cancelado. Se tiver dúvidas, fale com a gente.",
    };

    public static string PaymentFailureReason(string? reason) => reason switch
    {
        "insufficient_funds" => "O cartão não tinha limite disponível para esta compra.",
        "expired_card" => "O cartão está vencido.",
        "incorrect_cvc" or "invalid_cvc" => "O código de segurança do cartão não confere.",
        "payment_not_started" or "payment_window_expired" => "O pagamento não foi concluído a tempo.",
        "payment_canceled" => "O pagamento foi cancelado antes de ser concluído.",
        _ => "O pagamento não foi aprovado pela operadora.",
    };
}
