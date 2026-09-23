using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

public sealed class CategoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_starts_active()
    {
        var category = Category.Create("Electronics", Slug.Create("electronics"), null, null, Now);

        category.Active.Should().BeTrue();
    }

    [Fact]
    public void Rename_updates_name_and_slug()
    {
        var category = Category.Create("Electronics", Slug.Create("electronics"), null, null, Now);

        category.Rename("Consumer Electronics", Slug.Create("consumer-electronics"));

        category.Name.Should().Be("Consumer Electronics");
        category.Slug.Should().Be(Slug.Create("consumer-electronics"));
    }

    [Fact]
    public void Deactivate_then_Activate_toggles_Active()
    {
        var category = Category.Create("Electronics", Slug.Create("electronics"), null, null, Now);

        category.Deactivate();
        category.Active.Should().BeFalse();

        category.Activate();
        category.Active.Should().BeTrue();
    }
}
