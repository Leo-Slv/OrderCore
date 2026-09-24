using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using OrderCore.Api.Shared.Presentation.ExceptionHandling;
using Xunit;

namespace OrderCore.UnitTests.Shared;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public void Domain_rule_violation_maps_to_400_with_its_own_code()
    {
        var (status, code, _) = ApiExceptionHandler.Classify(new DomainRuleViolationException("invalid_order_state", "nope"));

        status.Should().Be(StatusCodes.Status400BadRequest);
        code.Should().Be("invalid_order_state");
    }

    [Fact]
    public void Not_found_maps_to_404_with_its_own_code()
    {
        var (status, code, _) = ApiExceptionHandler.Classify(new NotFoundException("order_not_found", "nope"));

        status.Should().Be(StatusCodes.Status404NotFound);
        code.Should().Be("order_not_found");
    }

    [Fact]
    public void Conflict_maps_to_409_with_its_own_code()
    {
        var (status, code, _) = ApiExceptionHandler.Classify(new ConflictException("insufficient_stock", "nope"));

        status.Should().Be(StatusCodes.Status409Conflict);
        code.Should().Be("insufficient_stock");
    }

    [Fact]
    public void Stock_concurrency_conflict_maps_to_409_concurrency_conflict()
    {
        var (status, code, _) = ApiExceptionHandler.Classify(new StockConcurrencyConflictException("race", new Exception()));

        status.Should().Be(StatusCodes.Status409Conflict);
        code.Should().Be(ApiExceptionHandler.ConcurrencyConflictCode);
    }

    [Fact]
    public void Ef_concurrency_exception_maps_to_409_concurrency_conflict()
    {
        var (status, code, _) = ApiExceptionHandler.Classify(new DbUpdateConcurrencyException("race"));

        status.Should().Be(StatusCodes.Status409Conflict);
        code.Should().Be(ApiExceptionHandler.ConcurrencyConflictCode);
    }

    [Fact]
    public void Argument_exceptions_from_factories_map_to_400_validation_error()
    {
        var (status, code, _) = ApiExceptionHandler.Classify(new ArgumentOutOfRangeException("amount"));

        status.Should().Be(StatusCodes.Status400BadRequest);
        code.Should().Be(ApiExceptionHandler.ValidationErrorCode);
    }

    [Fact]
    public void Anything_else_maps_to_500_internal_error()
    {
        var (status, code, _) = ApiExceptionHandler.Classify(new InvalidOperationException("bug"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
        code.Should().Be(ApiExceptionHandler.InternalErrorCode);
    }
}
