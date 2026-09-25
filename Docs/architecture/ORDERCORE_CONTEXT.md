# OrderCore — Contexto e Diretrizes do Projeto

## 1. Visão geral

O **OrderCore** é um sistema de processamento e gerenciamento de pedidos desenvolvido em **C# / .NET**, com foco em demonstrar arquitetura de software, modelagem de domínio, modularidade, consistência, concorrência, resiliência e preparação para evolução arquitetural.

O projeto não deve ser tratado como um simples CRUD de pedidos.

O objetivo é construir uma aplicação que represente um cenário real de processamento de pedidos, incluindo:

* clientes;
* catálogo de produtos;
* estoque;
* pedidos;
* itens do pedido;
* reserva de estoque;
* processamento de pagamento;
* estados do pedido;
* eventos de domínio;
* eventos de integração;
* idempotência;
* concorrência;
* processamento assíncrono;
* observabilidade;
* auditoria;
* resiliência;
* testes de integração;
* testes de arquitetura;
* preparação para futura distribuição em microservices.

O projeto será inicialmente implementado como um **Modular Monolith**.

A arquitetura deve permitir que determinados módulos, especialmente o contexto de pagamentos, possam futuramente ser extraídos para um serviço independente denominado **PayCore**.

---

# 2. Objetivo arquitetural

O principal objetivo do projeto é demonstrar capacidade de tomar decisões arquiteturais justificadas.

O projeto deve responder, por meio de código e documentação, perguntas como:

* Por que utilizar Modular Monolith?
* Quais são os bounded contexts existentes?
* Quem é responsável por cada regra?
* Onde está cada regra de negócio?
* Como evitar acoplamento entre módulos?
* Como tratar concorrência?
* Como garantir consistência?
* Como lidar com operações duplicadas?
* Como lidar com falhas?
* Como processar operações demoradas de forma assíncrona?
* Como publicar eventos de forma confiável?
* Como observar o comportamento do sistema?
* Quando uma funcionalidade deve permanecer dentro do monólito?
* Quando um bounded context poderia ser extraído para um microservice?

O objetivo NÃO é adicionar tecnologias apenas para aumentar a quantidade de tecnologias utilizadas.

Toda decisão técnica relevante deve possuir uma justificativa.

---

# 3. Estratégia arquitetural

A evolução arquitetural planejada é:

```text
Fase 1
Modular Monolith
        ↓
Fase 2
Bounded Contexts bem definidos
        ↓
Fase 3
Eventos e comunicação assíncrona
        ↓
Fase 4
Transactional Outbox
        ↓
Fase 5
Idempotent Consumers
        ↓
Fase 6
Resiliência e observabilidade
        ↓
Fase 7
Extração opcional do contexto de pagamentos
        ↓
PayCore como serviço independente
```

A extração para microservices NÃO deve ser feita artificialmente.

Primeiro o sistema deve possuir fronteiras de domínio bem definidas.

Somente depois deve ser avaliada a necessidade de distribuição.

---

# 4. Stack principal

Utilizar:

* C#
* .NET 10 / ASP.NET Core — mesma versão de framework usada pelo CourseCore
* Entity Framework Core
* PostgreSQL
* Redis quando houver necessidade real
* RabbitMQ para mensageria
* Docker / Docker Compose
* OpenTelemetry
* xUnit
* Testcontainers
* FluentValidation ou mecanismo equivalente de validação
* Polly para resiliência quando necessário

Ferramentas adicionais podem ser introduzidas posteriormente quando houver justificativa arquitetural.

Não adicionar tecnologias apenas para demonstrar conhecimento.

---

# 5. Estrutura da solução

O OrderCore segue **exatamente a mesma estrutura usada pelo CourseCore**:
um único projeto na raiz do repositório (`OrderCore.Api.csproj`, o
equivalente ao `CourseCore.csproj`), sem uma pasta `src/` intermediária, e
uma pasta `Modules/` também na raiz, com uma pasta por módulo de negócio
dentro dela (`Orders`, `Customers`, `Catalog`, `Inventory`, `Payments`).
Cada módulo possui, como filhos diretos, `Application/`, `Domain/`,
`Infrastructure/` e `Presentation/` — o mesmo layout de
`Modules/Courses/{Application,Domain,Infrastructure,Presentation}` no
CourseCore.

Não existem mais assemblies separadas por camada (`OrderCore.Domain`,
`OrderCore.Application`, `OrderCore.Infrastructure`, `OrderCore.Contracts`).
As fronteiras entre camadas (Domain não depende de Infrastructure, Domain
não depende de ASP.NET Core/EF Core, Application não depende de
Infrastructure) e entre módulos deixam de ser garantidas por referência de
projeto e passam a ser validadas por convenção de namespace, checada
automaticamente por `OrderCore.ArchitectureTests` via NetArchTest (seção
33) — a mesma abordagem que o CourseCore usa, já que ele também é um
projeto único.

Estrutura esperada:

```text
OrderCore/
│
├── Modules/
│   ├── Orders/
│   │   ├── Application/{Contracts,DTOs,Services,UseCases,Validation}
│   │   ├── Domain/{Entities,Enums,Events,Exceptions,Policies,Repositories,ValueObjects}
│   │   ├── Infrastructure/Persistence/{Configurations,Mappers,Models,Repositories}
│   │   ├── Presentation/{Controllers,Presenters,Requests,Responses}
│   │   └── OrdersDependencyInjection.cs
│   │
│   ├── Customers/{Application,Domain,Infrastructure,Presentation}/...
│   ├── Catalog/{Application,Domain,Infrastructure,Presentation}/...
│   ├── Inventory/{Application,Domain,Infrastructure,Presentation}/...
│   │
│   └── Payments/
│       ├── Application/{Contracts,DTOs,Services,UseCases,Validation}
│       │   └── Contracts/IntegrationEvents/   ← ver seção 19
│       ├── Domain/{Entities,Enums,Events,Exceptions,Policies,Repositories,ValueObjects}
│       ├── Infrastructure/
│       │   ├── Persistence/{Configurations,Mappers,Models,Repositories}
│       │   └── Providers/{Fake,Stripe}   ← ver seção 14/15
│       ├── Presentation/{Controllers,Presenters,Requests,Responses}
│       └── PaymentsDependencyInjection.cs
│
├── Shared/
│   └── Domain/    ← shared kernel (AggregateRoot, Entity, IDomainEvent)
│
├── Program.cs
├── OrderCore.Api.csproj
├── Dockerfile
│
├── Tests/
│   │
│   ├── OrderCore.UnitTests/
│   │   ├── Orders/
│   │   ├── Customers/
│   │   ├── Catalog/
│   │   ├── Inventory/
│   │   └── Payments/
│   │
│   ├── OrderCore.IntegrationTests/
│   │   ├── Orders/
│   │   ├── Customers/
│   │   ├── Catalog/
│   │   ├── Inventory/
│   │   └── Payments/
│   │
│   └── OrderCore.ArchitectureTests/
│
├── Docs/
│   ├── architecture/
│   ├── adr/
│   ├── diagrams/
│   └── api/
│
├── docker-compose.yml
├── README.md
└── OrderCore.sln
```

