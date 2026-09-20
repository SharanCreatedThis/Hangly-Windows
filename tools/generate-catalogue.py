#!/usr/bin/env python3
"""Generate the C# charm catalogue from the Swift source.

The catalogue is eighty-one entries of pure data — identity, mass, radius, palette,
sound and how the artwork divides into beads. Transcribing that by hand is eighty-one
chances to mistype a number that no compiler would catch and only a screenshot would,
so it is read out of `reference/swift/` instead and written as C#.

Re-runnable on purpose. When the Swift changes, run this rather than editing the
generated file; the header says so to whoever opens it next.

    python3 tools/generate-catalogue.py
"""

from __future__ import annotations

import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SWIFT = ROOT / "reference" / "swift"
OUT = ROOT / "src" / "Hangly.Core" / "Models" / "CharmCatalog.Generated.cs"

# The Library's own facts — region, category, description, tags — live in the metadata
# document the macOS app ships rather than in the Swift catalogue. Merged here by id into
# one table, so a charm is described in exactly one place on this side.
LIBRARY = SWIFT / "CharmLibrary.json"

# The order the charm menu offers them in. CollectionCharmCatalog.entries is
#   collectionEntries + seasonalEntries + collectionPackEntries + storyPackEntries
#   + classicEntries
# and storyPackEntries is legendEntries + screenEntries. Reproduced here because the
# order is the catalogue's own and a reordering would silently renumber the menu.
#
# seasonalEntries is deliberately absent. The Windows build does not ship the seasonal
# packs, so it does not ship the charms that only existed to fill them — see STATUS.md
# for the scope decision. The Swift is left exactly as it is; this is the one place that
# knows Windows carries fewer charms than macOS.
SOURCES = [
    ("Charms/CollectionCharmCatalog.swift", "collectionEntries"),
    ("Charms/CollectionPackCatalog.swift", "collectionPackEntries"),
    ("Charms/StoryPackCatalog.swift", "legendEntries"),
    ("Charms/StoryPackCatalog+Screen.swift", "screenEntries"),
    ("Charms/ClassicCharmCatalog.swift", "classicEntries"),
]

# Categories the Windows build does not offer, dropped along with every charm filed
# under them. A category left in the chip row with nothing behind it is worse than one
# that was never offered.
DROPPED_CATEGORIES = {"seasonal"}

# One line about each collection, for the Library's hero cards.
#
# These are not in CharmLibrary.json and not in the Swift that ships in reference/ —
# they live in the macOS view layer, which is not part of this repository. Every line
# below is a literal read out of the shipping macOS 2.0.0 binary, so they are quotations
# rather than copy written here. A collection with no line gets no card.
COLLECTION_BLURBS = {
    "marvel": "Iconic Marvel-inspired charms designed as hanging ornaments.",
    "dc": "Legendary DC-inspired symbols reimagined as hanging charms.",
    "tamilSpiritual": "Traditional Tamil spiritual symbols and guardian deities.",
    "bts": "Stylized BTS-inspired collectible hanging charms.",
    "footballLegends": "Icons of world football.",
    "musicLegends": "Artists who shaped modern music.",
    "friends": "The iconic friends from New York.",
    "breakingBad": "The legendary Breaking Bad universe.",
    "strangerThings": "Mysteries from the Upside Down.",
}

EXPECTED = 70


def read(path: str) -> str:
    return (SWIFT / path).read_text(encoding="utf-8")


def library_metadata() -> tuple[dict[str, dict], list[tuple[str, str]]]:
    """Per-charm Library facts, and the category list, out of CharmLibrary.json."""
    document = json.loads(LIBRARY.read_text(encoding="utf-8"))
    charms = {entry["id"]: entry for entry in document["charms"]}
    categories = [
        (c["id"], c["name"])
        for c in document["categories"]
        if c["id"] not in DROPPED_CATEGORIES
    ]
    return charms, categories


def display_names() -> dict[str, str]:
    """`case .nazar: "Nazar boncuğu"` out of CharmKind's displayName switch."""
    text = read("Models/CharmKind.swift")
    start = text.index("var displayName: String")
    end = text.index("var symbolName: String")
    pairs = re.findall(r'case \.(\w+):\s*"([^"]*)"', text[start:end])
    return dict(pairs)


def entry_blocks(text: str, list_name: str) -> list[str]:
    """Every Entry(...) literal inside one `static let <list_name>: [Entry] = [...]`."""
    anchor = re.search(rf"static let {list_name}: \[Entry\] = \[", text)
    if not anchor:
        raise SystemExit(f"could not find {list_name}")

    # Walk to the matching close bracket so a nested literal cannot end the list early.
    i = anchor.end() - 1
    depth = 0
    for j in range(i, len(text)):
        if text[j] == "[":
            depth += 1
        elif text[j] == "]":
            depth -= 1
            if depth == 0:
                body = text[i + 1 : j]
                break
    else:
        raise SystemExit(f"unterminated list {list_name}")

    blocks = []
    for m in re.finditer(r"\bEntry\(", body):
        k = m.end() - 1
        depth = 0
        for n in range(k, len(body)):
            if body[n] == "(":
                depth += 1
            elif body[n] == ")":
                depth -= 1
                if depth == 0:
                    blocks.append(body[k + 1 : n])
                    break
    return blocks


def colour(block: str, name: str) -> tuple[str, str, str]:
    m = re.search(rf"{name}: CharmColor\(([^)]*)\)", block)
    if not m:
        raise SystemExit(f"no {name} colour in block")
    parts = [p.strip() for p in m.group(1).split(",")]
    if len(parts) != 3:
        raise SystemExit(f"{name} expected 3 channels, got {parts}")
    return tuple(parts)  # type: ignore[return-value]


