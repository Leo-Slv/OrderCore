# OrderCore

Sistema de processamento e gerenciamento de pedidos em **C# / .NET**, construído
não como um CRUD, mas como um exercício deliberado de arquitetura de
software: modelagem de domínio, modularidade, consistência, concorrência,
resiliência e preparação para evolução arquitetural.

## Problema

Processar um pedido de forma correta envolve mais do que salvar uma linha
em uma tabela `orders`. Envolve reservar estoque sem sobrevender um produto
sob concorrência, cobrar um pagamento sem cobrar duas vezes por engano,
saber o que fazer quando o provedor de pagamento está fora do ar, e manter
tudo isso observável e recuperável quando algo dá errado. O OrderCore
constrói esse cenário de ponta a ponta e documenta, a cada passo, **por
que** cada decisão técnica foi tomada — não apenas o que foi usado.

## Objetivos

- Modelar um domínio de pedidos real: clientes, catálogo, estoque,
  pedidos, itens, reserva de estoque, pagamento.
- Demonstrar decisões arquiteturais justificadas, não tecnologia por
  tecnologia.
- Evoluir de um Modular Monolith em direção a bounded contexts bem
  definidos, eventos e, eventualmente, à extração opcional do contexto de
  pagamentos para um serviço independente (`PayCore`).
- Tratar concorrência, idempotência, falhas parciais e consistência
  eventual como preocupações de primeira classe, não como detalhes de
  implementação.

## Arquitetura

O projeto começa como um **Modular Monolith** (ver
[ADR-001](Docs/adr/ADR-001-modular-monolith.md)): um único processo,
organizado por módulos de negócio que não acessam as estruturas internas
uns dos outros diretamente.

```text
OrderCore
│
├── Orders
├── Customers
├── Catalog
├── Inventory
├── Payments   ← bounded context isolado desde o início
│
├── AuditLogs  ← módulos técnicos/transversais,
└── Identity   ← não bounded contexts de negócio
```

O código de produção é um único projeto (`OrderCore.Api.csproj`, na raiz
do repositório), com `Modules/` também na raiz e um módulo por bounded
context — o mesmo layout do módulo `Courses` no CourseCore. Dentro de cada
módulo, as camadas seguem Clean Architecture / Dependency Inversion:

```text
Modules/{Módulo}/
├── Presentation      → endpoints finos, delegam para Application
├── Application        → casos de uso, depende só de abstrações
├── Domain               → regras de negócio, sem dependências externas
└── Infrastructure         → implementações concretas (EF Core, providers)
```

A camada `Domain` de cada módulo não referencia ASP.NET Core, EF Core,
RabbitMQ, Redis, HttpClient ou SDKs de providers externos — essa regra é
validada automaticamente por `OrderCore.ArchitectureTests` (via checagem de
namespace, já que o projeto é único), não apenas documentada.

### Evolução planejada

```text
Modular Monolith
        ↓
Bounded Contexts bem definidos
        ↓
Eventos e comunicação assíncrona
        ↓
Transactional Outbox
        ↓
Idempotent Consumers
        ↓
Resiliência e observabilidade
        ↓
Extração opcional do contexto de pagamentos → PayCore
```

A extração para microservices **não** é um passo automático — só é
avaliada depois que as fronteiras de domínio estiverem comprovadamente
estáveis dentro do monólito.

## Fluxo de pedido

```text
Client
   ↓
Create Order
   ↓
Reserve Inventory
   ↓
Request Payment
   ↓
Payment Authorized  →  Confirm Order  →  Processing → Shipped (captura o pagamento) → Delivered
Payment Failed      →  Release Inventory → PaymentFailed
Cancelamento (admin, antes do envio) → acerta o pagamento (void/estorno) → devolve o estoque → Cancelled
```

## API para o storefront

O primeiro consumidor da API é um front de e-commerce (Next.js). O que
ele usa no MVP está especificado em
[`Docs/specs/storefront/storefront-api-mvp.md`](Docs/specs/storefront/storefront-api-mvp.md);
o contrato exato de cada endpoint está no documento OpenAPI (`/scalar/v1`
em Development).

