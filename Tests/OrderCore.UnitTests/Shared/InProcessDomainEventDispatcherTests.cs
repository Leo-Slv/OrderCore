using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Infrastructure;
using Xunit;

namespace OrderCore.UnitTests.Shared;

public sealed class InProcessDomainEventDispatcherTests
{
    private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class RecordingHandler : IDomainEventHandler<TestDomainEvent>
    {
        public List<TestDomainEvent> Handled { get; } = [];

        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            Handled.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchAsync_invokes_every_handler_registered_for_the_event_type()
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var dispatcher = new InProcessDomainEventDispatcher(provider);

        var domainEvent = new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);
        await dispatcher.DispatchAsync([domainEvent], CancellationToken.None);

        handler.Handled.Should().ContainSingle().Which.Should().Be(domainEvent);
    }

    [Fact]
    public async Task DispatchAsync_does_nothing_when_no_handler_is_registered()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new InProcessDomainEventDispatcher(provider);

        var act = () => dispatcher.DispatchAsync(
            [new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow)], CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
