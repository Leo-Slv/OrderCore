using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    [Fact]
    public void Unauthorized_maps_to_401_with_its_own_code()
    {
        var (status, code, _) = ApiExceptionHandler.Classify(new UnauthorizedException("invalid_credentials", "nope"));

        status.Should().Be(StatusCodes.Status401Unauthorized);
        code.Should().Be("invalid_credentials");
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, "validation_error")]
    [InlineData(StatusCodes.Status401Unauthorized, "unauthenticated")]
    [InlineData(StatusCodes.Status403Forbidden, "forbidden")]
    [InlineData(StatusCodes.Status404NotFound, "not_found")]
    [InlineData(StatusCodes.Status503ServiceUnavailable, "internal_error")]
    public void Framework_problem_details_get_a_default_code(int status, string expectedCode)
    {
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = new ProblemDetails { Status = status },
        };

        ProblemDetailsDefaults.AddDefaultCode(context);

        context.ProblemDetails.Extensions[ApiExceptionHandler.ErrorCodeExtension].Should().Be(expectedCode);
    }

    [Fact]
    public void A_code_already_set_is_kept()
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status404NotFound };
        problem.Extensions[ApiExceptionHandler.ErrorCodeExtension] = "order_not_found";

        ProblemDetailsDefaults.AddDefaultCode(new ProblemDetailsContext { HttpContext = new DefaultHttpContext(), ProblemDetails = problem });

        problem.Extensions[ApiExceptionHandler.ErrorCodeExtension].Should().Be("order_not_found");
    }
}
