using System.Text.RegularExpressions;

namespace OrderCore.Api.Shared.Domain.ValueObjects;

/// <summary>
/// URL-safe identifier (lowercase letters, digits and single hyphens, no
/// leading/trailing/consecutive hyphens). Used by <c>Product</c> and
/// <c>Category</c> (Catalog) — see 01-shared-kernel.md.
/// </summary>
public sealed partial record Slug
{
    public string Value { get; }

    private Slug(string value)
    {
        Value = value;
    }

    public static Slug Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Slug value is required.", nameof(value));
        }

        if (!SlugFormat().IsMatch(value))
        {
            throw new ArgumentException(
                "Slug must contain only lowercase letters, digits and single hyphens, with no leading, trailing or consecutive hyphens.",
                nameof(value));
        }

        return new Slug(value);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugFormat();
}
