# Módulo Orders

Agregado principal do sistema. Como [02-customers.md](02-customers.md), [03-catalog.md](03-catalog.md) e [04-inventory.md](04-inventory.md), este módulo está **implementado** de ponta a ponta (Domain, Application, Infrastructure/EF Core e Presentation), incluindo o fluxo completo de checkout (seção "Fluxo de checkout" abaixo) — não é mais um blueprint futuro. Ver `Docs/specs/orders/checkout-aggregate.md` para o spec completo, incluindo a decisão de implementar o módulo Payments (ver [06-payments.md](06-payments.md)) antes de fechar este módulo, já que `RequestOrderPaymentUseCase`/`PaymentGatewayAdapter`/os dois integration event handlers dependem dele. Este diagrama inclui os *adapters* que o próprio módulo Orders usa para falar com Catalog, Inventory e Payments sem depender do Domain/Infrastructure interno deles — só das classes externas marcadas `<<external>>` (o detalhe completo de cada uma está no arquivo do módulo dono). Base: [Shared kernel](01-shared-kernel.md).

Diferenças entre este diagrama e o código, todas documentadas nos comentários das classes correspondentes:

- `Order.Create` recebe também um `customerNotes` opcional (`string? customerNotes = null`) — nada mais no agregado tinha como definir esse campo.
- `IOrderRepository` ganhou `ListByCustomerIdAsync(Guid customerId)`, não listado no diagrama original — `ListCustomerOrdersUseCase` depende dessa interface e não tinha por onde consultar, mesma classe de lacuna de `IInventoryReservationRepository.ListByOrderIdAsync` em Inventory.
- `Order` ganhou dois métodos de passagem não listados no diagrama: `DecreaseItemQuantity(Guid productId, int quantity)` e `ApplyItemDiscount(Guid productId, decimal amount)`, delegando para `OrderItem.DecreaseQuantity`/`ApplyDiscount` — como esses dois métodos do item são `internal` (ver "Comportamento de OrderItem" abaixo), só o agregado (`Order`) pode chamá-los; sem esses dois métodos de passagem nada fora do assembly conseguiria disparar essa mudança.
- `EfOrderRepository.SaveChangesAsync` despacha os domain events do agregado via `IDomainEventDispatcher` logo após o save, igual ao padrão de `InventoryUnitOfWork` — só que aqui sem `IUnitOfWork`, porque toda operação de Orders mexe em um único agregado raiz (`Order`) por vez (ver a seção Transactions do `claude.md`).
- `IPaymentGateway`/`PaymentGatewayAdapter` (envolvendo `CreatePaymentUseCase` de Payments) e os dois `IDomainEventHandler` de `PaymentAuthorized`/`PaymentFailed` só existem porque `IntegrationEvent` (Payments) passou a implementar `IDomainEvent` — ver a nota em [06-payments.md](06-payments.md) sobre a ponte do outbox antes de existir RabbitMQ.
- `RequestOrderPaymentUseCase.ExecuteAsync` retorna `CreateOrderResult`, não `Order`/`void` — reaproveita o mesmo DTO de `CreateOrderHandler` já que ambos só precisam devolver id/total/status.
- `OrdersController.RequestPaymentAsync` devolve `Task<IActionResult>` (202 Accepted, sem corpo), como já estava no diagrama: confirmar ou falhar o pedido acontece depois, de forma assíncrona (ver `PaymentAuthorizedIntegrationEventHandler`/`PaymentFailedIntegrationEventHandler`), então um corpo `OrderResponse` aqui seria enganoso de qualquer forma — `CreateOrderResult` não carrega dados suficientes para montar um.

Adicionado pelo MVP do storefront (`Docs/specs/storefront/storefront-api-mvp.md`, etapas 6–7):

- **Checkout em um passo** — `CheckoutUseCase` faz, num único caso de uso, o que antes o cliente encadeava (`POST orders` → `PUT addresses` → `POST request-payment`, que continuam existindo para fluxos manuais/admin): resolve os endereços salvos via `ICustomerDirectory` (novo contrato, adaptado por `CustomerDirectoryAdapter` sobre `GetCustomerAddressUseCase` de Customers), valida produtos compráveis e moeda única, confere `ExpectedTotal` opcional (`price_changed`), reserva estoque **antes** de salvar o pedido, salva uma vez já em `PendingPayment` (liberando as reservas se o save falhar) e só então solicita o pagamento com o método escolhido. É uma sequência com compensação, não uma transação: Orders, Inventory e Payments têm `DbContext`s separados.
- **Idempotência** — `Order.CheckoutIdempotencyKey` (header `Idempotency-Key`, único por cliente via índice `(CustomerId, CheckoutIdempotencyKey)`): repetir o checkout devolve o mesmo pedido e, se o pagamento nunca chegou a começar, solicita-o de novo; uma requisição concorrente com a mesma chave recebe o pedido da vencedora. `Order.Create` ganhou o parâmetro opcional `checkoutIdempotencyKey`.
- **Cotação do carrinho** — `QuoteCartUseCase` (somente leitura) devolve, por linha, preço atual e no máximo um problema (`NotFound` > `Unavailable` > `InsufficientStock` > `PriceChanged`).
- **Contratos só com tipos do Orders** — `IProductCatalog` devolve `CatalogProductSnapshot` (antes devolvia a entidade `Product` do Catalog; uma regra de arquitetura agora proíbe a camada Application do Orders de depender do Domain/Infrastructure de outro módulo); `IPaymentGateway` recebe `PaymentMethodChoice` e expõe `GetPaymentSummaryAsync`; `IInventoryService` expõe `GetAvailableQuantitiesAsync` (sobre `GetStockAvailabilityUseCase` de Inventory). `InventoryServiceAdapter.TryReserveOrderItemsAsync` confere disponibilidade antes de reservar (produto sem registro de estoque é "estoque insuficiente", não 404) e libera reservas parciais em qualquer falha.
- **Acompanhamento** — `RequestPayment` passou a disparar `OrderPaymentRequested`, para o histórico registrar `Created → PendingPayment`; `order_status_history` ganhou `Sequence` (identity) para manter a ordem de transições gravadas no mesmo save com o mesmo horário; `IOrderStatusHistoryReader`/`EfOrderStatusHistoryReader` expõem esse histórico. `GetOrderDetailsUseCase` junta o pedido ao resumo do pagamento; `ListCustomerOrdersUseCase` e `IOrderRepository.ListByCustomerIdAsync` ficaram paginados (mais novo primeiro).
- `OrderPresenter.ToResponse(CreateOrderResult)` foi removido (não era usado por nenhum endpoint).

Adicionado pela autenticação (V2, `Docs/specs/identity/authentication-and-account.md`):