| Tela | Endpoint |
|---|---|
| Home / listagem | `GET /api/catalog/products?active=true&sort=PriceAsc&onSale=true&page=1&pageSize=20` → paginado, com slug, imagem principal e disponibilidade (`InStock`/`LowStock`/`OutOfStock`, nunca a quantidade) |
| Produto | `GET /api/catalog/products/by-slug/{slug}` → só produtos publicados |
| Carrinho | `POST /api/orders/cart/quote` → preço atual e problemas por linha (`PriceChanged`, `InsufficientStock`, `Unavailable`, `NotFound`), sem reservar nada |
| Cadastro / login | `POST /api/auth/sign-up`, `POST /api/auth/sign-in` → access token (JWT, 15 min) + refresh token (14 dias); `POST /api/auth/refresh` troca o refresh token por um par novo; `POST /api/auth/sign-out` encerra a sessão |
| Minha conta | `GET`/`PUT /api/customers/me`, `GET`/`POST /api/customers/me/addresses`, `PUT`/`DELETE /api/customers/me/addresses/{addressId}`, `POST …/{addressId}/default-shipping` e `…/default-billing` |
| Checkout | `GET /api/customers/me/addresses`, depois `POST /api/orders/checkout` com header `Idempotency-Key` → 202 com o pedido já em `PendingPayment` (o cliente vem do token) |
| Acompanhamento | `GET /api/orders/{id}` (polling até `Confirmed`/`PaymentFailed`) e `GET /api/orders/{id}/status-history` (timeline) — pedido de outro cliente responde 404 |
| Meus pedidos | `GET /api/orders/me?page=1&pageSize=20` |

O checkout valida, reserva estoque e inicia o pagamento num único caso
de uso: o front pede "quero criar este pedido" e o OrderCore decide se
ele é válido. Repetir a requisição com a mesma `Idempotency-Key` devolve
o mesmo pedido, nunca um segundo.

Erros de negócio chegam como `ProblemDetails` (RFC 7807) com um `code`
estável para o front decidir o que mostrar — por exemplo
`409 insufficient_stock`, `409 price_changed`, `404 address_not_found`.
A API só aceita chamadas de navegador das origens em `Cors:AllowedOrigins`
(`http://localhost:3000` por padrão).

**Acesso.** A API bloqueia por padrão: toda rota exige
`Authorization: Bearer <access token>`, exceto a vitrine (listagem e
produto por slug), a cotação do carrinho, `auth/sign-up|sign-in|refresh`,
`/health` e a documentação. Checkout, `customers/me` e `orders/me` exigem
um token de cliente; os endpoints administrativos (clientes, estoque,
pagamentos, auditoria, gestão do catálogo) exigem um token de admin.
Sem token → `401 unauthenticated`; token sem permissão → `403 forbidden`.
Os detalhes estão em
[`Docs/specs/identity/authentication-and-account.md`](Docs/specs/identity/authentication-and-account.md).

## API para o backoffice

As telas `/admin` do front usam endpoints só para admin, especificados em
[`Docs/specs/backoffice/backoffice-api.md`](Docs/specs/backoffice/backoffice-api.md).
Um comando fica na rota do recurso que ele muda; uma leitura que precisa
de um formato mais completo que o do cliente fica sob `admin/`.

