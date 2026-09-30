using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
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
    private const string LayoutName = "_layout";

    private readonly IReadOnlyDictionary<string, string> _resources;

    public EmbeddedEmailTemplates()
    {
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

        var (subjectTemplate, textBody) = SplitSubject(template, text);
        var subject = Fill(subjectTemplate, template, values, value => value.ReplaceLineEndings(" "));

        var htmlContent = Fill(html, template, values, WebUtility.HtmlEncode);
        // One pass over the layout, so nothing inserted is read as a placeholder again.
        var htmlBody = Fill(
            _resources[$"{LayoutName}.html"],
            LayoutName,
            new Dictionary<string, string> { ["subject"] = WebUtility.HtmlEncode(subject), ["content"] = htmlContent.TrimEnd() },
            value => value);

        var textContent = Fill(textBody, template, values, value => value);
        var textFull = Fill(
            _resources[$"{LayoutName}.txt"],
            LayoutName,
            new Dictionary<string, string> { ["content"] = textContent.Trim() },
            value => value);

        return new RenderedEmail(subject, htmlBody, textFull);
    }

    private static (string Subject, string Body) SplitSubject(string template, string text)
    {
        var normalized = text.ReplaceLineEndings("\n");
        var firstBreak = normalized.IndexOf('\n');
        var firstLine = firstBreak < 0 ? normalized : normalized[..firstBreak];
        if (!firstLine.StartsWith(SubjectHeader, StringComparison.Ordinal))
        {
            throw new ArgumentException($"E-mail template '{template}.txt' must start with a '{SubjectHeader}' line.", nameof(template));
        }

        return (firstLine[SubjectHeader.Length..].Trim(), firstBreak < 0 ? string.Empty : normalized[(firstBreak + 1)..]);
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