- **O cliente vem do token.** O checkout (política `Customer`) usa `ICurrentUser.CustomerId`; `CheckoutRequest.CustomerId` foi removido, então ninguém compra em nome de outro cliente.
- **Só o dono vê o pedido.** `GetOrderDetailsUseCase`/`GetOrderStatusHistoryUseCase` recebem o cliente que está pedindo (`null` para admin); `OrderAccess` responde `order_not_found` para o pedido de outro cliente, igual a um pedido que não existe, para ids não poderem ser sondados. É regra de caso de uso, não do controller.
- **`GET orders/me`**: o histórico do cliente logado. `GET orders/customers/{id}` e os endpoints passo a passo (`POST orders`, `PUT …/addresses`, `request-payment`, `cancel`) ficaram só para admin.

Adicionado pelo backoffice (`Docs/specs/backoffice/backoffice-api.md`, decisões 1 a 3):

- **Atendimento.** `StartProcessing`/`Ship`/`Deliver` passaram a disparar `OrderProcessingStarted`/`OrderShipped`/`OrderDelivered`, que o `OrderStatusHistoryProjector` grava no histórico. `FulfilOrderUseCase` (um caso de uso, três métodos) move o pedido pelos passos; **enviar captura o pagamento antes** (`IPaymentGateway.CaptureForOrderAsync`) — se o provider recusar, o pedido fica em `Processing` e o admin recebe `409 payment_capture_failed`. `Order.EnsureCanShip`/`EnsureCanBeCancelled` checam a regra antes de qualquer efeito colateral.
- **Cancelamento com acerto do pagamento.** `CancelOrderUseCase` agora, nesta ordem: confere se pode cancelar, acerta o pagamento (`SettleForCancellationAsync`: void de uma autorização, estorno de uma captura), libera reservas ainda retidas, devolve ao estoque o que já foi consumido (`ReturnConsumedStockAsync`), cancela e salva. Sem transação entre módulos, então cada passo antes do save é idempotente e repetir o cancelamento termina o serviço. Devolve `OrderPaymentSettlement`; a ação saiu do `OrdersController` para o `OrderFulfilmentController` (mesma rota) e responde 200 com o resultado.
- **Consumidores tolerantes.** `ConfirmOrderUseCase`/`MarkOrderPaymentFailedUseCase` ignoram (com log) um pedido que já não está `PendingPayment` — antes lançavam `invalid_order_state` dentro do publisher do outbox, que nunca marcava a mensagem como processada e travava as seguintes.
- **Leituras do admin** em `OrdersAdminController` (`admin/…`): `ListOrdersUseCase` (todos os pedidos, filtros por status/cliente/período, com o cliente e o status do pagamento buscados uma vez por página), `GetAdminOrderDetailsUseCase` (o pedido + notas internas, cliente, pagamento completo e reservas) e `GetDashboardUseCase` (pedidos por status, receita por moeda dos confirmados no período, clientes novos, alertas de estoque e pedidos recentes, calculado na hora a partir de cada módulo). `SetOrderInternalNotesUseCase` (até 2000 caracteres; vazio limpa).
- **Contratos ampliados, sempre com tipos do Orders:** `IPaymentGateway` (`GetPaymentSummariesAsync`, `GetPaymentDetailsAsync`, `CaptureForOrderAsync`, `SettleForCancellationAsync`), `IInventoryService` (`ReturnConsumedStockAsync`, `GetReservationsAsync`, `GetStockAlertCountsAsync`), `ICustomerDirectory` (`GetCustomersAsync`, `CountNewCustomersAsync`); `IOrderRepository` ganhou `ListAsync`, `CountByStatusAsync` e `SumConfirmedTotalsAsync` (total de cada pedido calculado no SQL a partir dos itens, somado por moeda em memória). Migration `AddOrderListIndexes` (`CreatedAt`, `ConfirmedAt`).

