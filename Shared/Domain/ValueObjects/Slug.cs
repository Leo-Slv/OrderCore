using System.Globalization;
using System.Text;
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

    /// <summary>
    /// Whether <paramref name="value"/> would be accepted by <see cref="Create"/>.
    /// For untrusted input (e.g. a route parameter) where a bad format
    /// means "no such resource", not a validation error.
    /// </summary>
    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value) && SlugFormat().IsMatch(value);

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

    /// <summary>
    /// Derives a <see cref="Slug"/> from free text (e.g. a product or
    /// category name) — 03-catalog.md's CreateProductCommand/
    /// CreateCategoryCommand have no slug field of their own, so the use
    /// case is expected to generate one from the name instead of asking
    /// the caller for it.
    /// </summary>
    public static Slug GenerateFrom(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required.", nameof(text));
        }

        var withoutDiacritics = RemoveDiacritics(text.Trim().ToLowerInvariant());
        var slugified = NonSlugCharacters().Replace(withoutDiacritics, "-");
        var collapsed = RepeatedHyphens().Replace(slugified, "-").Trim('-');

        if (collapsed.Length == 0)
        {
            throw new ArgumentException("Text does not contain any character usable in a slug.", nameof(text));
        }

        return Create(collapsed);
    }

    public override string ToString() => Value;

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugFormat();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedHyphens();
}