Seguindo o padrão adotado no CourseCore, os testes continuam organizados
**por módulo de negócio** (`Orders`, `Customers`, `Catalog`, `Inventory`,
`Payments`), e não por camada técnica — isso é tratado em detalhe na
seção 33.

`OrderCore.ArchitectureTests` é a exceção: por validar regras estruturais
entre módulos e camadas, permanece organizado por regra/convenção, não por
módulo.

A estrutura deve ser adaptada conforme o projeto evoluir, mas sem abandonar
os princípios de separação de responsabilidades.

## 5.1. Estrutura interna de um módulo

Cada módulo de negócio replica a mesma subestrutura usada pelo módulo
`Courses` do CourseCore. O namespace espelha o caminho de pastas
(`OrderCore.Api.Modules.{Módulo}.{Camada}.{Subpasta}`), exatamente como o
CourseCore faz com `CourseCore.Api.Modules.Courses.{Camada}.{Subpasta}`.

```text
Modules/{Módulo}/Domain/
├── Entities/       ← aggregate roots e entidades (Order, Payment, Product...)
├── Enums/          ← enums de domínio (OrderStatus, PaymentStatus...)
├── Events/         ← domain events (seção 18)
├── Exceptions/     ← exceções de domínio específicas, quando justificadas
├── Policies/        ← regras/policies de domínio que não pertencem a uma única entidade
├── Repositories/    ← abstrações que o domínio expõe (ex.: IPaymentProvider)
└── ValueObjects/     ← value objects, quando existirem

Modules/{Módulo}/Application/
├── Contracts/       ← abstrações que a Application depende (ex.: IOrderRepository, IProductCatalog)
├── DTOs/            ← inputs/outputs de casos de uso (ex.: CreateOrderCommand, CreateOrderResult)
├── Services/         ← serviços de aplicação compartilhados dentro do módulo
├── UseCases/         ← um caso de uso por classe (ex.: CreateOrderHandler)
└── Validation/        ← validação de input (FluentValidation ou equivalente)

Modules/{Módulo}/Infrastructure/
└── Persistence/
    ├── Configurations/  ← EF Core IEntityTypeConfiguration
    ├── Mappers/          ← domain <-> persistence model
    ├── Models/            ← modelos de persistência (quando não expor a entidade de domínio direto)
    └── Repositories/       ← implementações concretas (ex.: EfOrderRepository)

Modules/{Módulo}/Presentation/
├── Controllers/
├── Presenters/
├── Requests/
└── Responses/
```

O módulo `Payments`, além dessa estrutura, mantém
`Modules/Payments/Infrastructure/Providers/{Fake,Stripe}` (seção 14/15) —
uma pasta adicional que não existe no `Courses` do CourseCore porque
nenhum módulo daquele projeto depende de um provider externo da forma que
Payments depende de um payment provider. É uma extensão justificada da
estrutura de referência, não um desvio arbitrário (seção 38). Pelo mesmo
motivo, os contratos de integration events (seção 19) vivem em
`Modules/Payments/Application/Contracts/IntegrationEvents/`.

Pastas ainda sem conteúdo (por exemplo `ValueObjects/` antes de existir um
value object real) permanecem vazias com um `.gitkeep`, prontas para quando
a necessidade surgir — não são criadas classes apenas para preencher a
pasta (seção 38).

Cada módulo expõe um extension method de `IServiceCollection` para o
próprio registro de dependências, análogo a `CoursesDependencyInjection.cs`
no CourseCore — por exemplo `OrdersDependencyInjection.AddOrdersModule()`
e `PaymentsDependencyInjection.AddPaymentsModule()`, compostos em
`Program.cs`. Isso evita que o `Program.cs` cresça com o registro
individual de cada serviço de cada módulo à medida que o sistema evolui.

---

# 6. Modularização

O projeto deve ser organizado principalmente por **módulos de negócio**, e não simplesmente por tipos técnicos globais.

Os principais módulos inicialmente previstos são:

```text
OrderCore
│
├── Orders
├── Customers
├── Catalog
└── Inventory
└── Payments
```

O módulo `Payments` é especialmente importante.

Ele deve ser desenvolvido desde o início como um **bounded context isolado logicamente**, mesmo estando dentro do mesmo processo inicialmente.

A intenção é possibilitar sua futura extração para o projeto independente:

```text
PayCore
```

Além dos módulos de negócio acima, existe um módulo técnico/transversal,
`AuditLogs`, espelhando o mesmo módulo do CourseCore (registro de ações
como `OrderCreated`, `PaymentAuthorized`, etc. via `IAuditLogService`).
Ele não é um bounded context de negócio como Orders/Payments — é
infraestrutura de observabilidade (seção 30) consumida pelos demais
módulos através de uma Application Contract, seguindo a mesma estrutura
`Modules/AuditLogs/{Application,Domain,Infrastructure,Presentation}` da
seção 5.1. Desde o backoffice ele é persistido no PostgreSQL (seção 30).

Um segundo módulo técnico, `Identity`, guarda contas, senhas e sessões
(clientes e administradores) e emite e valida os tokens JWT; veja a
seção 32. Também não é um bounded context de negócio: o `Customer`
continua sendo do módulo Customers, e o `Identity` só guarda o id dele.

---

# 7. Regra fundamental de modularização

Um módulo não deve acessar diretamente as estruturas internas de outro módulo.

Evitar:

```text
Orders → Payments.Entities
Orders → Payments.Repositories
Orders → Payments.DbContext
```

Preferir:

```text
Orders
   ↓
Application Contract
   ↓
Payment Module
```

ou eventos:

```text
Orders
   ↓
Domain / Integration Event
   ↓
Payments
```

O objetivo é garantir que a futura extração do módulo seja possível sem reescrever todo o sistema.

Na prática (seção 5.1), isso significa: `Orders` nunca importa
`OrderCore.Api.Modules.Payments.Domain.*` ou
`OrderCore.Api.Modules.Payments.Infrastructure.*` diretamente. Quando
`Orders` precisa de algo do módulo `Catalog`, por exemplo, a dependência é
uma interface em `Modules/Orders/Application/Contracts/` (ex.:
`IProductCatalog`), nunca uma referência direta a
`Modules/Catalog/Domain/Entities/Product.cs`. Os contratos falam só os
tipos do próprio módulo consumidor (ex.: `IProductCatalog` devolve um
`CatalogProductSnapshot` do Orders, não a entidade `Product` do Catalog);
a tradução entre os tipos de um módulo e do outro acontece no adapter,
em `Infrastructure/Adapters/`, que chama a camada Application do módulo
dono. `OrderCore.ArchitectureTests` valida isso sobre os namespaces
`OrderCore.Api.Modules.Orders.Domain` e
`OrderCore.Api.Modules.Orders.Application` (seção 33).

Contratos entre módulos existentes hoje:

