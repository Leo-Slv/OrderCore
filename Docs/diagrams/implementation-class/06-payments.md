# Módulo Payments

Pagamento, estorno e os eventos de integração que o módulo publica pelo RabbitMQ. Como [02-customers.md](02-customers.md), [03-catalog.md](03-catalog.md), [04-inventory.md](04-inventory.md) e [05-orders.md](05-orders.md), este módulo está **implementado** de ponta a ponta (Domain, Application, Infrastructure/EF Core, outbox e Presentation) — os `IntegrationEvents` não são mais placeholders. Mantido isolado de Order/Customer/Product por design (referenciados apenas por id), para permitir extração futura para um serviço PayCore. Ver `Docs/specs/payments/payment-processing.md` para o spec completo e as decisões resolvidas antes da implementação. Base: [Shared kernel](01-shared-kernel.md).

Diferenças entre este diagrama e o código, todas documentadas nos comentários das classes correspondentes:

- **Eventos pelo broker** (`Docs/specs/events/async-messaging.md`): os contratos moram em `Modules/Payments/Contracts/IntegrationEvents` (um por arquivo, com `Name` estável, ex.: `payments.payment-authorized`, versão 1) e herdam o `IntegrationEvent` do Shared, que não é mais um `IDomainEvent`. A ponte temporária que "publicava" chamando o `IDomainEventDispatcher` (`OutboxPublisherBackgroundService`, `IntegrationEventTypeRegistry`, `IOutboxWriter`, `OutboxWriter` e o `OutboxMessage` do módulo) foi removida: os use cases enfileiram por `IPaymentsOutbox` (Application/Contracts), implementado por `PaymentsOutbox` sobre o `OutboxWriter<PaymentsDbContext>` compartilhado, e o relay do módulo Messaging publica — ver [09-messaging.md](09-messaging.md).
- O outbox é `payments_outbox_messages`, mapeado em `PaymentsDbContext` por `modelBuilder.AddOutbox("payments")`; a migration `MoveOutboxToSharedMessaging` renomeou `outbox_messages` (escrita à mão, para as linhas ainda não publicadas sobreviverem, com os nomes de tipo convertidos para os nomes de contrato).
- `PaymentCaptured` e `PaymentVoided` são novos: `CapturePaymentUseCase` e `SettlePaymentForCancellationUseCase` os enfileiram no mesmo save do pagamento (uma captura repetida não publica de novo). `PaymentRequested` existe e é registrado, mas o caminho síncrono de hoje (`CreatePaymentUseCase` autoriza na mesma chamada) só publica `PaymentAuthorized`/`PaymentFailed`.
- `Payment` tem, ao mesmo tempo, um método `Refund()` (sem parâmetros, marca o pagamento inteiro como `Refunded`) e precisa referenciar o tipo `Refund` (retorno de `RequestRefund`, chamada estática `Refund.Create(...)`) — colisão de nomes que o próprio diagrama já tinha. Resolvido qualificando o tipo com `global::OrderCore.Api.Modules.Payments.Domain.Entities.Refund` nesses dois pontos (coleções como `IReadOnlyCollection<Refund>` compilam normalmente sem qualificação).
- `Payment.Fail(string reason)` não recebe `now` (ao contrário de `Authorize`/`Capture`) — não existe um campo de timestamp para "quando falhou" no diagrama nem no documento de modelagem. `Refund.Complete`/`Fail`, por sua vez, recebem `now` (o diagrama original não mostrava o parâmetro), já que `ProcessedAt` precisa de um valor.
- `RequestRefundUseCase.ExecuteAsync` retorna `Task<Refund>`, não `Task` como no diagrama — `PaymentsController.RequestRefundAsync` não tem outro jeito de montar um `RefundResponse` depois, já que nenhum outro use case do módulo busca um refund pelo próprio id.
- `RequestRefundUseCase` só chama `Payment.Refund()` quando a soma dos reembolsos `Completed` atinge o valor total do pagamento — não existe status `PartiallyRefunded` (decisão registrada no spec), então marcar o pagamento inteiro como `Refunded` num primeiro reembolso parcial bloquearia qualquer reembolso seguinte (`RequestRefund` exige `Status == Captured`).
- `CreatePaymentUseCase` pede a autorização na mesma chamada que cria o pagamento (não fica com `Status = Pending` aguardando um passo separado). O fake responde na hora; o Stripe responde "aguardando o comprador" (ver a seção do Stripe abaixo).
- O antigo `PaymentWebhookHandler` (sem rota, nunca usado) foi removido e substituído pelo `StripeWebhookHandler`.
- **Forma de pagamento** (MVP do storefront, `Docs/specs/storefront/storefront-api-mvp.md`, decisão 1): `PaymentMethod` (`Card`/`Pix`) é gravado no `Payment` por `Create` e é diferente de `Provider` (quem processa) — os dois métodos passam hoje pelo mesmo `FakePaymentProvider`. A migração `AddPaymentMethod` preenche `Card` nos pagamentos já existentes. `CreatePaymentRequest.Method` é obrigatório (sem ele, 400, em vez de assumir o primeiro valor do enum); `PaymentResponse` ganhou `Method`, `Currency`, `FailureReason`, `CreatedAt` e `AuthorizedAt`.
- `RefundPersistenceModel.Id` é configurado com `ValueGeneratedNever()`: o id vem do domínio, e sem isso o EF Core tratava um reembolso novo num pagamento já salvo como linha existente (UPDATE que não afetava nada).
- **Backoffice** (`Docs/specs/backoffice/backoffice-api.md`, decisões 1 e 2):
  - `Payment.Void(now)` e o status final `Voided` (coluna `VoidedAt`, migration `AddPaymentVoid`): libera uma autorização nunca capturada; `Fail` também recusa um pagamento `Voided`. `IPaymentProvider.VoidAsync` e o modo `CaptureDeclined` do `FakePaymentProvider` (autoriza, recusa capturar; void/refund funcionam).
  - `CapturePaymentUseCase` ficou idempotente (um pagamento já `Captured` volta como está, sem ir ao provider), nunca manda ao provider um pagamento que não está `Authorized`, e ganhou `ExecuteForOrderAsync` — o que o Orders chama ao enviar o pedido.
  - `SettlePaymentForCancellationUseCase` (chamado pelo Orders ao cancelar): `Authorized` → void, `Captured` → estorno do saldo ainda retido via `RequestRefundUseCase`, `Processing` aguardando o comprador (Stripe) → cancelado no provedor e `Voided`, `Pending`/`Processing` sem referência do provedor → `409 payment_in_progress`, o resto → nada. Idempotente. Não existe um `VoidPaymentUseCase` separado: o void só acontece aqui.
  - Leitura para o backoffice: `ListPaymentsUseCase` (`GET payments`, filtros por status/forma/período), `GetPaymentByIdUseCase` (`GET payments/{id}`) e `GetPaymentsByOrderIdsUseCase` (a lista de pedidos do admin, via adapter do Orders). `PaymentResponse` ganhou provider, referência, datas de captura/void e os estornos; `RefundResponse`, motivo e datas — só campos a mais.

