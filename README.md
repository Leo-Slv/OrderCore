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
└── Payments   ← bounded context isolado desde o início
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
Payment Authorized  →  Confirm Order  →  Processing → Shipped → Delivered
Payment Failed      →  Release Inventory → Cancel Order
```

## Fluxo de pagamento

O `Payment` tem sua própria máquina de estados, independente da do
`Order` — é válido `Order = PendingPayment` enquanto `Payment =
Authorized` durante uma transição:

```text
Pending → Processing → Authorized → Captured
                    ↘ Failed
Captured → Refunded
```

O domínio depende de uma abstração, `IPaymentProvider`, nunca de um SDK de
provider diretamente. Hoje existe um `FakePaymentProvider`
(`Modules/Payments/Infrastructure/Providers/Fake`) capaz de simular
sucesso, falha, timeout e indisponibilidade, o que permite desenvolver e
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
│   └── Payments/
├── OrderCore.IntegrationTests/   # PostgreSQL, EF Core, concorrência real
│   └── (mesma organização por módulo)
└── OrderCore.ArchitectureTests/  # regras estruturais entre camadas/módulos
```

Os projetos de teste são organizados **por módulo de negócio**, não por
camada técnica — o mesmo critério usado em `Modules/`. Isso mantém a
navegabilidade alinhada entre produção e testes, e significa que, no dia
em que `Payments` for extraído para `PayCore`, seus testes já estão
isolados em uma única pasta junto com o resto do módulo.

`OrderCore.ArchitectureTests` é a exceção deliberada: valida regras que
atravessam módulos e camadas, então é organizado por regra/convenção.

## Como executar

Pré-requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) e
Docker.

```bash
# Subir PostgreSQL + API
docker compose up --build

# Rodar a API localmente (fora do container), contra o Postgres do compose
dotnet run --project OrderCore.Api.csproj

# Rodar todos os testes
dotnet test
```

A API expõe `GET /health` para health check e `GET /` como smoke test.

## Estado atual do scaffold

Este repositório está no estágio inicial da Fase 1 (Modular Monolith):
solution, módulos, aggregate `Order` com máquina de estados completa,
`InventoryReservation`, `Payment` com `FakePaymentProvider`, e a suíte de
testes de arquitetura validando os limites entre camadas. Persistência real
(EF Core + PostgreSQL), o caso de uso completo de reserva de estoque sob
concorrência, o Transactional Outbox e a integração com RabbitMQ ainda não
foram implementados — eles entram conforme as fases descritas em
[Arquitetura](#arquitetura), com ADR próprio quando a decisão for tomada.

## Filosofia

O objetivo do OrderCore não é parecer complexo. É ser tecnicamente
justificável. Tecnologia não entra no projeto para aumentar a lista de
tecnologias usadas — entra quando um problema real a justifica.