| Consumidor → dono | Contrato (no consumidor) | Adapter → o que chama no dono |
|---|---|---|
| Orders → Catalog | `IProductCatalog` | `ProductCatalogAdapter` → `IProductRepository` |
| Orders → Inventory | `IInventoryService` | `InventoryServiceAdapter` → `Reserve`/`Release`/`ConsumeReservationUseCase`, `GetStockAvailabilityUseCase`, `ReturnOrderStockUseCase`, `ListReservationsUseCase`, `GetStockSummaryUseCase` |
| Orders → Payments | `IPaymentGateway` | `PaymentGatewayAdapter` → `CreatePaymentUseCase`, `GetPaymentByOrderIdUseCase`, `GetPaymentsByOrderIdsUseCase`, `CapturePaymentUseCase` (por pedido), `SettlePaymentForCancellationUseCase` |
| Orders → Customers | `ICustomerDirectory` | `CustomerDirectoryAdapter` → `GetCustomerAddressUseCase`, `GetCustomersByIdsUseCase`, `CountNewCustomersUseCase` |
| Catalog → Inventory | `IStockAvailabilityProvider` (vitrine) e `IStockLevels` (backoffice) | `InventoryStockAvailabilityAdapter` → `GetStockAvailabilityUseCase`, `EnsureStockItemUseCase`, `GetStockLevelsUseCase`, `ListProductIdsInStockStateUseCase` |
| Identity → Customers | `ICustomerRegistry` | `CustomerRegistryAdapter` → `RegisterCustomerUseCase`, `GetCustomerByIdUseCase` (cliente ativo?) |
| Payments → Orders | eventos de integração via outbox | `PaymentAuthorized`/`PaymentFailed` → handlers do Orders |

Todas as dependências seguem uma só direção (Orders → Catalog/Inventory/
Payments/Customers, Catalog → Inventory, Identity → Customers): quando o
backoffice precisou de uma tela que juntasse dados de dois módulos na
direção contrária, a composição foi feita no módulo que já dependia do
outro (a tela de estoque é a lista de produtos do Catalog com os números
do Inventory) ou no front (o detalhe do cliente com os pedidos), nunca
com um contrato de volta.

---

# 8. Domain Layer

O Domain deve conter exclusivamente regras e conceitos de negócio.

Não deve depender diretamente de:

* ASP.NET Core;
* Entity Framework Core;
* RabbitMQ;
* Redis;
* HttpClient;
* Stripe SDK;
* APIs externas.

Organizado por módulo, com a subestrutura da seção 5.1 (a pasta `Domain/`
de cada módulo, sob `Modules/{Módulo}/Domain/`):

```text
Shared/Domain
├── AggregateRoot
├── Entity
└── IDomainEvent

Modules
│
├── Orders/Domain
│   ├── Entities/       (Order, OrderItem)
│   ├── Enums/          (OrderStatus)
│   └── Events/         (OrderCreated, OrderConfirmed, ...)
│
├── Inventory/Domain
│   ├── Entities/       (InventoryReservation)
│   └── Enums/          (ReservationStatus)
│
├── Catalog/Domain
│   └── Entities/       (Product)
│
├── Customers/Domain
│   └── Entities/       (Customer)
│
└── Payments/Domain
    ├── Entities/       (Payment)
    ├── Enums/          (PaymentStatus)
    └── Repositories/   (IPaymentProvider — abstração exposta pelo domínio)
```

As entidades devem proteger suas próprias invariantes.

Evitar entidades anêmicas com propriedades públicas sendo alteradas indiscriminadamente:

```csharp
order.Status = OrderStatus.Confirmed;
```

Preferir comportamento de domínio:

```csharp
order.Confirm();
order.Cancel();
order.AddItem(...);
order.RemoveItem(...);
```

A mudança de estado deve respeitar as regras do domínio.

---

# 9. Aggregate principal: Order

`Order` será o principal aggregate do sistema.

Conceitualmente:

```text
Order
├── Id
├── CustomerId
├── Status
├── Items
├── TotalAmount
├── Currency
├── CreatedAt
├── ConfirmedAt
├── CancelledAt
└── Version
```

`OrderItem` representa os produtos comprados:

```text
OrderItem
├── ProductId
├── ProductName
├── UnitPrice
├── Quantity
└── Total
```

O preço praticado deve ser armazenado no item do pedido.

O pedido não deve depender do preço atual do produto para recalcular pedidos históricos.

Exemplo:

```text
Product
CurrentPrice = 150.00

OrderItem
UnitPrice = 100.00
```

O pedido deve continuar representando a transação original.

---

# 10. Estado do pedido

O pedido deverá possuir uma máquina de estados explícita.

Estado inicial:

```text
Created
```

Depois:

```text
Created
   ↓
PendingPayment
   ↓
Confirmed
   ↓
Processing
   ↓
Shipped
   ↓
Delivered
```

Também devem existir caminhos de falha/cancelamento:

```text
PendingPayment
   ↓
PaymentFailed
```

ou:

```text
PendingPayment
   ↓
Cancelled
```

Desde o backoffice (`Docs/specs/backoffice/backoffice-api.md`), toda a
máquina de estados é alcançável pela API: o checkout leva a
`PendingPayment`, o outbox confirma (`Confirmed`) ou falha
(`PaymentFailed`), e o admin move o pedido por `Processing` → `Shipped` →
`Delivered` (`OrderFulfilmentController`). Enviar **captura o pagamento
antes** de mudar o estado; se o provedor recusar, o pedido continua em
`Processing`. O admin pode cancelar qualquer pedido ainda não enviado
(`Created`, `PendingPayment`, `Confirmed`, `Processing`), e o próprio
cliente pode cancelar o seu enquanto a loja não começou a prepará-lo
(antes de `Processing`, `orders/me/{id}/cancel`); o cancelamento
**acerta o pagamento** (libera uma autorização ou estorna uma captura) e
devolve o estoque, cada passo repetível sem efeito duplicado, já que não
há transação entre módulos (seções 26 e 27). Cada transição fica no
histórico de status (`order_status_history`).

As transições devem ser controladas pelo domínio.

Não permitir transições arbitrárias.

Exemplo:

```text
Delivered → PendingPayment
```

deve ser inválido.

---

# 11. Inventory

O Inventory será responsável pelo controle da disponibilidade de produtos.

Uma preocupação fundamental será a concorrência.

Exemplo:

```text
Stock = 1

Request A → reserve product
Request B → reserve product
```

O sistema deve garantir que apenas uma operação consiga reservar a unidade disponível.

Esse cenário deve ser coberto por testes de concorrência.

O projeto deverá explorar:

* optimistic concurrency;
* transactions;
* unique constraints;
* isolation levels;
* race conditions;
* retry quando aplicável.

---

# 12. Reserva de estoque

A reserva de estoque não deve ser tratada simplesmente como:

```text
stock -= quantity
```

Deve existir um conceito explícito de reserva.

Exemplo:

```text
Inventory
    ↓
InventoryReservation
```

Estados possíveis:

