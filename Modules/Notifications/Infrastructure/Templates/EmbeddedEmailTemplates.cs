using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using OrderCore.Api.Modules.Notifications.Application.Contracts;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Templates;

/// <summary>
/// The templates in this folder, embedded in the assembly: for each name, a
/// <c>&lt;name&gt;.html</c> fragment and a <c>&lt;name&gt;.txt</c> whose
/// first line is <c>Subject: …</c>, both wrapped in the shared
/// <c>_layout</c>. <c>{{placeholder}}</c>s are replaced by the given values —
/// HTML-encoded in the HTML part, line breaks removed in the subject. A
/// placeholder without a value is an error, so a half-filled e-mail never
/// goes out.
/// </summary>
public sealed partial class EmbeddedEmailTemplates : IEmailTemplates
{
    private const string ResourcePrefix = "EmailTemplates/";
    private const string SubjectHeader = "Subject:";
    private const string TagHeader = "Tag:";
    private const string PreheaderHeader = "Preheader:";
    private const string LayoutName = "_layout";

    // Escapes markup (<, >, &, quotes) but keeps accented letters as they are.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly IReadOnlyDictionary<string, string> _resources;
    private readonly IReadOnlyDictionary<string, string> _brand;

    public EmbeddedEmailTemplates(EmailBrandOptions? brand = null)
    {
        brand ??= new EmailBrandOptions();
        _brand = new Dictionary<string, string>
        {
            ["brandName"] = brand.Name,
            ["brandWordmark"] = string.IsNullOrWhiteSpace(brand.Wordmark) ? brand.Name : brand.Wordmark,
            ["brandWordmarkAccent"] = brand.WordmarkAccent,
            ["brandColor"] = brand.AccentColor,
            ["brandSoftColor"] = brand.AccentSoftColor,
            ["brandFooter"] = brand.Footer,
        };

        var assembly = typeof(EmbeddedEmailTemplates).Assembly;
        _resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToDictionary(name => name[ResourcePrefix.Length..], name => Read(assembly, name));
    }

    /// <summary>Every template name available (layout excluded).</summary>
    public IEnumerable<string> Names => _resources.Keys
        .Where(file => file.EndsWith(".txt", StringComparison.Ordinal) && !file.StartsWith(LayoutName, StringComparison.Ordinal))
        .Select(file => file[..^".txt".Length]);

    public RenderedEmail Render(string template, IReadOnlyDictionary<string, string> values)
    {
        if (!_resources.TryGetValue($"{template}.html", out var html) || !_resources.TryGetValue($"{template}.txt", out var text))
        {
            throw new ArgumentException($"Unknown e-mail template '{template}'.", nameof(template));
        }

        // The brand's values are available to every template and to the layout.
        var all = new Dictionary<string, string>(_brand);
        foreach (var (key, value) in values)
        {
            all[key] = value;
        }

        var (headers, textBody) = SplitHeaders(template, text);
        var subject = Fill(headers[SubjectHeader], template, all, value => value.ReplaceLineEndings(" "));
        var tag = Fill(headers.GetValueOrDefault(TagHeader, string.Empty), template, all, value => value.ReplaceLineEndings(" "));
        var preheader = Fill(headers.GetValueOrDefault(PreheaderHeader, string.Empty), template, all, value => value.ReplaceLineEndings(" "));

        var htmlContent = Fill(html, template, all, Html.Encode);
        // One pass over the layout, so nothing inserted is read as a placeholder again.
        var layoutHtml = BrandValues(Html.Encode);
        layoutHtml["subject"] = Html.Encode(subject);
        layoutHtml["tag"] = Html.Encode(tag);
        layoutHtml["preheader"] = Html.Encode(preheader);
        layoutHtml["content"] = htmlContent.TrimEnd();
        var htmlBody = Fill(_resources[$"{LayoutName}.html"], LayoutName, layoutHtml, value => value);

        var textContent = Fill(textBody, template, all, value => value);
        var layoutText = BrandValues(value => value);
        layoutText["content"] = textContent.Trim();
        var textFull = Fill(_resources[$"{LayoutName}.txt"], LayoutName, layoutText, value => value);

        return new RenderedEmail(subject, htmlBody, textFull);
    }

    private Dictionary<string, string> BrandValues(Func<string, string> encode) =>
        _brand.ToDictionary(pair => pair.Key, pair => encode(pair.Value));

    /// <summary>
    /// The header lines at the top of a <c>.txt</c>: <c>Subject:</c> (required, first), then optionally
    /// <c>Tag:</c> and <c>Preheader:</c>; the rest is the plain-text body.
    /// </summary>
    private static (Dictionary<string, string> Headers, string Body) SplitHeaders(string template, string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (!lines[0].StartsWith(SubjectHeader, StringComparison.Ordinal))
        {
            throw new ArgumentException($"E-mail template '{template}.txt' must start with a '{SubjectHeader}' line.", nameof(template));
        }

        var headers = new Dictionary<string, string> { [SubjectHeader] = lines[0][SubjectHeader.Length..].Trim() };
        var index = 1;
        for (; index < lines.Length; index++)
        {
            var header = new[] { TagHeader, PreheaderHeader }.FirstOrDefault(name => lines[index].StartsWith(name, StringComparison.Ordinal));
            if (header is null)
            {
                break;
            }

            headers[header] = lines[index][header.Length..].Trim();
        }

        return (headers, string.Join('\n', lines[index..]));
    }

    private static string Fill(string content, string template, IReadOnlyDictionary<string, string> values, Func<string, string> encode) =>
        Placeholder().Replace(content, match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var value)
                ? encode(value)
                : throw new ArgumentException($"E-mail template '{template}' needs a value for '{key}'.", nameof(values));
        });

    private static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9]*)\s*\}\}")]
    private static partial Regex Placeholder();
}
