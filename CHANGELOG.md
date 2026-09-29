# Changelog

The section for a version is what the release page says and what the app shows on its
About page when it finds that update. One text, three places: `tools/release-notes.ps1`
pulls the section out and the release workflow hands it to both.

Headings are `## <version> — <date>`. Nothing else is a version heading.

## 2.1.1 — 2026-09-29

**Fixed**

- **The Spider-Man entrance plays its sound again.** Builds of 2.1.0 from the release pipeline
  shipped without the recording, so the entrance was silent on Windows.
- **The Spider-Man showcase gives your whole setup back.** Anyone updating from 0.9.x meets
  the entrance on its own look for two launches, and on the third every charm they had on the
  rope comes back, in order and at its own size, with their rope style. A charm or rope chosen
  meanwhile is kept.
- **Updates from 0.9.x are reported as updates**, from 0.9.x, rather than as a first launch.

**Added**

- **Sessions and engagement for analytics**, so a Windows installation that is running counts
  as active: every event now carries the session and engagement GA4 needs.
- **Update reports**: available, download started, download completed, installed or failed,
  and `daily_active` once a day while Hangly runs. Nothing personal. See PRIVACY.md.

## 2.1.0 — 2026-09-29

**Changed**

- **A simpler Appearance page.** **Charm & Rope** first — horizontal and vertical position
  with Reset position, size, opacity, glow, rope length, Standard or Elastic, and mouse
  interaction — then **Behaviour**, **Sound & Motion**, **Spider-Man** (only with him on
  the rope), **About You** and **Privacy**. The picture of the desktop is gone (the sliders
  move the charm), and ropes are chosen in the Library, not here. The same sections, in the
  same order, as Hangly for Mac.
- **New defaults** for a new install and for Reset: Spider-Man on Spider Thread, a little
  larger and further in from the corner, and Auto-hide during full-screen video on. A fresh
  Hangly on Windows and on a Mac now looks the same.
- **Sound effects start at half volume** on a new install (they were at 14%, too quiet to hear
  the Spider-Man entrance). Volume is in Appearance → Sound & Motion.
- **This update shows off the Spider-Man entrance, then gives your look back.** For the
  first two launches after updating, Hangly starts as a new install does — Spider-Man on
  Spider Thread at the shipped size and position, default opacity and glow, sound on at half
  volume — so the entrance plays, with its sound, both times. On the third launch your own
  size, position, opacity, glow, rope length and sound come back, except anything you
  changed in the meantime. Spider-Man stays until you choose another charm; the entrance
  plays on every launch while he is on the rope, and never with any other charm.
- **Every Hangly window opens in the middle of the display the pointer is on** — Library,
  Welcome and Enjoying Hangly — every time, rather than on the main display or wherever it
  was last left.
- **"❤️ Support Hangly Development"** is the support button everywhere. The
  Enjoying Hangly? card and the support sheet say one thing — "Even **₹1,000** helps bring
  new ideas to life." — with the amount in bold and one clear Support button. The welcome
  card has a Support button too. The card appears on every third launch (3, 6, 9…).
- **A simpler welcome.** A bigger Spider-Man hangs from the top edge of the card and
  drops in on his web (silently), above "What should we call you?", a name field and
  Continue. The name is required, and can be changed later in Appearance → About You.
- **Two new ticks in the tray menu, directly below Library:** **Always on Top** and
  **Auto-hide during full-screen video**, which stay in step with the Appearance page.
- **PostHog is gone; Hangly now uses Firebase.** Each installation has one permanent
  record — the name you gave, the Windows, processor and Hangly versions, the city the
  network places it in (worked out on the server; your IP address is never stored), when
  it was first and last seen, and its crash count. It is updated when something changes
  and at most once a day otherwise. Six events are counted in Google Analytics (launch,
  update, charm chosen, rope chosen, Support clicked, welcome finished). A crash sends its
  stack trace, with your user name and profile path removed. Uninstalling tells the
  registry, once. There is no switch; Appearance → Privacy, About → Installation and
  [PRIVACY.md](PRIVACY.md) say what is sent. An installation from an earlier version keeps
  its identity and its original install date.
