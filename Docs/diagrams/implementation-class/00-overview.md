# Visão geral dos módulos

Diagrama leve, só com a direção de dependência entre módulos (via Application Contracts, nunca acessando Domain/Infrastructure alheios). Serve como mapa para navegar até o diagrama detalhado de cada módulo.

```mermaid
graph LR
    Customers["Customers module\n(Domain · Application · Infrastructure · Presentation)"]
    Catalog["Catalog module\n(Domain · Application · Infrastructure · Presentation)"]
    Inventory["Inventory module\n(Domain · Application · Infrastructure · Presentation)"]
    Orders["Orders module\n(Domain · Application · Infrastructure · Presentation)"]
    Payments["Payments module\n(Domain · Application · Infrastructure · Presentation)"]
    AuditLogs["AuditLogs module\n(technical/cross-cutting, not a business bounded context)"]
    Shared["Shared kernel\n(Entity, AggregateRoot, IDomainEvent, Address, Slug, PagedResult, PagedResponse,\nexceções tipadas + ApiExceptionHandler, CORS)"]

    Orders -->|IProductCatalog| Catalog
    Orders -->|IInventoryService| Inventory
    Orders -->|IPaymentGateway| Payments
    Orders -->|"ICustomerDirectory (endereços no checkout)"| Customers
    Catalog -->|"IStockAvailabilityProvider (disponibilidade)"| Inventory
    Payments -.->|IntegrationEvents via Outbox| Orders
    Orders -.->|IAuditLogService| AuditLogs
    Payments -.->|IAuditLogService| AuditLogs
    Inventory -.->|IAuditLogService| AuditLogs
    Catalog -.->|IAuditLogService| AuditLogs
    Customers -.->|IAuditLogService| AuditLogs

    Customers --> Shared
    Catalog --> Shared
    Inventory --> Shared
    Orders --> Shared
    Payments --> Shared
    AuditLogs --> Shared
```

## Diagramas detalhados (um por módulo, cada um pequeno o suficiente para renderizar)

1. [Shared kernel](01-shared-kernel.md) — `Entity`, `AggregateRoot`, `IDomainEvent`, value objects, dispatcher de eventos, contrato de erros (exceções tipadas → `ProblemDetails`), CORS.
2. [Customers](02-customers.md) — cliente, endereços, métodos de pagamento salvos.
3. [Catalog](03-catalog.md) — categorias, produtos, imagens, variações, listagem da vitrine e produto por slug.
4. [Inventory](04-inventory.md) — saldo de estoque, reservas e consulta de disponibilidade.
5. [Orders](05-orders.md) — pedido, itens, ciclo de vida, checkout em um passo, cotação do carrinho, adapters para os outros módulos.
6. [Payments](06-payments.md) — pagamento (com forma de pagamento), estornos, outbox de eventos de integração.
7. [AuditLogs](07-auditlogs.md) — registro de ações via `IAuditLogService`, listagem paginada.

Todos os diagramas refletem código já implementado; cada um lista, no topo, onde o código difere do desenho original e o que foi acrescentado depois (ex.: o MVP do storefront, `Docs/specs/storefront/storefront-api-mvp.md`).

Cada arquivo é autocontido: quando um módulo depende de outro (ex.: Orders → Catalog), a classe externa aparece como um "stub" marcado `<<external>>`, só com a assinatura que importa para aquele módulo — o detalhe completo dela está no arquivo do módulo dono.
