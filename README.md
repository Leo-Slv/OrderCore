# OrderCore

[![CI](https://github.com/Leo-Slv/OrderCore/actions/workflows/ci.yml/badge.svg)](https://github.com/Leo-Slv/OrderCore/actions/workflows/ci.yml)

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
├── AuditLogs      ← módulos técnicos/transversais,
├── Identity       ← não bounded contexts de negócio
├── Messaging
└── Notifications  ← e-mails da conta e do pedido
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
| Confirmar e-mail | o cadastro envia um link de 24 h para a página `Identity:Links:ConfirmEmail` da loja; ela manda o token em `POST /api/auth/email/confirm` e depois **renova a sessão** (`/auth/refresh`), porque o checkout lê a confirmação do token; `POST /api/auth/email/confirmation` (cliente) manda um link novo |
| Esqueci / trocar a senha | `POST /api/auth/password/forgot` (sempre 202) envia um link de 30 min, de uso único, para `Identity:Links:ResetPassword`; a página manda token e senha nova em `POST /api/auth/password/reset` (encerra todas as sessões); logado, `POST /api/auth/password/change` com a senha atual, a nova e o refresh token da sessão que fica |
| Minha conta | `GET`/`PUT /api/customers/me`, `GET`/`POST /api/customers/me/addresses`, `PUT`/`DELETE /api/customers/me/addresses/{addressId}`, `POST …/{addressId}/default-shipping` e `…/default-billing` |
| Formas de pagamento | `GET /api/payments/methods` (anônimo) → o provedor, as formas que o checkout aceita agora (`Card`/`Pix` com o fake, só `Card` com o Stripe) e a chave publicável do Stripe |
| Checkout | `GET /api/customers/me/addresses`, depois `POST /api/orders/checkout` com header `Idempotency-Key` → 202 com o pedido já em `PendingPayment` (o cliente vem do token); com o Stripe, `payment.nextAction` traz a confirmação do cartão (ver [Pagando com cartão](#pagando-com-cartão-stripe)) |
| Acompanhamento | `GET /api/orders/{id}` (polling até `Confirmed`/`PaymentFailed`) e `GET /api/orders/{id}/status-history` (timeline) — pedido de outro cliente responde 404 |
| Meus pedidos | `GET /api/orders/me?page=1&pageSize=20`; `POST /api/orders/me/{id}/cancel` (motivo opcional) cancela o próprio pedido enquanto a loja não começou a prepará-lo — pagamento liberado ou estornado, estoque devolvido; depois disso, `400 order_in_fulfilment` |

O checkout valida, reserva estoque e inicia o pagamento num único caso
de uso: o front pede "quero criar este pedido" e o OrderCore decide se
ele é válido. Repetir a requisição com a mesma `Idempotency-Key` devolve
o mesmo pedido, nunca um segundo.

Erros de negócio chegam como `ProblemDetails` (RFC 7807) com um `code`
estável para o front decidir o que mostrar — por exemplo
`409 insufficient_stock`, `409 price_changed`, `404 address_not_found`,
`403 email_not_confirmed` (checkout antes de confirmar o e-mail: ofereça
mandar o link de novo).
A API só aceita chamadas de navegador das origens em `Cors:AllowedOrigins`
(`http://localhost:3000` por padrão).

**Acesso.** A API bloqueia por padrão: toda rota exige
`Authorization: Bearer <access token>`, exceto a vitrine (listagem e
produto por slug), a cotação do carrinho, as formas de pagamento,
`auth/sign-up|sign-in|refresh`, `auth/password/forgot|reset`,
`auth/email/confirm`, `/health` e a documentação — e o webhook
do Stripe, que não usa token mas só aceita o que o Stripe assinou. Checkout, `customers/me` e `orders/me` exigem
um token de cliente; os endpoints administrativos (clientes, estoque,
pagamentos, auditoria, gestão do catálogo) exigem um token de admin.
Sem token → `401 unauthenticated`; token sem permissão → `403 forbidden`.
Um cliente desativado pela loja recebe `401 account_inactive` no login
(só com a senha certa; senha errada continua `invalid_credentials`).
Cinco senhas erradas seguidas bloqueiam a conta por 15 minutos; enquanto
isso, o login responde `invalid_credentials` como para uma senha errada
(mesmo com a certa). Login, cadastro, refresh, esqueci/redefinir a senha,
confirmar o e-mail, pedir outro link, checkout e o webhook do Stripe têm
limite de requisições: acima dele, `429 too_many_requests` com
o cabeçalho `Retry-After` (segundos para tentar de novo).
Os detalhes estão em
[`Docs/specs/identity/authentication-and-account.md`](Docs/specs/identity/authentication-and-account.md).

## API para o backoffice

As telas `/admin` do front usam endpoints só para admin, especificados em
[`Docs/specs/backoffice/backoffice-api.md`](Docs/specs/backoffice/backoffice-api.md).
Um comando fica na rota do recurso que ele muda; uma leitura que precisa
de um formato mais completo que o do cliente fica sob `admin/`.

| Tela | Endpoints |
|---|---|
| Dashboard | `GET /api/admin/dashboard?from=&to=` → pedidos por status, receita por moeda, clientes novos, estoque baixo/esgotado, autorizações de pagamento que vencem em até dois dias (envie esses pedidos primeiro) e pedidos recentes (padrão: últimos 30 dias) |
| Pedidos | `GET /api/admin/orders?status=&customerId=&createdFrom=&createdTo=` (com cliente, status do pagamento e `authorizationExpiringSoon`); `GET /api/admin/orders/{id}` (notas internas, cliente, pagamento completo com o prazo da autorização, reservas); `GET /api/admin/orders/{id}/timeline` (a vida do pedido em todos os módulos: pedido, pagamento, estoque); `GET /api/orders/{id}/status-history`; `GET /api/audit-logs?entityName=Order&entityId={id}` (quem fez o quê) |
| Atendimento | `POST /api/orders/{id}/start-processing`, `/ship` (captura o pagamento; `409 payment_capture_failed` se o provedor recusar), `/deliver`, `/cancel` (acerta o pagamento e devolve o estoque; responde o que aconteceu com o pagamento); `PUT /api/orders/{id}/internal-notes` |
| Produtos e estoque | `GET /api/admin/catalog/products?status=&searchTerm=&stock=LowStock` (com os números de estoque); `PUT /api/catalog/products/{id}/price`, `/compare-at-price`; `POST …/discontinue`; `POST`/`DELETE …/images`, `PUT …/images/order`; `POST`/`DELETE …/variants`; `POST /api/inventory/stock-items/{productId}/receive`, `/adjust`; `PUT …/reorder-level`; `GET …/movements`, `…/reservations` |
| Pagamentos | `GET /api/payments?status=&method=&createdFrom=&createdTo=`; `GET /api/payments/{id}` (com estornos); `POST /api/payments/{id}/refunds`; `POST /api/payments/{id}/reconcile` (confere com o provedor e corrige se um webhook se perdeu; responde o que mudou) |
| Clientes | `GET /api/customers?searchTerm=`; `GET /api/customers/{id}` + `GET /api/orders/customers/{id}` (pedidos do cliente); `POST /api/customers/{id}/deactivate`, `/reactivate` (o cliente desativado não faz login nem checkout) |
| Mensagens que falharam | `GET /api/messaging/failed-messages?status=Pending`; `GET /api/messaging/failed-messages/{id}` (com a mensagem como foi recebida); `POST …/{id}/replay` (volta para o consumidor que falhou), `POST …/{id}/discard` |

Criar um produto já cria o registro de estoque dele (com 0 unidades); o
admin só dá entrada e ajusta. Os pagamentos são capturados quando o
pedido é enviado, e cancelar um pedido pago nunca deixa o dinheiro retido:
uma autorização é liberada (`Voided`) e uma captura, estornada.

## Pedidos em tempo real (SignalR)

As mudanças de pedido chegam às telas conectadas sem polling
([`Docs/specs/tracking/realtime-order-tracking.md`](Docs/specs/tracking/realtime-order-tracking.md)).
O SignalR não faz parte do documento OpenAPI, então o contrato do hub é
este:

- **Endereço:** `/api/hubs/orders` (cliente `@microsoft/signalr`,
  `HubConnectionBuilder().withUrl(...)`).
- **Autenticação:** o mesmo access token da API, obrigatório
  (`accessTokenFactory`). O navegador não manda cabeçalho em WebSocket,
  então o cliente o envia na query `access_token` — aceita só nos hubs.
  Anônimo ou token expirado: a conexão é recusada (401); ao renovar o
  token, reconecte.
- **O que cada um recebe (decidido pelo token):** o cliente, só os
  próprios pedidos; o admin, todos. Não há como pedir para seguir um
  pedido: a tela de acompanhamento filtra pelo `orderId` que mostra.
- **Mensagem:** `orderUpdated`, com
  `{ orderId, orderNumber, status, changedAt, customerId, totalAmount, currency, shipment }`,
  onde `shipment` (`{ carrier, trackingCode, trackingUrl }`, qualquer
  parte pode faltar) vem quando o pedido foi enviado com rastreio.
- **Regras do cliente:** ignore uma atualização com `changedAt` mais
  antigo do que o estado que a tela já mostra (mensagens podem chegar fora
  de ordem); ao reconectar, recarregue o pedido (ou a lista) por HTTP — o
  que mudou durante a desconexão não é reenviado.
- O envio com rastreio é `POST /api/orders/{id}/ship` (admin) com corpo
  opcional `{ carrier, trackingCode, trackingUrl }`; o cliente vê o
  mesmo em `GET /api/orders/{id}` (`shipment`).

## Eventos entre módulos (RabbitMQ)

Os módulos se avisam de forma assíncrona pelo RabbitMQ
([`Docs/specs/events/async-messaging.md`](Docs/specs/events/async-messaging.md),
diagrama em [09-messaging.md](Docs/diagrams/implementation-class/09-messaging.md)):

```text
use case ── save ──► DbContext do módulo ┬─ agregado
                                         └─ {módulo}_outbox_messages   (mesma transação)
                                                 │  relay: publica e marca como enviado
                                                 ▼
                         exchange ordercore.events  (routing key <módulo>.<evento>.v<versão>)
                                                 │
                        uma fila por consumidor (orders.payment-outcomes, orders.timeline)
                                                 │  inbox: cada mensagem tratada uma vez
                                                 ▼
                                              handler ── falhou? 10 s, 1 min, 5 min, 30 min
                                                 │
                                   5ª falha ──► failed_messages (backoffice: reprocessar/descartar)
```

- **Payments** publica o que acontece com o pagamento; **Orders**, cada
  mudança do pedido; **Inventory**, as reservas de um pedido e alertas de
  estoque baixo/esgotado.
- **Orders** consome o resultado do pagamento (confirma ou falha o
  pedido) e monta a timeline do pedido para o admin.
- A entrega é pelo menos uma vez; todo consumidor é idempotente.
- Um checkout e tudo o que ele causa nos outros módulos ficam no mesmo
  trace (W3C `traceparent` nas mensagens).
- Se o RabbitMQ cair, a API continua aceitando pedidos: os eventos
  esperam no outbox e saem quando a conexão volta.

## Fluxo de pagamento

O `Payment` tem sua própria máquina de estados, independente da do
`Order` — é válido `Order = PendingPayment` enquanto `Payment =
Authorized` durante uma transição:

```text
Pending → Processing → Authorized → Captured (no envio do pedido)
             │      ↘ Failed        (recusado; com o Stripe, a janela de 30 min acabou)
             └ aguardando o comprador (Stripe): recusas ficam registradas, ele pode tentar outro cartão
Processing/Authorized → Voided   (pedido cancelado antes do envio, ou autorização expirada)
Captured → Refunded
```

O domínio depende de uma abstração, `IPaymentProvider`, nunca de um SDK de
provider diretamente
([`Docs/specs/payments/stripe-provider.md`](Docs/specs/payments/stripe-provider.md)).
A configuração escolhe o provedor:

- **Fake** (padrão, sem chave do Stripe): cartão e Pix, responde na hora e
  simula sucesso, recusa (inclusive só da captura, `CaptureDeclined`),
  timeout e indisponibilidade — os caminhos de falha são testados sem
  nenhuma integração real.
- **Stripe** (com `Payments:Stripe:SecretKey`, modo de teste): só cartão,
  PaymentIntent com captura manual (captura no envio, anula no
  cancelamento), confirmação do cartão no navegador, resultado por webhook
  assinado e deduplicado, e uma chave de idempotência em toda chamada que
  mexe em dinheiro. Nenhum dado de cartão passa pelo OrderCore.

Com o Stripe, três coisas cuidam do que pode dar errado entre a API, o
navegador e o Stripe:

- **Janela de pagamento:** o comprador tem 30 minutos para confirmar o
  cartão; depois o pagamento é cancelado no Stripe e falha, e o pedido
  termina `PaymentFailed` com o estoque liberado.
- **Pedido sem pagamento** (qualquer provedor): se o início do pagamento
  falhar no checkout e o cliente não repetir a requisição, o pedido também
  termina `PaymentFailed` depois de 30 minutos, com o motivo
  `payment_not_started`, e o estoque volta. Toda reserva de estoque vale
  no máximo 2 horas, como rede de segurança.
- **Expiração da autorização:** o cartão fica autorizado até o prazo de
  captura dele (cerca de 7 dias). O admin vê os pedidos cuja autorização
  vence em até dois dias; se vencer, o pedido é cancelado pelo sistema e o
  estoque volta.
- **Reconciliação:** a cada 15 minutos (e em `POST
  /api/payments/{id}/reconcile`) os pagamentos que podem ter perdido um
  webhook são conferidos com o Stripe e corrigidos pelo mesmo caminho dos
  webhooks.

### Pagando com cartão (Stripe)

O que o front faz, com o [Payment Element](https://docs.stripe.com/payments/payment-element)
(`@stripe/stripe-js`):

1. `GET /api/payments/methods` → se `provider` for `Stripe`, só `Card` é
   oferecido e `publishableKey` inicializa o Stripe.js
   (`loadStripe(publishableKey)`).
2. `POST /api/orders/checkout` com `paymentMethod: "Card"` → 202 com o
   pedido em `PendingPayment` e `payment.nextAction`:

   ```json
   { "type": "confirm_card", "clientSecret": "pi_..._secret_..." }
   ```

   Com o fake, `nextAction` é `null` e o pedido se resolve sozinho.
3. Monte o Payment Element com o `clientSecret` e chame
   `stripe.confirmPayment(...)`. O 3-D Secure acontece ali mesmo. Um
   cartão recusado pode ser trocado na mesma tela.
4. O resultado chega por webhook: acompanhe pelo hub
   (`orderUpdated` com `Confirmed` ou `PaymentFailed`) ou por
   `GET /api/orders/{id}`.
5. Se a tela recarregar antes de confirmar, repita o checkout com a mesma
   `Idempotency-Key`: volta o mesmo pedido e o mesmo `nextAction`, sem um
   segundo pagamento.

O `clientSecret` é só para o navegador do comprador: não o guarde nem o
registre em log.

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

Os testes de integração sobem PostgreSQL e RabbitMQ via Testcontainers,
então precisam do Docker rodando. Os testes que passam pela API HTTP usam
`OrderCoreApiFactory` (host real com uma chave de assinatura de teste e um
vhost próprio num RabbitMQ compartilhado, com retentativas em
milissegundos) e `ApiDatabase` (banco migrado + admin semeado + passos
HTTP comuns). `Messaging/MessagingEndToEndTests` cobre o trace de ponta a
ponta, entregas duplicadas e o broker fora do ar.

## Como executar

Pré-requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download) e
Docker.

A API não sobe sem uma chave de assinatura de JWT (`Jwt:SigningKey`, no
mínimo 32 bytes) nem sem o RabbitMQ (`RabbitMq:Password`), e o primeiro
admin é criado na inicialização a partir de
`IdentitySeed:AdminEmail`/`AdminPassword`. Nenhum desses segredos fica no
repositório:

```bash
# Com docker compose: copie .env.example para .env (git-ignored) e preencha
# JWT_SIGNING_KEY, ADMIN_EMAIL, ADMIN_PASSWORD, RABBITMQ_USER e RABBITMQ_PASSWORD
cp .env.example .env

# Subir PostgreSQL + RabbitMQ + Grafana LGTM + Mailpit + API
docker compose up --build

# Rodando a API localmente (fora do container): guarde os segredos em user-secrets
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "IdentitySeed:AdminEmail" "admin@ordercore.local"
dotnet user-secrets set "IdentitySeed:AdminPassword" "<senha com letra e dígito>"
dotnet user-secrets set "RabbitMq:Password" "<a mesma RABBITMQ_PASSWORD do .env>"

# ...suba só PostgreSQL, RabbitMQ e Mailpit pelo compose e rode a API
docker compose up -d postgres rabbitmq mailpit
dotnet run --project OrderCore.Api.csproj

# Rodar todos os testes (os do Stripe usam o stripe-mock em container; não precisam de conta)
dotnet test
```

**E-mail.** Localmente nenhum e-mail sai de verdade: tudo vai para o
Mailpit, com uma caixa de entrada em <http://localhost:8025> — é lá que se
clica no link de confirmação ou de redefinição de senha (eles apontam
para a loja em `http://localhost:3000`). Em produção, o envio é pelo
Resend: `Notifications:Resend:ApiKey` (do ambiente, nunca no repositório)
e `Notifications:From` num domínio verificado no Resend
([`Docs/specs/identity/password-recovery.md`](Docs/specs/identity/password-recovery.md)).

**Stripe em modo de teste (opcional).** Sem chaves, a API usa o provedor
fake. Para usar o Stripe:

1. Crie uma conta em stripe.com (o modo de teste é grátis e não exige
   ativar a conta), ligue **Test mode** e copie de **Developers → API
   keys** a `pk_test_...` e a `sk_test_...` para `STRIPE_PUBLISHABLE_KEY`
   e `STRIPE_SECRET_KEY` no `.env`. Nunca use chaves `live`.
2. Pegue o segredo dos webhooks (fixo por conta) e cole em
   `STRIPE_WEBHOOK_SECRET`:

   ```bash
   docker compose --profile stripe run --rm --no-deps stripe-cli listen --print-secret
   ```

3. Suba com o perfil `stripe`: o Stripe CLI encaminha os webhooks do modo
   de teste para a API.

   ```bash
   docker compose --profile stripe up --build
   ```

Cartões de teste: `4242 4242 4242 4242` (aprovado),
`4000 0000 0000 0002` (recusado), `4000 0000 0000 9995` (sem saldo) e
`4000 0025 0000 3155` (pede 3-D Secure) — qualquer validade futura e
qualquer CVC. Rodando a API fora do container, os mesmos valores vão em
user-secrets (`Payments:Stripe:SecretKey`, `PublishableKey`,
`WebhookSecret`), e o Stripe CLI local encaminha para
`localhost:<porta>/api/payments/webhooks/stripe`.

A API expõe `GET /` como smoke test e três health checks:
`GET /health/live` (o processo responde; `/health` é um apelido),
`GET /health/ready` (PostgreSQL e RabbitMQ alcançáveis; 200 ou 503, sem
detalhes, é o que o container usa como healthcheck) e
`GET /health/details` (só admin: cada check com status e duração, o
backlog dos outboxes e as mensagens que falharam esperando alguém).
O painel do RabbitMQ fica em <http://localhost:15672> (usuário e senha do
`.env`): filas, mensagens em espera e as filas de retentativa.

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
aplicar as migrations de todos os bancos de uma vez (o mesmo comando que o
deploy usa, ver [`Docs/operations/deployment.md`](Docs/operations/deployment.md)):

```bash
dotnet run --project OrderCore.Api.csproj -- migrate
```

Ou as de um contexto só:

```bash
dotnet ef database update --context AuditLogsDbContext
```

(repetir para `CustomersDbContext`, `CatalogDbContext`,
`InventoryDbContext`, `OrdersDbContext`, `PaymentsDbContext`,
`IdentityDbContext` e `MessagingDbContext`). A mensageria acrescentou
migrations em Payments, Orders, Inventory e o `MessagingDbContext` novo;
o Stripe, duas no Payments (`AddStripePaymentFields` e
`AddPaymentAuthorizationExpiry`); a V4, o bloqueio de conta no Identity
(`AddAccountLockout`) e índices de retenção em Orders, Payments,
Inventory e Messaging (`AddRetentionIndexes`).

## Produção

A hospedagem ainda não foi escolhida; tudo é neutro de plataforma
([`Docs/specs/operations/production-readiness.md`](Docs/specs/operations/production-readiness.md)):

- **CI** (GitHub Actions): build, `dotnet format` e os três projetos de
  teste em todo push; o `master` verde publica a imagem da API em
  `ghcr.io/leo-slv/ordercore-api` (`sha-<commit>` e `latest`).
- **Deploy** ([`Docs/operations/deployment.md`](Docs/operations/deployment.md)):
  backup, `docker run <imagem> migrate`, e uma instância da API (o envio
  de eventos, os jobs e o SignalR assumem uma só). A imagem roda sem root
  na porta 8080, atrás de um proxy que termina o HTTPS.
- **Configuração:** o runbook lista tudo o que um deploy informa. Fora de
  Development a API **não sobe** sem banco, origens do CORS, `AllowedHosts`
  e RabbitMQ de produção, sem um jeito de enviar e-mail (Resend, ou um SMTP
que não seja localhost) e os links da loja nos e-mails da conta, nem com
uma configuração do Stripe que
  funcionaria mal (chave live sem `AllowLiveKeys`, modos misturados, sem
  segredo do webhook) — e a mensagem diz o que corrigir.
- **Borda:** cabeçalhos encaminhados só de proxies confiáveis
  (`ForwardedHeaders`), HSTS e redirecionamento para HTTPS (menos
  `/health`), cabeçalhos de segurança em toda resposta e `no-store` nas
  autenticadas.
- **Operação:** retenção diária dos registros técnicos (outbox, inbox,
  mensagens com falha resolvidas, e-mails enviados há mais de 90 dias); alertas como código em
  `deploy/grafana/alerting`; amostragem de traces no coletor
  (`deploy/otel/otelcol-config.yaml`: todo erro, todo lento e 10% do resto;
  100% no compose local).

## Observabilidade

Traces, métricas e logs com OpenTelemetry
([`Docs/specs/observability/observability.md`](Docs/specs/observability/observability.md)),
exportados por OTLP para o Grafana LGTM do `docker compose`:

- **Grafana** em <http://localhost:3001> (a porta 3000 é da loja), já com
  os dados do Tempo (traces), Prometheus (métricas) e Loki (logs) e dois
  dashboards versionados em `deploy/grafana`, na pasta **OrderCore**:
  **Negócio** (pedidos por etapa, valor confirmado, aprovação de
  pagamentos e motivos de recusa, duração e recusas do checkout, reservas
  e alertas de estoque) e **API e mensageria** (requisições, latência e
  erros por rota, tempo de banco, backlog do outbox, mensagens tratadas,
  retentativas e falhas, traces recentes com erro).
- **Um checkout é um trace só**, da requisição até a confirmação pelo
  RabbitMQ. Toda resposta traz o cabeçalho `traceparent` e todo erro
  (`ProblemDetails`) o `traceId`: com ele, o trace e os logs daquela
  chamada são encontrados no Grafana (Explore → Tempo/Loki).
- **Logs e traces levam só ids** (`order.id`, `payment.id`,
  `customer.id`, `product.id`), nunca e-mail, nome, endereço, segredos ou
  dados de pagamento. Os logs de um pedido são encontrados pelo id dele.
- Fora do compose (`dotnet run`), nada é exportado a menos que
  `OTEL_EXPORTER_OTLP_ENDPOINT` aponte para um coletor (ex.:
  `http://localhost:4317` com o LGTM do compose de pé).

## Estado atual do scaffold

`Customers`, `Catalog`, `Orders`, `Inventory` e `Payments` — todos os
módulos de negócio previstos — estão implementados de ponta a ponta
(Domain + Application + Infrastructure/EF Core + Presentation) contra
PostgreSQL real. `AuditLogs`, o módulo técnico/transversal, também está
implementado e persistido no PostgreSQL. `Identity`, o outro módulo técnico, cuida de contas,
senhas, sessões de refresh e emissão dos JWT, com persistência EF Core
própria — ver
[08-identity.md](Docs/diagrams/implementation-class/08-identity.md).
`Messaging`, o terceiro módulo técnico, leva os eventos entre os módulos
pelo RabbitMQ — ver
[09-messaging.md](Docs/diagrams/implementation-class/09-messaging.md).
`Notifications`, o quarto, envia os e-mails (fila com retentativas, Resend
ou SMTP/Mailpit, templates em português): redefinição de senha,
confirmação de e-mail e os e-mails do pedido — ver
[10-notifications.md](Docs/diagrams/implementation-class/10-notifications.md).
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
o Payments grava o evento de integração no próprio outbox → Orders
confirma o pedido e consome a reserva de estoque permanentemente (o teste
entrega o evento à mão; pelo RabbitMQ, isso é coberto pelos testes do
storefront e de mensageria). O caso de uso de reserva de estoque
sob concorrência (`Stock = 1`, N requisições concorrentes, exatamente 1
reserva bem-sucedida) também está implementado e validado contra Postgres
real — ver a seção acima.

O caminho do storefront (catálogo → cotação do carrinho → checkout em um
passo → confirmação pelo RabbitMQ → acompanhamento) é testado pela API
HTTP real contra PostgreSQL e RabbitMQ, com o relay e os consumidores do
próprio host confirmando (ou falhando, com o provedor em modo `Declined`)
o pedido: `Tests/OrderCore.IntegrationTests/Orders/StorefrontCheckoutTests.cs`.

Autenticação e autorização estão implementadas (cadastro, login,
refresh com rotação, papéis cliente/admin, posse dos próprios dados) e
testadas pela API HTTP em `Tests/OrderCore.IntegrationTests/Identity`,
`Shared/EndpointAccessTests`, `Orders/OrderOwnershipTests` e
`Customers/MyAccountTests`.

Recuperação de senha, confirmação de e-mail e os e-mails do pedido são
testados com o e-mail chegando de verdade num Mailpit em container (o
link lido da mensagem, a redefinição e a confirmação feitas por ele, o
checkout recusado até confirmar, uma reentrega que não manda outro
e-mail): `Tests/OrderCore.IntegrationTests/Identity/PasswordRecoveryFlowTests.cs`,
`Identity/EmailConfirmationTests.cs` e `Notifications`.

O backoffice está implementado e testado pela API HTTP real, inclusive os
fluxos que atravessam módulos (cancelar um pedido confirmado anula o
pagamento e devolve o estoque; uma captura recusada segura o envio; um
pedido entregue pode ser estornado):
`Tests/OrderCore.IntegrationTests/Orders/BackofficeFlowTests.cs`,
`OrdersBackofficeTests`, `Catalog/CatalogBackofficeTests`,
`Inventory/InventoryBackofficeTests`, `Payments/PaymentsBackofficeTests`
e `Customers/CustomersBackofficeTests`.

A comunicação assíncrona pelo RabbitMQ está implementada (outbox por
módulo, consumidores idempotentes com retentativas, timeline do pedido,
mensagens que falharam no backoffice) e testada de ponta a ponta:
`Tests/OrderCore.IntegrationTests/Messaging` e
`Orders/OrderTimelineTests`. A extração opcional de `Payments` para
`PayCore` ainda não foi implementada — entra conforme as fases descritas
em [Arquitetura](#arquitetura), com ADR próprio quando a decisão for
tomada.

## Filosofia

O objetivo do OrderCore não é parecer complexo. É ser tecnicamente
justificável. Tecnologia não entra no projeto para aumentar a lista de
tecnologias usadas — entra quando um problema real a justifica.
