# Brand

What ResultCrafter looks like, and why. Every file comes from one script, `assets/build.py`; change the art there and
run it again.

## The idea

**Return errors. Don't throw them.** The mark is that sentence as a picture: a value (the dot) goes down and comes
back as a return arrow. Without the dot it would be a keyboard's Enter key; with it, it is a `Result<T>` carrying its
`T` home.

Why not the old mark (braces with a checkmark): it is a stock Microsoft Fluent icon ("Braces Checkmark") and a common
JSON-validator logo, it showed only the success half, its navy square vanished on dark pages (1.06:1 on GitHub dark),
and the name inside it was unreadable below 64 px.

## Mark

- A rounded amber square (128 units, corner radius 28) with an ink arrow and dot: stroke 15 units, round caps and
  joins, so the stroke is about 2 px at 16 px.
- No letters inside the mark. The name sits beside it, in the lockup.
- One mark for all five packages.

## Colors

| Name  | Hex       | Where                                              |
|-------|-----------|----------------------------------------------------|
| Amber | `#F5A524` | The square; the tagline on the social card         |
| Ink   | `#1B1E27` | The arrow and dot; the name on light pages         |
| Paper | `#EEF0F4` | The name on dark pages and on the social card      |
| Night | `#14161C` | The social card background                         |
| Muted | `#A3A8B6` | The small line on the social card                  |

Contrast (WCAG 2): Ink on Amber 8.16; Amber on GitHub dark `#0D1117` 9.27; Ink on white 16.65; Paper on GitHub dark
16.59; on Night: Amber 8.86, Paper 15.85, Muted 7.61. Weak spot: the amber square on white is 2.04, so its edge is
soft on white pages; the arrow (8.16 against the square) carries the mark there.

Why amber: on NuGet, Serilog owns a red square, LanguageExt a crimson one, FluentResults and Refit are green, and the
.NET shelf is mostly blue and purple. Amber is free, and it means "heads-up", not "alarm": an error you planned for is
not an emergency. Never use the .NET purple `#512BD4`: it makes a community package look like an official Microsoft
one.

## Type

Instrument Sans SemiBold for the name and the tagline, Regular for other card text, tracking -0.01em. Version 1.000
from the designer's repo, SIL Open Font License 1.1 (`assets/fonts/OFL.txt`). The letters
in every SVG are outlines, so no viewer needs the font.

Spell the name one way: ResultCrafter, one word, capital C, as in the package IDs.

## Lockup

Mark and name in a row: the name's cap height is 0.46 of the square, centered on it, with 0.26 of the square between
them. Ink name on light pages, Paper name on dark pages.

## Files

| File | Use |
|------|-----|
| `assets/resultcrafter-mark.svg` | The mark alone, the master drawing |
| `assets/resultcrafter-icon.png` | NuGet package icon, 128 x 128 with transparent corners (NuGet takes PNG or JPEG only, one file for light and dark mode) |
| `assets/resultcrafter-lockup-light.svg`, `-dark.svg` | README header, switched with `<picture>` |
| `assets/social-card.svg`, `.png` | GitHub social preview, 1280 x 640, every part at least 108 px from the edges. Upload it in the repo settings: GitHub has no API for it |
| `ResultCrafterLogHorizontal.png` (repo root) | The old 2.x header. Kept only because the 2.0.0 NuGet pages load it from this path |

Build: `pip install fonttools brotli uharfbuzz`, then `python assets/build.py` from the repo root. It needs Google
Chrome for the PNGs (or set `CHROME` to a Chromium-based browser).
