# Security

## Reporting something

Email **swarnsharan@gmail.com** with "Hangly security" in the subject. Please do not open
a public issue for a vulnerability.

Tell me what you did, what happened, and what you expected. A proof of concept is welcome
and never required.

You will get a reply. If a fix is needed it ships in the next release, and you will be
credited unless you would rather not be.

## What is worth reporting

Hangly asks for no permissions, has no server, has no accounts and runs as an ordinary
user. The places where something could still go wrong:

- **Imported artwork.** Hangly accepts SVG, PNG and JPG files that you choose or drop on
  the charm. SVG is the interesting one: it is markup, and markup can be hostile.
  `src/Hangly.Core/Import/SvgSanitizer.cs` refuses documents with a DTD (which closes
  entity expansion and external entities), refuses scripts, refuses external references,
  and allows only inline raster data — not inline SVG data. A 42-file hostile corpus lives
  in `tests/Hangly.Core.Tests/Hostile`. **If you get something past it, that is a
  vulnerability.**
- **The settings file and the charm store**, both under `%APPDATA%\Hangly`. A file name
  that escapes that directory would be a vulnerability.
- **The updater.** Hangly updates itself from its own GitHub releases over HTTPS and
  verifies each package against the SHA256 in the feed before applying it. Anything that
  would let a different package be applied is a vulnerability.
- **Analytics.** Hangly sends a small, documented set of events when you leave sharing on.
  Anything leaving the machine that [PRIVACY.md](PRIVACY.md) does not list is a bug, and if
  it is personal information it is a vulnerability. There is a switch that shows exactly
  what a real event contains: `Hangly.exe --check-analytics`.

## What is not a vulnerability

- **The SmartScreen warning on first run.** Builds before v1.0 are not code-signed, which
  is stated on the download page and in [Docs/DISTRIBUTION.md](Docs/DISTRIBUTION.md).
  Signing is in progress through SignPath Foundation.
- Anything that needs administrator rights or physical access to the machine to set up.
- The fact that Hangly reads the cursor position. It needs that to let you pick the charm
  up; Windows publishes it to every process and it needs no permission.

## Supported versions

The most recent release. This is a one-person project and there is no back-porting.