| Tela | Endpoints |
|---|---|
| Dashboard | `GET /api/admin/dashboard?from=&to=` → pedidos por status, receita por moeda, clientes novos, estoque baixo/esgotado e pedidos recentes (padrão: últimos 30 dias) |
| Pedidos | `GET /api/admin/orders?status=&customerId=&createdFrom=&createdTo=` (com cliente e status do pagamento); `GET /api/admin/orders/{id}` (notas internas, cliente, pagamento completo, reservas); `GET /api/orders/{id}/status-history`; `GET /api/audit-logs?entityName=Order&entityId={id}` (linha do tempo) |
| Atendimento | `POST /api/orders/{id}/start-processing`, `/ship` (captura o pagamento; `409 payment_capture_failed` se o provedor recusar), `/deliver`, `/cancel` (acerta o pagamento e devolve o estoque; responde o que aconteceu com o pagamento); `PUT /api/orders/{id}/internal-notes` |
| Produtos e estoque | `GET /api/admin/catalog/products?status=&searchTerm=&stock=LowStock` (com os números de estoque); `PUT /api/catalog/products/{id}/price`, `/compare-at-price`; `POST …/discontinue`; `POST`/`DELETE …/images`, `PUT …/images/order`; `POST`/`DELETE …/variants`; `POST /api/inventory/stock-items/{productId}/receive`, `/adjust`; `PUT …/reorder-level`; `GET …/movements`, `…/reservations` |
| Pagamentos | `GET /api/payments?status=&method=&createdFrom=&createdTo=`; `GET /api/payments/{id}` (com estornos); `POST /api/payments/{id}/refunds` |
| Clientes | `GET /api/customers?searchTerm=`; `GET /api/customers/{id}` + `GET /api/orders/customers/{id}` (pedidos do cliente); `POST /api/customers/{id}/deactivate`, `/reactivate` (o cliente desativado não faz login nem checkout) |

Criar um produto já cria o registro de estoque dele (com 0 unidades); o
admin só dá entrada e ajusta. Os pagamentos são capturados quando o
pedido é enviado, e cancelar um pedido pago nunca deixa o dinheiro retido:
uma autorização é liberada (`Voided`) e uma captura, estornada.

## Fluxo de pagamento

O `Payment` tem sua própria máquina de estados, independente da do
`Order` — é válido `Order = PendingPayment` enquanto `Payment =
Authorized` durante uma transição:

```text
Pending → Processing → Authorized → Captured (no envio do pedido)
                    ↘ Failed
Authorized → Voided   (pedido cancelado antes do envio)
Captured → Refunded
```

O domínio depende de uma abstração, `IPaymentProvider`, nunca de um SDK de
provider diretamente. Hoje existe um `FakePaymentProvider`
(`Modules/Payments/Infrastructure/Providers/Fake`) capaz de simular
sucesso, recusa (inclusive só da captura, `CaptureDeclined`), timeout e indisponibilidade, o que permite desenvolver e
testar os caminhos de falha antes de qualquer integração real (ex.:
Stripe).

## Decisões arquiteturais (ADRs)

Decisões relevantes ficam registradas em [`Docs/adr/`](Docs/adr/), no
formato Context / Decision / Alternatives / Consequences — nunca só para
documentar o uso de uma tecnologia (ver
[ADR-000 (template)](Docs/adr/ADR-000-template.md)). Hoje:

- [ADR-001 — Iniciar como Modular Monolith](Docs/adr/ADR-001-modular-monolith.md)

Novos ADRs são adicionados conforme decisões concretas são tomadas ao
longo das fases descritas acima (outbox, RabbitMQ, extração de Payments,
etc.), não antecipadamente.

## Estratégia de consistência e concorrência

- O aggregate `Order` carrega um `Version` (concorrência otimista) —
  escritas concorrentes sobre o mesmo pedido são detectadas, não
  silenciosamente sobrescritas.
- Reserva de estoque não é `stock -= quantity`: é um objeto de primeira
  classe, `InventoryReservation`, com estados próprios (`Reserved`,
  `Released`, `Consumed`, `Expired`), o que viabiliza compensações
  (ex.: pagamento falhou → estoque é liberado).
- O cenário de concorrência central do projeto — `Stock = 1`, 100
  requisições concorrentes, exatamente 1 reserva bem-sucedida — é
  validado em `OrderCore.IntegrationTests/Inventory` contra PostgreSQL
  real (via Testcontainers), não com dublês em memória.

## Testes