```mermaid

classDiagram
    direction LR

    class AggregateRoot~TId~ {
        <<external>>
    }

    class Address {
        <<external>>
    }

    class IDomainEventDispatcher {
        <<external>>
        <<interface>>
    }

    class IProductRepository {
        <<external>>
        <<interface>>
    }

    class ReserveStockUseCase {
        <<external>>
    }

    class ReleaseReservationUseCase {
        <<external>>
    }

    class ConsumeReservationUseCase {
        <<external>>
    }

    class IInventoryReservationRepository {
        <<external>>
        <<interface>>
    }

    class GetStockAvailabilityUseCase {
        <<external>>
    }

    class GetCustomerAddressUseCase {
        <<external>>
    }

    class CreatePaymentUseCase {
        <<external>>
    }

    class GetPaymentByOrderIdUseCase {
        <<external>>
    }

    class GetPaymentsByOrderIdsUseCase {
        <<external>>
    }

    class CapturePaymentUseCase {
        <<external>>
    }

    class SettlePaymentForCancellationUseCase {
        <<external>>
    }

    class ReturnOrderStockUseCase {
        <<external>>
    }

    class ListReservationsUseCase {
        <<external>>
    }

    class GetStockSummaryUseCase {
        <<external>>
    }

    class GetCustomersByIdsUseCase {
        <<external>>
    }

    class CountNewCustomersUseCase {
        <<external>>
    }

    class ICurrentUser {
        <<external>>
        <<interface>>
    }

    class PaymentAuthorized {
        <<external>>
    }

    class PaymentFailed {
        <<external>>
    }

    note for IProductRepository "Catalog module — ver 03-catalog.md"
    note for ReserveStockUseCase "Inventory module — ver 04-inventory.md"
    note for ReleaseReservationUseCase "Inventory module — ver 04-inventory.md"
    note for ConsumeReservationUseCase "Inventory module — ver 04-inventory.md"
    note for IInventoryReservationRepository "Inventory module — ver 04-inventory.md"
    note for GetStockAvailabilityUseCase "Inventory module — ver 04-inventory.md"
    note for GetCustomerAddressUseCase "Customers module — ver 02-customers.md"
    note for CreatePaymentUseCase "Payments module — ver 06-payments.md"
    note for GetPaymentByOrderIdUseCase "Payments module — ver 06-payments.md"
    note for ICurrentUser "Shared kernel — ver 01-shared-kernel.md"
    note for PaymentAuthorized "Payments module — ver 06-payments.md"
    note for PaymentFailed "Payments module — ver 06-payments.md"

    %% OrderCore.Api.Modules.Orders.Domain.Entities
    class Order {
        +string OrderNumber
        +Guid CustomerId
        +OrderStatus Status
        +IReadOnlyCollection~OrderItem~ Items
        +decimal SubtotalAmount
        +decimal DiscountAmount
        +decimal ShippingAmount
        +decimal TaxAmount
        +decimal TotalAmount
        +string Currency
        +Address? ShippingAddress
        +Address? BillingAddress
        +string? CustomerNotes
        +int MaxCheckoutIdempotencyKeyLength$
        +string? InternalNotes
        +string? CheckoutIdempotencyKey
        +DateTimeOffset CreatedAt
        +DateTimeOffset UpdatedAt
        +DateTimeOffset? ConfirmedAt
        +DateTimeOffset? CancelledAt
        +DateTimeOffset? ShippedAt
        +DateTimeOffset? DeliveredAt
        +Create(Guid customerId, string currency, string orderNumber, DateTimeOffset now, string? customerNotes, string? checkoutIdempotencyKey)$ Order
        +AddItem(Guid productId, Guid? productVariantId, string productSku, string productName, string? productImageUrl, decimal unitPrice, int quantity) void
        +RemoveItem(Guid productId) void
        +DecreaseItemQuantity(Guid productId, int quantity) void
        +ApplyItemDiscount(Guid productId, decimal amount) void
        +SetAddresses(Address shippingAddress, Address billingAddress) void
        +MaxInternalNotesLength int$
        +SetInternalNotes(string? notes) void
        +ApplyDiscount(decimal amount) void
        +SetShippingAmount(decimal amount) void
        +SetTaxAmount(decimal amount) void
        +RequestPayment(DateTimeOffset now) void
        +Confirm(DateTimeOffset now) void
        +StartProcessing(DateTimeOffset now) void
        +EnsureCanShip() void
        +Ship(DateTimeOffset now) void
        +Deliver(DateTimeOffset now) void
        +FailPayment(string reason, DateTimeOffset now) void
        +EnsureCanBeCancelled() void
        +Cancel(string reason, DateTimeOffset now) void
    }

    class OrderItem {
        +Guid ProductId
        +Guid? ProductVariantId
        +string ProductSku
        +string ProductName
        +string? ProductImageUrl
        +decimal UnitPrice
        +int Quantity
        +decimal DiscountAmount
        +decimal Total
        +IncreaseQuantity(int quantity) void
        +DecreaseQuantity(int quantity) void
        +ApplyDiscount(decimal amount) void
    }


    %% OrderCore.Api.Modules.Orders.Domain.Enums
    class OrderStatus {
        <<enumeration>>
        Created
        PendingPayment
        Confirmed
        Processing
        Shipped
        Delivered
        PaymentFailed
        Cancelled
    }


    %% OrderCore.Api.Modules.Orders.Domain.Events
    class OrderCreated {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid OrderId
        +Guid CustomerId
    }

    class OrderPaymentRequested {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid OrderId
    }

    class OrderConfirmed {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid OrderId
    }

    class OrderCancelled {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid OrderId
        +string Reason
    }

    class OrderPaymentFailed {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid OrderId
        +string Reason
    }

    class OrderProcessingStarted {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid OrderId
    }

    class OrderShipped {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid OrderId
    }

    class OrderDelivered {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid OrderId
    }


    %% OrderCore.Api.Modules.Orders.Application.Contracts
    class IOrderRepository {
        <<interface>>
        +GetByIdAsync(Guid orderId) Task~Order?~
        +ListByCustomerIdAsync(Guid customerId, int page, int pageSize) Task~ValueTuple~IReadOnlyList~Order~, int~~
        +FindByCheckoutIdempotencyKeyAsync(Guid customerId, string idempotencyKey) Task~Order?~
        +ListAsync(ListOrdersFilter filter) Task~ValueTuple~IReadOnlyList~Order~, int~~
        +CountByStatusAsync(DateTimeOffset from, DateTimeOffset to) Task~IReadOnlyDictionary~OrderStatus, int~~
        +SumConfirmedTotalsAsync(DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection~OrderStatus~ statuses) Task~IReadOnlyDictionary~string, decimal~~
        +AddAsync(Order order) Task
        +SaveChangesAsync() Task
    }

    class IProductCatalog {
        <<interface>>
        +GetAsync(Guid productId) Task~CatalogProductSnapshot?~
        +GetManyAsync(IReadOnlyCollection~Guid~ productIds) Task~IReadOnlyDictionary~Guid, CatalogProductSnapshot~~
    }

    class IInventoryService {
        <<interface>>
        +TryReserveOrderItemsAsync(Order order) Task~bool~
        +GetAvailableQuantitiesAsync(IReadOnlyCollection~Guid~ productIds) Task~IReadOnlyDictionary~Guid, int~~
        +ReleaseReservationsAsync(Guid orderId) Task
        +ConsumeReservationsAsync(Guid orderId) Task
        +ReturnConsumedStockAsync(Guid orderId) Task~int~
        +GetReservationsAsync(Guid orderId) Task~IReadOnlyList~OrderReservationSummary~~
        +GetStockAlertCountsAsync() Task~StockAlertCounts~
    }

    class IPaymentGateway {
        <<interface>>
        +RequestPaymentAsync(Guid orderId, decimal amount, string currency, PaymentMethodChoice method, string idempotencyKey) Task~Guid~
        +GetPaymentSummaryAsync(Guid orderId) Task~OrderPaymentSummary?~
        +GetPaymentSummariesAsync(IReadOnlyCollection~Guid~ orderIds) Task~IReadOnlyDictionary~Guid, OrderPaymentSummary~~
        +GetPaymentDetailsAsync(Guid orderId) Task~OrderPaymentDetails?~
        +CaptureForOrderAsync(Guid orderId) Task
        +SettleForCancellationAsync(Guid orderId, string reason) Task~OrderPaymentSettlement~
    }

    class ICustomerDirectory {
        <<interface>>
        +GetAddressAsync(Guid customerId, Guid addressId) Task~Address~
        +GetCustomersAsync(IReadOnlyCollection~Guid~ customerIds) Task~IReadOnlyDictionary~Guid, OrderCustomerSnapshot~~
        +CountNewCustomersAsync(DateTimeOffset from, DateTimeOffset to) Task~int~
    }

    class OrderPaymentSettlement {
        <<enumeration>>
        NothingToSettle
        Voided
        Refunded
    }

    class OrderCustomerSnapshot {
        +Guid Id
        +string Name
        +string Email
        +bool Active
    }

    class OrderPaymentDetails {
        +Guid PaymentId
        +string Status
        +PaymentMethodChoice Method
        +decimal Amount
        +string Currency
        +string Provider
        +string? ProviderReference
        +string? FailureReason
        +DateTimeOffset CreatedAt
        +DateTimeOffset? AuthorizedAt
        +DateTimeOffset? CapturedAt
        +DateTimeOffset? VoidedAt
        +IReadOnlyList~OrderRefundSummary~ Refunds
    }

    class OrderRefundSummary {
        +Guid Id
        +decimal Amount
        +string Reason
        +string Status
        +DateTimeOffset RequestedAt
        +DateTimeOffset? ProcessedAt
    }

    class OrderReservationSummary {
        +Guid ReservationId
        +Guid ProductId
        +int Quantity
        +string Status
        +DateTimeOffset ReservedAt
        +DateTimeOffset? ReleasedAt
        +DateTimeOffset? ConsumedAt
        +DateTimeOffset? ReturnedAt
    }

    class StockAlertCounts {
        +int LowStock
        +int OutOfStock
    }

    class ListOrdersFilter {
        +OrderStatus? Status
        +Guid? CustomerId
        +DateTimeOffset? CreatedFrom
        +DateTimeOffset? CreatedTo
        +int Page
        +int PageSize
    }

    class AdminOrderSummaryOutput {
        +OrderSummaryOutput Order
        +OrderCustomerSnapshot? Customer
        +string? PaymentStatus
    }

    class AdminOrderDetailsOutput {
        +Order Order
        +OrderCustomerSnapshot? Customer
        +OrderPaymentDetails? Payment
        +IReadOnlyList~OrderReservationSummary~ Reservations
    }

    class DashboardOutput {
        +DateTimeOffset From
        +DateTimeOffset To
        +IReadOnlyDictionary~OrderStatus, int~ OrdersByStatus
        +IReadOnlyDictionary~string, decimal~ RevenueByCurrency
        +int NewCustomers
        +StockAlertCounts Stock
        +IReadOnlyList~AdminOrderSummaryOutput~ RecentOrders
    }

    class IOrderStatusHistoryReader {
        <<interface>>
        +ListAsync(Guid orderId) Task~IReadOnlyList~OrderStatusHistoryEntry~~
    }

    class IOrderNumberGenerator {
        <<interface>>
        +NextAsync() Task~string~
    }


    %% OrderCore.Api.Modules.Orders.Application.DTOs
    class CreateOrderItem {
        +Guid ProductId
        +int Quantity
    }

    class CreateOrderCommand {
        +Guid CustomerId
        +string Currency
        +IReadOnlyCollection~CreateOrderItem~ Items
    }

    class CreateOrderResult {
        +Guid OrderId
        +decimal TotalAmount
        +string Status
    }

    class SetOrderAddressesCommand {
        +Guid OrderId
        +Address ShippingAddress
        +Address BillingAddress
    }

    class CancelOrderCommand {
        +Guid OrderId
        +string Reason
    }

    class CatalogProductSnapshot {
        +Guid Id
        +string Sku
        +string Slug
        +string Name
        +string? PrimaryImageUrl
        +decimal CurrentPrice
        +string Currency
        +bool IsPurchasable
    }

    class PaymentMethodChoice {
        <<enumeration>>
        Card
        Pix
    }

    class OrderPaymentSummary {
        +Guid PaymentId
        +string Status
        +PaymentMethodChoice Method
        +string? FailureReason
    }

    class OrderDetailsOutput {
        +Order Order
        +OrderPaymentSummary? Payment
    }

    class OrderSummaryOutput {
        +Guid Id
        +string OrderNumber
        +OrderStatus Status
        +DateTimeOffset CreatedAt
        +decimal TotalAmount
        +string Currency
        +int ItemCount
    }

    class OrderStatusHistoryEntry {
        +string? FromStatus
        +string ToStatus
        +string? Reason
        +DateTimeOffset ChangedAt
    }

    class ListCustomerOrdersInput {
        +Guid CustomerId
        +int Page
        +int PageSize
    }

    class CheckoutItem {
        +Guid ProductId
        +int Quantity
    }

    class CheckoutCommand {
        +Guid CustomerId
        +IReadOnlyList~CheckoutItem~ Items
        +Guid ShippingAddressId
        +Guid BillingAddressId
        +PaymentMethodChoice PaymentMethod
        +string IdempotencyKey
        +string? CustomerNotes
        +decimal? ExpectedTotal
    }

    class QuoteCartLine {
        +Guid ProductId
        +int Quantity
        +decimal? ExpectedUnitPrice
    }

    class CartLineIssue {
        <<enumeration>>
        NotFound
        Unavailable
        InsufficientStock
        PriceChanged
    }

    class CartQuoteLine {
        +Guid ProductId
        +string? ProductName
        +string? Slug
        +string? ImageUrl
        +decimal? UnitPrice
        +int Quantity
        +decimal LineTotal
        +CartLineIssue? Issue
        +decimal? PreviousUnitPrice
    }

    class CartQuote {
        +string? Currency
        +decimal Total
        +bool IsValid
        +IReadOnlyList~CartQuoteLine~ Lines
    }


    %% OrderCore.Api.Modules.Orders.Application.UseCases
    class CreateOrderHandler {
        -IOrderRepository orderRepository
        -IProductCatalog productCatalog
        -IOrderNumberGenerator orderNumbers
        -IAuditLogService auditLog
        -TimeProvider timeProvider
        +HandleAsync(CreateOrderCommand command) Task~CreateOrderResult~
    }

    class CheckoutUseCase {
        -IOrderRepository orderRepository
        -IProductCatalog productCatalog
        -ICustomerDirectory customerDirectory
        -IInventoryService inventoryService
        -IPaymentGateway paymentGateway
        -IOrderNumberGenerator orderNumbers
        -IAuditLogService auditLog
        -TimeProvider timeProvider
        +ExecuteAsync(CheckoutCommand command) Task~Guid~
    }

    class QuoteCartUseCase {
        -IProductCatalog productCatalog
        -IInventoryService inventoryService
        +ExecuteAsync(IReadOnlyList~QuoteCartLine~ lines) Task~CartQuote~
    }

    class GetOrderDetailsUseCase {
        -IOrderRepository orderRepository
        -IPaymentGateway paymentGateway
        +ExecuteAsync(Guid orderId, Guid? requestingCustomerId) Task~OrderDetailsOutput~
    }

    class OrderAccess {
        <<static>>
        +LoadVisibleToAsync(IOrderRepository orders, Guid orderId, Guid? requestingCustomerId)$ Task~Order~
    }

    class GetOrderStatusHistoryUseCase {
        -IOrderRepository orderRepository
        -IOrderStatusHistoryReader history
        +ExecuteAsync(Guid orderId, Guid? requestingCustomerId) Task~IReadOnlyList~OrderStatusHistoryEntry~~
    }

    class SetOrderAddressesUseCase {
        -IOrderRepository orderRepository
        +ExecuteAsync(SetOrderAddressesCommand command) Task
    }

    class RequestOrderPaymentUseCase {
        -IOrderRepository orderRepository
        -IInventoryService inventoryService
        -IPaymentGateway paymentGateway
        -TimeProvider timeProvider
        +ExecuteAsync(Guid orderId, PaymentMethodChoice paymentMethod) Task~CreateOrderResult~
    }

    class ConfirmOrderUseCase {
        -IOrderRepository orderRepository
        -IInventoryService inventoryService
        -TimeProvider timeProvider
        -ILogger logger
        +ExecuteAsync(Guid orderId) Task
    }

    class MarkOrderPaymentFailedUseCase {
        -IOrderRepository orderRepository
        -IInventoryService inventoryService
        -TimeProvider timeProvider
        -ILogger logger
        +ExecuteAsync(Guid orderId, string reason) Task
    }

    class CancelOrderUseCase {
        -IOrderRepository orderRepository
        -IInventoryService inventoryService
        -IPaymentGateway paymentGateway
        -TimeProvider timeProvider
        +ExecuteAsync(CancelOrderCommand command) Task~OrderPaymentSettlement~
    }

    class FulfilOrderUseCase {
        -IOrderRepository orderRepository
        -IPaymentGateway paymentGateway
        +StartProcessingAsync(Guid orderId) Task~OrderDetailsOutput~
        +ShipAsync(Guid orderId) Task~OrderDetailsOutput~
        +DeliverAsync(Guid orderId) Task~OrderDetailsOutput~
    }

    class SetOrderInternalNotesUseCase {
        -IOrderRepository orderRepository
        +ExecuteAsync(Guid orderId, string? notes) Task
    }

    class ListOrdersUseCase {
        -IOrderRepository orderRepository
        -ICustomerDirectory customerDirectory
        -IPaymentGateway paymentGateway
        +ExecuteAsync(ListOrdersFilter filter) Task~PagedResult~AdminOrderSummaryOutput~~
    }

    class GetAdminOrderDetailsUseCase {
        -IOrderRepository orderRepository
        -ICustomerDirectory customerDirectory
        -IPaymentGateway paymentGateway
        -IInventoryService inventoryService
        +ExecuteAsync(Guid orderId) Task~AdminOrderDetailsOutput~
    }

    class GetDashboardUseCase {
        -IOrderRepository orderRepository
        -ICustomerDirectory customerDirectory
        -IInventoryService inventoryService
        -ListOrdersUseCase listOrders
        +ExecuteAsync(DateTimeOffset? from, DateTimeOffset? to) Task~DashboardOutput~
    }

    class GetOrderByIdUseCase {
        -IOrderRepository orderRepository
        +ExecuteAsync(Guid orderId) Task~Order?~
    }

    class ListCustomerOrdersUseCase {
        -IOrderRepository orderRepository
        +ExecuteAsync(ListCustomerOrdersInput input) Task~PagedResult~OrderSummaryOutput~~
    }


    %% OrderCore.Api.Modules.Orders
    class OrdersDependencyInjection {
        <<static>>
        +AddOrdersModule(IServiceCollection services)$ IServiceCollection
    }


    %% OrderCore.Api.Modules.Orders.Infrastructure.Persistence
    class OrderPersistenceModel {
        +Guid Id
        +Guid CustomerId
        +string Status
        +decimal SubtotalAmount
        +decimal DiscountAmount
        +decimal ShippingAmount
        +decimal TaxAmount
        +string Currency
        +string? CheckoutIdempotencyKey
        +int Version
        +ICollection~OrderItemPersistenceModel~ Items
    }

    class OrderItemPersistenceModel {
        +Guid Id
        +Guid OrderId
        +Guid ProductId
        +string ProductSku
        +string ProductName
        +decimal UnitPrice
        +int Quantity
    }

    class OrderStatusHistoryPersistenceModel {
        +Guid Id
        +Guid OrderId
        +long Sequence
        +string FromStatus
        +string ToStatus
        +string? Reason
        +DateTimeOffset ChangedAt
    }

    class OrderMapper {
        +ToDomain(OrderPersistenceModel model) Order
        +ToPersistence(Order domain) OrderPersistenceModel
        +ApplyChanges(Order domain, OrderPersistenceModel model) void
    }

    class OrdersDbContext {
        +DbSet~OrderPersistenceModel~ Orders
        +DbSet~OrderStatusHistoryPersistenceModel~ StatusHistory
        +SaveChangesAsync() Task~int~
    }

    class EfOrderRepository {
        -OrdersDbContext dbContext
        -OrderMapper mapper
        -IDomainEventDispatcher dispatcher
    }

    class SequentialOrderNumberGenerator {
        -OrdersDbContext dbContext
        +NextAsync() Task~string~
    }

    class EfOrderStatusHistoryReader {
        -OrdersDbContext dbContext
        +ListAsync(Guid orderId) Task~IReadOnlyList~OrderStatusHistoryEntry~~
    }


    %% OrderCore.Api.Modules.Orders.Infrastructure.Adapters
    class ProductCatalogAdapter {
        -IProductRepository catalogProducts
        +GetAsync(Guid productId) Task~CatalogProductSnapshot?~
        +GetManyAsync(IReadOnlyCollection~Guid~ productIds) Task~IReadOnlyDictionary~Guid, CatalogProductSnapshot~~
    }

    class InventoryServiceAdapter {
        -ReserveStockUseCase reserveStock
        -ReleaseReservationUseCase releaseReservation
        -ConsumeReservationUseCase consumeReservation
        -GetStockAvailabilityUseCase getStockAvailability
        -IInventoryReservationRepository reservations
        -ReturnOrderStockUseCase returnOrderStock
        -ListReservationsUseCase listReservations
        -GetStockSummaryUseCase getStockSummary
        +TryReserveOrderItemsAsync(Order order) Task~bool~
        +GetAvailableQuantitiesAsync(IReadOnlyCollection~Guid~ productIds) Task~IReadOnlyDictionary~Guid, int~~
        +ReleaseReservationsAsync(Guid orderId) Task
        +ConsumeReservationsAsync(Guid orderId) Task
        +ReturnConsumedStockAsync(Guid orderId) Task~int~
        +GetReservationsAsync(Guid orderId) Task~IReadOnlyList~OrderReservationSummary~~
        +GetStockAlertCountsAsync() Task~StockAlertCounts~
    }

    class PaymentGatewayAdapter {
        -CreatePaymentUseCase createPayment
        -GetPaymentByOrderIdUseCase getPaymentByOrderId
        -GetPaymentsByOrderIdsUseCase getPaymentsByOrderIds
        -CapturePaymentUseCase capturePayment
        -SettlePaymentForCancellationUseCase settlePayment
        +RequestPaymentAsync(Guid orderId, decimal amount, string currency, PaymentMethodChoice method, string idempotencyKey) Task~Guid~
        +GetPaymentSummaryAsync(Guid orderId) Task~OrderPaymentSummary?~
        +GetPaymentSummariesAsync(IReadOnlyCollection~Guid~ orderIds) Task~IReadOnlyDictionary~Guid, OrderPaymentSummary~~
        +GetPaymentDetailsAsync(Guid orderId) Task~OrderPaymentDetails?~
        +CaptureForOrderAsync(Guid orderId) Task
        +SettleForCancellationAsync(Guid orderId, string reason) Task~OrderPaymentSettlement~
    }

    class CustomerDirectoryAdapter {
        -GetCustomerAddressUseCase getCustomerAddress
        -GetCustomersByIdsUseCase getCustomersByIds
        -CountNewCustomersUseCase countNewCustomers
        +GetAddressAsync(Guid customerId, Guid addressId) Task~Address~
        +GetCustomersAsync(IReadOnlyCollection~Guid~ customerIds) Task~IReadOnlyDictionary~Guid, OrderCustomerSnapshot~~
        +CountNewCustomersAsync(DateTimeOffset from, DateTimeOffset to) Task~int~
    }


    %% OrderCore.Api.Modules.Orders.Infrastructure.EventHandlers
    class OrderStatusHistoryProjector {
        -OrdersDbContext dbContext
        +HandleAsync(OrderCreated domainEvent) Task
        +HandleAsync(OrderPaymentRequested domainEvent) Task
        +HandleAsync(OrderConfirmed domainEvent) Task
        +HandleAsync(OrderCancelled domainEvent) Task
        +HandleAsync(OrderPaymentFailed domainEvent) Task
        +HandleAsync(OrderProcessingStarted domainEvent) Task
        +HandleAsync(OrderShipped domainEvent) Task
        +HandleAsync(OrderDelivered domainEvent) Task
    }


    %% OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers
    class PaymentAuthorizedIntegrationEventHandler {
        -ConfirmOrderUseCase confirmOrder
        +HandleAsync(PaymentAuthorized integrationEvent) Task
    }

    class PaymentFailedIntegrationEventHandler {
        -MarkOrderPaymentFailedUseCase markPaymentFailed
        +HandleAsync(PaymentFailed integrationEvent) Task
    }


    %% OrderCore.Api.Modules.Orders.Presentation
    class OrdersController {
        -CreateOrderHandler createOrderHandler
        -SetOrderAddressesUseCase setOrderAddressesUseCase
        -RequestOrderPaymentUseCase requestOrderPaymentUseCase
        -GetOrderByIdUseCase getOrderByIdUseCase
        -GetOrderDetailsUseCase getOrderDetailsUseCase
        -GetOrderStatusHistoryUseCase getOrderStatusHistoryUseCase
        -ListCustomerOrdersUseCase listCustomerOrdersUseCase
        -CheckoutUseCase checkoutUseCase
        -QuoteCartUseCase quoteCartUseCase
        -ICurrentUser currentUser
        +QuoteCartAsync(QuoteCartRequest request) Task~ActionResult~CartQuoteResponse~~
        +CheckoutAsync(CheckoutRequest request, string idempotencyKey) Task~ActionResult~OrderResponse~~
        +CreateOrderAsync(CreateOrderRequest request) Task~ActionResult~OrderResponse~~
        +SetAddressesAsync(Guid id, SetOrderAddressesRequest request) Task~IActionResult~
        +RequestPaymentAsync(Guid id, RequestOrderPaymentRequest request) Task~IActionResult~
        +GetByIdAsync(Guid id) Task~ActionResult~OrderResponse~~
        +GetStatusHistoryAsync(Guid id) Task~ActionResult~IReadOnlyList~OrderStatusHistoryEntryResponse~~~
        +ListMineAsync(int page, int pageSize) Task~ActionResult~PagedResponse~OrderSummaryResponse~~~
        +ListByCustomerAsync(Guid customerId, int page, int pageSize) Task~ActionResult~PagedResponse~OrderSummaryResponse~~~
    }

    class OrderFulfilmentController {
        <<admin>>
        -FulfilOrderUseCase fulfilOrder
        -CancelOrderUseCase cancelOrder
        -SetOrderInternalNotesUseCase setInternalNotes
        +StartProcessingAsync(Guid id) Task~ActionResult~OrderResponse~~
        +ShipAsync(Guid id) Task~ActionResult~OrderResponse~~
        +DeliverAsync(Guid id) Task~ActionResult~OrderResponse~~
        +CancelAsync(Guid id, CancelOrderRequest request) Task~ActionResult~CancelOrderResponse~~
        +SetInternalNotesAsync(Guid id, SetOrderInternalNotesRequest request) Task~IActionResult~
    }

    class OrdersAdminController {
        <<admin>>
        -ListOrdersUseCase listOrders
        -GetAdminOrderDetailsUseCase getAdminOrderDetails
        -GetDashboardUseCase getDashboard
        +ListOrdersAsync(ListOrdersFilter filter) Task~ActionResult~PagedResponse~AdminOrderSummaryResponse~~~
        +GetOrderAsync(Guid id) Task~ActionResult~AdminOrderDetailsResponse~~
        +GetDashboardAsync(DateTimeOffset? from, DateTimeOffset? to) Task~ActionResult~DashboardResponse~~
    }

    class SetOrderInternalNotesRequest {
        +string? Notes
    }

    class CancelOrderResponse {
        +string PaymentSettlement
    }

    class AdminOrderSummaryResponse {
        +Guid Id
        +string OrderNumber
        +string Status
        +decimal TotalAmount
        +OrderCustomerResponse? Customer
        +string? PaymentStatus
    }

    class AdminOrderDetailsResponse {
        +OrderResponse Order
        +string? InternalNotes
        +OrderCustomerResponse? Customer
        +OrderPaymentDetailsResponse? Payment
        +IReadOnlyList~OrderReservationResponse~ Reservations
    }

    class DashboardResponse {
        +DateTimeOffset From
        +DateTimeOffset To
        +IReadOnlyDictionary~string, int~ OrdersByStatus
        +IReadOnlyDictionary~string, decimal~ RevenueByCurrency
        +int NewCustomers
        +DashboardStockResponse Stock
        +IReadOnlyList~AdminOrderSummaryResponse~ RecentOrders
    }

    class AdminOrderPresenter {
        <<static>>
        +ToResponse(AdminOrderSummaryOutput output)$ AdminOrderSummaryResponse
        +ToResponse(AdminOrderDetailsOutput output)$ AdminOrderDetailsResponse
        +ToResponse(DashboardOutput output)$ DashboardResponse
    }

    class CheckoutItemRequest {
        +Guid ProductId
        +int Quantity
    }

    class CheckoutRequest {
        +IReadOnlyList~CheckoutItemRequest~ Items
        +Guid ShippingAddressId
        +Guid BillingAddressId
        +PaymentMethodChoice? PaymentMethod
        +string? CustomerNotes
        +decimal? ExpectedTotal
    }

    class QuoteCartLineRequest {
        +Guid ProductId
        +int Quantity
        +decimal? ExpectedUnitPrice
    }

    class QuoteCartRequest {
        +IReadOnlyList~QuoteCartLineRequest~ Items
    }

    class RequestOrderPaymentRequest {
        +PaymentMethodChoice? PaymentMethod
    }

    class CreateOrderItemRequest {
        +Guid ProductId
        +int Quantity
    }

    class CreateOrderRequest {
        +Guid CustomerId
        +string Currency
        +IReadOnlyCollection~CreateOrderItemRequest~ Items
    }

    class SetOrderAddressesRequest {
        +string ShippingStreet
        +string ShippingCity
        +string BillingStreet
        +string BillingCity
    }

    class CancelOrderRequest {
        +string Reason
    }

    class OrderItemResponse {
        +Guid ProductId
        +string ProductSku
        +string ProductName
        +string? ProductImageUrl
        +decimal UnitPrice
        +int Quantity
        +decimal Total
    }

    class OrderAddressResponse {
        +string Street
        +string Number
        +string? Complement
        +string Neighborhood
        +string City
        +string State
        +string PostalCode
        +string Country
    }

    class OrderPaymentResponse {
        +Guid PaymentId
        +string Status
        +string Method
        +string? FailureReason
    }

    class OrderResponse {
        +Guid Id
        +string OrderNumber
        +string Status
        +DateTimeOffset CreatedAt
        +DateTimeOffset? ConfirmedAt
        +DateTimeOffset? CancelledAt
        +DateTimeOffset? ShippedAt
        +DateTimeOffset? DeliveredAt
        +decimal SubtotalAmount
        +decimal DiscountAmount
        +decimal ShippingAmount
        +decimal TaxAmount
        +decimal TotalAmount
        +string Currency
        +OrderAddressResponse? ShippingAddress
        +OrderAddressResponse? BillingAddress
        +string? CustomerNotes
        +IReadOnlyList~OrderItemResponse~ Items
        +OrderPaymentResponse? Payment
    }

    class OrderSummaryResponse {
        +Guid Id
        +string OrderNumber
        +string Status
        +DateTimeOffset CreatedAt
        +decimal TotalAmount
        +string Currency
        +int ItemCount
    }

    class OrderStatusHistoryEntryResponse {
        +string? FromStatus
        +string ToStatus
        +string? Reason
        +DateTimeOffset ChangedAt
    }

    class CartQuoteLineResponse {
        +Guid ProductId
        +string? ProductName
        +string? Slug
        +string? ImageUrl
        +decimal? UnitPrice
        +int Quantity
        +decimal LineTotal
        +string? Issue
        +decimal? PreviousUnitPrice
    }

    class CartQuoteResponse {
        +string? Currency
        +decimal Total
        +bool IsValid
        +IReadOnlyList~CartQuoteLineResponse~ Lines
    }

    class OrderPresenter {
        +ToCommand(CheckoutRequest request, Guid customerId, string idempotencyKey) CheckoutCommand
        +ToLines(QuoteCartRequest request) IReadOnlyList~QuoteCartLine~
        +ToResponse(OrderDetailsOutput details) OrderResponse
        +ToResponse(Order order) OrderResponse
        +ToResponse(PagedResult~OrderSummaryOutput~ output) PagedResponse~OrderSummaryResponse~
        +ToResponse(IReadOnlyList~OrderStatusHistoryEntry~ history) IReadOnlyList~OrderStatusHistoryEntryResponse~
        +ToResponse(CartQuote quote) CartQuoteResponse
    }


    AggregateRoot~TId~ <|-- Order
    Order "1" *-- "1..*" OrderItem
    Order --> OrderStatus
    Order --> Address : ShippingAddress/BillingAddress
    Order ..> OrderCreated : raises
    Order ..> OrderPaymentRequested : raises
    Order ..> OrderConfirmed : raises
    Order ..> OrderCancelled : raises
    Order ..> OrderPaymentFailed : raises

    CreateOrderHandler --> IOrderRepository
    CreateOrderHandler --> IProductCatalog
    CreateOrderHandler --> IOrderNumberGenerator
    CreateOrderHandler ..> Order : creates
    SetOrderAddressesUseCase --> IOrderRepository
    RequestOrderPaymentUseCase --> IOrderRepository
    RequestOrderPaymentUseCase --> IInventoryService
    RequestOrderPaymentUseCase --> IPaymentGateway
    ConfirmOrderUseCase --> IOrderRepository
    ConfirmOrderUseCase --> IInventoryService
    MarkOrderPaymentFailedUseCase --> IOrderRepository
    MarkOrderPaymentFailedUseCase --> IInventoryService
    CancelOrderUseCase --> IOrderRepository
    CancelOrderUseCase --> IInventoryService : release + return stock
    CancelOrderUseCase --> IPaymentGateway : settle payment first
    FulfilOrderUseCase --> IOrderRepository
    FulfilOrderUseCase --> IPaymentGateway : capture on ship
    SetOrderInternalNotesUseCase --> IOrderRepository
    ListOrdersUseCase --> IOrderRepository
    ListOrdersUseCase --> ICustomerDirectory
    ListOrdersUseCase --> IPaymentGateway
    GetAdminOrderDetailsUseCase --> IOrderRepository
    GetAdminOrderDetailsUseCase --> ICustomerDirectory
    GetAdminOrderDetailsUseCase --> IPaymentGateway
    GetAdminOrderDetailsUseCase --> IInventoryService
    GetDashboardUseCase --> IOrderRepository
    GetDashboardUseCase --> ICustomerDirectory
    GetDashboardUseCase --> IInventoryService
    GetDashboardUseCase --> ListOrdersUseCase : recent orders
    Order ..> OrderProcessingStarted : raises
    Order ..> OrderShipped : raises
    Order ..> OrderDelivered : raises
    GetOrderByIdUseCase --> IOrderRepository
    ListCustomerOrdersUseCase --> IOrderRepository
    CreateOrderCommand "1" *-- "1..*" CreateOrderItem
    CheckoutUseCase --> IOrderRepository
    CheckoutUseCase --> IProductCatalog
    CheckoutUseCase --> ICustomerDirectory
    CheckoutUseCase --> IInventoryService
    CheckoutUseCase --> IPaymentGateway
    CheckoutUseCase --> IOrderNumberGenerator
    CheckoutUseCase ..> Order : creates
    CheckoutCommand "1" *-- "1..*" CheckoutItem
    QuoteCartUseCase --> IProductCatalog
    QuoteCartUseCase --> IInventoryService
    QuoteCartUseCase ..> CartQuote : returns
    CartQuote "1" *-- "*" CartQuoteLine
    CartQuoteLine --> CartLineIssue
    GetOrderDetailsUseCase --> IOrderRepository
    GetOrderDetailsUseCase --> IPaymentGateway
    GetOrderDetailsUseCase ..> OrderDetailsOutput : returns
    OrderDetailsOutput --> OrderPaymentSummary
    OrderPaymentSummary --> PaymentMethodChoice
    GetOrderStatusHistoryUseCase --> IOrderRepository
    GetOrderStatusHistoryUseCase --> IOrderStatusHistoryReader
    GetOrderDetailsUseCase ..> OrderAccess : owner or admin only
    GetOrderStatusHistoryUseCase ..> OrderAccess : owner or admin only
    IProductCatalog ..> CatalogProductSnapshot : returns

    IOrderRepository <|.. EfOrderRepository
    EfOrderRepository --> OrdersDbContext
    EfOrderRepository --> OrderMapper
    EfOrderRepository --> IDomainEventDispatcher : dispatches after save
    IOrderNumberGenerator <|.. SequentialOrderNumberGenerator
    SequentialOrderNumberGenerator --> OrdersDbContext
    OrderMapper --> OrderPersistenceModel
    OrderMapper --> Order
    OrdersDbContext --> OrderPersistenceModel
    OrdersDbContext --> OrderStatusHistoryPersistenceModel

    IProductCatalog <|.. ProductCatalogAdapter
    ProductCatalogAdapter --> IProductRepository : reads Catalog module
    IInventoryService <|.. InventoryServiceAdapter
    InventoryServiceAdapter --> ReserveStockUseCase
    InventoryServiceAdapter --> ReleaseReservationUseCase
    InventoryServiceAdapter --> ConsumeReservationUseCase
    InventoryServiceAdapter --> IInventoryReservationRepository
    InventoryServiceAdapter --> GetStockAvailabilityUseCase
    InventoryServiceAdapter --> ReturnOrderStockUseCase
    InventoryServiceAdapter --> ListReservationsUseCase
    InventoryServiceAdapter --> GetStockSummaryUseCase
    IPaymentGateway <|.. PaymentGatewayAdapter
    PaymentGatewayAdapter --> CreatePaymentUseCase
    PaymentGatewayAdapter --> GetPaymentByOrderIdUseCase
    PaymentGatewayAdapter --> GetPaymentsByOrderIdsUseCase
    PaymentGatewayAdapter --> CapturePaymentUseCase
    PaymentGatewayAdapter --> SettlePaymentForCancellationUseCase
    ICustomerDirectory <|.. CustomerDirectoryAdapter
    CustomerDirectoryAdapter --> GetCustomerAddressUseCase
    CustomerDirectoryAdapter --> GetCustomersByIdsUseCase
    CustomerDirectoryAdapter --> CountNewCustomersUseCase
    IOrderStatusHistoryReader <|.. EfOrderStatusHistoryReader
    EfOrderStatusHistoryReader --> OrdersDbContext

    OrderStatusHistoryProjector --> OrdersDbContext
    PaymentAuthorizedIntegrationEventHandler --> ConfirmOrderUseCase
    PaymentFailedIntegrationEventHandler --> MarkOrderPaymentFailedUseCase

    OrdersDependencyInjection --> CreateOrderHandler : registers
    OrdersDependencyInjection --> IOrderRepository : registers
    OrdersDependencyInjection --> IOrderNumberGenerator : registers

    OrdersController --> CreateOrderHandler
    OrdersController --> SetOrderAddressesUseCase
    OrdersController --> RequestOrderPaymentUseCase
    OrdersController --> GetOrderByIdUseCase
    OrdersController --> ListCustomerOrdersUseCase
    OrderFulfilmentController --> FulfilOrderUseCase
    OrderFulfilmentController --> CancelOrderUseCase
    OrderFulfilmentController --> SetOrderInternalNotesUseCase
    OrdersAdminController --> ListOrdersUseCase
    OrdersAdminController --> GetAdminOrderDetailsUseCase
    OrdersAdminController --> GetDashboardUseCase
    OrdersAdminController --> AdminOrderPresenter
    OrdersController --> CheckoutUseCase
    OrdersController --> QuoteCartUseCase
    OrdersController --> GetOrderDetailsUseCase
    OrdersController --> GetOrderStatusHistoryUseCase
    OrdersController --> ICurrentUser : customer from the token
    OrdersController --> OrderPresenter
    OrderPresenter --> OrderResponse
    OrderPresenter --> OrderSummaryResponse
    OrderPresenter --> CartQuoteResponse
    OrderPresenter --> OrderStatusHistoryEntryResponse
    OrderResponse --> OrderAddressResponse
    OrderResponse --> OrderPaymentResponse
    CartQuoteResponse "1" *-- "*" CartQuoteLineResponse

```