```text
Reserved
Released
Consumed
Expired
```

Isso permitirá posteriormente trabalhar com compensações.

Exemplo:

```text
Order created
    ↓
Inventory reserved
    ↓
Payment failed
    ↓
Inventory released
```

---

# 13. Payments dentro do OrderCore

Inicialmente, Payments será um módulo interno do OrderCore.

Estrutura conceitual:

```text
OrderCore
│
├── Orders
├── Inventory
├── Customers
├── Catalog
└── Payments
```

Porém, Payments deverá ser tratado como um bounded context separado.

O módulo será responsável por:

* Payment;
* Transaction;
* Refund;
* Payment state;
* Provider abstraction;
* idempotência;
* integração com payment provider;
* webhooks;
* reconciliation.

O OrderCore não deverá conhecer detalhes internos do provider.

---

# 14. Stripe e Payment Provider

O projeto NÃO deve tentar implementar um processador de pagamentos real.

Um provider externo será responsável pelo processamento efetivo.

Exemplo:

```text
Payments
   ↓
IPaymentProvider
   ↓
StripePaymentProvider
   ↓
Stripe
```

Inicialmente deve existir um:

```text
FakePaymentProvider
```

capaz de simular:

* sucesso;
* falha;
* timeout;
* indisponibilidade;
* resposta lenta;
* erros temporários.

Posteriormente pode ser implementado um provider real, como Stripe.

---

# 15. Abstração de Payment Provider

O domínio/aplicação deve depender de uma abstração:

```csharp
public interface IPaymentProvider
{
    Task<PaymentAuthorizationResult> AuthorizeAsync(
        Payment payment,
        CancellationToken cancellationToken);

    Task<PaymentCaptureResult> CaptureAsync(
        Payment payment,
        CancellationToken cancellationToken);

    Task<PaymentRefundResult> RefundAsync(
        Payment payment,
        CancellationToken cancellationToken);
}
```

A implementação concreta pertence à Infrastructure.

Exemplo (seção 5.1):

```text
Modules/Payments/Infrastructure/Providers
├── Fake
│   └── FakePaymentProvider
│
└── Stripe
    └── StripePaymentProvider
```

---

# 16. Payment State Machine

O pagamento deve possuir sua própria máquina de estados.

Exemplo:

```text
Pending
   ↓
Processing
   ↓
Authorized
   ↓
Captured
```

Falhas:

```text
Processing
   ↓
Failed
```

Reembolso:

```text
Captured
   ↓
Refunded
```

Cancelamento antes da captura (backoffice):

```text
Authorized
   ↓
Voided
```

`Voided` é final: a autorização foi liberada e o comprador nunca é
cobrado. A captura acontece quando o pedido é enviado; um pedido
cancelado antes disso tem o pagamento anulado, e um já capturado (não
acontece no fluxo normal, já que pedido enviado não é cancelado) é
estornado pelo saldo que ainda estiver retido.

O estado de Payment não deve ser confundido com o estado de Order.

Por exemplo:

```text
Order = PendingPayment
Payment = Authorized
```

é possível durante uma transição.

A consistência entre esses estados pode ser eventualmente consistente quando o sistema evoluir para comunicação assíncrona.

---

# 17. Idempotência

Operações relacionadas a pagamentos devem ser idempotentes.

Exemplo:

```text
PaymentRequested
EventId = abc123
```

Se o evento for recebido duas vezes:

```text
abc123
abc123
```

o sistema não deve criar dois pagamentos nem realizar duas cobranças.

Deve existir mecanismo para identificar operações já processadas.

Na prática, hoje há duas camadas:

- **Checkout** (`POST /api/orders/checkout`): o cliente envia o header
  `Idempotency-Key`; o pedido guarda a chave (`Order.CheckoutIdempotencyKey`,
  única por cliente). Repetir o checkout devolve o mesmo pedido — e, se o
  pagamento nunca chegou a ser solicitado, solicita-o de novo — em vez de
  criar outro.
- **Pagamento**: `Payment.IdempotencyKey` é único; o Orders usa o id do
  pedido como chave, então um mesmo pedido nunca tem dois pagamentos,
  mesmo que o checkout seja repetido.

---

# 18. Domain Events

O domínio deverá utilizar Domain Events quando uma mudança relevante ocorrer.

Exemplos:

```text
OrderCreated
OrderCancelled
OrderConfirmed
InventoryReserved
InventoryReleased
PaymentAuthorized
PaymentFailed
PaymentCaptured
PaymentRefunded
```

Domain Events representam fatos ocorridos no domínio.

Eles não devem ser confundidos automaticamente com Integration Events.

---

# 19. Integration Events

Quando o sistema evoluir para comunicação entre processos, eventos externos serão utilizados.

Exemplos:

```text
PaymentRequested
PaymentAuthorized
PaymentFailed
PaymentRefunded
```

Os contratos devem ser estáveis e versionáveis.

Exemplo:

```json
{
  "eventId": "uuid",
  "eventType": "PaymentAuthorized",
  "version": 1,
  "occurredAt": "2026-09-22T15:00:00Z",
  "paymentId": "uuid",
  "orderId": "uuid",
  "amount": 199.90,
  "currency": "BRL"
}
```

Não compartilhar entidades de domínio entre módulos ou futuros serviços.

Os integration events também são organizados por módulo
(`Modules/Payments/Application/Contracts/IntegrationEvents/`), pelo mesmo
motivo da seção 5.1: à medida que outros módulos publicarem integration
events, cada um ganha sua própria pasta em vez de um `IntegrationEvents/`
genérico compartilhado por todos.

---

# 20. Transactional Outbox

O projeto deverá posteriormente implementar o Transactional Outbox.

Exemplo:

```text
BEGIN TRANSACTION

UPDATE orders
SET status = 'PendingPayment';

INSERT INTO outbox_messages (...);

COMMIT;
```

Um worker posteriormente publica a mensagem:

```text
Outbox
   ↓
RabbitMQ
```

O objetivo é evitar:

```text
Database update = success
RabbitMQ publish = failure
```

sem possibilidade de recuperação.

---

# 21. RabbitMQ

RabbitMQ será utilizado somente quando existir uma necessidade clara de comunicação assíncrona.

A arquitetura futura será:

```text
OrderCore
   ↓
Outbox
   ↓
RabbitMQ
   ↓
PayCore
```

E:

```text
PayCore
   ↓
RabbitMQ
   ↓
OrderCore
```

O projeto deve considerar que mensagens podem:

* ser entregues mais de uma vez;
* chegar posteriormente;
* falhar;
* ser processadas fora do request HTTP original;
* precisar de retry.

Portanto, consumidores devem ser idempotentes.

---

# 22. Futuro PayCore

Quando o módulo Payments estiver suficientemente isolado, ele poderá ser extraído para um repositório separado:

```text
PayCore
```

Com sua própria solution:

```text
PayCore.sln
```

E:

```text
PayCore.Api
PayCore.Application
PayCore.Domain
PayCore.Infrastructure
PayCore.Contracts
```