```text
Tests/
├── OrderCore.UnitTests/          # regras de domínio, por módulo de negócio
│   ├── Orders/
│   ├── Customers/
│   ├── Catalog/
│   ├── Inventory/
│   ├── Payments/
│   └── Identity/
├── OrderCore.IntegrationTests/   # PostgreSQL, EF Core, concorrência real, API HTTP
│   └── (mesma organização por módulo)
└── OrderCore.ArchitectureTests/  # regras estruturais entre camadas/módulos
```

Os projetos de teste são organizados **por módulo de negócio**, não por
camada técnica — o mesmo critério usado em `Modules/`. Isso mantém a
navegabilidade alinhada entre produção e testes, e significa que, no dia
em que `Payments` for extraído para `PayCore`, seus testes já estão
isolados em uma única pasta junto com o resto do módulo.

`OrderCore.ArchitectureTests` é a exceção deliberada: valida regras que
atravessam módulos e camadas, então é organizado por regra/convenção —
incluindo `EndpointAuthorizationTests`, que falha se alguma action não
declarar explicitamente quem pode chamá-la.

Os testes de integração sobem PostgreSQL via Testcontainers, então
precisam do Docker rodando. Os testes que passam pela API HTTP usam
`OrderCoreApiFactory` (host real com uma chave de assinatura de teste) e
`ApiDatabase` (banco migrado + admin semeado + passos HTTP comuns).

## Como executar

Pré-requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) e
Docker.

A API não sobe sem uma chave de assinatura de JWT (`Jwt:SigningKey`, no
mínimo 32 bytes), e o primeiro admin é criado na inicialização a partir
de `IdentitySeed:AdminEmail`/`AdminPassword`. Nenhum dos dois fica no
repositório:

```bash
# Com docker compose: copie .env.example para .env (git-ignored) e preencha
# JWT_SIGNING_KEY, ADMIN_EMAIL e ADMIN_PASSWORD
cp .env.example .env

# Subir PostgreSQL + API
docker compose up --build

# Rodando a API localmente (fora do container): guarde os segredos em user-secrets
dotnet user-secrets set "Jwt:SigningKey" "1049089openssl rand -base64 48)"
dotnet user-secrets set "IdentitySeed:AdminEmail" "admin.local"
dotnet user-secrets set "IdentitySeed:AdminPassword" "<senha com letra e dígito>"

# ...e rode contra o Postgres do compose
dotnet run --project OrderCore.Api.csproj

# Rodar todos os testes
dotnet test
```

A API expõe `GET /health` para health check e `GET /` como smoke test.

