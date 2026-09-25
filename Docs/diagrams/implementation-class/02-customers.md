# Módulo Customers

Cadastro de clientes, endereços salvos e métodos de pagamento tokenizados. Base: [Shared kernel](01-shared-kernel.md) (`AggregateRoot<Guid>`, `Address`).

Diferente da maioria dos diagramas desta pasta, este módulo já está **implementado** de ponta a ponta (Domain, Application, Infrastructure/EF Core e Presentation) — não é mais um blueprint futuro. `CustomersEndpoints` (minimal API estática) foi substituído por `CustomersController` ([ApiController]) ao implementar, para seguir a mesma convenção já usada por `AuditLogsController` em vez de introduzir um segundo padrão de Presentation (seção 33 do contexto do projeto). Outras diferenças entre este diagrama e o código, todas documentadas nos comentários das classes correspondentes:

- `Customer.Create`/`CustomerPaymentMethod.Create` recebem um `now` explícito (como `Order.Create`), já que `CreatedAt`/`UpdatedAt` precisam de um valor e o domínio não deve ler o relógio sozinho.
- `AddCustomerAddressCommand`/`CustomerAddressRequest` incluem todos os campos de `Address` (não só Street/City/State/PostalCode), já que `Address.Create` exige todos eles.
- `Customer`, `CustomerAddress` e `CustomerPaymentMethod` ganharam um factory `internal static Rehydrate(...)`, distinto de `Create`, para que `CustomerMapper` reconstrua o agregado a partir do banco sem re-levantar `CustomerRegistered`.
- `CustomerAddressPersistenceModel` guarda todos os campos de `Address` (não só Street/City/State/PostalCode) pelo mesmo motivo.
- **Endereços no checkout** (MVP do storefront, `Docs/specs/storefront/storefront-api-mvp.md`, etapa 4): `CustomerAddressResponse` devolve o endereço completo (antes só `Label`/`City`/`IsDefaultShipping`), para o checkout poder mostrar e escolher um endereço salvo; `GetCustomerAddressUseCase` resolve um endereço de um cliente para o checkout do Orders — `customer_not_found`, `address_not_found` (também para o endereço de outro cliente) e `customer_inactive` para cliente desativado.
- `CustomerAddressPersistenceModel.Id`/`CustomerPaymentMethodPersistenceModel.Id` usam `ValueGeneratedNever()`: o id vem do domínio, e sem isso o EF Core tratava um endereço novo num cliente já salvo como linha existente (UPDATE que não afetava nada, 409 na API).
- **Conta do cliente** (V2, `Docs/specs/identity/authentication-and-account.md`): o `Customer` não guarda mais senha. As credenciais ficam no `UserAccount` do módulo Identity ([08-identity.md](08-identity.md)), que cria o cliente no cadastro chamando `RegisterCustomerUseCase` e guarda o id dele. O `POST customers` público foi removido: todo cliente nasce de um cadastro (`POST auth/sign-up`). O próprio cliente cuida dos seus dados em `MyAccountController` (`customers/me`: perfil e endereços, com `UpdateAddress`, `RemoveCustomerAddressUseCase` e `SetDefaultAddressUseCase`), sempre com o cliente vindo do token (`ICurrentUser`); um endereço de outro cliente responde 404, como um que não existe. `CustomersController` (por id) ficou só para admin. `AddCustomerAddressRequest` virou `CustomerAddressRequest`, usado para criar e editar.