O PayCore possuirá seu próprio banco.

```text
OrderCore
    ↓
Order Database

PayCore
    ↓
Payment Database
```

Nenhum serviço poderá acessar diretamente o banco do outro.

Como `Payments` já vive isolado em `Modules/Payments/` dentro de cada
assembly (seção 5.1), a extração passa a ser, em grande parte, mover essa
pasta para o novo repositório — e não uma separação a ser descoberta na
hora da extração.

---

# 23. Extração para microservice

A extração deverá ocorrer conceitualmente assim:

```text
ANTES

OrderCore
├── Orders
├── Inventory
├── Customers
└── Payments
```

Depois:

```text
DEPOIS

OrderCore
├── Orders
├── Inventory
└── Customers

        │
        │ RabbitMQ
        ▼

PayCore
├── Payments
├── Transactions
└── Refunds
```

A extração deve ser feita removendo a implementação de Payments do OrderCore e substituindo a comunicação interna por contratos de integração.

Não simplesmente copiar o módulo.

---

# 24. PayCore após a extração

O PayCore será responsável pelo contexto de pagamentos.

Responsabilidades:

* criação de pagamentos;
* autorização;
* captura;
* refund;
* idempotência;
* provider abstraction;
* integração com Stripe/outros providers;
* webhooks;
* retry;
* circuit breaker;
* reconciliation;
* processamento assíncrono;
* publicação de eventos.

O PayCore não deverá conhecer regras internas de:

* pedidos;
* estoque;
* catálogo;
* clientes.

Ele conhecerá somente os dados necessários para processar a operação financeira.

---

# 25. Comunicação futura

Fluxo esperado:

```text
Client
   ↓
OrderCore
   ↓
Create Order
   ↓
Reserve Inventory
   ↓
PaymentRequested
   ↓
Outbox
   ↓
RabbitMQ
   ↓
PayCore
   ↓
Payment Provider
   ↓
PaymentAuthorized
   ↓
RabbitMQ
   ↓
OrderCore
   ↓
Confirm Order
```

Falha:

```text
OrderCore
   ↓
PaymentRequested
   ↓
PayCore
   ↓
PaymentFailed
   ↓
OrderCore
   ↓
Release Inventory
   ↓
Cancel Order
```

---

# 26. Saga

Depois da extração, o sistema deverá explorar Saga/compensating actions.

Exemplo:

```text
Create Order
     ↓
Reserve Inventory
     ↓
Authorize Payment
     ↓
Create Shipment
```

Se Shipment falhar:

```text
Shipment Failed
       ↓
Refund Payment
       ↓
Release Inventory
       ↓
Cancel Order
```

Não tentar utilizar uma transação SQL única para controlar operações que pertencem a bancos/serviços diferentes.

---

# 27. Resiliência

O projeto deverá tratar explicitamente:

* timeout;
* retry;
* exponential backoff;
* circuit breaker;
* duplicate messages;
* failed messages;
* dead-letter queues;
* provider unavailable;
* network failures;
* eventual consistency.

Não implementar retry indiscriminadamente.

Cada operação deve possuir uma política adequada.

Operações não idempotentes exigem atenção especial antes de retry.

---

# 28. Webhooks

Quando houver integração com Stripe ou outro provider, PayCore deverá aceitar webhooks.

Fluxo:

```text
Stripe
   ↓
Webhook
   ↓
PayCore
   ↓
Validate
   ↓
Idempotency
   ↓
Update Payment
   ↓
Publish Payment Event
```

Webhooks também devem ser tratados como mensagens potencialmente duplicadas.

---

# 29. Reconciliation

PayCore deverá possuir posteriormente um processo de reconciliation.

Objetivo:

comparar o estado local com o estado informado pelo provider.

Exemplo:

```text
PayCore:
Payment = Pending

Provider:
Payment = Succeeded
```

O processo de reconciliation deverá identificar a divergência e permitir corrigir o estado local.

Isso deve ser tratado como uma preocupação de consistência e operação, não simplesmente como um CRUD administrativo.

---

# 30. Observabilidade

O projeto deve possuir observabilidade desde o início e evoluí-la conforme a arquitetura.

Utilizar:

* structured logging;
* correlation ID;
* trace ID;
* event ID;
* Order ID;
* Payment ID;
* metrics;
* distributed tracing;
* health checks.

Quando OrderCore e PayCore forem separados, deve ser possível rastrear:

```text
POST /orders
      ↓
OrderCreated
      ↓
PaymentRequested
      ↓
RabbitMQ
      ↓
PayCore
      ↓
Stripe
      ↓
PaymentAuthorized
      ↓
RabbitMQ
      ↓
OrderCore
      ↓
OrderConfirmed
```

Idealmente tudo deve ser associado ao mesmo distributed trace.

**Audit log.** O `AuditLogs` guarda cada ação relevante (pedido criado,
enviado, cancelado; pagamento autorizado, capturado, anulado; produto
publicado; cliente desativado...) com o autor (o usuário autenticado, ou
nenhum quando foi o sistema) e é persistido no PostgreSQL
(`AuditLogsDbContext`), para a linha do tempo do pedido no backoffice
sobreviver a um restart. A listagem filtra por entidade (a linha do tempo
de um pedido), autor e ação. Gravar é "melhor esforço": acontece depois
que a mudança do caso de uso já foi salva, então uma falha vai para o log
em vez de virar um 500 que convidaria o cliente a repetir uma operação
que já aconteceu.

---

# 31. Redis

Redis não deve ser introduzido apenas porque faz parte da stack.

Utilizações possíveis:

* cache;
* rate limiting;
* dados temporários;
* locks distribuídos quando realmente necessários.

Não utilizar Redis como substituto do PostgreSQL para dados transacionais.

---

# 32. Segurança

A API deve considerar:

* autenticação;
* autorização;
* validação de entrada;
* secrets fora do código;
* proteção de endpoints administrativos;
* rate limiting;
* proteção de webhooks;
* validação de assinatura de webhook quando aplicável;
* não armazenar dados sensíveis de cartão.

O sistema não deve armazenar informações completas de cartão.

O que já está implementado (módulo `Identity`, ver
`Docs/diagrams/implementation-class/08-identity.md`):

* **autenticação** por JWT emitido pelo próprio OrderCore (HMAC-SHA256,
  15 minutos) mais refresh token rotativo (256 bits aleatórios, só o hash
  é guardado, 14 dias). Reapresentar um refresh token já usado revoga a
  sessão inteira, porque indica um token roubado;
* **senhas** com hash PBKDF2 salgado (`PasswordHasher<T>` do ASP.NET Core),
  política mínima de 8 a 128 caracteres com letra e número. Qualquer falha
  de login responde o mesmo `invalid_credentials`;
* **autorização** com as políticas `Customer` e `Admin` e bloqueio por
  padrão (seção 39). Cliente só enxerga os próprios dados; os de outro
  cliente respondem 404, como se não existissem;
