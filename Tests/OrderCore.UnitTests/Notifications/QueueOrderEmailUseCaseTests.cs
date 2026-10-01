using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Modules.Notifications.Application.UseCases;
using OrderCore.Api.Modules.Notifications.Domain.Entities;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;
using OrderCore.Api.Modules.Notifications.Infrastructure.Templates;
using Xunit;

namespace OrderCore.UnitTests.Notifications;

/// <summary>
/// Each order event becomes the right Portuguese e-mail to the customer:
/// money in reais, the shipment's details and link, reasons as friendly text
/// (password-recovery spec, item 6).
/// </summary>
public sealed class QueueOrderEmailUseCaseTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private readonly RecordingRepository _queued = new();
    private readonly FakeContacts _contacts = new();

    private QueueOrderEmailUseCase UseCase() => new(
        _contacts,
        new QueueEmailUseCase(_queued, new EmbeddedEmailTemplates(), TestMetrics.Notifications, TimeProvider.System, NullLogger<QueueEmailUseCase>.Instance),
        NullLogger<QueueOrderEmailUseCase>.Instance);

    private static OrderEmail Email(OrderEmailKind kind) => new(kind, Guid.NewGuid(), "ORD-000042", CustomerId, 1234.5m, "BRL");

    private EmailMessage Sent => _queued.Messages.Should().ContainSingle().Subject;

    [Fact]
    public async Task A_confirmed_order_is_emailed_to_the_customer_with_the_total_in_reais()
    {
        await UseCase().ExecuteAsync(Email(OrderEmailKind.Confirmed), CancellationToken.None);

        Sent.To.Should().Be("maria@example.com");
        Sent.Template.Should().Be("order-confirmed");
        Sent.Subject.Should().Be("Pedido ORD-000042 confirmado");
        Sent.HtmlBody.Should().Contain("Olá, Maria Silva.").And.Contain("R$ 1.234,50");
    }

    [Fact]
    public async Task A_shipped_order_with_a_tracking_link_gets_the_button()
    {
        await UseCase().ExecuteAsync(
            Email(OrderEmailKind.Shipped) with { Carrier = "Correios", TrackingCode = "AB123BR", TrackingUrl = "https://track.example/AB123BR" },
            CancellationToken.None);

        Sent.Template.Should().Be("order-shipped-tracking");
        Sent.HtmlBody.Should().Contain("Transportadora: Correios. Código de rastreio: AB123BR.").And.Contain("href=\"https://track.example/AB123BR\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a link")]
    public async Task A_shipped_order_without_a_usable_link_gets_no_button(string? trackingUrl)
    {
        await UseCase().ExecuteAsync(Email(OrderEmailKind.Shipped) with { TrackingUrl = trackingUrl }, CancellationToken.None);

        Sent.Template.Should().Be("order-shipped");
        Sent.HtmlBody.Should().NotContain("href=").And.Contain("Ele já está a caminho do endereço de entrega.");
    }

    [Theory]
    [InlineData("Cancelled by the customer", "O pedido foi cancelado a seu pedido.")]
    [InlineData("authorization_expired", "O prazo para concluir o pagamento terminou")]
    [InlineData("estoque errado no galpão 3", "O pedido foi cancelado. Se tiver dúvidas, fale com a gente.")]
    public async Task A_cancellation_explains_itself_without_showing_codes_or_backoffice_notes(string reason, string expected)
    {
        await UseCase().ExecuteAsync(Email(OrderEmailKind.Cancelled) with { Reason = reason }, CancellationToken.None);

        Sent.Subject.Should().Be("Pedido ORD-000042 cancelado");
        Sent.HtmlBody.Should().Contain(expected).And.NotContain(reason);
    }

    [Theory]
    [InlineData("insufficient_funds", "não tinha limite")]
    [InlineData("payment_not_started", "não foi concluído a tempo")]
    [InlineData("card_declined", "não foi aprovado pela operadora")]
    [InlineData(null, "não foi aprovado pela operadora")]
    public async Task A_payment_failure_explains_itself_in_portuguese(string? reason, string expected)
    {
        await UseCase().ExecuteAsync(Email(OrderEmailKind.PaymentFailed) with { Reason = reason }, CancellationToken.None);

        Sent.Subject.Should().Be("Pagamento do pedido ORD-000042 não aprovado");
        Sent.HtmlBody.Should().Contain(expected);
        if (reason is not null)
        {
            Sent.HtmlBody.Should().NotContain(reason);
        }
    }

    [Fact]
    public async Task A_customer_that_no_longer_exists_gets_nothing()
    {
        _contacts.Contact = null;

        await UseCase().ExecuteAsync(Email(OrderEmailKind.Confirmed), CancellationToken.None);

        _queued.Messages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "BRL", "R$ 0,00")]
    [InlineData(1234567.891, "BRL", "R$ 1.234.567,89")]
    [InlineData(19.9, "usd", "19,90 USD")]
    public void Money_is_written_the_brazilian_way(decimal amount, string currency, string expected)
    {
        QueueOrderEmailUseCase.Money(amount, currency).Should().Be(expected);
    }

    private sealed class FakeContacts : ICustomerContacts
    {
        public CustomerContact? Contact { get; set; } = new("Maria Silva", "maria@example.com");

        public Task<CustomerContact?> GetAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult(customerId == CustomerId ? Contact : null);
    }

    private sealed class RecordingRepository : IEmailMessageRepository
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task AddAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<EmailMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.Id == id));

        public Task<IReadOnlyList<Guid>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<int> DeleteFinishedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