Em ambiente de Development, a API expõe documentação interativa via
[Scalar](https://scalar.com/) em `/scalar/v1`, gerada a partir do documento
OpenAPI padrão do .NET (`Microsoft.AspNetCore.OpenApi`) servido em
`/openapi/v1.json` — sem Swashbuckle/SwaggerUI. Ambos ficam disponíveis
apenas em Development (`app.Environment.IsDevelopment()`), nunca expostos
por padrão fora do ambiente local. O Scalar aceita o access token (botão
de autenticação Bearer) para chamar as rotas protegidas.

As migrations não são aplicadas na inicialização. Bancos locais criados
antes da autenticação precisam das migrations novas do Identity e do
Customers (`RemoveCustomerPasswordHash`); clientes antigos não têm conta
de login, então o mais simples é recriar o banco local
(`docker compose down -v`) e aplicar as migrations de novo. O backoffice
acrescentou migrations em todos os contextos (inclusive o novo
`AuditLogsDbContext`); produtos publicados antes dele podem não ter
registro de estoque, o que também se resolve recriando o banco. Para
aplicar as migrations de um contexto:

```bash
dotnet ef database update --context AuditLogsDbContext
```

(repetir para `CustomersDbContext`, `CatalogDbContext`,
`InventoryDbContext`, `OrdersDbContext`, `PaymentsDbContext` e
`IdentityDbContext`).

## Estado atual do scaffold

`Customers`, `Catalog`, `Orders`, `Inventory` e `Payments` — todos os
módulos de negócio previstos — estão implementados de ponta a ponta
(Domain + Application + Infrastructure/EF Core + Presentation) contra
PostgreSQL real. `AuditLogs`, o módulo técnico/transversal, também está
implementado e persistido no PostgreSQL. `Identity`, o outro módulo técnico, cuida de contas,
senhas, sessões de refresh e emissão dos JWT, com persistência EF Core
própria — ver
[08-identity.md](Docs/diagrams/implementation-class/08-identity.md).
`AuditLogs` recebe entradas de verdade: as 27 ações de
`AuditLogActionNames` (ciclo de vida do pedido, autorização/captura/
anulação/falha/estorno de pagamento, reserva/liberação/consumo/expiração
de estoque, produto criado/publicado/com preço ou promoção alterados/
descontinuado, cliente cadastrado/desativado/reativado, criação de conta,
reuso de refresh token) chamam `IAuditLogService.RecordAsync` — ver
[07-auditlogs.md](Docs/diagrams/implementation-class/07-auditlogs.md).
Ver o topo de cada `Docs/diagrams/implementation-class/0N-*.md` para os
desvios documentados entre cada diagrama e o código.

O fluxo de checkout completo está implementado e validado por um teste de
integração de ponta a ponta (`Tests/OrderCore.IntegrationTests/Orders/CheckoutFlowTests.cs`):
criar pedido → reservar estoque (`InventoryServiceAdapter`) → solicitar
pagamento (`PaymentGatewayAdapter`, autorizado por `FakePaymentProvider`) →
o Transactional Outbox do Payments "publica" o evento de integração
chamando o `IDomainEventDispatcher` do shared kernel (ponte deliberada e
temporária até o RabbitMQ existir) → Orders confirma o pedido e consome a
reserva de estoque permanentemente. O caso de uso de reserva de estoque
sob concorrência (`Stock = 1`, N requisições concorrentes, exatamente 1
reserva bem-sucedida) também está implementado e validado contra Postgres
real — ver a seção acima.

O caminho do storefront (catálogo → cotação do carrinho → checkout em um
passo → confirmação pelo outbox → acompanhamento) é testado pela API
HTTP real contra PostgreSQL, com o `OutboxPublisherBackgroundService` do
próprio host confirmando (ou falhando, com o provedor em modo `Declined`)
o pedido: `Tests/OrderCore.IntegrationTests/Orders/StorefrontCheckoutTests.cs`.

Autenticação e autorização estão implementadas (cadastro, login,
refresh com rotação, papéis cliente/admin, posse dos próprios dados) e
testadas pela API HTTP em `Tests/OrderCore.IntegrationTests/Identity`,
`Shared/EndpointAccessTests`, `Orders/OrderOwnershipTests` e
`Customers/MyAccountTests`.

O backoffice está implementado e testado pela API HTTP real, inclusive os
fluxos que atravessam módulos (cancelar um pedido confirmado anula o
pagamento e devolve o estoque; uma captura recusada segura o envio; um
pedido entregue pode ser estornado):
`Tests/OrderCore.IntegrationTests/Orders/BackofficeFlowTests.cs`,
`OrdersBackofficeTests`, `Catalog/CatalogBackofficeTests`,
`Inventory/InventoryBackofficeTests`, `Payments/PaymentsBackofficeTests`
e `Customers/CustomersBackofficeTests`.

A integração com RabbitMQ de verdade (o outbox hoje despacha in-process)
e a extração opcional de `Payments` para `PayCore` ainda não foram
implementadas — entram conforme as fases descritas em
[Arquitetura](#arquitetura), com ADR próprio quando a decisão for tomada.

## Filosofia

O objetivo do OrderCore não é parecer complexo. É ser tecnicamente
justificável. Tecnologia não entra no projeto para aumentar a lista de
tecnologias usadas — entra quando um problema real a justifica.