* **secrets fora do código**: a chave de assinatura (`Jwt:SigningKey`) e o
  primeiro admin (`IdentitySeed:*`) vêm do ambiente ou de user-secrets; o
  docker-compose lê de um `.env` ignorado pelo git. A API não sobe sem a
  chave;
* a auditoria registra quem fez cada ação.

Ainda não implementado: rate limiting de login, verificação de e-mail,
recuperação de senha e permissões finas de admin.

Quando utilizar Stripe, os dados sensíveis devem ser tratados conforme o modelo de integração escolhido pelo provider.

---

# 33. Testes

O projeto deve possuir diferentes níveis de testes.

## Unit Tests

Testar:

* regras de Order;
* transições de estado;
* regras de Payment;
* Inventory;
* invariantes;
* value objects;
* domain services.

## Integration Tests

Testar:

* PostgreSQL;
* EF Core;
* RabbitMQ;
* Outbox;
* consumidores;
* providers fake;
* concorrência.

Preferir Testcontainers para infraestrutura real nos testes de integração.

## Architecture Tests

Validar regras como:

```text
Domain
    NÃO depende de Infrastructure

Domain
    NÃO depende de API

Application
    NÃO depende diretamente de Infrastructure concreta

Modules
    NÃO acessam internals de outros módulos
```

## Organização física dos testes

Independentemente do nível (Unit, Integration), os projetos de teste devem ser organizados **por módulo de negócio**, seguindo o mesmo padrão adotado no CourseCore, e não por camada técnica (ver seção 5).

Evitar:

```text
OrderCore.UnitTests/
├── Application/
├── Domain/
└── Infrastructure/
```

Preferir:

```text
OrderCore.UnitTests/
├── Orders/
├── Customers/
├── Catalog/
├── Inventory/
└── Payments/
```

Essa regra vale tanto para `OrderCore.UnitTests` quanto para `OrderCore.IntegrationTests` (ver seção 5). `OrderCore.ArchitectureTests` é a única exceção, por validar regras estruturais que atravessam módulos e camadas.

---

# 34. Teste de concorrência

Um dos testes importantes do projeto:

```text
Inventory = 1

100 requests
    ↓
Reserve product
```

Resultado esperado:

```text
1 successful reservation
99 rejected reservations
```

O teste deve demonstrar que a regra permanece correta sob concorrência.

---

# 35. Load Testing

Posteriormente utilizar uma ferramenta de load testing.

Objetivos:

* medir throughput;
* medir latência;
* identificar gargalos;
* observar banco;
* observar filas;
* observar workers;
* testar concorrência.

Não otimizar prematuramente.

Primeiro medir.

---

# 36. ADRs

Decisões arquiteturais importantes devem ser documentadas.

Exemplos:

```text
Docs/adr/

ADR-001-modular-monolith.md
ADR-002-module-boundaries.md
ADR-003-postgresql.md
ADR-004-payment-provider-abstraction.md
ADR-005-domain-events.md
ADR-006-transactional-outbox.md
ADR-007-rabbitmq.md
ADR-008-idempotent-consumers.md
ADR-009-optimistic-concurrency.md
ADR-010-payment-context-extraction.md
ADR-011-saga.md
ADR-012-observability.md
```

Cada ADR deve explicar:

```text
Context
Decision
Alternatives
Consequences
```

Não escrever ADR apenas para documentar tecnologias.

---

# 37. Princípios de implementação

Priorizar:

* SOLID;
* encapsulamento;
* baixo acoplamento;
* alta coesão;
* dependency inversion;
* domain-driven design;
* modularidade;
* explicit boundaries;
* testability;
* observability;
* resilience.

Evitar:

* abstrações sem necessidade;
* generic repository sem justificativa;
* services gigantes;
* controllers contendo regras de negócio;
* entidades anêmicas;
* compartilhamento indiscriminado de modelos;
* dependências circulares;
* microservices artificiais;
* tecnologias adicionadas sem necessidade.

---

# 38. Regra sobre abstrações

Não criar abstrações somente porque "pode ser útil no futuro".

Criar abstrações quando existir:

* uma variação real;
* uma fronteira arquitetural;
* uma necessidade de teste;
* uma dependência externa;
* uma regra de negócio que justifique polimorfismo.

Exemplo válido:

```text
IPaymentProvider
```

porque existirão múltiplos providers.

Exemplo potencialmente desnecessário:

```text
IGenericService<T>
IGenericManager<T>
IGenericProcessor<T>
```

sem necessidade concreta.

---

# 39. API

A API deve seguir REST de maneira pragmática.

Endpoints iniciais:

```http
POST   /api/orders
GET    /api/orders/{id}
POST   /api/orders/{id}/cancel
GET    /api/orders/{id}/status

GET    /api/products
GET    /api/products/{id}

GET    /api/customers/{id}
```

Os endpoints administrativos do backoffice estão descritos em
`Docs/specs/backoffice/backoffice-api.md`.

A lista acima era o ponto de partida. O contrato real é o documento
OpenAPI gerado (`/openapi/v1.json`, UI em `/scalar/v1`, só em
Development). O primeiro consumidor é um front de e-commerce (Next.js);
os endpoints que ele usa no MVP estão descritos em
`Docs/specs/storefront/storefront-api-mvp.md`: listagem paginada e
produto por slug no Catalog, cotação do carrinho, checkout em um passo e
acompanhamento do pedido no Orders.

Os endpoints devem permanecer finos e delegar os casos de uso para a Application Layer.

**Erros.** Casos de uso e agregados não devolvem códigos HTTP nem usam
try/catch por endpoint: lançam exceções tipadas do shared kernel
(`DomainRuleViolationException`, `NotFoundException`, `ConflictException`),
cada uma com um código estável, e o `ApiExceptionHandler` as converte em
`ProblemDetails` (400/404/409, com a extensão `code`) — ver
`Docs/diagrams/implementation-class/01-shared-kernel.md`. O cliente
decide o que mostrar a partir do `code` (ex.: `insufficient_stock`,
`price_changed`), não da mensagem.

**CORS.** Só as origens listadas em `Cors:AllowedOrigins` podem chamar a
API do navegador.

**Acesso.** A API bloqueia por padrão: todo endpoint exige um token, a
não ser que esteja marcado como público. As classes de acesso são:

| Acesso | Endpoints |
|---|---|
| Público | navegação do catálogo (listagem, produto por slug, categorias), cotação do carrinho, `auth/sign-up`, `sign-in`, `refresh`, health, docs |
| Cliente | checkout, `customers/me` (perfil e endereços), `orders/me` (e cancelar o próprio pedido, `orders/me/{id}/cancel`) |
| Cliente dono ou admin | `orders/{id}` e o histórico de status |
| Admin | todo o resto (escrita no catálogo, estoque, pagamentos, audit logs, clientes, pedidos por id, e o backoffice em `admin/…`) |

Um teste de arquitetura falha se alguma action não estiver classificada.

