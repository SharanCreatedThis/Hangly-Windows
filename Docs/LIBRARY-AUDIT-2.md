# Library audit, second pass

After the detail panel, hero cards and reordering landed. 21 September 2026.
Evidence tags as elsewhere: **[SWIFT] [DOC] [BINARY] [LIVE] [MEAS] [WIN] [INFER]**.

---

## 1. Catalogue integrity — clean **[MEAS]**

Parsed out of the generated catalogue and checked against the artwork on disk:

| Check | Result |
|---|---|
| Charms | **70** |
| Duplicate ids / names / file names | **none** |
| Missing region | none |
| Missing description | none |
| Missing tags | none |
| Shortest description | 61 characters |
| Tags per charm | 3 to 6 |
| Categories declared vs used | 13 vs 13 — **no dead category, no orphan charm** |
| Artwork files missing | **none** |

The "no dead category" property is also a test, added with the seasonal cut: a charm
filed under a category the chips do not offer fails the suite.

## 2. Hero cards — exact **[MEAS]**

Nine cards. Every card's stated count matches what its filter actually returns:

| Collection | Card says | Filter returns |
|---|---|---|
| Marvel | 6 | 6 |
| DC | 5 | 5 |
| Tamil Spiritual | 5 | 5 |
| BTS | 7 | 7 |
| Football Legends | 5 | 5 |
| Music Legends | 6 | 6 |
| Friends | 6 | 6 |
| Breaking Bad | 7 | 7 |
| Stranger Things | 6 | 6 |

**Ordering** is catalogue order, which is the macOS catalogue's own order — the generator
reproduces it deliberately so the two platforms number their charms the same way
**[WIN]**. **Artwork** is the first three charms of the collection, overlapped; macOS does
the same **[LIVE]**, and neither platform ships a drawn cover, which is what lets a
collection be added without one.

**Filtering** is in place — tapping a card sets the category filter and reports
`collection_opened`. The cards hide themselves once anything is filtering, because they
are a way in and become noise once the view is already narrowed **[WIN]**.

## 3. Detail sidebar — parity **[LIVE] + [MEAS]**

Preview, name, region in small capitals, description, tag pills, rope state, favourite
toggle. All six fields are populated for all 70 charms (§1). It follows the selection and
never drives it, which is the rule macOS states outright **[DOC]**: "the detail pane,
which always shows the selection".

Verified by the UI suite: the panel names the charm, gives its region, describes it, says
whether it is hanging, and offers favouriting.

## 4. Search — parity, and better than macOS in one respect **[MEAS]**

| Property | Result |
|---|---|
| Every charm found by its own display name | **70 / 70** |
| Queries matching more than one charm | 8 — all genuine (shared words across a pack) |
| Accent folding, unaccented query | `boncugu` → Nazar boncuğu |
| Accent folding, accented query | `boncuğu` → Nazar boncuğu |
| Both directions on a second charm | `panchangjie` and `Pánchángjié` → Pánchángjié |
| Case folding | `NAZAR` → nazar |
| Tag search | `evil eye` → 3, `glass` → 4 |
| Region search | `Turkey` → 1 |
| Description search | `Upside Down` → 2 |

**Deliberate Windows deviation:** the placeholder reads "Search charms, places, materials"
where macOS says "Search charms". Windows searches region and tags as well as names, so
the longer placeholder is accurate rather than decorative. Keeping it.

## 5. Favourites and recents — parity **[MEAS]**

- Favourites toggle on and off, and order is the order they were starred.
- Recents cap at **12** and the twelfth push evicts the oldest.
- Re-adding an existing recent **moves it to the front rather than duplicating** —
  verified: count stayed 12, no duplicates present.
- Deleting an import removes it from both, and from every place on the rope including the
  ones put away.

## 6. The one defect found, and fixed **[MEAS]**

**Stale thumbnails.** The cache held **84 files for 70 charms**: the eleven seasonal
charms removed from v1, plus three imports that had been deleted. Inert — the cache is
keyed by id and nothing looks up an id that is gone — but it accumulates, and the import
ones would have kept arriving for as long as people tried charms and changed their minds.

`CharmThumbnails.Prune` now runs when the Library builds. Measured after: `pruned 14
stale thumbnail(s) of 70 live`, cache **70 files, 3.62 MB**.

## 7. Performance **[MEAS]**

| | |
|---|---|
| Thumbnail cache | 70 files, 3.62 MB, in `%TEMP%\Hangly\thumbnails\256` |
| Process memory, Library open | 104.9 MB |
| Process memory, overlay only | 60.3 MB |
| Cost of a Library visit | ~45 MB *(superseded: re-measured at **153 MB** on 21 Sep — `RELEASE-HARDENING-AUDIT.md` §5.3)* |

That 45 MB is the same shape macOS reports for its own Library — its RC2 audit measured a
visit at 37 MB and recovered about 9 MB of it on close **[DOC]**. Windows does not yet
release interface artwork when Customize closes; macOS does, through
`ArtworkMemory.reclaim()`. **Recorded as a gap, not fixed** — it is a 45 MB steady state
on a machine with gigabytes, and the milestone brief is stabilisation.

## 8. Differences, sorted

**Exact parity:** hero card counts and ordering, card artwork strategy, detail panel
fields and its follow-the-selection rule, accent and case folding, favourites, recents
capacity and de-duplication, category integrity.

**Deliberate Windows deviations:**
1. Search placeholder names what it actually searches.
2. "Yours" rather than macOS's "Imported" for the custom category — shipped earlier,
   renaming now would churn a string for no gain.
3. Reordering offers buttons as well as a drag, because a drag cannot be reached from the
   keyboard or verified by a test.
4. No Ropes tab; rope style lives on Appearance.
5. No Seasonal chip — cut from v1.

**Missing, still:**
1. No release of interface artwork when Customize closes (§7).
2. No Create page — Studio is deferred to 1.1.

**Not measurable here:** exact spacing, radii and type scale, because the macOS view layer
is not in the repository. Unchanged from the first audit.
