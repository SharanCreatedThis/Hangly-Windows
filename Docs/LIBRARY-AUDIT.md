# Library and creator experience: what macOS actually has

Audit before implementation, against macOS **2.0.0 (200)**. Evidence classes as in
VISUAL-PARITY.md: **[DOC] [BINARY] [LIVE] [WIN] [MEAS] [INFER]**.

The macOS view layer is **not** in `reference/swift/`, so nothing below is read from
macOS source. It is read from the shipping binary's symbols and literals, from the
running app, and from the documentation.

---

## 1. Structures that exist — named by the binary **[BINARY]**

| Symbol | What it is |
|---|---|
| `LibraryDetailPanel` | The sidebar |
| `CollectionHeroCard` / `CollectionHeroCards` | The collection cards, and their container |
| `CharmLibraryGrid` | The charm grid |
| `CharmCard` | One charm tile |
| `CharmLibraryItem` | The merged model behind a tile |
| `LibraryCollection` | A collection as the Library models it |
| `AboutPage`, `AboutHero`, `AboutStatistics`, `AboutSecrets`, `AboutCoffeeButton` | About's sections |
| `CreatorCard` | The creator card |
| `BuyCoffeeSheet` | The coffee flow, a sheet rather than a link |
| `SecretVault`, `Secret` | The secrets feature and its store |
| `AppMilestones` | The milestones model |

This settles that every item in the brief exists. It does not settle their layout.

## 2. Behaviour that is documented **[DOC]**

`Docs/Charm-System.md`:

> `CharmLibraryViewModel` merges the JSON entries with the import store's entries, the
> latter under a synthetic **"Yours"** category, into one list of `CharmLibraryItem`s.
> Search folds case and diacritics once per item. Favourites are a `Set<CharmID>` in
> `AppSettings` … **Selecting a card sets the charm manager's selection and nothing
> else: the rope changes on its next frame and the detail pane, which always shows the
> selection, follows.**

That last sentence matters for the brief's "separate Select from Hang". **On macOS they
are not separate.** Selecting a card *is* hanging it — the rope changes on the next
frame, and the detail pane follows the same selection. There is no second action.

**[LIVE]** The running app agrees: the detail panel's footer shows "On the rope" with a
check for the selected charm, i.e. selection and hanging are one state.

**So the brief's request here is a deliberate Windows deviation, not parity.** It is a
reasonable one — Windows has a "Charms on the cord" count and per-slot buttons that macOS
does not — but it should be recorded as a deviation rather than described as matching.
See §6.

## 3. Copy that can be reproduced exactly **[BINARY]**

These are literals in the shipping binary, so they are quotations rather than inventions.

**Creator and coffee**
- "Support the Creator"
- "Buy Creator a Coffee"
- "Thank you for using Hangly."
- "Suggest a charm to the creator (swarnsharan@gmail.com)"
- "Opens https://instagram.com/sharan.created.this"
- "Opens a message to the creator"
- `CreatorUPIQR.png` / `.jpg` ship in `Contents/Resources` — the coffee flow shows a real
  UPI QR, which is why it is a sheet and not a link.

**Secrets** — button "Tell me a secret", empty state "No secret revealed yet",
description "Reveals one of the app's secrets, and pushes the rope". The vault:

- "There is no secret."
- "This rope secretly prefers neon."
- "The charm believes in you."
- "The charm has witnessed every tab you've left open."
- "Some charms swing longer when nobody is watching."
- "Every swing is calculated. The luck is not."
- "The physics engine is working harder than it looks."
- "The rope knows where it is. The rope knows where it isn't."
- "Every charm on the rope is drawn at the size it hangs at. None of them is scaled to fit."
- "The charms are real objects. People hang them on doors, mirrors, rear-view mirrors and babies. Yours hangs on a menu bar."
- "This app began as: \"What if desktop icons needed emotional support?\""
- "This rope has survived more swings than most relationships."
- "You found the rare secret."

Note "**and pushes the rope**": revealing a secret also nudges the overlay. That is a
behaviour, not just copy.

**Statistics** — the only label recovered is "Swings survived: ". **[INFER]** Other
counters presumably exist; their labels did not survive as literals.

## 4. Layout, from the running app **[LIVE]**

From the macOS Customize window, Library page:

- **Left sidebar**, full height: large charm preview in a rounded well; name; region in
  small uppercase grey; description paragraph; tag pills; then at the bottom "On the
  rope" with a filled check and "Favorite" with a star.
- **Top strip**, "CURRENT ROPE" in small caps, then "1 charm on Thread"; charm thumbnails
  with a dashed "Add Charm" tile; "Drag to reorder · up to three" right-aligned; a size
  slider under the selected thumbnail.
- **Segmented control**: Charms | Ropes.
- **Search field**, "Search charms".
- **Category row**: All, Favorites, Protection, Luck & Fortune, Ritual & Home, Classic,
  Seasonal, Imported, Ma… (clipped).
- **Collection hero cards**: three per row, each with artwork, a title, "N charms" and a
  one-line description — Marvel, DC, Tamil Spiritual, BTS, Football Legends, Music
  Legends, Friends, Breaking Bad, Stranger Things.
- **Charm grid** below the cards, selected tile outlined.

**[MEAS]** Not measured: exact paddings, corner radii, type sizes. The screenshot is a
different scale factor from the Windows window and I will not pretend a pixel ruler over
it is a specification. Proportions are reproduced by eye and labelled as such.

## 5. What Windows has today **[WIN]**

Grid of every charm grouped by pack; category chips; search; favourites; recents; a
"Charms on the cord" 1/2/3 radio row with one button per slot; Import Charm. No sidebar,
no hero cards, no reorder, no per-charm size, no Ropes tab.

The data the sidebar needs is already present for all 70 charms — `CharmCatalogEntry`
carries `Region`, `Description` and `Tags`, and they are populated.

## 6. Deviations I intend to make, and why

| Deviation | Reason |
|---|---|
| Select and Hang separated | Asked for in the brief. macOS conflates them **[DOC]**. Windows already has an explicit slot model, which makes a two-step flow coherent where macOS's single-selection model would not. |
| "Yours" rather than "Imported" | Already shipped on Windows; renaming it now would break nothing but would churn a string for no gain. **[DOC]** macOS calls it "Imported". Worth reconciling later. |
| No Seasonal chip | Cut from v1 — see STATUS.md. |

## 7. Honest unknowns

- Exact spacing, radii and type scale — **[INFER]** throughout.
- What `AboutStatistics` counts beyond swings — **[INFER]**.
- Whether hero cards filter in place or push a sub-page — **[INFER]**; the screenshot
  shows them above the grid, which suggests in-place.
- The milestones model's contents — `AppMilestones` exists **[BINARY]**; Windows already
  has a `MilestoneSettings` with `launchCount`, which is **[WIN]** and not necessarily
  the same thing.