**Rotas do backoffice.** Um comando fica na rota do recurso que ele muda
(`orders/{id}/ship`, `catalog/products/{id}/price`,
`customers/{id}/deactivate`). Uma leitura que o cliente também tem, mas
que o admin precisa num formato mais completo, ganha uma rota própria
sob `admin/` (`admin/orders`, `admin/orders/{id}`,
`admin/catalog/products`, `admin/dashboard`), servida por um
`<Módulo>AdminController`: cada rota tem um único formato de resposta,
nunca um que muda conforme quem chama, porque o documento OpenAPI não
consegue descrever isso.

Quando implementados como controllers (em vez de minimal API), cada
controller vive em `OrderCore.Api/Modules/{Módulo}/Presentation/Controllers/`
(seção 5.1), ao lado de `Requests/`, `Responses/` e `Presenters/` do mesmo
módulo — nunca em uma pasta `Controllers/` compartilhada por todos os
módulos.

---

# 40. Banco de dados

PostgreSQL será o banco principal.

Entidades/tabelas inicialmente previstas:

```text
orders
order_items
customers
products
inventory
inventory_reservations
payments
payment_transactions
refunds
outbox_messages
processed_messages
```

A estrutura exata deverá ser definida conforme a modelagem do domínio.

Não criar tabelas simplesmente para representar cada classe.

O modelo relacional deve representar as necessidades de persistência do domínio.

`Customers`, `Catalog`, `Orders`, `Inventory` e `Payments` têm EF Core de
fato implementado — todos os módulos de negócio previstos (seção 4) estão
completos de ponta a ponta, assim como os módulos técnicos `Identity` e
`AuditLogs` (este último só desde o backoffice; antes era em memória). `Customers` (`Modules/Customers/Infrastructure/Persistence`)
cria `customers`, `customer_addresses` e `customer_payment_methods` via
`InitialCustomersSchema`; `Catalog` (`Modules/Catalog/Infrastructure/Persistence`)
cria `products`, `product_images`, `product_variants` e `categories` via
`InitialCatalogSchema`; `Orders` (`Modules/Orders/Infrastructure/Persistence`)
cria `orders`, `order_items` e `order_status_history` via
`ExpandOrdersSchema` (sobre a `InitialOrdersSchema` original) — `order_items`
é chaveado por `(OrderId, ProductId)`, não um id substituto, porque
`OrderItem` não tem identidade própria no domínio; `Inventory`
(`Modules/Inventory/Infrastructure/Persistence`) cria `stock_items`,
`inventory_reservations` e `stock_movements` via `InitialInventorySchema`;
`Payments` (`Modules/Payments/Infrastructure/Persistence`) cria `payments`,
`refunds` e `outbox_messages` via `InitialPaymentsSchema`.
O MVP do storefront acrescentou três migrações: `AddProductSlugUniqueIndex`
(Catalog — índice único em `products.Slug`, renomeando duplicatas
antigas antes), `AddPaymentMethod` (Payments — coluna `Method`, `Card`
para os pagamentos existentes) e `AddCheckoutIdempotencyKeyAndHistorySequence`
(Orders — `orders.CheckoutIdempotencyKey` com índice único
`(CustomerId, CheckoutIdempotencyKey)` e `order_status_history.Sequence`
identity, para a timeline manter a ordem de transições com o mesmo
horário).
A autenticação acrescentou `InitialIdentitySchema` (Identity —
`user_accounts` e `refresh_sessions`, com e-mail normalizado único) e
`RemoveCustomerPasswordHash` (Customers — a senha saiu de `customers`).
O backoffice acrescentou `InitialAuditLogsSchema` (AuditLogs — `audit_logs`,
com índices pelos filtros da listagem), `AddPaymentVoid` (Payments —
`payments.VoidedAt`), `AddStockMovementReasonAndReturns` (Inventory —
`stock_movements.Reason`, `inventory_reservations.ReturnedAt` e o índice
de movimentos por `(ProductId, CreatedAt)`), `AddCustomerCreatedAtIndex`
(Customers) e `AddOrderListIndexes` (Orders — `CreatedAt` e `ConfirmedAt`).
Os status novos (`Voided`, `Returned`) cabem nas colunas de texto que já
existiam.
`IProductCatalog` (contrato do próprio Orders) também ganhou sua
implementação real, `ProductCatalogAdapter` (`Modules/Orders/Infrastructure/Adapters`),
que lê de `IProductRepository` do Catalog — a indireção de "Application
Contract" da seção 7. `IInventoryService` e `IPaymentGateway` (também
contratos do próprio Orders) seguem o mesmo padrão: `InventoryServiceAdapter`
envolve os use cases de reserva/liberação/consumo de `Inventory`, e
`PaymentGatewayAdapter` envolve `CreatePaymentUseCase` de `Payments` — ver
[05-orders.md](../diagrams/implementation-class/05-orders.md). O padrão
estabelecido em `Customers` e replicado em `Catalog`, `Orders`, `Inventory`
e `Payments`:

- Um `<Módulo>DbContext` por módulo (não um `ApplicationDbContext` único),
  cada um só enxergando as tabelas do próprio módulo — preserva o
  isolamento de módulos também na camada de persistência.
- `<Entidade>Mapper.ToDomain`/`ToPersistence`/`ApplyChanges` traduzindo
  entre a entidade de domínio e seu Persistence Model, nunca mapeando EF
  Core diretamente sobre a entidade de domínio (seção 5.1).
- Entidades de domínio que precisam ser reconstruídas a partir do banco
  ganham um factory `internal static Rehydrate(...)`, separado de
  `Create(...)`: `Create` valida invariantes de criação e levanta domain
  events; `Rehydrate` não deveria fazer nenhum dos dois.
- O repositório concreto (`Ef<Entidade>Repository`) guarda a associação
  entre a instância de domínio devolvida por um `GetByIdAsync`/`GetByEmailAsync`
  e o Persistence Model rastreado pelo EF Core, para poder aplicar
  `ApplyChanges` antes de `SaveChangesAsync` — a interface do repositório
  não tem um `UpdateAsync` explícito (mesmo formato de `IOrderRepository`).
- A reconciliação de coleções filhas (add/update/remove ao comparar o
  agregado de domínio com a coleção rastreada pelo EF Core) vive em
  `Shared/Infrastructure/Persistence/ChildCollectionReconciler`, não
  duplicada em cada `Mapper` — extraída quando `Catalog` precisou da mesma
  lógica que `Customers` já tinha para `CustomerAddress`/`CustomerPaymentMethod`.
- **`ApplyChanges` deve sempre sincronizar `Version`.** Bug real descoberto
  ao escrever o teste de concorrência do Inventory (seção 34): nenhum dos
  quatro mappers existentes (`Customer`/`Product`/`Category`/`Order`)
  copiava `domain.Version` para o Persistence Model em `ApplyChanges` —
  só em `ToPersistence` (usado apenas no insert). Isso tornava a checagem
  de concorrência otimista do EF Core um no-op após o primeiro update em
  qualquer agregado do projeto: a coluna `Version` nunca mudava de fato,
  então o "valor original" que um segundo escritor concorrente comparava
  continuava batendo. Corrigido nos quatro mappers existentes e em
  `StockItemMapper`/`InventoryReservationMapper` desde o início.