- **Version 2.1.0**, from 0.9.4, so Hangly for Windows and Hangly for Mac share one
  version number from now on.

**Added**

- **An elastic rope**, in Appearance under Motion → Rope. Pull the charm and the cord
  stretches; let go and it bounces back and settles, like a good bungee cord. It hangs
  exactly as the Standard rope does at rest, and every rope style keeps its look. With
  motion reduced it still stretches, and bounces less. The same choice as Hangly for Mac.
- **The Spider-Man entrance.** With Spider-Man or Spider-Man Swinging on the rope, Hangly
  starts with a web spun across the top edge of the screen and Spider-Man dropping in on
  his strand, a little past where he hangs and back, with the thwip of the web — about a
  second and a half. The web stays, hanging from the top of the screen, for as long as
  Spider-Man is on the rope. The sound follows Play sound effects and
  Volume. On by default; switch it off in Appearance under Motion (the switch
  only appears with a Spider-Man charm on the rope). Skipped while motion is reduced.
- **Reactive charms**, in Appearance under Motion → Interaction. The charm drifts away from
  a pointer moving quickly towards it and swings back on its own; reach for it slowly and
  it lets you take hold, as before. Normal, the default, is the charm as it always was.
  Off while motion is reduced. The same choice as Hangly for Mac.
- **Always on Top or On the Desktop**, in Appearance under Behaviour. Always on Top is how
  Hangly has always hung, and stays the default. On the Desktop hangs the charm over your
  wallpaper and icons and behind every window: it shows, and you can push it, wherever the
  desktop does, and it comes back after Show desktop. The same two choices as Hangly for
  Mac.
- **Glow: Off, Soft or Strong**, in Appearance under Size and reach. Soft is the faint
  halo in the charm's own colour that every charm has always had, so nothing changes
  unless you choose. Strong carries more colour a little further — best on a dark desktop
  — and Off has none. Imported charms glow in their own colour too. The same three
  choices as Hangly for Mac.
- **Keyboard shortcuts in Customize**, the same set as Hangly for Mac (Ctrl where the Mac
  has ⌘): Ctrl+1 to Ctrl+4 for Library, Create, Appearance and About; Ctrl+F to search the
  Library; Ctrl+D to favourite what is selected; Alt+Up and Alt+Down to move the selected
  charm on the rope; Ctrl+Shift+O to show or hide the charm; Ctrl+W to close.
- **A rope shelf you can see.** Library → Ropes shows each rope as a card with its cord
  drawn by the same renderer the charm hangs from, so you choose by the look — twist,
  braid, chain, glow. Star the ones you like and **Favourites** shows only those. The pane
  beside it shows the rope in use, large. The same shelf as Hangly for Mac.
- **A collection brings its cord.** Put a Marvel, DC, BTS or other collection charm on a
  rope you have never changed, and it arrives on the cord it was drawn for — Spider Thread,
  Midnight Cord, Silver Cord and so on. A rope you chose yourself is never changed. The
  same rule as Hangly for Mac.
- **Tags** under the charm in the Library pane, on one line.
- **Choose how far down the charm hangs.** Customize → Appearance → Where it hangs now has a
  small picture of your screen: drag the charm on it and the real one follows. A Vertical
  position slider sets the same thing from the keyboard, and Reset position puts it back.
  The same picture, range and behaviour as Hangly for Mac.
- **Opening Hangly takes you to your Library.** After the first launch's welcome, starting
  Hangly from the Start menu, a desktop shortcut or the .exe opens the Library in front —
  whether Hangly was already running or not — instead of appearing to do nothing. Still
  only one Hangly, and only one Library window, restored if it was minimised. A start at
  sign-in or after an update opens nothing. The welcome card and About say so: "Open Hangly
  again anytime to return to your Library." The tray menu is unchanged.
