# Branded, responsive e-mails

Mockups (Marfim repo): `Docs/design/mockups/Email.dc.html` and the
`Email*.dc.html` screens, `MobileEmails.dc.html` (same template, `mobile`).

## What

- Every transactional e-mail (confirmation, password reset, order
  confirmed / shipped / cancelled, payment not approved) follows the store's
  e-mail design: wordmark header with a small tag, an icon, a large light
  title, the message, an optional note box (reason), a call-to-action
  button where there is a link, and a dark footer.
- One layout that reads well on a phone and on a desktop client: fluid up
  to 600 px, with narrower paddings, smaller title and a full-width button
  below 480 px. No web fonts or remote images (privacy; blocked by many
  clients), so the type falls back to the system sans.
- The brand is **configuration, not code**: `Notifications:Brand:*` sets the
  name, the wordmark, its accent mark, the accent colors and the footer.
  Defaults keep today's neutral OrderCore look, so other stores are
  unaffected. The Marfim deployment sets Marfim's values.
- A hidden preheader (inbox preview line) per e-mail.
- The plain-text part stays, with the same wording and the brand footer.

## Why

The e-mails are the one place the shopper meets the store outside the site,
usually on a phone. A grey "OrderCore" message from an unknown sender looks
like phishing; a branded one reads as the store speaking.

## Out of scope

- Order items, delivery address, carrier/tracking code and a "view order"
  link in the e-mail: the integration events don't carry them
  (`Docs/backend-pendencies/emails/transactional-emails.md` in Marfim).
- Store data placeholders of the mockup (support WhatsApp, CNPJ, address).
- Localisation beyond pt-BR.

## Decisions (asked and answered)

1. Configurable brand, Marfim as the deployment's default (not hard-coded).
2. E-mail delivery is orthogonal (Mailpit by default, Resend by setting).
