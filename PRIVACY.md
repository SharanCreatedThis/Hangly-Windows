# Privacy

Hangly is an ornament that hangs on your desktop. What leaves your PC, and why, is all
below: the installation registry, a few usage events, crash reports, and the update check.

## The installation registry

Every installation of Hangly is registered with its developer, in Google Firebase. The
record exists so the developer can see how many people use Hangly, where, on what, and for
how long — and it is the only record kept about you. It is part of using Hangly: there is
no switch.

### What the record holds

| | |
|---|---|
| Installation ID | A random identifier made on this PC the first time Hangly runs, kept in `%AppData%\Hangly\installation.json`, and used for nothing else. It never changes. |
| Nickname | What you typed when Hangly asked "What should we call you?" — never read from your account, your PC's name or anything else. Change it any time in **Customize → Appearance → About You**. |
| City, region, country | Worked out from your internet connection's address by Hangly's server, using the MaxMind GeoLite2 database. **The address itself is never stored or logged.** Nothing more exact than a city. |
| Platform and processor | Windows, and whether it is x64 or ARM64. |
| Versions | Of Windows and of Hangly. |
| Versions over time | The version of Hangly it started on, the one before the latest update, and when it updated. |
| First and last seen | When Hangly was first installed and when it was last in use. The time comes from Hangly's server. For an installation from before 2.1.0, the first date comes from the old analytics records or, if analytics was switched off, from the creation date of Hangly's folders in `%AppData%` and `%LocalAppData%` (only the date is sent). |
| Retention and crash counts | How many days it has been used, how long since it was first seen, and how many times Hangly has crashed. |

### When it is sent

The record is saved on your PC first, then sent. An update is sent only when something
has changed — your nickname, a new version, a new city — and at most once a day to say
Hangly is still in use. Nothing is sent before you have given a nickname. If you are
offline, it waits and is sent when you are back.

The record is written by Hangly's server only; the app cannot read it back, and neither can
anyone else's. To have your record deleted, contact the developer from **About**.

### When you uninstall

Windows tells Hangly when it is being uninstalled, and Hangly sends one last update so the record is marked uninstalled. If you install it again, the same record is used.

## Usage events

Hangly reports these things to Google Analytics (the same property, through its Measurement Protocol), tagged with the installation ID and nothing
else — never your nickname or your city:

- that Hangly started, and whether at sign-in or by hand;
- that Hangly was updated;
- which charm you hang (a charm you made yourself is reported only as "custom");
- which rope you choose;
- that you opened the support page, and from where;
- that you finished the welcome;
- that Hangly was running today (once a day, with nothing attached);
- how an update went: that one was available, that it downloaded, that it installed, or at
  which step it failed and the error's type — with the version numbers involved.

Each event also carries a session number (the time the session began) and how long it
was since the previous event, which is what Google Analytics needs to count an installation
as active. Neither says anything about what you did.

Nothing else you do in Hangly is reported.

## Crash reports

Crashes, and a few non-fatal errors, are written to a file on your PC when they happen and sent on the next launch: the error's type, message and stack trace, the app and Windows versions, the processor, and when it happened (the server also records when it arrived). Your user folder and account name are removed from the text first. They are stored in Hangly's Firebase project, beside your installation's record.

## What is never collected

- Your email address, your Windows account name or your PC's name.
- Your IP address, or any location more exact than your city.
- Images you import, or anything about them — not the file, its name or its size.
- Where your charm sits on screen, or anything else about your desktop.
- Keystrokes, screen contents, other apps, or what you are doing.

## Checking what your copy holds

**Customize → About → Installation** shows the installation ID (masked), your nickname, the
place the registry has for you, and when the record was last updated.

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

Hangly checks whether a newer version exists about twenty seconds after it starts and
once a day while it runs. When there is one, it is downloaded quietly and installed the
next time Hangly starts or quits — nothing asks and nothing is shown. The tray menu offers
"Restart to update" for anybody who would rather not wait.

- **The check asks GitHub what releases exist.** Hangly's releases are published on
  GitHub, and the check is an ordinary request to GitHub's public releases API for this
  repository, followed by a request for one file — the release's `releases.win-arm64.json`
  or `releases.win-x64.json`, depending on which build you have. The package is
  downloaded from the same release.
- **Nothing about you or your copy goes with it.** No identifier, no display name, no
  system profile, no account: GitHub sees a request for a public file with an IP address,
  as it does for anyone reading the repository in a browser. The updater
  ([Velopack](https://velopack.io)) states that its runtime and the binaries it ships with
  the app collect no telemetry, analytics or tracking data.
- **There is no Hangly update server.** Updates come from GitHub; nothing reports that you
  checked. (The installation registry above is a separate service, in Firebase.)

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

Beyond the registry, usage events, crash reports and the update check, Hangly makes no
network requests. It loads no remote content and contacts no other service. The links on the About page open in your
browser; the app does not fetch them.
