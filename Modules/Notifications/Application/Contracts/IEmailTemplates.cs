namespace OrderCore.Api.Modules.Notifications.Application.Contracts;

public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Turns a template name and its values into the e-mail to send. The
/// templates live in the repository (pt-BR, one shared layout); a value is
/// HTML-encoded wherever it lands in HTML. An unknown template or a missing
/// value is a programming error (<see cref="ArgumentException"/>).
/// </summary>
public interface IEmailTemplates
{
    RenderedEmail Render(string template, IReadOnlyDictionary<string, string> values);
}