Adicionado pela observabilidade (`Docs/specs/observability/observability.md`):

- **`PaymentsMetrics`** (meter `OrderCore.Payments`): autorizações aprovadas/recusadas com a forma de pagamento e o motivo da recusa (o código do provedor, nunca dado de cartão), capturas, anulações e estornos, registrados pelos use cases depois do save.
- **`MeasuredPaymentProvider`** envolve o provedor configurado (hoje o `FakePaymentProvider`, registrado como tipo concreto) e mede cada chamada por operação e resultado (`succeeded`, `refused`, `error`) — o Stripe ganha isso sem código a mais.
- Os use cases marcam o span com `payment.id`/`order.id`.

Adicionado pelo Stripe como provedor (`Docs/specs/payments/stripe-provider.md` e o plano de implementação, com as notas de execução):

- **Escolha do provedor e capacidades:** `PaymentsDependencyInjection` registra o `StripePaymentProvider` quando `Payments:Stripe:SecretKey` está configurada e o `FakePaymentProvider` caso contrário, sempre envolvido pelo `MeasuredPaymentProvider`. `IPaymentProvider.Info` (`PaymentProviderInfo`: nome, formas aceitas, chave publicável) substitui qualquer "if Stripe" nos use cases: `CreatePaymentUseCase` recusa uma forma que o provedor não aceita (`400 payment_method_unavailable`, Pix com o Stripe) e grava `Info.Name` como `Provider`. `GET payments/methods` (anônimo, `PaymentMethodsController` → `GetAvailablePaymentMethodsUseCase`) diz o que oferecer.
- **`StripePaymentProvider`** (SDK `Stripe.net`, `HttpClient` nomeado `stripe`, timeout e retentativas de rede): autorizar cria um PaymentIntent com `capture_method=manual`, só cartão, valor na menor unidade da moeda (`ToMinorUnits`) e os ids do pedido/pagamento nos metadados; captura, void (cancelar o intent) e estorno agem sobre ele. Toda chamada que mexe em dinheiro manda uma chave de idempotência derivada do pagamento (`ordercore-{id}-{operação}`) ou do estorno. Recusa de cartão volta como resultado com o `decline_code`; falha do Stripe é lançada.
- **Aguardando o comprador:** `PaymentAuthorizationResult` ganhou `ClientSecret`/`RequiresBuyer`/`WaitingForBuyer`. Nesse caso o pagamento fica `Processing` com a referência do provedor (`Payment.AwaitBuyer`, `IsAwaitingBuyer`), nada é publicado e `CreatePaymentResult.NextAction` leva o `PaymentNextAction` (`confirm_card` + client secret). O client secret nunca é gravado: `GetPaymentNextActionUseCase` o pede de novo ao provedor (`GetClientSecretAsync`) quando o checkout é repetido. `Payment.Void` também aceita um pagamento aguardando o comprador (cancelar o pedido cancela o intent).
- **Webhooks:** `POST payments/webhooks/stripe` (`StripeWebhooksController`, anônimo mas assinado) → `StripeWebhookHandler` (Infrastructure): confere a assinatura com o segredo e a tolerância de horário (`400 invalid_webhook_signature`; `404 stripe_webhooks_disabled` sem segredo), deduplica pelo id do evento no inbox do Payments (`payments_processed_messages`, consumidor `stripe-webhooks`) e traduz o evento num `PaymentProviderUpdate` neutro. `ApplyPaymentProviderUpdateUseCase` é o único caminho de "o provedor disse X": autorizado, recusado (`RecordDecline`: o comprador pode tentar outro cartão), capturado, cancelado (void; com `PaymentAuthorizationExpired` quando a autorização expirou; falha quando ainda aguardava o comprador), estorno liquidado (`SettleRefund`) e disputa aberta (`MarkDisputed`). Uma atualização atrasada, repetida ou fora de ordem é ignorada. Migration `AddStripePaymentFields` (última recusa, disputa, índice por referência, inbox).
- **Janela de pagamento e expiração:** `ExpirePaymentWindowUseCase`, rodado pelo `PaymentWindowBackgroundService` (`Payments:PaymentWindow`, 30 minutos), cancela no provedor e falha (`payment_window_expired` ou a última recusa) os pagamentos que ainda aguardam o comprador — só esses; os travados sem referência ficam para a reconciliação. `Payment.AuthorizationExpiresAt` guarda o `capture_before` do cartão (pedido ao Stripe no webhook de autorização) ou 7 dias (`DefaultAuthorizationValidity`); `CountExpiringAuthorizationsUseCase` alimenta o painel do Orders. Migration `AddPaymentAuthorizationExpiry`.
- **Reconciliação:** `ReconcilePaymentUseCase` pergunta ao provedor (`GetStateAsync` → `PaymentProviderState`) e aplica a resposta pelo mesmo `ApplyPaymentProviderUpdateUseCase`; um pagamento sem referência é autorizado de novo com a mesma chave (`AuthorizePaymentUseCase`). `POST payments/{id}/reconcile` (admin) e o `ReconciliationBackgroundService` (`Payments:Reconciliation`, a cada 15 minutos: `Processing` há mais de 5 minutos e autorizações vencidas). Só o status do pagamento é reconciliado, não estornos nem disputas.
- **Métricas:** `ProviderUpdate` (`ordercore.payments.provider_updates`, por tipo e resultado) e `Reconciled` (`ordercore.payments.reconciliations`, em dia ou corrigido).


