# Privacy

Hangly is an ornament that hangs on your desktop. It needs almost nothing about you to do
that, and it collects almost nothing.

This is the **Windows** build. It is a port of the macOS one and the promises are the
same, but the two apps are not identical and this document describes what *this* one
does. Where they differ, it says so.

## Analytics

On by default, and switchable off in **Customize → About → Anonymous analytics**. Turning
it off stops collection immediately and discards the installation identifier.

### The name you give

Hangly asks for a name the first time it runs, and will not go further without one. That
name is sent with every analytics event while sharing is on.

This is a change, and it is deliberate. Earlier versions of this document said Hangly
never collects your name. That is no longer true and the sentence has been removed rather
than softened.

What has not changed is where the name comes from. **You type it.** Hangly does not read
your Windows account name, your Microsoft account, your email address, your computer name,
or any other part of the machine — there is no code in this build that could. You can
change it whenever you like in **Customize → Appearance → Your name**, and the new one is
used from the next event onward.

If you would rather not send it, switch analytics off. The name stays on your machine and
is still used to greet you.

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
| Charms | `charm_selected`, `charm_added`, `charm_removed`, `charm_reordered`. Built-in charms are named by their catalogue id; a charm you made is reported as `custom`. Reordering carries the two positions and no id. |
| Rope | `rope_count_changed`, `rope_style_changed`. |
| Settings | `appearance_changed` — the **name** of the setting that moved, never its value. |
| Library | `collection_opened` — which collection, by name. |
| Making a charm | `charm_imported` and `charm_saved` — that a charm was made, and **nothing about the file**: not its name, not its path, not its contents. |
| Dropping a file on the charm | `airdrop_drag_entered`, `airdrop_picker_opened`, and `airdrop_file_dropped`. **The dropped-file event is the one exception to the line above**: it carries the file's **extension** — `svg`, `png`, `jpg` — and a **coarse size bucket**, and nothing else. No name, no path, no contents. It is there to answer "is anybody using this, and with what", which is not answerable without knowing the kind of file. |
| The follow card | `follow_popup_shown`, `follow_popup_follow_clicked`, `follow_popup_maybe_later`, `follow_popup_dismissed`. |
| Links | `follow_instagram_clicked`, `coffee_sheet_opened`, `coffee_copy_upi`, `coffee_qr_viewed`. The coffee events carry which button was pressed, not what you did next — Hangly has no idea whether you sent anything. |
| With every event | `charm_count`, `active_charm_ids`, `rope_style`, `analytics_enabled`, and the name you typed. |

That is the whole list. One further name exists in the code — `collection_charm_selected`
— and **nothing sends it**; it is defined so that both platforms would report that act
under the same name if it were ever wired up. Nothing this build cannot do is sent,
because the code that would send it does not exist.

Both builds report into the same PostHog project and use the same event names, so a
question asked of one can be asked of both.

Two keys carry the operating system version, deliberately. macOS sends `macos_version`, so
this build sends `windows_version` for anything asking about Windows specifically, and
`os_version` as well for anything asking across both. Naming only one of them would break
one of those two questions.

### Exactly what a single event carries

This is a real payload, taken from this build with `--check-analytics`:

```json
{
  "event": "app_launch",
  "distinct_id": "c4639c10-0bb3-4a15-9017-1b80c5c9ebc7",
  "properties": {
    "user_name": "Sharan",
    "platform": "windows",
    "architecture": "arm64",
    "app_version": "2.0.0",
    "build_number": "1",
    "os_version": "10.0.26200.0",
    "windows_version": "10.0.26200.0",
    "analytics_enabled": true,
    "charm_count": 2,
    "active_charm_ids": ["Nazar boncuğu", "Hamsa"],
    "rope_style": "Thread"
  }
}
```

The same list is on the About page, under **Anonymous analytics**, built from the code that
sends it rather than typed out — so it cannot drift from what actually leaves.

### What is never sent

- Your email address, phone number, or any account. Hangly has no accounts.
- Your Windows account name, Microsoft account, or computer name. The only name Hangly
  holds is the one you typed.
- Files you import: not the contents, not the markup, not the file name, not the folder
  it came from, not the drive. A dropped file reports its extension and a size bucket, and
  that is all.
- Your clipboard.
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

Hangly checks whether a newer version exists, about twenty seconds after it starts, and
tells you in the tray menu when there is one. Nothing is downloaded until you ask for it.

- **The check asks GitHub what releases exist.** Hangly's releases are published on
  GitHub, and the check is an ordinary request to GitHub's public releases API for this
  repository, followed by a request for one file — the release's `releases.win-arm64.json`
  or `releases.win-x64.json`, depending on which build you have. If you choose to
  install, the package is downloaded from the same release.
- **Nothing about you or your copy goes with it.** No identifier, no display name, no
  system profile, no account: GitHub sees a request for a public file with an IP address,
  as it does for anyone reading the repository in a browser. The updater
  ([Velopack](https://velopack.io)) states that its runtime and the binaries it ships with
  the app collect no telemetry, analytics or tracking data.
- **There is no Hangly server**, for updates or for anything else. There is nothing to
  report to and nothing that knows you checked.

See `Docs/DISTRIBUTION.md` for how releases are built and signed.

## Weather

**Not in this build, and removed from the roadmap permanently.** The macOS app can
optionally ask Open-Meteo what the weather is in one city you type. Hangly for Windows
ships no weather feature at all — no service, no setting, no analytics event — so it
makes no such request and there is no city stored anywhere, and there is no version of
Hangly for Windows planned in which it does. Seasonal charms are removed on the same
terms. If that ever changes, this document is updated before it ships, not after.

## Permissions

Hangly asks for none, and this is a design constraint rather than a happy accident. It
reads the position of the cursor and the state of the mouse button so the charm can be
picked up, which is information Windows publishes to any process and needs no permission.
It does not read window contents, other applications, or anything you type.

## No other network use

Beyond analytics and the update check, Hangly makes no network requests. It loads no
remote content and contacts no other service. The links on the About page open in your
browser; the app does not fetch them.