## Comportamento de OrderItem

`OrderItem` ganhou `DecreaseQuantity` (simétrico ao `IncreaseQuantity` já existente — sem ele, reduzir a quantidade de uma linha exigia remover e recriar o item inteiro) e `ApplyDiscount(decimal amount)`, que deve validar `0 <= amount <= UnitPrice * Quantity` antes de aceitar o desconto — a propriedade `DiscountAmount` só deve mudar através desse método, nunca setada diretamente. `Total` passa a ser `UnitPrice * Quantity - DiscountAmount`.

## Paridade com `Docs/database/OrderCore_Modelagem_Banco_Backend.docx`

O documento de modelagem de banco já especificava `internal_notes` e `updated_at` em `ORDERS` (seção 6.1) e `product_variant_id` em `ORDER_ITEMS` (seção 6.2), mas nenhum tinha chegado a este diagrama. Adicionados agora: `InternalNotes` ganhou `SetInternalNotes` como único jeito de alterá-lo (mesmo padrão de encapsulamento do resto do agregado); `UpdatedAt` fica por conta da camada de persistência a cada gravação, sem entrar na assinatura dos métodos de transição; `ProductVariantId` foi ao mesmo tempo adicionado em `OrderItem` e no parâmetro de `Order.AddItem`, já que é o único lugar onde um `OrderItem` é construído.