- **Creator Studio.** Drop a picture on the charm and it opens in the Studio with the
  background removed and the subject found for you — people, pets, products, anything that
  stands out. Choose how the background goes (Automatic, Detected subject, Flat background
  or Keep original), pick one subject when there are several, set the size, weight and
  fill, and look at it as a cut-out, as a charm, or hanging on a rope you can push. Save
  puts it in your Library and, with **Use on rope**, on the charm you dropped it on. Opens
  PNG, JPEG, WebP, HEIC and SVG, from a drop, **Create…** in the tray menu, Choose Image, or
  Ctrl+V. The same Studio as Hangly for Mac. Everything happens on your PC: nothing is
  uploaded, and it works offline. HEIC photos need Microsoft's free HEIF Image Extensions;
  the Studio says so, with a link, if they are missing.
- **Auto-hide during full-screen video**, in Customize → Appearance → Behaviour. Off by
  default. While a film, a video in your browser or a game is full screen on the display
  the charm hangs on, the charm steps out of the way, and it comes back when that stops.
  A maximised window, a full-screen page with no video, or a borderless tool covering the
  screen does not hide it.
- **Sound.** Each charm sounds like what it is made of — bell, glass, metal, wood or cloth
  — when you throw it, when it joins the rope, and when two charms knock together: the
  same sounds, made the same way, as Hangly for Mac. On by default and quiet; switch it
  off or change the volume in Customize → Appearance → Sound. Silent while a full-screen
  app, game or presentation is in front, and nothing runs between sounds.
- **Motion**, in Customize → Appearance: Follow system, Reduced or Full. Reduced hangs the
  charm still until you move it, lets a swing settle in seconds rather than most of a
  minute, and throws and nudges more gently; the rope looks exactly the same at rest.
  Follow system uses Animation effects in Windows Settings → Accessibility → Visual
  effects, and changes the moment you switch it.

- **Choose which display the charm hangs on**, from **Display** in the tray menu, shown
  when more than one display is connected. The choice is remembered by the monitor itself,
  not its position, so it survives a restart, a dock and a rearrangement. Unplug that
  monitor and the charm moves to the main display; plug it back in and it goes back.

**Changed**

- **The welcome says what the Mac's says.** Its second step now tells you that dropping a
  picture on the charm makes your own, and its words are shared with Hangly for Mac.

- **Updates install themselves while you're away.** Hangly checks shortly after it starts
  and once a day while it runs, downloads a new version quietly, and installs it once your
  PC is locked or the screen saver is up — never in front of you — or the next time Hangly
  starts or quits. The restart opens nothing else; after an update to a new version,
  **What's New** is waiting in the middle of the screen when you come back.
  **Restart to update** in the tray menu applies it at once. The same as Hangly for Mac.

- You can change your name in **Customize → Appearance → About You**.
- If you uninstall Hangly, it says so once, and removes itself from your Windows startup
  list. It used to leave that entry behind, pointing at a program that was gone.

**Fixed**

- **The welcome card's last line is no longer cut off.** The credit and the support button
  at its foot had been pushed below the window's edge.

- **The rope follows the clock while Hangly runs.** Morning, afternoon and night used to be
  decided once, at launch, so a PC left on overnight kept the morning's rope all day. It now
  changes at 05:00, 12:00 and 18:00, and after the clock is changed or the PC wakes — with
  one timer, not a clock being watched. The same as Hangly for Mac.
- **Every way of hanging a charm counts.** Picking a favourite from the tray menu now counts
  in "Charms hung" and appears in Recent, as the Library always did; choosing the charm a
  place already has is not counted twice. Importing a drawing into a place keeps that
  place's size.
- **Swings are no longer lost.** "Swings survived" used to be saved only when About was
  opened; it is now also saved as the charm swings (at most every five minutes), when the
  charm is hidden, and when Hangly quits.

- On a desk with displays at different scales, the charm is sized for the display it is
  moving to, rather than coming out the wrong size for a second.