```mermaid

classDiagram
    direction LR

    class AggregateRoot~TId~ {
        <<external>>
    }

    class IntegrationEvent {
        <<external>>
        <<abstract>>
    }

    class IOutbox {
        <<external>>
        <<interface>>
    }

    class OutboxWriter~TDbContext~ {
        <<external>>
    }

    note for IntegrationEvent "Shared messaging — ver 09-messaging.md"

    %% OrderCore.Api.Modules.Payments.Domain.Entities
    class Payment {
        +Guid OrderId
        +Guid? CustomerPaymentMethodId
        +decimal Amount
        +string Currency
        +PaymentMethod Method
        +PaymentStatus Status
        +string IdempotencyKey
        +string Provider
        +string? ProviderReference
        +string? FailureReason
        +DateTimeOffset CreatedAt
        +DateTimeOffset UpdatedAt
        +DateTimeOffset? AuthorizedAt
        +DateTimeOffset? CapturedAt
        +DateTimeOffset? VoidedAt
        +DateTimeOffset? AuthorizationExpiresAt
        +string? LastDeclineReason
        +DateTimeOffset? LastDeclinedAt
        +DateTimeOffset? DisputedAt
        +bool IsAwaitingBuyer
        +bool IsDisputed
        +IReadOnlyCollection~Refund~ Refunds
        +TimeSpan DefaultAuthorizationValidity$
        +Create(Guid orderId, decimal amount, string currency, PaymentMethod method, string idempotencyKey, string provider, Guid? customerPaymentMethodId, DateTimeOffset now)$ Payment
        +MarkProcessing() void
        +AwaitBuyer(string providerReference, DateTimeOffset now) void
        +Authorize(string providerReference, DateTimeOffset now, DateTimeOffset? expiresAt) void
        +RecordDecline(string reason, DateTimeOffset at) void
        +MarkDisputed(DateTimeOffset at) bool
        +SettleRefund(Guid refundId, bool succeeded, string? failureReason, DateTimeOffset now) bool
        +Capture(DateTimeOffset now) void
        +Void(DateTimeOffset now) void
        +Fail(string reason) void
        +Refund() void
        +RequestRefund(decimal amount, string reason, DateTimeOffset now) Refund
    }

    class Refund {
        +decimal Amount
        +string Reason
        +RefundStatus Status
        +DateTimeOffset RequestedAt
        +DateTimeOffset? ProcessedAt
        +Complete(DateTimeOffset now) void
        +Fail(string reason, DateTimeOffset now) void
    }


    %% OrderCore.Api.Modules.Payments.Domain.Enums
    class PaymentStatus {
        <<enumeration>>
        Pending
        Processing
        Authorized
        Captured
        Failed
        Refunded
        Voided
    }

    class RefundStatus {
        <<enumeration>>
        Pending
        Completed
        Failed
    }

    class PaymentMethod {
        <<enumeration>>
        Card
        Pix
    }


    %% OrderCore.Api.Modules.Payments.Domain.Repositories
    class IPaymentProvider {
        <<interface>>
        +PaymentProviderInfo Info
        +AuthorizeAsync(Payment payment) Task~PaymentAuthorizationResult~
        +CaptureAsync(Payment payment) Task~PaymentCaptureResult~
        +RefundAsync(Payment payment) Task~PaymentRefundResult~
        +VoidAsync(Payment payment) Task~PaymentVoidResult~
        +GetClientSecretAsync(Payment payment) Task~string?~
        +GetStateAsync(Payment payment) Task~PaymentProviderState~
    }

    class PaymentProviderInfo {
        +string Name
        +IReadOnlyCollection~PaymentMethod~ SupportedMethods
        +string? PublishableKey
    }

    class PaymentProviderState {
        +PaymentProviderStatus Status
        +string? DeclineReason
        +DateTimeOffset? AuthorizationExpiresAt
        +bool AuthorizationExpired
    }

    class PaymentProviderStatus {
        <<enumeration>>
        WaitingForBuyer
        Authorized
        Captured
        Canceled
    }

    class PaymentVoidResult {
        +bool Succeeded
        +string? FailureReason
    }

    class PaymentAuthorizationResult {
        +bool Succeeded
        +string? ProviderReference
        +string? FailureReason
        +string? ClientSecret
        +bool RequiresBuyer
        +WaitingForBuyer(string providerReference, string clientSecret)$ PaymentAuthorizationResult
    }

    class PaymentCaptureResult {
        +bool Succeeded
        +string? FailureReason
    }

    class PaymentRefundResult {
        +bool Succeeded
        +string? FailureReason
    }


    %% OrderCore.Api.Modules.Payments.Application.Contracts
    class IPaymentRepository {
        <<interface>>
        +GetByIdAsync(Guid paymentId) Task~Payment?~
        +GetByOrderIdAsync(Guid orderId) Task~Payment?~
        +GetByProviderReferenceAsync(string providerReference) Task~Payment?~
        +ListAwaitingBuyerCreatedBeforeAsync(DateTimeOffset cutoff, int limit) Task~IReadOnlyList~Guid~~
        +ListToReconcileAsync(DateTimeOffset processingSince, DateTimeOffset now, int limit) Task~IReadOnlyList~Guid~~
        +CountAuthorizationsExpiringBeforeAsync(DateTimeOffset cutoff) Task~int~
        +ListByOrderIdsAsync(IReadOnlyCollection~Guid~ orderIds) Task~IReadOnlyList~Payment~~
        +ListAsync(ListPaymentsFilter filter) Task~(IReadOnlyList~Payment~, int)~
        +AddAsync(Payment payment) Task
        +SaveChangesAsync() Task
    }


    %% OrderCore.Api.Modules.Payments.Application.DTOs
    class ListPaymentsFilter {
        +PaymentStatus? Status
        +PaymentMethod? Method
        +DateTimeOffset? CreatedFrom
        +DateTimeOffset? CreatedTo
        +int Page
        +int PageSize
    }

    class PaymentSettlementOutcome {
        <<enumeration>>
        NothingToSettle
        Voided
        Refunded
    }

    class CreatePaymentCommand {
        +Guid OrderId
        +decimal Amount
        +string Currency
        +PaymentMethod Method
        +string IdempotencyKey
    }

    class CreatePaymentResult {
        +Guid PaymentId
        +string Status
        +PaymentNextAction? NextAction
    }

    class PaymentNextAction {
        +string Type
        +string ClientSecret
        +ConfirmCardWith(string clientSecret)$ PaymentNextAction
    }

    class AvailablePaymentMethods {
        +string Provider
        +IReadOnlyCollection~PaymentMethod~ Methods
        +string? PublishableKey
    }

    class PaymentProviderUpdate {
        +PaymentProviderUpdateKind Kind
        +string ProviderReference
        +DateTimeOffset OccurredAt
        +string? Reason
        +Guid? RefundId
        +bool AuthorizationExpired
        +DateTimeOffset? AuthorizationExpiresAt
    }

    class PaymentProviderUpdateKind {
        <<enumeration>>
        Authorized
        Declined
        Captured
        Canceled
        RefundSucceeded
        RefundFailed
        DisputeOpened
    }

    class PaymentProviderUpdateOutcome {
        <<enumeration>>
        Applied
        Ignored
        UnknownPayment
    }

    class PaymentReconciliation {
        +Guid PaymentId
        +string StatusBefore
        +string StatusAfter
        +string? ProviderStatus
        +bool Changed
    }

    class RequestRefundCommand {
        +Guid PaymentId
        +decimal Amount
        +string Reason
    }


    %% OrderCore.Api.Modules.Payments.Application.UseCases
    class CreatePaymentUseCase {
        -IPaymentRepository payments
        -IPaymentProvider provider
        -IPaymentsOutbox outbox
        -PaymentsMetrics metrics
        +ExecuteAsync(CreatePaymentCommand command) Task~CreatePaymentResult~
    }

    class AuthorizePaymentUseCase {
        -IPaymentRepository payments
        -IPaymentProvider provider
        -IPaymentsOutbox outbox
        -PaymentsMetrics metrics
        +ExecuteAsync(Guid paymentId) Task~CreatePaymentResult~
    }

    class CapturePaymentUseCase {
        -IPaymentRepository payments
        -IPaymentProvider provider
        -IPaymentsOutbox outbox
        -PaymentsMetrics metrics
        +ExecuteAsync(Guid paymentId) Task~CreatePaymentResult~
        +ExecuteForOrderAsync(Guid orderId) Task~CreatePaymentResult~
    }

    class SettlePaymentForCancellationUseCase {
        -IPaymentRepository payments
        -IPaymentProvider provider
        -IPaymentsOutbox outbox
        -RequestRefundUseCase requestRefund
        -PaymentsMetrics metrics
        +ExecuteAsync(Guid orderId, string reason) Task~PaymentSettlementOutcome~
    }

    class ListPaymentsUseCase {
        -IPaymentRepository payments
        +ExecuteAsync(ListPaymentsFilter filter) Task~PagedResult~Payment~~
    }

    class GetPaymentByIdUseCase {
        -IPaymentRepository payments
        +ExecuteAsync(Guid paymentId) Task~Payment~
    }

    class GetPaymentsByOrderIdsUseCase {
        -IPaymentRepository payments
        +ExecuteAsync(IReadOnlyCollection~Guid~ orderIds) Task~IReadOnlyList~Payment~~
    }

    class FailPaymentUseCase {
        -IPaymentRepository payments
        -IPaymentsOutbox outbox
        +ExecuteAsync(Guid paymentId, string reason) Task
    }

    class RequestRefundUseCase {
        -IPaymentRepository payments
        -IPaymentProvider provider
        -IPaymentsOutbox outbox
        -PaymentsMetrics metrics
        +ExecuteAsync(RequestRefundCommand command) Task~Refund~
    }

    class GetPaymentByOrderIdUseCase {
        -IPaymentRepository payments
        +ExecuteAsync(Guid orderId) Task~Payment?~
    }

    class GetAvailablePaymentMethodsUseCase {
        -IPaymentProvider provider
        +Execute() AvailablePaymentMethods
    }

    class GetPaymentNextActionUseCase {
        -IPaymentRepository payments
        -IPaymentProvider provider
        +ExecuteAsync(Guid orderId) Task~PaymentNextAction?~
    }

    class ApplyPaymentProviderUpdateUseCase {
        -IPaymentRepository payments
        -IPaymentsOutbox outbox
        -PaymentsMetrics metrics
        +ExecuteAsync(PaymentProviderUpdate update) Task~PaymentProviderUpdateOutcome~
        +KindTag(PaymentProviderUpdateKind kind)$ string
    }

    class ExpirePaymentWindowUseCase {
        -IPaymentRepository payments
        -IPaymentProvider provider
        -IPaymentsOutbox outbox
        -PaymentsMetrics metrics
        +FindExpiredAsync(TimeSpan window, int limit) Task~IReadOnlyList~Guid~~
        +ExpireAsync(Guid paymentId, TimeSpan window) Task~bool~
    }

    class CountExpiringAuthorizationsUseCase {
        -IPaymentRepository payments
        +ExecuteAsync(DateTimeOffset cutoff) Task~int~
    }

    class ReconcilePaymentUseCase {
        -IPaymentRepository payments
        -IPaymentProvider provider
        -ApplyPaymentProviderUpdateUseCase applyUpdate
        -AuthorizePaymentUseCase authorize
        -PaymentsMetrics metrics
        +ExecuteAsync(Guid paymentId) Task~PaymentReconciliation~
    }


    %% OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents
    class PaymentRequested {
        +Guid OrderId
        +Guid PaymentId
        +decimal Amount
        +string Currency
        +string IdempotencyKey
    }

    class PaymentAuthorized {
        +Guid OrderId
        +Guid PaymentId
        +decimal Amount
        +string Currency
    }

    class PaymentFailed {
        +Guid OrderId
        +Guid PaymentId
        +string Reason
    }

    class PaymentRefunded {
        +Guid OrderId
        +Guid PaymentId
    }

    class PaymentCaptured {
        +Guid OrderId
        +Guid PaymentId
        +decimal Amount
        +string Currency
    }

    class PaymentVoided {
        +Guid OrderId
        +Guid PaymentId
    }

    class PaymentAuthorizationExpired {
        +Guid OrderId
        +Guid PaymentId
    }

    %% OrderCore.Api.Modules.Payments.Application.Contracts
    class IPaymentsOutbox {
        <<interface>>
        +Enqueue(IntegrationEvent integrationEvent) void
    }


    %% OrderCore.Api.Modules.Payments
    class PaymentsDependencyInjection {
        <<static>>
        +AddPaymentsModule(IServiceCollection services)$ IServiceCollection
    }


    %% OrderCore.Api.Modules.Payments.Infrastructure.Persistence
    class PaymentPersistenceModel {
        +Guid Id
        +Guid OrderId
        +decimal Amount
        +string Currency
        +string Method
        +string Status
        +string IdempotencyKey
        +string? ProviderReference
        +DateTimeOffset? VoidedAt
        +DateTimeOffset? AuthorizationExpiresAt
        +string? LastDeclineReason
        +DateTimeOffset? LastDeclinedAt
        +DateTimeOffset? DisputedAt
        +int Version
        +ICollection~RefundPersistenceModel~ Refunds
    }

    class RefundPersistenceModel {
        +Guid Id
        +Guid PaymentId
        +decimal Amount
        +string Status
        +DateTimeOffset RequestedAt
    }

    class PaymentMapper {
        +ToDomain(PaymentPersistenceModel model) Payment
        +ToPersistence(Payment domain) PaymentPersistenceModel
        +ApplyChanges(Payment domain, PaymentPersistenceModel model) void
    }

    class PaymentsDbContext {
        +DbSet~PaymentPersistenceModel~ Payments
        +DbSet~RefundPersistenceModel~ Refunds
        +SaveChangesAsync() Task~int~
    }

    note for PaymentsDbContext "payments_outbox_messages + payments_processed_messages (Stripe webhooks)"

    class EfPaymentRepository {
        -PaymentsDbContext dbContext
        -PaymentMapper mapper
    }


    %% OrderCore.Api.Modules.Payments.Infrastructure.Messaging
    class PaymentsOutbox {
        -OutboxWriter~PaymentsDbContext~ writer
        +Enqueue(IntegrationEvent integrationEvent) void
    }


    %% OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake
    class FakePaymentProviderOptions {
        +FakePaymentProviderMode Mode
        +TimeSpan SimulatedLatency
    }

    class FakePaymentProviderMode {
        <<enumeration>>
        Success
        Declined
        CaptureDeclined
        Timeout
        Unavailable
    }

    class FakePaymentProvider {
        -FakePaymentProviderOptions options
        +AuthorizeAsync(Payment payment) Task~PaymentAuthorizationResult~
        +CaptureAsync(Payment payment) Task~PaymentCaptureResult~
        +RefundAsync(Payment payment) Task~PaymentRefundResult~
        +VoidAsync(Payment payment) Task~PaymentVoidResult~
        +GetClientSecretAsync(Payment payment) Task~string?~
        +GetStateAsync(Payment payment) Task~PaymentProviderState~
    }


    %% OrderCore.Api.Modules.Payments.Infrastructure.Providers.Stripe
    class StripeOptions {
        +string? SecretKey
        +string? PublishableKey
        +string? WebhookSecret
        +string? ApiBase
        +TimeSpan RequestTimeout
        +int MaxNetworkRetries
        +bool IsEnabled
    }

    class StripePaymentProvider {
        -PaymentIntentService paymentIntents
        -RefundService refunds
        +string HttpClientName$
        +AuthorizeAsync(Payment payment) Task~PaymentAuthorizationResult~
        +CaptureAsync(Payment payment) Task~PaymentCaptureResult~
        +RefundAsync(Payment payment) Task~PaymentRefundResult~
        +VoidAsync(Payment payment) Task~PaymentVoidResult~
        +GetClientSecretAsync(Payment payment) Task~string?~
        +GetStateAsync(Payment payment) Task~PaymentProviderState~
        +GetCaptureDeadlineAsync(string paymentIntentId) Task~DateTimeOffset?~
        +ToMinorUnits(decimal amount, string currency)$ long
        +Idempotency(Payment payment, string operation)$ RequestOptions
    }


    %% OrderCore.Api.Modules.Payments.Infrastructure.Webhooks
    class StripeWebhookHandler {
        -StripeOptions options
        -PaymentsDbContext dbContext
        -ApplyPaymentProviderUpdateUseCase applyUpdate
        -StripePaymentProvider stripe
        +string Consumer$
        +HandleAsync(string payload, string? signature) Task~StripeWebhookOutcome~
        +MessageIdOf(string stripeEventId)$ Guid
    }

    class StripeWebhookOutcome {
        <<enumeration>>
        Handled
        Duplicate
        NotHandled
    }


    %% OrderCore.Api.Modules.Payments.Infrastructure.Jobs
    class PaymentWindowOptions {
        +TimeSpan Window
        +TimeSpan CheckInterval
        +int BatchSize
    }

    class PaymentWindowBackgroundService {
        -IServiceScopeFactory scopeFactory
        -PaymentWindowOptions options
        +CheckAsync() Task
    }

    class ReconciliationOptions {
        +TimeSpan Interval
        +TimeSpan ProcessingAge
        +int BatchSize
    }

    class ReconciliationBackgroundService {
        -IServiceScopeFactory scopeFactory
        -ReconciliationOptions options
    }


    %% OrderCore.Api.Modules.Payments.Presentation
    class PaymentsController {
        -CreatePaymentUseCase createPaymentUseCase
        -GetPaymentByOrderIdUseCase getPaymentByOrderIdUseCase
        -RequestRefundUseCase requestRefundUseCase
        -ListPaymentsUseCase listPaymentsUseCase
        -GetPaymentByIdUseCase getPaymentByIdUseCase
        -ReconcilePaymentUseCase reconcilePaymentUseCase
        +ListAsync(ListPaymentsFilter filter) Task~ActionResult~PagedResponse~PaymentSummaryResponse~~~
        +GetByIdAsync(Guid id) Task~ActionResult~PaymentResponse~~
        +CreateAsync(CreatePaymentRequest request) Task~ActionResult~PaymentResponse~~
        +GetByOrderIdAsync(Guid orderId) Task~ActionResult~PaymentResponse~~
        +RequestRefundAsync(Guid id, RequestRefundRequest request) Task~ActionResult~RefundResponse~~
        +ReconcileAsync(Guid id) Task~ActionResult~PaymentReconciliationResponse~~
    }

    class PaymentMethodsController {
        <<AllowAnonymous>>
        -GetAvailablePaymentMethodsUseCase getAvailableMethods
        +Get() ActionResult~PaymentMethodsResponse~
    }

    class StripeWebhooksController {
        <<AllowAnonymous>>
        -StripeWebhookHandler handler
        +ReceiveAsync(string? signature) Task~IActionResult~
    }

    class PaymentMethodsResponse {
        +string Provider
        +IReadOnlyList~string~ Methods
        +string? PublishableKey
    }

    class PaymentReconciliationResponse {
        +Guid PaymentId
        +string StatusBefore
        +string StatusAfter
        +string? ProviderStatus
        +bool Changed
    }

    class CreatePaymentRequest {
        +Guid OrderId
        +decimal Amount
        +string Currency
        +PaymentMethod? Method
        +string IdempotencyKey
    }

    class RequestRefundRequest {
        +decimal Amount
        +string Reason
    }

    class PaymentResponse {
        +Guid Id
        +Guid OrderId
        +decimal Amount
        +string Currency
        +string Method
        +string Status
        +string Provider
        +string? ProviderReference
        +string? FailureReason
        +DateTimeOffset CreatedAt
        +DateTimeOffset? AuthorizedAt
        +DateTimeOffset? CapturedAt
        +DateTimeOffset? VoidedAt
        +DateTimeOffset? AuthorizationExpiresAt
        +string? LastDeclineReason
        +DateTimeOffset? LastDeclinedAt
        +DateTimeOffset? DisputedAt
        +IReadOnlyList~RefundResponse~ Refunds
    }

    class PaymentSummaryResponse {
        +Guid Id
        +Guid OrderId
        +decimal Amount
        +decimal RefundedAmount
        +string Currency
        +string Method
        +string Status
        +DateTimeOffset CreatedAt
    }

    class RefundResponse {
        +Guid Id
        +decimal Amount
        +string Reason
        +string Status
        +DateTimeOffset RequestedAt
        +DateTimeOffset? ProcessedAt
    }

    class PaymentPresenter {
        +ToResponse(Payment payment) PaymentResponse
        +ToResponse(Refund refund) RefundResponse
        +ToSummaryResponse(Payment payment) PaymentSummaryResponse
        +ToResponse(PagedResult~Payment~ page) PagedResponse~PaymentSummaryResponse~
    }


    AggregateRoot~TId~ <|-- Payment
    Payment "1" *-- "0..*" Refund
    Payment --> PaymentStatus
    Payment --> PaymentMethod
    Refund --> RefundStatus
    IntegrationEvent <|-- PaymentRequested
    IntegrationEvent <|-- PaymentAuthorized
    IntegrationEvent <|-- PaymentFailed
    IntegrationEvent <|-- PaymentRefunded
    IntegrationEvent <|-- PaymentCaptured
    IntegrationEvent <|-- PaymentVoided
    IntegrationEvent <|-- PaymentAuthorizationExpired
    IOutbox <|-- IPaymentsOutbox

    CreatePaymentUseCase --> IPaymentRepository
    CreatePaymentUseCase --> IPaymentProvider
    CreatePaymentUseCase --> IPaymentsOutbox
    AuthorizePaymentUseCase --> IPaymentRepository
    AuthorizePaymentUseCase --> IPaymentProvider
    AuthorizePaymentUseCase --> IPaymentsOutbox
    CapturePaymentUseCase --> IPaymentRepository
    CapturePaymentUseCase --> IPaymentProvider
    CapturePaymentUseCase --> IPaymentsOutbox : PaymentCaptured
    FailPaymentUseCase --> IPaymentRepository
    FailPaymentUseCase --> IPaymentsOutbox
    RequestRefundUseCase --> IPaymentRepository
    RequestRefundUseCase --> IPaymentProvider
    RequestRefundUseCase --> IPaymentsOutbox
    GetPaymentByOrderIdUseCase --> IPaymentRepository
    SettlePaymentForCancellationUseCase --> IPaymentRepository
    SettlePaymentForCancellationUseCase --> IPaymentProvider
    SettlePaymentForCancellationUseCase --> IPaymentsOutbox : PaymentVoided
    SettlePaymentForCancellationUseCase --> RequestRefundUseCase : refunds a capture
    SettlePaymentForCancellationUseCase ..> PaymentSettlementOutcome
    ListPaymentsUseCase --> IPaymentRepository
    ListPaymentsUseCase ..> ListPaymentsFilter
    GetPaymentByIdUseCase --> IPaymentRepository
    GetPaymentsByOrderIdsUseCase --> IPaymentRepository
    IPaymentProvider ..> PaymentVoidResult
    IPaymentProvider ..> PaymentProviderInfo
    IPaymentProvider ..> PaymentProviderState
    PaymentProviderState --> PaymentProviderStatus
    CreatePaymentUseCase ..> PaymentNextAction : waiting for the buyer
    GetAvailablePaymentMethodsUseCase --> IPaymentProvider
    GetPaymentNextActionUseCase --> IPaymentRepository
    GetPaymentNextActionUseCase --> IPaymentProvider : asks the client secret again
    ApplyPaymentProviderUpdateUseCase --> IPaymentRepository
    ApplyPaymentProviderUpdateUseCase --> IPaymentsOutbox : authorized / captured / voided / expired / failed / refunded
    ApplyPaymentProviderUpdateUseCase ..> PaymentProviderUpdate
    PaymentProviderUpdate --> PaymentProviderUpdateKind
    ApplyPaymentProviderUpdateUseCase ..> PaymentProviderUpdateOutcome
    ExpirePaymentWindowUseCase --> IPaymentRepository
    ExpirePaymentWindowUseCase --> IPaymentProvider : cancels the intent
    ExpirePaymentWindowUseCase --> IPaymentsOutbox : PaymentFailed
    CountExpiringAuthorizationsUseCase --> IPaymentRepository
    ReconcilePaymentUseCase --> IPaymentProvider : GetStateAsync
    ReconcilePaymentUseCase --> ApplyPaymentProviderUpdateUseCase : same path as webhooks
    ReconcilePaymentUseCase --> AuthorizePaymentUseCase : no provider reference yet
    ReconcilePaymentUseCase ..> PaymentReconciliation

    IPaymentRepository <|.. EfPaymentRepository
    EfPaymentRepository --> PaymentsDbContext
    EfPaymentRepository --> PaymentMapper
    PaymentMapper --> PaymentPersistenceModel
    PaymentMapper --> Payment
    PaymentsDbContext --> PaymentPersistenceModel
    PaymentsDbContext --> RefundPersistenceModel
    PaymentsDbContext ..> OutboxWriter~TDbContext~ : payments_outbox_messages

    IPaymentProvider <|.. FakePaymentProvider
    FakePaymentProvider --> FakePaymentProviderOptions
    FakePaymentProviderOptions --> FakePaymentProviderMode

    IPaymentsOutbox <|.. PaymentsOutbox
    PaymentsOutbox --> OutboxWriter~TDbContext~

    IPaymentProvider <|.. StripePaymentProvider
    StripePaymentProvider --> StripeOptions
    StripeWebhookHandler --> StripeOptions : webhook secret
    StripeWebhookHandler --> PaymentsDbContext : inbox stripe-webhooks
    StripeWebhookHandler --> ApplyPaymentProviderUpdateUseCase
    StripeWebhookHandler --> StripePaymentProvider : capture deadline
    StripeWebhookHandler ..> StripeWebhookOutcome
    PaymentWindowBackgroundService --> ExpirePaymentWindowUseCase
    PaymentWindowBackgroundService --> PaymentWindowOptions
    ReconciliationBackgroundService --> ReconcilePaymentUseCase
    ReconciliationBackgroundService --> ReconciliationOptions
    ReconciliationBackgroundService --> IPaymentRepository : ListToReconcileAsync

    PaymentsDependencyInjection --> IPaymentProvider : registers
    PaymentsDependencyInjection ..> FakePaymentProvider : without a Stripe secret key
    PaymentsDependencyInjection ..> StripePaymentProvider : with Payments:Stripe:SecretKey
    PaymentsDependencyInjection --> IPaymentRepository : registers
    PaymentsDependencyInjection --> CreatePaymentUseCase : registers

    PaymentsController --> CreatePaymentUseCase
    PaymentsController --> GetPaymentByOrderIdUseCase
    PaymentsController --> RequestRefundUseCase
    PaymentsController --> ListPaymentsUseCase
    PaymentsController --> GetPaymentByIdUseCase
    PaymentsController --> ReconcilePaymentUseCase
    PaymentsController ..> PaymentReconciliationResponse
    PaymentMethodsController --> GetAvailablePaymentMethodsUseCase
    PaymentMethodsController ..> PaymentMethodsResponse
    StripeWebhooksController --> StripeWebhookHandler
    PaymentPresenter --> PaymentSummaryResponse
    PaymentsController --> PaymentPresenter
    PaymentPresenter --> PaymentResponse
    PaymentPresenter --> RefundResponse

    %% OrderCore.Api.Modules.Payments.Application.Telemetry
    class PaymentsMetrics {
        +Authorized(string method) void
        +Declined(string method, string? reason) void
        +Captured() void
        +Voided() void
        +Refunded(string outcome) void
        +ProviderCalled(string provider, string operation, string outcome, TimeSpan duration) void
        +ProviderUpdate(string kind, string outcome) void
        +Reconciled(string outcome) void
    }

    %% OrderCore.Api.Modules.Payments.Infrastructure.Providers
    class MeasuredPaymentProvider {
        -IPaymentProvider inner
        -PaymentsMetrics metrics
    }

    IPaymentProvider <|.. MeasuredPaymentProvider
    MeasuredPaymentProvider --> IPaymentProvider : wraps the fake or Stripe (registered as IPaymentProvider)
    ApplyPaymentProviderUpdateUseCase --> PaymentsMetrics : provider updates
    ReconcilePaymentUseCase --> PaymentsMetrics : in sync / corrected
    ExpirePaymentWindowUseCase --> PaymentsMetrics : declines
    MeasuredPaymentProvider --> PaymentsMetrics : provider call duration
    CreatePaymentUseCase --> PaymentsMetrics : approved / declined
    AuthorizePaymentUseCase --> PaymentsMetrics : approved / declined
    CapturePaymentUseCase --> PaymentsMetrics
    SettlePaymentForCancellationUseCase --> PaymentsMetrics : voids
    RequestRefundUseCase --> PaymentsMetrics

```

