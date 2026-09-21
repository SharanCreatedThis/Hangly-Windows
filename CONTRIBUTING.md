# Contributing

Thank you for looking. Hangly is a small project with one maintainer, and that shapes what
is easy to accept and what is not.

## The quickest useful thing

**Tell me it broke.** Hangly runs on hardware I do not have — every scaling factor except
200%, every multi-monitor desk, every x64 machine, and Windows 10. A bug report from one
of those is worth more to this project than a patch to something I can already see.

[Open an issue](https://github.com/SharanCreatedThis/Hangly-Windows/issues/new/choose) and
say what happened. `%APPDATA%\Hangly\hangly.log` holds the last run and is usually the
whole answer; it contains no personal information — see [PRIVACY.md](PRIVACY.md).

## Building it

```
git clone https://github.com/SharanCreatedThis/Hangly-Windows.git
cd Hangly-Windows
dotnet test tests/Hangly.Core.Tests/Hangly.Core.Tests.csproj
dotnet run --project src/Hangly.App/Hangly.App.csproj -c Release -r win-x64 -p:Platform=x64
```

You need the .NET 9 SDK and Windows 10 1809 or later. You do **not** need Visual Studio:
the Windows App SDK and the XAML compiler arrive through NuGet. Use `win-arm64` and
`-p:Platform=ARM64` on an ARM machine.

`README.md` has more, including why `EnableMsixTooling` has to stay.

## Before you open a pull request

Please open an issue first if it is more than a few lines. Not to gatekeep — to save you
writing something I have already decided against for a reason that is not obvious from the
code.

Things that will be accepted quickly:

- A fix for something that is demonstrably wrong, with a note on how you saw it fail.
- A test that fails before the fix and passes after it.
- A correction to the documentation, including this file.

Things that will take longer, or may be declined:

- New features. The roadmap is deliberately short and is in
  [STATUS.md](STATUS.md). Weather and seasonal charms are removed permanently; sound and
  the Creator Studio are v1.1.
- Refactors of working code. This is a port with a specific shape and the shape is
  load-bearing in places that are not obvious.
- Changes to physics constants in `src/Hangly.Core`. Every number there was measured
  against the macOS original. **A change to one needs a failing test that justifies it.**

## House rules the code follows

They are not arbitrary, and a pull request that ignores them will get comments about them
rather than about the change itself.

- **`Hangly.Core` stays free of WinUI, Win2D and Win32.** That separation is what lets the
  solver be tested on any machine. Anything platform-specific lives in `Hangly.App`.
- **Comments explain why, not what.** If a line needs a comment saying what it does, the
  line is the problem.
- **Do not edit `reference/`.** It is a read-only copy of the macOS original, kept so the
  port can be checked against it. When the two disagree, the Swift is right and the C# has
  a bug.
- **Green CI is not evidence that something works.** Four fatal bugs in this project
  passed green builds. If you changed something visible, run it and look at it.

## Tests

`dotnet test tests/Hangly.Core.Tests/Hangly.Core.Tests.csproj` — 759 of them, and they run
on any machine, including a Mac. Anything that needs Windows lives in `tools/` as a script
rather than pretending to be a unit test.

## Code of conduct

By taking part you agree to the [Code of Conduct](CODE_OF_CONDUCT.md). It is short.
