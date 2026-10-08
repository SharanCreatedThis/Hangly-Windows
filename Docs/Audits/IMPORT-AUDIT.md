# Import security audit

21 September 2026. Evidence tags: **[SWIFT] [DOC] [BINARY] [LIVE] [MEAS] [WIN] [INFER]**.

Nothing here is inferred. Every verdict is a file that was written to disk, read back and
put through the shipping code — first the sanitiser directly, then the whole importer via
the app's own `--check-import` switch, which rasterises and measures exactly as a real
import does.

The corpus is **42 files** in `tests/Hangly.Core.Tests/Hostile`, copied beside the test
binary. Adding a file adds a case.

---

## 1. XML layer — all refused **[MEAS]**

DTDs are prohibited outright (`DtdProcessing.Prohibit`, `XmlResolver = null`), which is
why this whole class collapses into one answer.

| Attack | File | Verdict |
|---|---|---|
| XXE reading a Windows file | `xxe-file.svg` | **REFUSED** — not readable XML |
| XXE reading `/etc/passwd` | `xxe-unix.svg` | **REFUSED** |
| XXE over HTTP | `xxe-http.svg` | **REFUSED** |
| Parameter entity fetching an external DTD | `xxe-parameter.svg` | **REFUSED** |
| Billion laughs | `billion-laughs.svg` | **REFUSED** |
| External DOCTYPE | `external-dtd.svg` | **REFUSED** |
| Harmless internal DTD | `dtd-internal.svg` | **REFUSED** — deliberately, no DTD is allowed |
| Unclosed / truncated / junk | 3 files | **REFUSED** |
| Empty / whitespace only | 2 files | **REFUSED** |

The user-facing message says so plainly: *"That file isn't a readable SVG. If it has a
document type declaration, Hangly refuses it on purpose."*

## 2. SVG layer — payload stripped, file kept **[MEAS]**

| Attack | Removed |
|---|---|
| `<script>` | `<script>` |
| `<script><![CDATA[…]]>` | `<script>` |
| `onload`, `onclick` | both |
| `onmouseover`, `onfocus` | both |
| `<foreignObject>` with an iframe | `<foreignObject>` |
| XHTML `<script>` in a foreign namespace | `<script>` |
| `@import url(http://…)` | the whole `<style>` |
| `url(http://evil/…)` in CSS | the whole `<style>` |
| `url(file:///…)` in CSS | the whole `<style>` |
| `href="file:///…"` | the href |
| `href="http://…"`, `href="https://…"` | the href |
| `href="javascript:…"` | the href |
| `<use href="http://…">` | the href |
| `href="data:text/html,<script>"` | the href |
| `<animate>` | the element |

Keeping the file and removing the payload is deliberate: the message names how many things
were left out, so an import that arrives stripped says so rather than looking untouched.

## 3. Resource abuse — answered, never hung **[MEAS]**

Recursive `<use>`, circular `<use>`, 600 levels of nesting, nested `<svg>`, a viewBox of
1e11 and coordinates of 1e30 all return in milliseconds. The two that describe impossible
geometry are refused further down by the renderer — *"That drawing couldn't be rendered."*
Files over 2 MB are refused by size before anything parses them.

## 4. Two real defects, found here and fixed

### 4.1 An inline raster was being stripped — **serious**

`IsLocalReference` accepted only `#fragment`, so `data:image/png;base64,…` went the way of
a remote reference. Most real SVGs carry a photograph exactly that way, and **every one of
this collection's own seventy charms is built like it**. Importing an ordinary Illustrator
export produced a blank charm — worse than a refusal, because it looked like it had worked.

Proven both ways with the same file, a shipped charm:

| | Before | After |
|---|---|---|
| Sanitiser | `accepted, removed [@href (external)]` | `accepted, removed []` |
| Full importer | `REFUSED — That drawing couldn't be rendered.` | `ACCEPTED` |
| Stored drawing | — | 490.7 KB, `data:image/png;base64` present |

`data:image/svg+xml` is deliberately **not** allowed: it is a document, it can carry
script, and it would arrive already past everything above.

### 4.2 A drawing with nothing in it was accepted

`HasDrawing` counted `<g>`, so `<defs><g id="a"/></defs>` passed on the strength of a
container that draws nothing inside a definition that is only ever referenced. It now
ignores everything inside `<defs>` and no longer counts groups. Verdict is now
*"There's nothing to draw in that file."*

## 5. Filesystem safety **[MEAS]**

**Where traversal is actually closed:** the store never uses the imported file's name.
Every drawing is written as a fresh GUID — measured on a real import:
`66d2f331-522c-4e21-849c-8e603bc16066.svg`. A file called `../../../etc/passwd` cannot
name its own destination, and a test asserts exactly that.

**A second guard, added here.** `PathFor` combined `Directory` with whatever
`ImageFileName` the manifest held, and `Remove` deletes what it returns. The manifest
lives in `%APPDATA%` and is editable, so a manifest carrying `..\..\something` — by malice
or by corruption — would have had the app delete a file outside its own folder. A name is
now only a name: no separators, no volume, no `.` or `..`, no invalid characters. Eleven
cases assert it.

**What the manifest does not contain**, checked against a real import: no source path, no
source file name, no folder, no drive, no SVG markup, no base64. Only the id, the derived
display name, the metrics and the palette.

**No write path outside the store.** Imports write two things: the GUID-named drawing and
`manifest.json`, both inside `%APPDATA%\Hangly\Charms`.

## 6. Lifecycle **[MEAS]**

| | |
|---|---|
| Import → restart | survives: on the rope, starred, recent, and the drawing still on disk |
| Per-place size across restart | kept — 1.3 stayed 1.3 |
| Delete → drawing | removed from disk |
| Delete → rope | removed |
| Delete → **hidden places** | removed — new check, the case where a deleted charm could return when the count grew |
| Delete → favourites | removed — new check |
| Delete → recents | removed — new check |
| Delete → "Yours" category | disappears when the last import goes |

## 7. Not verified here

- **Upgrade persistence across a Velopack install-over** was verified earlier in the
  project and is unchanged by this work, but it has not been re-run against this build.
  It belongs to the Velopack audit.
- **A drag from Explorer onto the charm** is implemented and registered, but an OLE drag
  is a modal loop driven by real mouse input and cannot be driven synthetically. Marked
  unverified in STATUS.md.

## 8. Verdict

Every attack in the brief was tested against real files and is either refused or stripped,
with the evidence above. Two genuine defects were found by doing it — one of which would
have made importing ordinary artwork silently produce a blank charm.

**748 core tests** (from 693), **87 UI checks** (from 81).
