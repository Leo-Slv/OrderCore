using FluentAssertions;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using Xunit;

namespace OrderCore.UnitTests.Customers;

public sealed class RegisterCustomerUseCaseTests
{
    private static RegisterCustomerCommand Command(string email = "jane@example.com") =>
        new("Jane Doe", email, Phone: null, DocumentNumber: null, PasswordHash: "hashed-password");

    [Fact]
    public async Task ExecuteAsync_registers_a_new_customer()
    {
        var useCase = new RegisterCustomerUseCase(new FakeCustomerRepository(), TimeProvider.System);

        var output = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        output.Name.Should().Be("Jane Doe");
        output.Active.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_duplicate_email()
    {
        var repository = new FakeCustomerRepository();
        var useCase = new RegisterCustomerUseCase(repository, TimeProvider.System);
        await useCase.ExecuteAsync(Command(), CancellationToken.None);

        var act = () => useCase.ExecuteAsync(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
