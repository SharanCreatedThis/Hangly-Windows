# Changelog

The section for a version is what the release page says and what the app shows on its
About page when it finds that update. One text, three places: `tools/release-notes.ps1`
pulls the section out and the release workflow hands it to both.

Headings are `## <version> — <date>`. Nothing else is a version heading.

## 2.3.0 — 2026-10-04

**Added**

- **Pop-ups from the charm.** News about new charms and collections pops up under your charm in
  glass, **swings with it**, and leaves on its own after its time — or at once with the ×, which
  is always there. It holds still while the pointer is on it. Nothing is kept afterwards and no
  dot is left behind.
- **Update in Background.** The update card has one button: press it and the card goes, the
  update downloads and Hangly restarts into the new version by itself. Hangly now looks for a new
  version every hour, so the card appears soon after a release.
- **No notification window, nothing kept.** Every notification appears under the charm and
  leaves on its own; there is no Notification Center and no history. The tray menu no longer has
  Notifications or Quit Hangly.
- **The welcome card always fits.** With Windows' text made bigger, the name card's Continue
  button could fall below the window and the Library could not be reached. The card now sizes
  itself to what it shows, scrolls if the screen is small, and Enter continues.
- **Hangly works on PCs with Smart App Control.** Windows 11's Smart App Control, and the App
  Control policies many companies set, block unsigned program files. Hangly drew SVG through a
  set of unsigned libraries, so on those PCs the charms could not be drawn, the overlay did not
  start, and Setup ended with "Install Partially Succeeded". Hangly no longer uses them: charm
  artwork is read and drawn with Microsoft's own signed graphics library.
- **Right-click the charm** for the Library and Hide Charm.

**Fixed**

- **Every rope looks real.** Each of the nine ropes is now drawn from a photographic render of the
  real thing — twisted gold thread, braided leather, polished gold and silver chain, spun silk, a
  midnight braid, temple thread, silver satin cord and a twisted neon tube — laid along the cord so
  the twist and the links follow every bend as it swings. Before, each was a coloured line with a
  pattern painted on it. Neon is now a twisted magenta-and-cyan tube.
  On Gold Chain and Silver Chain the chain's own last link passes through the charm's ring, as in
  real jewellery: no extra ring, and no gap.
- **No more charms flipping upside down while you play with them.** Dragged close to the hook or
  whipped round, a charm could suddenly turn its back on the rope or spin in a single frame. Lifted
  toward the top it now stays upright, stops just under its hook, and the slack rope drapes below;
  the rope no longer shoots off along the top of the screen. It turns smoothly at every angle.
- **Charms with a ring hang like fine jewellery.** A tiny jump ring passes through the charm's own
  ring, and on heavy charms a small end cap finishes the rope. The hardware is sized to the charm and
  always smaller than what it holds, so the charm stays the star. The metal follows the rope: gold,
  brass, silver, gunmetal or neon blue, and the rope curves smoothly into rings that sit off-centre — a
  sneaker's heel, a camera's corner — with no kink above the clasp; left alone, the rope falls
  dead straight into the ring. The star and the Himmeli hang
  from their own loops too, and the classics — ornament, star, heart, diamond, horseshoe,
  scarab and Himmeli — whose rings were drawn open at the top now have whole rings, so they hang
  from a jump ring with no gap.
- **Ropes meet every charm properly.** A charm without a ring now has the rope run right down
  behind it, so there is no gap between rope and charm on figures like Ronaldo, Usopp or the
  Shiva lingam, and nothing shows through openings below. The daruma and the Chinese knot are
  shown whole, hanging from their own cord loops; Karuppu, FC Barcelona and the Deathly Hallows
  hang cleanly from their rings and tip.
- **The glow takes each charm's own shape.** It used to be a circle of colour behind every
  charm, whatever its shape: a ring round a tall Spider-Man, a moon behind a slender bell. Now
  it follows the charm's outline — the Snitch's wings, a dreamcatcher's feathers, both figures
  of Spider-Man and Gwen — and each part glows its own colour, leaning towards the charm's.
- **Spider-Man and Gwen swing for real.** The charm used to be one stiff picture that turned
  about the anchor like a clock hand, with only Spider-Man grabbable. Now Spider-Man and Gwen
  are two weights on one cord:
  - The picture's own rope, knots and all, bends between them and above them.
  - It stretches with Elastic.
  - A flick sends the pair tumbling like a double pendulum.
  - Either one can be grabbed, thrown, or dropped onto.
  - The size slider scales the whole picture as one, rope included, so Spider-Man stays the
    same distance from Gwen as in the artwork; the rope is always whole, at every size.
  - Spider-Man holds the rope straight through his hands, and his body never stretches on
    Elastic, so the rope no longer steps out sideways from his hands in a swing or a stretch.
  - The rope is drawn exactly as the artwork has it: the fold hanging from his lower hand and
    the twist above his upper one stay on his hands instead of opening out beside them.
