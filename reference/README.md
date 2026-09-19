# Reference — the macOS original

Read-only. **Nothing here is built, and nothing here should be edited.**

These are the Swift files the port is transcribed from, copied out of the macOS
repository so the port can be continued on a machine that does not have it. They are the
authority whenever this port and your memory disagree.

| | |
|---|---|
| `swift/Charms/` | The charm catalogue — the largest piece still to transcribe |
| `swift/Models/` | Charm metrics, kinds, sounds, stacks, seasonal packs |
| `swift/Physics/` | The solver, already ported to `src/Hangly.Core/Physics` |
| `swift/CharmLibrary.json` | Names, categories and previews for the Library |
| `swift/Docs/` | The original's own documentation of the physics and charm system |

`swift/Physics/` is here for comparison rather than for work: it has already been ported,
and if you find a place where the C# and the Swift disagree, the Swift is right and the
C# has a bug.
