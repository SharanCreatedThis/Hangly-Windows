# Privacy

Hangly is an ornament that hangs on your desktop. It needs almost nothing about you to do
that, and it collects almost nothing.

This is the **Windows** build. It is a port of the macOS one and the promises are the
same, but the two apps are not identical and this document describes what *this* one
does. Where they differ, it says so.

## Analytics

Hangly keeps a register of the people who use it: who they are, and what they run it on.
It does not record what anybody does with it.

On by default, and switchable off in **Customize → About → Analytics**. Turning it off
stops it immediately and discards the installation identifier.

### The name you give

Hangly asks for a name the first time it runs, and will not go further without one. With
analytics on, that name is part of what is sent.

**You type it.** Hangly does not read your Windows account name, your Microsoft account,
your email address, your computer name, or any other part of the machine — there is no
code in this build that could. Change it whenever you like in **Customize → Appearance →
Your name**.

If you would rather not send it, switch analytics off. The name stays on your machine and
is still used to greet you.

### When anything is sent

One message, called an *identify*, at exactly three moments:

| When | |
|---|---|
| The first time you start Hangly | After you have given a name, never before |
| You change your name | Once, with the new name |
| A new major version arrives | The first time you start, say, 2.0 after 1.x |

And once a day that Hangly runs, and once when you uninstall it, the messages below. That is
all. Starting Hangly, quitting it, hanging a charm, changing a rope, opening a window,
importing a picture — none of it is sent, and the code that used to send it has
been removed. If an identify cannot be delivered because you are offline, it is tried again
the next time you start Hangly.

It goes to [PostHog](https://posthog.com) (US region).

> **Builds made from the source send nothing.** The key that lets Hangly reach PostHog is
> added only by the release workflow. A copy built by anybody else — or by the author for
> testing — has no destination, and its About page says *No destination configured*.

### Once a day: "still running"

On each day Hangly runs, it sends one more message, `daily_active`, after the identify
and never before it. It says only that this copy ran today, and which platform and
versions it is:

```json
{
  "event": "daily_active",
  "distinct_id": "<the same identifier>",
  "properties": {
    "platform": "windows", "architecture": "arm64",
    "app_version": "1.0.0", "build_number": "6", "os_version": "10.0.26200.0",
    "$geoip_disable": true, "$ip": null,
    "$set": { "platform": "windows", "app_version": "1.0.0", "...": "the same platform facts" }
  }
}
```

No name, no charms, no settings, nothing about what you did. At most once per calendar day,
checked once an hour while Hangly runs. A day with no network is not sent later. It is what
lets the project count how many people are still using Hangly and which versions they are
on, without counting what they do.

### When you uninstall

If you uninstall Hangly through **Settings → Apps**, it sends one last message,
`app_uninstalled`, with the same identifier and the platform facts — no name — so the
project knows this copy is gone rather than just quiet. Only with analytics on, and only if
Hangly had already told the project who you are. If the machine is offline at that moment,
or Hangly is removed by deleting its folder by hand, nothing is sent: there is no Hangly left
to try again.

The same step removes Hangly's entry from your Windows startup list, so Windows does not go
on trying to start a program that is no longer there.

### Exactly what an identify carries

This is a real payload, printed by this build with `Hangly.exe --check-analytics` (which
shows it without sending it):

```json
{
  "event": "$identify",
  "distinct_id": "ce319cc7-eae6-4a95-ae97-f38fcab87ad6",
  "properties": {
    "identify_reason": "first_launch",
    "$lib": "hangly-windows",
    "$lib_version": "0.9.4",
    "$geoip_disable": true,
    "$ip": null,
    "$set": {
      "user_name": "Sharan",
      "name": "Sharan",
      "username": "Sharan",
      "platform": "windows",
      "architecture": "arm64",
      "app_version": "0.9.4",
      "build_number": "5",
      "os_version": "10.0.26200.0",
      "$os": "Windows",
      "$os_version": "10.0.26200.0",
      "$app_version": "0.9.4"
    }
  }
}
```

- **`distinct_id`** is a random identifier made on this machine the first time anything is
  sent. It is not derived from your hardware, account or network.
- **`$set`** is what PostHog stores on you. The name appears three times because PostHog
  shows a person by `name` or `username`, and older Hangly data used `user_name`; it is one
  name, written where it can be read.
- **`$geoip_disable`** and **`$ip: null`** stop PostHog working out a country, region or
  city from the address the message came from.
- **`identify_reason`** is which of the three moments above this is.

The same list is on the About page, built from the code that sends it rather than typed
out, so it cannot drift from what actually leaves.

### What is never sent

- Anything about what you do in Hangly: launches, clicks, charms, ropes, windows, imports.
- Your email address, phone number, or any account. Hangly has no accounts.
- Your Windows account name, Microsoft account, or computer name.
- Files you import, their names, their contents, or where they came from.
- Your clipboard.
- Your location.
- Where your charm sits, how large it is, which display it is on, or anything else
  describing your desktop.
- Keystrokes, screen contents, other applications, or what you are doing.

These are not aspirations. `AnalyticsTests` in `Hangly.Core.Tests` asserts them against a
recording provider that captures exactly what would have left the machine — including that
the provider has no way to send anything but an identify.

### Turning it off

**Customize → About → Analytics → Tell Hangly who is using it.**

Switching it off stops it at the source and throws away the installation identifier and
the record of what was sent under it. If you switch it back on, a new identifier is made and
you are sent as a first launch, so the two cannot be joined.

### Checking what your copy is doing

The same panel shows, for this machine: whether sharing is on, whether a destination is
configured, the installation identifier (masked), the last identify sent this session and
why, and your name.

## Charms you import

A charm you import never leaves your machine. The drawing is copied into
`%APPDATA%\Hangly\Charms\`, and that copy is the only one Hangly keeps.

- **The file is not read for anything but drawing it.** It is rewritten into the subset of
  SVG that draws — script, event handlers, embedded documents and anything referring to a
  URL are removed before it is stored, so an imported drawing cannot ask Hangly to fetch
  anything or run anything.
- **Nothing about it is sent anywhere** — not that it happened, not its name, size or
  contents.
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

**Not in this build, and removed from the roadmap permanently** — on macOS as well, where
it used to be an option. Hangly for Windows ships no weather feature at all — no service,
no setting, no analytics event — so it makes no weather request and there is no city
stored anywhere. Seasonal charms are removed on the same terms, on both platforms. If that ever changes, this document is updated before it ships, not after.

## Permissions

Hangly asks for none, and this is a design constraint rather than a happy accident. It
reads the position of the cursor and the state of the mouse button so the charm can be
picked up, which is information Windows publishes to any process and needs no permission.
It does not read window contents, other applications, or anything you type.

## No other network use

Beyond analytics and the update check, Hangly makes no network requests. It loads no
remote content and contacts no other service. The links on the About page open in your
browser; the app does not fetch them.