- Plugging a monitor in or out, or moving the taskbar, now moves the charm with it.
- Artwork the rope no longer draws is let go: the in-between sizes a charm grows through
  as it appears, and everything belonging to a charm taken off the rope.
- A rare race between the tray menu changing charms and the overlay drawing them.
- Launch at login now starts the copy of Hangly you are running. An entry left by an
  earlier copy in another folder used to count as "on" while Windows started the old one.
- **Less CPU while the charm swings.** Frames now go straight from the GPU to the screen
  instead of being copied back through system memory first, and once the charm is still
  Hangly checks the mouse thirty times a second instead of at the display's full rate.
- **Memory no longer grows as you change charms.** Forty charm changes used to take Hangly
  from 123 MB to 317 MB, and none of it came back; it now stays between 55 and 75 MB.


## 0.9.4 — 2026-09-22

**Changed**

- Hangly asks for your name before it starts, and now means it. The welcome card's
  Continue button was already disabled until you typed one, but closing the card with the
  X went around that and left Hangly running without a name. Closing it without answering
  now closes Hangly, and the card comes back next time you open it.

**Fixed**

- Nothing is sent to the usage-data project until you have given a name. The first launch
  used to report itself before the welcome card had appeared, so those reports carried no
  name at all. They wait for it now — the launch is still counted, just with you on it.
- Anonymous analytics remains entirely optional and can be switched off on the About page,
  exactly as before.

## 0.9.3 — 2026-09-22

**Fixed**

- Updating from 0.9.1 or earlier no longer throws the charm half off the side of the
  screen. A charm that hung in a corner before the position slider existed now lands just
  inside that edge instead of exactly on it. If you have already moved the slider
  yourself, nothing changes — your position is kept.

## 0.9.2 — 2026-09-22

Hangly now says who made it, quietly, in the places you would look.

**New**

- The creator's name and handle appear in the sidebar on every page — Library, Create,
  Appearance and About — and the handle opens Instagram.
- A creator card on the About page, with the website, Instagram, a charm suggestion and
  Buy Creator a Coffee in one place.
- The same credit on the welcome card and on the "Enjoying Hangly?" card, each with the
  website and coffee links beside it.
- The UPI address in the coffee sheet copies when you click it.

**Improved**

- Small charms are much sharper. The artwork is reduced in steps now rather than sampled
  down in one go, so a charm at 50% keeps its detail instead of breaking up into speckle.
- The About page fits without scrolling, and its two columns balance.
- The sidebar shows the charm larger, and nothing in it scrolls.
- Hangly wears its own icon in the title bar and the task switcher.
- New installs hang a Spider-Man on a spider thread, and Restore defaults puts the whole
  rope back rather than only the cord.

**Fixed**

- Library on the tray menu opens the Library, not whichever page you last read, and it
  works when the window is minimised.
- The rope stays pinned where it meets the top of the screen instead of sliding along the
  edge while the charm swings.
- The cord ends at the charm's clamp rather than running on through it.
- Collection previews are no longer cropped.
- Windows now reaches the analytics project at all. Every 0.9.1 build shipped without its
  key, so nothing was ever sent; names and platform now arrive with it.

## 0.9.1 — 2026-09-21

- Fixes the update check, which failed on every launch of 0.9.0. GitHub's "latest
  release" only counts full releases, and answers with an error when every release is a
  beta — so a beta could never see the beta that followed it. It asks a different way now.
- If you are on 0.9.0, please install this one over the top; 0.9.0 cannot fetch it itself.

## 0.9.0 — 2026-09-21

The first Hangly for Windows.

- A charm hangs from a rope on your desktop, swings when you push it, and settles the way
  a real one would.
- Seventy charms, in collections, with favourites and a record of what you hung recently.
- Up to three charms on one rope, each at its own size.
- Nine rope styles, and a charm you can drag, flick and drop wherever you like.
- Create your own charm from a picture or an SVG.
- Runs on ARM64 and x64, and updates itself.
