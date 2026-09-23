# ADR-001: Iniciar como Modular Monolith

**Status:** Accepted

**Data:** 2026-09-22

## Context

O OrderCore precisa representar um cenário realista de processamento de
pedidos — clientes, catálogo, estoque, pedidos, pagamentos — com fronteiras
de domínio claras, preparado para uma eventual extração do contexto de
pagamentos para um serviço independente (`PayCore`).

Duas opções extremas estavam disponíveis desde o início:

1. Um monólito não modularizado, com tudo em um único projeto/camada.
2. Uma arquitetura de microservices desde o primeiro commit, com
   `OrderCore` e `PayCore` já como serviços separados, comunicando-se via
   rede/mensageria.

Nenhuma das duas atende ao objetivo do projeto. A primeira não demonstra
capacidade de definir bounded contexts nem prepara o terreno para extração
futura. A segunda introduz complexidade operacional (dois bancos, rede,
mensageria, consistência eventual, observabilidade distribuída) antes de
existir qualquer prova de que as fronteiras de domínio estão corretas —
exatamente o tipo de "microservice artificial" que a seção 44 do contexto
do projeto pede para evitar.

## Decision

O OrderCore inicia como um **Modular Monolith**: um único processo e uma
única solution (`OrderCore.sln`), mas organizado por módulos de negócio
(`Orders`, `Customers`, `Catalog`, `Inventory`, `Payments`) que não acessam
as estruturas internas uns dos outros diretamente (ver seção 7 do contexto
do projeto). O módulo `Payments` recebe atenção especial: é tratado como um
bounded context logicamente isolado desde o início, mesmo compartilhando o
mesmo processo e banco que os demais módulos por enquanto.

A extração para `PayCore` (seção 22) só deve ser avaliada depois que essas
fronteiras estiverem comprovadamente estáveis — não como um passo
automático deste ADR.

## Alternatives

- **Monólito não modularizado** — descartado: dificulta identificar quem é
  responsável por cada regra e tende a acoplar módulos silenciosamente ao
  longo do tempo.
- **Microservices desde o início** — descartado: adiciona custo
  operacional (múltiplos bancos, rede, mensageria, tracing distribuído)
  sem que exista ainda evidência de que as fronteiras de domínio
  desenhadas estão corretas. Errar uma fronteira de bounded context é
  barato dentro de um monólito modular e caro entre dois serviços com
  bancos próprios.
- **Monólito modular sem isolamento explícito de Payments** — descartado:
  adiar a disciplina de isolamento do módulo de pagamentos para o momento
  da extração tende a gerar acoplamento acumulado (referências diretas a
  entidades de `Payments`, dependências implícitas) que tornaria a
  extração futura uma reescrita, não uma evolução.

## Consequences

- Um único deploy, um único banco (inicialmente) e um ciclo de
  desenvolvimento mais simples que microservices, o que é apropriado para
  o estágio atual do projeto.
- Em contrapartida, a modularização precisa ser mantida por disciplina de
  código e validada automaticamente (`OrderCore.ArchitectureTests`), já
  que nada na infraestrutura impede, por si só, que um módulo acesse
  diretamente o `DbContext` ou as entidades de outro.
- A extração de `Payments` para `PayCore` (Fase 7 da seção 3) é uma
  decisão futura e condicional, a ser registrada em um ADR próprio
  (ADR-010) quando — e se — o bounded context estiver suficientemente
  isolado para justificá-la.