- Quando um caso de uso mexe em mais de um agregado raiz na mesma
  operação (ex.: `ReserveStockUseCase` mexe em `StockItem` e
  `InventoryReservation`), um `SaveChangesAsync` por repositório deixa de
  funcionar — seriam duas transações SQL separadas, não uma unidade
  atômica. `Inventory` introduziu `IUnitOfWork` (`Modules/Inventory/Application/Contracts/IUnitOfWork.cs`,
  implementado por `InventoryUnitOfWork`) para esse caso — ver a seção
  Transactions do `claude.md`, que antes dizia que não existia nenhum
  equivalente ainda.
- `InventoryUnitOfWork.SaveChangesAsync` é o primeiro lugar do código que
  de fato chama `IDomainEventDispatcher.DispatchAsync` (shared kernel):
  acontece depois que o save do EF Core já teve sucesso, sobre os eventos
  de domínio combinados de todos os agregados rastreados pelos
  repositórios envolvidos. `StockMovementRecorder` é o primeiro
  `IDomainEventHandler<T>` real do projeto; `OrderStatusHistoryProjector`
  (Orders) é o segundo, despachado por `EfOrderRepository.SaveChangesAsync`
  sem precisar de `IUnitOfWork` — toda operação de Orders mexe em um único
  agregado raiz por vez.
- `Payments` publica seus `IntegrationEvent` (seção 19) através da mesma
  infraestrutura de domain events, não de um message broker de verdade:
  `IntegrationEvent` passou a implementar `IDomainEvent`, e
  `OutboxPublisherBackgroundService` (`Modules/Payments/Infrastructure/Outbox`)
  lê periodicamente as linhas pendentes de `outbox_messages` e chama
  `IDomainEventDispatcher.DispatchAsync` diretamente sobre elas. É uma ponte
  deliberada e temporária até o RabbitMQ (seção 21) existir — o outbox
  garante a escrita atômica evento+agregado na mesma transação (a
  necessidade real do padrão), e o dispatcher in-process cobre a "entrega"
  até existir um transporte de verdade. `Orders` reage a esses eventos
  (`PaymentAuthorized`/`PaymentFailed`) como qualquer outro
  `IDomainEventHandler<T>` — ver
  [06-payments.md](../diagrams/implementation-class/06-payments.md).

---

# 41. Docker

O ambiente local deverá ser reproduzível.

Inicialmente:

```text
OrderCore
PostgreSQL
```

Posteriormente:

```text
OrderCore
PostgreSQL
Redis
RabbitMQ
```

Após a extração:

```text
OrderCore
OrderCore PostgreSQL

PayCore
PayCore PostgreSQL

RabbitMQ
Redis
```

A infraestrutura deverá ser inicializável de forma simples.

---

# 42. README

O README deve explicar não apenas:

```text
"o que foi utilizado"
```

mas principalmente:

```text
"por que foi utilizado"
```

Deve conter:

* problema;
* objetivos;
* arquitetura;
* diagrama;
* módulos;
* fluxo de pedido;
* fluxo de pagamento;
* decisões arquiteturais;
* estratégia de consistência;
* estratégia de concorrência;
* estratégia de resiliência;
* observabilidade;
* testes;
* como executar;
* evolução arquitetural.

---

# 43. Critério principal de qualidade

O projeto deve ser avaliado pela capacidade de responder:

> "O que acontece quando algo dá errado?"

Exemplos:

```text
Database unavailable
RabbitMQ unavailable
Payment provider unavailable
Message duplicated
Message delayed
Consumer crashes
Network timeout
Concurrent inventory reservation
Payment succeeds but response is lost
Order cancellation after payment
Webhook duplicated
Webhook delayed
Provider state differs from local state
```

Para cada cenário relevante, deve existir uma estratégia documentada e, quando possível, um teste demonstrando o comportamento.

---

# 44. Filosofia do projeto

O objetivo do OrderCore não é parecer complexo.

O objetivo é ser **tecnicamente justificável**.

Não utilizar:

```text
Microservices
Kafka
Kubernetes
Event Sourcing
CQRS
Redis
RabbitMQ
```

simplesmente para aumentar a quantidade de tecnologias.

A arquitetura deve evoluir conforme os problemas surgem.

A progressão desejada é:

```text
Simple
  ↓
Modular
  ↓
Reliable
  ↓
Observable
  ↓
Resilient
  ↓
Distributed
```

e não:

```text
Simple
  ↓
Everything at once
```

---

# 45. Resultado esperado

Ao final da primeira grande versão, o projeto deverá demonstrar:

```text
✓ Modular Monolith
✓ DDD
✓ Bounded Contexts
✓ Clean Architecture principles
✓ Domain Events
✓ Integration Events
✓ Transactional Outbox
✓ Idempotency
✓ Optimistic Concurrency
✓ Async Processing
✓ RabbitMQ
✓ Resilience
✓ Retry
✓ Circuit Breaker
✓ Dead Letter Queue
✓ Observability
✓ PostgreSQL
✓ Redis where justified
✓ Docker
✓ Automated Tests
✓ Integration Tests
✓ Architecture Tests
✓ Load Testing
✓ ADRs
```

Posteriormente:

```text
OrderCore
      │
      │ Integration Events
      ▼
   RabbitMQ
      │
      ▼
PayCore
      │
      ▼
Payment Provider
```

O PayCore deve ser extraído somente quando o bounded context estiver suficientemente isolado para que essa extração seja uma evolução natural da arquitetura.

---

# 46. Regra para o agente de desenvolvimento

Ao implementar qualquer funcionalidade, primeiro identificar:

1. Qual módulo é responsável?
2. Qual camada é responsável?
3. Qual regra pertence ao domínio?
4. Existe dependência com outro módulo?
5. Essa dependência deve ser direta ou por contrato/evento?
6. Existe problema de concorrência?
7. Existe possibilidade de operação duplicada?
8. Existe falha parcial?
9. Existe necessidade de transação?
10. Existe necessidade de observabilidade?
11. A decisão cria acoplamento que dificultará futura extração do PayCore?
12. A complexidade introduzida é realmente necessária?
13. O arquivo está sendo criado dentro de `Modules/{Módulo}/{Camada}/{Subpasta}`
    correta (seção 5.1), e não solto na raiz da camada?

Não implementar funcionalidades apenas para aumentar o escopo.

Priorizar qualidade arquitetural, clareza e comportamento correto.

---

# 47. Diretriz para futuras decisões

Quando houver mais de uma solução tecnicamente válida, documentar as alternativas e escolher a solução mais simples que satisfaça os requisitos atuais.

Sempre considerar:

```text
Correctness
Maintainability
Testability
Operational Complexity
Scalability
Failure Handling
Future Evolution
```

A escalabilidade deve ser consequência de uma arquitetura bem projetada, e não motivo para introduzir complexidade sem necessidade.
