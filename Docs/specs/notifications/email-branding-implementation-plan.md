# Branded e-mails — implementation plan

Spec: `Docs/specs/notifications/email-branding.md`.

- `EmailBrandOptions` (section `Notifications:Brand`): `Name`, `Wordmark`,
  `WordmarkAccent`, `AccentColor`, `AccentSoftColor`, `Footer`. Colors must
  be `#rrggbb` (validated at startup, because they land in inline CSS).
- `EmbeddedEmailTemplates(EmailBrandOptions? brand = null)`: adds the
  `brand*` values to every template and to the layout; the `.txt` header
  accepts `Tag:` and `Preheader:` after `Subject:` (placeholders allowed).
- New `_layout.html` / `_layout.txt` and the seven fragments (inline styles,
  table layout, one `@media (max-width: 480px)` block).
- DI: the templates are built from `IOptions<EmailBrandOptions>`.
- Settings: compose sets Marfim's values (`Notifications__Brand__*`);
  `appsettings` keeps the neutral defaults.
- Tests: template rendering (brand values and defaults, preheader/tag,
  encoding, text part), options validation, and the existing end-to-end
  e-mail tests.
- Docs: README/operations settings table, CLAUDE.md note.