## Paridade com `Docs/database/OrderCore_Modelagem_Banco_Backend.docx`

O documento de modelagem de banco já especificava `customer_payment_method_id`, `provider`, `authorized_at`, `captured_at`, `created_at` e `updated_at` em `PAYMENTS` (seção 7.1) — nenhum tinha chegado a este diagrama, que ficava sem qualquer timestamp. Adicionados agora, com `Authorize`/`Capture` passando a receber `now` para preencher `AuthorizedAt`/`CapturedAt`, e `Create` passando a receber `provider`, `customerPaymentMethodId` (nullable — nem todo pagamento usa um cartão salvo) e `now`.

## Invariante a confirmar

`Refund` já não é anêmica (`Complete`/`Fail`), mas a regra "o valor do estorno não pode exceder o saldo reembolsável do pagamento" deve ser validada dentro de `Payment.RequestRefund` (o dono do invariante, que conhece `Amount` e os `Refunds` já concedidos) — não só na camada de Application.

## Consumido por outros módulos

- **Orders** chama `CreatePaymentUseCase` (com a forma de pagamento escolhida no checkout, mapeada de `PaymentMethodChoice` do Orders) e `GetPaymentByOrderIdUseCase` (para o resumo do pagamento na tela do pedido) de dentro de um `PaymentGatewayAdapter` (implementa o `IPaymentGateway` do próprio Orders) e reage a `PaymentAuthorized`/`PaymentFailed`/`PaymentAuthorizationExpired`, que chegam pelo RabbitMQ na fila `orders.payment-outcomes` — ver [05-orders.md](05-orders.md). Pelo mesmo adapter, o Orders também usa `GetAvailablePaymentMethodsUseCase` (checkout recusa uma forma indisponível), `GetPaymentNextActionUseCase` (a etapa de confirmação no checkout repetido) e `CountExpiringAuthorizationsUseCase` (o painel). A timeline do pedido do admin (`orders.timeline`) arquiva todos os eventos de Payments sobre o pedido. `Payment.OrderId` guarda apenas o id, nunca uma referência a `Order`.
