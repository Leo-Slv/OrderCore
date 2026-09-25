# Diagrama de implementação — OrderCore

> Este é o blueprint de arquitetura que o OrderCore deve seguir. Um único `classDiagram` Mermaid com ~200 classes não renderiza na maioria das ferramentas (GitHub, VS Code, Mermaid Live), então o diagrama foi dividido em um arquivo por módulo — cada um pequeno o suficiente para carregar, mas o conteúdo é o mesmo (nada foi cortado).

Comece por [implementation-class/00-overview.md](implementation-class/00-overview.md) — o mapa de dependências entre módulos — e depois entre no módulo que precisar:

1. [Shared kernel](implementation-class/01-shared-kernel.md)
2. [Customers](implementation-class/02-customers.md)
3. [Catalog](implementation-class/03-catalog.md)
4. [Inventory](implementation-class/04-inventory.md)
5. [Orders](implementation-class/05-orders.md)
6. [Payments](implementation-class/06-payments.md)
7. [AuditLogs](implementation-class/07-auditlogs.md) — módulo técnico/transversal.
8. [Identity](implementation-class/08-identity.md) — módulo técnico/transversal: contas, credenciais e sessões.

Todos os módulos já estão implementados; cada diagrama reflete o código atual e lista, no topo, onde ele difere do desenho original (incluindo o que o MVP do storefront acrescentou — ver `Docs/specs/storefront/storefront-api-mvp.md`).

Quando um módulo depende de classes de outro (ex.: o `ProductCatalogAdapter` do Orders chamando `IProductRepository` do Catalog), essas classes aparecem como um "stub" `<<external>>` só com a assinatura relevante — o detalhe completo mora no arquivo do módulo dono, referenciado logo abaixo do diagrama.