def parse(block: str) -> dict:
    def field(name: str, pattern: str = r"([^,\n]+)"):
        m = re.search(rf"\b{name}:\s*{pattern}", block)
        return m.group(1).strip() if m else None

    kind = field("kind", r"\.(\w+)")
    source = re.search(r'sourceFileName:\s*"([^"]*)"', block)
    bead_count = field("beadCount", r"(\d+)")
    body_run = field("bodyRun", r"(\d+)")

    if not kind or not source or bead_count is None:
        raise SystemExit(f"incomplete entry: {block[:120]}")

    return {
        "kind": kind,
        "file": source.group(1),
        "mass": field("mass", r"([0-9.]+)"),
        "radius": field("radiusRatio", r"([0-9.]+)"),
        "beadCount": bead_count,
        # Swift defaults bodyRun to beadCount; make it explicit rather than reproduce
        # the defaulting in two languages.
        "bodyRun": body_run if body_run is not None else bead_count,
        "primary": colour(block, "primary"),
        "secondary": colour(block, "secondary"),
        "deep": colour(block, "deep"),
        "light": colour(block, "light"),
    }


def csharp_literal(value: str) -> str:
    """Swift and C# agree on these, but a bare `.5` is legal in neither's style."""
    return value if value.startswith("0") or value.startswith("-") else value


def escape(text: str) -> str:
    return text.replace("\\", "\\\\").replace('"', '\\"')


def main() -> int:
    names = display_names()
    library, categories = library_metadata()
    entries: list[dict] = []
    for path, list_name in SOURCES:
        entries.extend(parse(b) for b in entry_blocks(read(path), list_name))

    if len(entries) != EXPECTED:
        print(f"expected {EXPECTED} entries, parsed {len(entries)}", file=sys.stderr)
        return 1

    ids = [e["kind"] for e in entries]
    if len(set(ids)) != len(ids):
        dupes = {i for i in ids if ids.count(i) > 1}
        print(f"duplicate kinds: {sorted(dupes)}", file=sys.stderr)
        return 1

    missing_metadata = [e["kind"] for e in entries if e["kind"] not in library]
    if missing_metadata:
        print(f"no library metadata for: {sorted(missing_metadata)}", file=sys.stderr)
        return 1

    lines = [
        "//",
        "//  CharmCatalog.Generated.cs",
        "//  Hangly",
        "//",
        "//  GENERATED FILE — DO NOT EDIT.",
        "//",
        "//  Written by tools/generate-catalogue.py from reference/swift/. Eighty-one",
        "//  entries of pure data, read out of the Swift rather than typed again, because",
        "//  a mistyped mass is a bug no compiler sees and only a screenshot catches.",
        "//",
        "//  To change a charm, change the Swift and re-run the generator.",
        "//",
        "",
        "namespace Hangly.Core.Models;",
        "",
        "public static partial class CharmCatalog",
        "{",
        "    /// <summary>The Library's categories, in the order the chips offer them.</summary>",
        "    public static IReadOnlyList<CharmCategory> Categories { get; } =",
        "    [",
    ]

    for identifier, name in categories:
        lines.append(f'        new("{escape(identifier)}", "{escape(name)}"),')

    lines += [
        "    ];",
        "",
        "    /// <summary>The collections the Library offers as cards, in catalogue order.</summary>",
        "    /// <remarks>",
        "    /// A collection is a category that has a line written about it. The ones without",
        "    /// — protection, luck, ritual, classic — are filters rather than collections and",
        "    /// are offered as chips only, which is how macOS presents them.",
        "    /// </remarks>",
        "    public static IReadOnlyList<CharmCollection> Collections { get; } =",
        "    [",
    ]

    for identifier, name in categories:
        blurb = COLLECTION_BLURBS.get(identifier)
        if blurb is None:
            continue
        lines.append(
            f'        new("{escape(identifier)}", "{escape(name)}", "{escape(blurb)}"),'
        )

    lines += [
        "    ];",
        "",
        "    /// <summary>Every built-in charm, in the order the charm menu offers them.</summary>",
        "    public static IReadOnlyList<CharmCatalogEntry> All { get; } =",
        "    [",
    ]

    for e in entries:
        name = names.get(e["kind"])
        if name is None:
            print(f"no display name for {e['kind']}", file=sys.stderr)
            return 1
        meta = library[e["kind"]]
        tags = ", ".join(f'"{escape(tag)}"' for tag in meta.get("tags", []))
        lines += [
            "        new(",
            f'            Id: "{e["kind"]}",',
            f'            DisplayName: "{escape(name)}",',
            f'            FileName: "{escape(e["file"])}",',
            f'            Mass: {csharp_literal(e["mass"])},',
            f'            RadiusRatio: {csharp_literal(e["radius"])},',
            "            Palette: new CharmPalette(",
            f'                new CharmColor({", ".join(e["primary"])}),',
            f'                new CharmColor({", ".join(e["secondary"])}),',
            f'                new CharmColor({", ".join(e["deep"])}),',
            f'                new CharmColor({", ".join(e["light"])})),',
            f'            BeadCount: {e["beadCount"]},',
            f'            BodyRun: {e["bodyRun"]},',
            f'            CategoryId: "{escape(meta["category"])}",',
            f'            Region: "{escape(meta["region"])}",',
            f'            Description: "{escape(meta["description"])}",',
            f'            Tags: [{tags}]),',
        ]

    lines += ["    ];", "}", ""]
    OUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)} — {len(entries)} charms")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
