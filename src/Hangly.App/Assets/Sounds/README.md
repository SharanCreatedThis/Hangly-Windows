# Sounds

`SpiderManEntrance.wav` is the Spider-Man entrance's sound. The app reads it from
`Assets/Sounds/` beside the executable, lines its attack up with the web by rule
(`Hangly.Core.Audio.EntranceSound`), plays it for the entrance and a short tail, and fades it
out. With no file here, the entrance is silent.

**A licensed recording has not been added yet.** The file used in development is a temporary
stand-in and is deliberately not committed (see `.gitignore`). To add the licensed one: put it
here under the same name — WAV, 16- or 24-bit PCM or 32-bit float, any rate or channel count —
remove the `.gitignore` line, and commit. Nothing else changes.