```mermaid

classDiagram
    direction LR

    class AggregateRoot~TId~ {
        <<external>>
    }

    class ICurrentUser {
        <<external>>
        <<interface>>
    }

    class Address {
        <<external>>
    }

    %% OrderCore.Api.Modules.Customers.Domain.Entities
    class Customer {
        +string Name
        +string Email
        +string? Phone
        +string? DocumentNumber
        +bool Active
        +DateTimeOffset? EmailVerifiedAt
        +DateTimeOffset CreatedAt
        +DateTimeOffset UpdatedAt
        +IReadOnlyCollection~CustomerAddress~ Addresses
        +IReadOnlyCollection~CustomerPaymentMethod~ PaymentMethods
        +Create(string name, string email, DateTimeOffset now)$ Customer
        +UpdateProfile(string name, string? phone, string? documentNumber) void
        +VerifyEmail(DateTimeOffset now) void
        +Activate() void
        +Deactivate() void
        +AddAddress(CustomerAddress address) void
        +UpdateAddress(Guid addressId, string label, string recipientName, string? phone, Address address, DateTimeOffset now) void
        +RemoveAddress(Guid addressId) void
        +SetDefaultShippingAddress(Guid addressId) void
        +SetDefaultBillingAddress(Guid addressId) void
        +AddPaymentMethod(CustomerPaymentMethod method) void
        +RemovePaymentMethod(Guid paymentMethodId) void
    }

    class CustomerAddress {
        +string Label
        +string RecipientName
        +string? Phone
        +Address Address
        +bool IsDefaultShipping
        +bool IsDefaultBilling
        +DateTimeOffset CreatedAt
        +DateTimeOffset UpdatedAt
        +Create(string label, string recipientName, string? phone, Address address, DateTimeOffset now)$ CustomerAddress
        +UpdateContactInfo(string recipientName, string? phone) void
        +MarkAsDefaultShipping() void
        +UnmarkAsDefaultShipping() void
        +MarkAsDefaultBilling() void
        +UnmarkAsDefaultBilling() void
    }

    class CustomerPaymentMethod {
        +string Provider
        +string ProviderCustomerReference
        +string Brand
        +string Last4Digits
        +int ExpiryMonth
        +int ExpiryYear
        +bool IsDefault
        +DateTimeOffset CreatedAt
        +DateTimeOffset UpdatedAt
        +Create(string provider, string providerCustomerReference, string brand, string last4Digits, int expiryMonth, int expiryYear)$ CustomerPaymentMethod
        +IsExpired(DateTimeOffset now) bool
        +MarkAsDefault() void
        +UnmarkAsDefault() void
    }


    %% OrderCore.Api.Modules.Customers.Domain.Events
    class CustomerRegistered {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid CustomerId
        +string Email
    }

    class CustomerAddressAdded {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid CustomerId
        +Guid AddressId
    }


    %% OrderCore.Api.Modules.Customers.Application.Contracts
    class ICustomerRepository {
        <<interface>>
        +GetByIdAsync(Guid customerId) Task~Customer?~
        +GetByEmailAsync(string email) Task~Customer?~
        +AddAsync(Customer customer) Task
        +SaveChangesAsync() Task
    }


    %% OrderCore.Api.Modules.Customers.Application.DTOs
    class RegisterCustomerCommand {
        +string Name
        +string Email
        +string? Phone
        +string? DocumentNumber
    }

    class AddCustomerAddressCommand {
        +Guid CustomerId
        +string Label
        +string RecipientName
        +Address Address
    }

    class UpdateCustomerAddressCommand {
        +Guid CustomerId
        +Guid AddressId
        +string Label
        +string RecipientName
        +string? Phone
        +Address Address
    }

    class DefaultAddressKind {
        <<enumeration>>
        Shipping
        Billing
    }

    class CustomerOutput {
        +Guid Id
        +string Name
        +string Email
        +bool Active
    }


    %% OrderCore.Api.Modules.Customers.Application.UseCases
    class RegisterCustomerUseCase {
        -ICustomerRepository customers
        +ExecuteAsync(RegisterCustomerCommand command) Task~CustomerOutput~
    }

    class UpdateCustomerProfileUseCase {
        -ICustomerRepository customers
        +ExecuteAsync(Guid customerId, string name, string? phone) Task~CustomerOutput~
    }

    class AddCustomerAddressUseCase {
        -ICustomerRepository customers
        +ExecuteAsync(AddCustomerAddressCommand command) Task~Guid~
    }

    class ListCustomerAddressesUseCase {
        -ICustomerRepository customers
        +ExecuteAsync(Guid customerId) Task~IReadOnlyList~CustomerAddress~~
    }

    class GetCustomerByIdUseCase {
        -ICustomerRepository customers
        +ExecuteAsync(Guid customerId) Task~CustomerOutput~
    }

    class GetCustomerAddressUseCase {
        -ICustomerRepository customers
        +ExecuteAsync(Guid customerId, Guid addressId) Task~CustomerAddress~
    }

    class UpdateCustomerAddressUseCase {
        -ICustomerRepository customers
        -TimeProvider timeProvider
        +ExecuteAsync(UpdateCustomerAddressCommand command) Task~CustomerAddress~
    }

    class RemoveCustomerAddressUseCase {
        -ICustomerRepository customers
        +ExecuteAsync(Guid customerId, Guid addressId) Task
    }

    class SetDefaultAddressUseCase {
        -ICustomerRepository customers
        +ExecuteAsync(Guid customerId, Guid addressId, DefaultAddressKind kind) Task
    }


    %% OrderCore.Api.Modules.Customers
    class CustomersDependencyInjection {
        <<static>>
        +AddCustomersModule(IServiceCollection services)$ IServiceCollection
    }


    %% OrderCore.Api.Modules.Customers.Infrastructure.Persistence
    class CustomerPersistenceModel {
        +Guid Id
        +string Name
        +string Email
        +string? Phone
        +bool Active
        +DateTimeOffset CreatedAt
        +DateTimeOffset UpdatedAt
        +ICollection~CustomerAddressPersistenceModel~ Addresses
        +ICollection~CustomerPaymentMethodPersistenceModel~ PaymentMethods
    }

    class CustomerAddressPersistenceModel {
        +Guid Id
        +Guid CustomerId
        +string Label
        +string Street
        +string City
        +string State
        +string PostalCode
        +bool IsDefaultShipping
        +bool IsDefaultBilling
    }

    class CustomerPaymentMethodPersistenceModel {
        +Guid Id
        +Guid CustomerId
        +string Provider
        +string Brand
        +string Last4Digits
        +bool IsDefault
    }

    class CustomerMapper {
        +ToDomain(CustomerPersistenceModel model) Customer
        +ToPersistence(Customer domain) CustomerPersistenceModel
        +ApplyChanges(Customer domain, CustomerPersistenceModel model) void
    }

    class CustomersDbContext {
        +DbSet~CustomerPersistenceModel~ Customers
        +DbSet~CustomerAddressPersistenceModel~ CustomerAddresses
        +DbSet~CustomerPaymentMethodPersistenceModel~ CustomerPaymentMethods
        +SaveChangesAsync() Task~int~
    }

    class EfCustomerRepository {
        -CustomersDbContext dbContext
        -CustomerMapper mapper
    }


    %% OrderCore.Api.Modules.Customers.Presentation
    class CustomersController {
        <<admin>>
        -GetCustomerByIdUseCase getCustomerByIdUseCase
        -AddCustomerAddressUseCase addCustomerAddressUseCase
        -ListCustomerAddressesUseCase listCustomerAddressesUseCase
        +GetByIdAsync(Guid id) Task~ActionResult~CustomerResponse~~
        +AddAddressAsync(Guid id, CustomerAddressRequest request) Task~IActionResult~
        +ListAddressesAsync(Guid id) Task~ActionResult~IReadOnlyList~CustomerAddressResponse~~~
    }

    class MyAccountController {
        <<customer>>
        -GetCustomerByIdUseCase getCustomer
        -UpdateCustomerProfileUseCase updateProfile
        -ListCustomerAddressesUseCase listAddresses
        -AddCustomerAddressUseCase addAddress
        -GetCustomerAddressUseCase getAddress
        -UpdateCustomerAddressUseCase updateAddress
        -RemoveCustomerAddressUseCase removeAddress
        -SetDefaultAddressUseCase setDefaultAddress
        -ICurrentUser currentUser
        +GetProfileAsync() Task~ActionResult~CustomerResponse~~
        +UpdateProfileAsync(UpdateProfileRequest request) Task~ActionResult~CustomerResponse~~
        +ListAddressesAsync() Task~ActionResult~IReadOnlyList~CustomerAddressResponse~~~
        +AddAddressAsync(CustomerAddressRequest request) Task~ActionResult~CustomerAddressResponse~~
        +UpdateAddressAsync(Guid addressId, CustomerAddressRequest request) Task~ActionResult~CustomerAddressResponse~~
        +RemoveAddressAsync(Guid addressId) Task~IActionResult~
        +SetDefaultShippingAddressAsync(Guid addressId) Task~IActionResult~
        +SetDefaultBillingAddressAsync(Guid addressId) Task~IActionResult~
    }

    class UpdateProfileRequest {
        +string Name
        +string? Phone
    }

    class CustomerAddressRequest {
        +string Label
        +string RecipientName
        +string Street
        +string City
        +string State
        +string PostalCode
    }

    class CustomerResponse {
        +Guid Id
        +string Name
        +string Email
        +bool Active
    }

    class CustomerAddressResponse {
        +Guid Id
        +string Label
        +string RecipientName
        +string? Phone
        +string Street
        +string Number
        +string? Complement
        +string Neighborhood
        +string City
        +string State
        +string PostalCode
        +string Country
        +bool IsDefaultShipping
        +bool IsDefaultBilling
    }

    class CustomerPresenter {
        +ToCommand(Guid customerId, CustomerAddressRequest request) AddCustomerAddressCommand
        +ToCommand(Guid customerId, Guid addressId, CustomerAddressRequest request) UpdateCustomerAddressCommand
        +ToResponse(CustomerOutput output) CustomerResponse
        +ToResponse(CustomerAddress address) CustomerAddressResponse
    }


    AggregateRoot~TId~ <|-- Customer
    IDomainEvent <|.. CustomerRegistered
    IDomainEvent <|.. CustomerAddressAdded
    Customer "1" *-- "0..*" CustomerAddress
    Customer "1" *-- "0..*" CustomerPaymentMethod
    CustomerAddress --> Address
    Customer ..> CustomerRegistered : raises
    Customer ..> CustomerAddressAdded : raises

    RegisterCustomerUseCase --> ICustomerRepository
    UpdateCustomerProfileUseCase --> ICustomerRepository
    AddCustomerAddressUseCase --> ICustomerRepository
    GetCustomerByIdUseCase --> ICustomerRepository
    ListCustomerAddressesUseCase --> ICustomerRepository
    GetCustomerAddressUseCase --> ICustomerRepository
    UpdateCustomerAddressUseCase --> ICustomerRepository
    RemoveCustomerAddressUseCase --> ICustomerRepository
    SetDefaultAddressUseCase --> ICustomerRepository
    SetDefaultAddressUseCase ..> DefaultAddressKind

    ICustomerRepository <|.. EfCustomerRepository
    EfCustomerRepository --> CustomersDbContext
    EfCustomerRepository --> CustomerMapper
    CustomerMapper --> CustomerPersistenceModel
    CustomerMapper --> Customer
    CustomersDbContext --> CustomerPersistenceModel
    CustomersDbContext --> CustomerAddressPersistenceModel
    CustomersDbContext --> CustomerPaymentMethodPersistenceModel

    CustomersDependencyInjection --> RegisterCustomerUseCase : registers
    CustomersDependencyInjection --> ICustomerRepository : registers

    CustomersController --> GetCustomerByIdUseCase
    CustomersController --> AddCustomerAddressUseCase
    CustomersController --> ListCustomerAddressesUseCase
    CustomersController --> CustomerPresenter
    MyAccountController --> GetCustomerByIdUseCase
    MyAccountController --> UpdateCustomerProfileUseCase
    MyAccountController --> ListCustomerAddressesUseCase
    MyAccountController --> AddCustomerAddressUseCase
    MyAccountController --> GetCustomerAddressUseCase
    MyAccountController --> UpdateCustomerAddressUseCase
    MyAccountController --> RemoveCustomerAddressUseCase
    MyAccountController --> SetDefaultAddressUseCase
    MyAccountController --> ICurrentUser : customer from the token
    MyAccountController --> CustomerPresenter
    CustomerPresenter --> CustomerResponse
    CustomerPresenter --> CustomerAddressResponse

```

