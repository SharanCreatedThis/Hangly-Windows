# Privacy

Hangly is an ornament that hangs on your desktop. It needs almost nothing about you to do
that, and it collects almost nothing.

This is the **Windows** build. It is a port of the macOS one and the promises are the
same, but the two apps are not identical and this document describes what *this* one
does. Where they differ, it says so.

## Anonymous analytics

On by default, and switchable off in **Customize → About → Anonymous analytics**. Turning
it off stops collection immediately and discards the installation identifier.

Events are sent to [PostHog](https://posthog.com) (US region). Nothing about analytics can
delay, block or change what the app does: every send is fire-and-forget, and a send that
fails is written to the local log and forgotten.

> **In a build with no project key, nothing is sent at all.** The key is a build property
> rather than something written in the source, and a build made without one runs with no
> destination. The About page states which of the two you are running: it reads either
> *No destination configured* or *Connected — <host>*.

### What is sent

| | |
|---|---|
| Installation identifier | A random UUID made on this machine the first time anything is sent. It is not derived from your hardware, account, network or anything else, and it is used for nothing but counting installs. |
| Build and system | App version, build number, Windows version. |
| Lifecycle | `app_first_launch`, `app_launch`, `app_quit`. |
| Charms | `charm_selected`, `charm_added`, `charm_removed`. Built-in charms are named by their catalogue id; a charm you made is reported as `custom`. |
| Rope | `rope_count_changed`, `rope_style_changed`. |
| Settings | `appearance_changed` — the **name** of the setting that moved, never its value. |
| Imports | `charm_imported` and `charm_saved` — that a charm was imported, and nothing about the file. Not its name, not its size, not its contents. |
| Links | `follow_instagram_clicked`, `coffee_sheet_opened`. |
| With every event | `charm_count`, `active_charm_ids`, `rope_style`, `analytics_enabled`. |

The vocabulary also contains names for features this build does not have yet — the
collections browser, weather, the follow card, dropping a file on a charm.
They are defined so that the two platforms report the same act under the same name when
those features land. **Nothing this build cannot do is ever sent**, because the code that
would send it does not exist yet.

The one deliberate difference from macOS: that build sends `macos_version`, and this one
sends `windows_version`. The same key holding a different kind of number would make the
two datasets disagree about what the word means.

### What is never sent

- Your name, email address, or any account. Hangly has no accounts.
- Images you import, or anything about them — not the file, its name, or its size.
- Charms you make. They are reported as the word `custom`.
- Your location.
- Where your charm sits, how large it is, which display it is on, or anything else
  describing your desktop.
- Keystrokes, screen contents, other applications, or what you are doing.

These are not aspirations. `AnalyticsTests` in `Hangly.Core.Tests` asserts each of them
against a recording provider that captures exactly what would have left the machine —
including a test that fails if a property describing your screen ever appears on an event.

### Turning it off

**Customize → About → Anonymous analytics → Share anonymous analytics.**

Switching it off stops capture at the source rather than filtering it later, and throws
away the installation identifier. If you switch it back on, a new identifier is made, so
the two cannot be joined.

### Checking what your copy is doing

The same panel shows, for this machine:

- whether sharing is on
- whether a destination is configured, and which
- the installation identifier, masked
- the last event sent, and when
- how many events have been sent this session

It is in the app rather than behind a developer flag because the argument for collecting
anything at all is that it can be inspected.

## Charms you import

A charm you import never leaves your machine. The drawing is copied into
`%APPDATA%\Hangly\Charms\`, and that copy is the only one Hangly keeps.

- **The file is not read for anything but drawing it.** It is rewritten into the subset of
  SVG that draws — script, event handlers, embedded documents and anything referring to a
  URL are removed before it is stored, so an imported drawing cannot ask Hangly to fetch
  anything or run anything.
- **Nothing about it is sent anywhere.** The analytics events above record *that* an
  import happened. Not the file name, not its size, not its contents, not the name you
  see in the Library.
- **On the rope it is reported as the word `custom`**, as `PRIVACY.md` has always said.
  The identifier Hangly gives it is random and local to this machine.
- Deleting a charm in the Library deletes the copy.

## Updates

Hangly checks whether a newer version exists and installs it quietly when there is one.
The check is a request for one static file, and the download that may follow comes from
the same place.

- **Nothing about you or your copy goes with the check.** It carries no identifier and no
  system profile; the server sees a request for a file, with an IP address, as with any
  web request. The updater ([Velopack](https://velopack.io)) states that its runtime and
  the binaries it ships with the app collect no telemetry, analytics or tracking data.
- The update feed is a plain file on a web server. There is no server-side component and
  nothing to report to.

See `Docs/DISTRIBUTION.md` for how releases are built and signed.

## Weather

**Not in this build.** The macOS app can optionally ask Open-Meteo what the weather is in
one city you type. Nothing in this build makes that request, because the feature is not
ported. When it is, this document will be updated before it ships.

## Permissions

Hangly asks for none, and this is a design constraint rather than a happy accident. It
reads the position of the cursor and the state of the mouse button so the charm can be
picked up, which is information Windows publishes to any process and needs no permission.
It does not read window contents, other applications, or anything you type.

## No other network use

Beyond analytics and the update check, Hangly makes no network requests. It loads no
remote content and contacts no other service. The links on the About page open in your
browser; the app does not fetch them.
