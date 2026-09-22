# Diagrama de implementação — OrderCore

> Este é o blueprint de arquitetura que o OrderCore deve seguir. Um único `classDiagram` Mermaid com ~200 classes não renderiza na maioria das ferramentas (GitHub, VS Code, Mermaid Live), então o diagrama foi dividido em um arquivo por módulo — cada um pequeno o suficiente para carregar, mas o conteúdo é o mesmo (nada foi cortado).

Comece por [implementation-class-diagram/00-overview.md](implementation-class-diagram/00-overview.md) — o mapa de dependências entre módulos — e depois entre no módulo que precisar:

1. [Shared kernel](implementation-class-diagram/01-shared-kernel.md)
2. [Customers](implementation-class-diagram/02-customers.md)
3. [Catalog](implementation-class-diagram/03-catalog.md)
4. [Inventory](implementation-class-diagram/04-inventory.md)
5. [Orders](implementation-class-diagram/05-orders.md)
6. [Payments](implementation-class-diagram/06-payments.md)

Quando um módulo depende de classes de outro (ex.: o `ProductCatalogAdapter` do Orders chamando `IProductRepository` do Catalog), essas classes aparecem como um "stub" `<<external>>` só com a assinatura relevante — o detalhe completo mora no arquivo do módulo dono, referenciado logo abaixo do diagrama.