`OrderNumber`, `ShippedAt` e `DeliveredAt` não existiam em nenhum dos dois documentos — acrescentados em ambos agora. `Id` (Guid) não é algo que se mostre a um cliente numa confirmação de pedido; `OrderNumber` é o identificador legível (ex.: `"ORD-2024-000123"`), gerado por `IOrderNumberGenerator` (novo contrato, com `SequentialOrderNumberGenerator` como implementação apoiada numa sequence do PostgreSQL) e passado para `Order.Create`. `ShippedAt`/`DeliveredAt` seguem o mesmo raciocínio de `ConfirmedAt`/`CancelledAt`: são os dois status mais relevantes para uma tela de acompanhamento do cliente, então ganham timestamp próprio em vez de depender só do histórico (`ORDER_STATUS_HISTORY`); `Ship`/`Deliver` já recebiam `now`, então nenhuma assinatura precisou mudar.

## Fluxo de checkout (leitura sugerida)

`CreateOrderHandler` → `RequestOrderPaymentUseCase` (reserva estoque via `InventoryServiceAdapter`, depois solicita pagamento via `PaymentGatewayAdapter`) → `PaymentAuthorizedIntegrationEventHandler`/`PaymentFailedIntegrationEventHandler` (recebem o resultado assíncrono do módulo Payments — ver [06-payments.md](06-payments.md) — e chamam `ConfirmOrderUseCase`/`MarkOrderPaymentFailedUseCase`).

## Fluxo do storefront

O caminho que o front usa, em vez de encadear os passos acima:

1. `POST orders/cart/quote` → `QuoteCartUseCase` reprecifica o carrinho (sem reservar nada).
2. `POST orders/checkout` (access token do cliente, header `Idempotency-Key`) → `CheckoutUseCase`: endereços (`CustomerDirectoryAdapter`) → produtos (`ProductCatalogAdapter.GetManyAsync`) → reserva (`InventoryServiceAdapter`) → salva o pedido já `PendingPayment` → `PaymentGatewayAdapter.RequestPaymentAsync`. Responde 202 com o pedido.
3. O outbox de Payments publica `PaymentAuthorized`/`PaymentFailed` (a cada 5 s) → os mesmos integration event handlers confirmam ou falham o pedido.
4. O front faz polling de `GET orders/{id}` (`GetOrderDetailsUseCase`, com o resumo do pagamento) e desenha a timeline com `GET orders/{id}/status-history`; o histórico do cliente vem de `GET orders/me`.