## Comportamento das entidades filhas

`CustomerAddress` e `CustomerPaymentMethod` deixaram de ser bags de propriedades: `Customer.SetDefaultShippingAddress`/`SetDefaultBillingAddress`/`AddPaymentMethod` delegam para `MarkAsDefaultShipping`/`MarkAsDefaultBilling`/`MarkAsDefault` no filho correspondente (e desmarcam o anterior), em vez de mexer nos campos diretamente a partir do agregado. `CustomerPaymentMethod.IsExpired(now)` existe porque comparar mês/ano de expiração é uma regra de negócio, não um detalhe de apresentação — não deveria ser recalculada em cada lugar que precisa checar isso.

## Paridade com `Docs/database/OrderCore_Modelagem_Banco_Backend.docx`

`UpdatedAt` (em `CustomerAddress`) já existia no documento de modelagem de banco (seção 4.2) mas não tinha chegado a este diagrama — adicionado, junto com o parâmetro `now` que faltava no `Create`. O `PasswordHash` que a seção 4.1 punha em `Customer` saiu daqui na V2: a senha passou para o `UserAccount` do Identity (tabela `user_accounts`). `CustomerPaymentMethod.UpdatedAt` não existia em nenhum dos dois documentos — foi acrescentado em ambos por consistência com toda entidade mutável do sistema (`Customer`, `CustomerAddress`, `Category`, `Product` etc. já rastreiam `updated_at`); mantido como responsabilidade da camada de persistência, sem entrar na assinatura de `MarkAsDefault`/`UnmarkAsDefault`.

## Consumido por outros módulos

- Nenhum outro módulo referencia `Customer` diretamente — `Order.CustomerId` guarda apenas o id (ver [05-orders.md](05-orders.md)), seguindo a mesma regra de isolamento que já existe entre Orders e Payments no código atual.
- **Orders** chama `GetCustomerAddressUseCase` de dentro de um `CustomerDirectoryAdapter` (implementa o `ICustomerDirectory` do Orders) no checkout; só o value object `Address` (shared kernel) atravessa a fronteira — ver [05-orders.md](05-orders.md).
- **Identity** chama `RegisterCustomerUseCase` de dentro de um `CustomerRegistryAdapter` (implementa o `ICustomerRegistry` do Identity) no cadastro, e guarda só o id do cliente criado — ver [08-identity.md](08-identity.md).