- **The Library opens without freezing.** Charm thumbnails are made in the background instead of
  while the window waits, and kept where Windows' clean-up does not delete them, so the first
  Library open after an update no longer locks up for seconds.
- **"That update couldn't be installed" is fixed.** Install and restart, in About or the tray,
  started a second download while Hangly was already downloading the update in the background,
  and Windows refused it. Install now joins the download already under way, shows its progress,
  and waits up to a minute and a half if another update step is still finishing.
- **The charm comes back after a graphics driver update, a GPU reset or sleep.** When Windows
  took the graphics device away, the charm disappeared until Hangly was restarted, and the
  Library window went blank. Both are now rebuilt on the new device within a few seconds.
- **The tray menu no longer crashes Hangly.** Library, Create or Quit could end the app if they
  were picked while a window was opening or closing.
- **The tray icon appears even when Hangly starts before the taskbar.** At sign-in Hangly could
  start before Windows' taskbar was ready, and then run all session without a tray icon. It now
  keeps trying for two minutes.
- **The Library opens again after its window was closed for real,** instead of doing nothing until
  a restart.
- **Smaller fixes:** Support no longer fails when pressed twice; Creator Studio no longer reports
  an error when closed while it is still finding the subject, or when a dragged file's source app
  goes away; update downloads that fail because the network dropped are no longer reported as
  errors; an installation whose saved identity was lost registers again instead of being refused
  for good.
- **Smaller updates.** From this version, an update downloads only what changed — under a
  megabyte instead of 300 MB.

**Changed**

- **Charms are made from pictures on Windows: PNG, JPEG, WebP or HEIC.** SVG drawings can no
  longer be imported or opened in Create. Charms imported from an SVG drawing are removed,
  because Hangly can no longer draw them; charms made from photos or in Create are kept.

## 2.2.0 — 2026-10-02

**New**

- **Ninety new charms and eight new collections:** One Piece, Harry Potter, Ben 10, Attack on
  Titan, Naruto, Game of Thrones, Air Jordan and Pokémon. Marvel gains Doctor Strange's Eye of
  Agamotto, Stormbreaker, Doctor Doom, Deadpool and Wolverine, and Spider-Man holding Gwen;
  Stranger Things gains Max and Steve; Football Legends gains two more Ronaldos, the Ballon d'Or
  and the Champions League trophy. 160 charms in all.
- **Spirituality.** Tamil Spiritual is now Spirituality and holds every faith: the rudraksha,
  Shiva lingam, Hanuman and Buddha beside the Kaaba, the Green Dome, the crescent and star and
  an Allah pendant, and a cross, a dove, a praying angel and the Sacred Heart.
- **Every collection has a card.** The collection cards are one row that scrolls sideways, so
  all seventeen fit without pushing the charms down the page.
- **Delete your own charms from the Library.** Right-click a charm you imported or made in
  Create and choose Delete Charm…. Hangly now asks before it deletes one.
- **Remove an image from Create.** A ✕ next to Open drops an image you do not want to keep,
  without saving it.

**Changed**

- **The cord meets every charm where it actually is.** Where the cord arrives is measured on
  the charm's own centre line, so it no longer stops short above a charm whose top is two
  ears or a raised sword, and where a charm has its own loop the cord's end is tucked into it
  rather than butting against it. Nothing is added to the artwork: a charm drawn without a
  loop hangs as it was drawn. Sneakers hang tilted from the loop at their heel, and the
  Golden Snitch's cord runs between its wings to the ball.

**Fixed**

- **Creator Studio no longer reports a cancelled cut-out as an error.** Opening another image
  (or closing the studio) while the subject was still being found stopped the model, and Hangly
  logged and reported that as a failure. It is now treated as the cancellation it is.

## 2.1.2 — 2026-09-30

**Fixed**

- **Launch at login is on by default, for everyone.** Only a brand-new install ever switched it
  on, so installing or reinstalling over existing settings — or an entry the uninstaller had
  removed — left Hangly off at sign-in. 2.1.2 turns it on once for every installation; after that,
  turning off Launch at login in Customize, or Hangly in Task Manager → Startup apps is respected.

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
