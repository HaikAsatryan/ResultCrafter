"""Builds every ResultCrafter brand file from one place: the mark below and the wordmark font in assets/fonts.

Run from the repo root:  python assets/build.py
Needs: pip install fonttools brotli uharfbuzz, and Google Chrome (or set CHROME to its path) for the PNGs.
Writes, next to this script:
  resultcrafter-mark.svg            the mark alone (master)
  resultcrafter-icon.png            128 x 128, the NuGet package icon
  resultcrafter-lockup-light.svg    mark + name for light pages (README header)
  resultcrafter-lockup-dark.svg     mark + name for dark pages
  social-card.svg / social-card.png 1280 x 640, the GitHub social preview
Rules behind the numbers: docs/brand.md.
"""
import io
import os
import shutil
import subprocess
import tempfile
import time
from pathlib import Path

import uharfbuzz as hb
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

HERE = Path(__file__).resolve().parent
FONT_SEMIBOLD = HERE / "fonts" / "InstrumentSans-SemiBold.woff2"
FONT_REGULAR = HERE / "fonts" / "InstrumentSans-Regular.woff2"

AMBER = "#F5A524"
INK = "#1B1E27"
PAPER = "#EEF0F4"
NIGHT = "#14161C"
MUTED_ON_NIGHT = "#A3A8B6"

# The mark on a 128-unit plate: a value (the dot) goes down and comes back as the return arrow.
PLATE = 128
MARK = f"""<rect width="128" height="128" rx="28" fill="{AMBER}"/>
<g fill="none" stroke="{INK}" stroke-width="15" stroke-linecap="round" stroke-linejoin="round">
<path d="M86 44V64Q86 80 70 80H38"/>
<path d="M54 61L35 80L54 99"/>
</g>
<circle cx="86" cy="32" r="12.5" fill="{INK}"/>"""

# Lockup proportions, shared with Wakeshot's: cap height 0.46 of the plate, centred on it, gap 0.26 of the plate.
CAP_SHARE = 0.46
GAP_SHARE = 0.26
TRACKING_EM = -0.01


def num(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


class Face:
    def __init__(self, path):
        self.tt = TTFont(path)
        self.tt.flavor = None
        data = io.BytesIO()
        self.tt.save(data)
        self.hb_font = hb.Font(hb.Face(data.getvalue()))
        self.upem = self.tt["head"].unitsPerEm
        self.cap = self.tt["OS/2"].sCapHeight / self.upem
        self.glyphs = self.tt.getGlyphSet()
        self.order = self.tt.getGlyphOrder()

    def text(self, s, size, x, baseline):
        """The text as one SVG path d, kerned by HarfBuzz, and its advance width."""
        buf = hb.Buffer()
        buf.add_str(s)
        buf.guess_segment_properties()
        hb.shape(self.hb_font, buf, {"kern": True, "liga": True})
        scale = size / self.upem
        track = TRACKING_EM * self.upem
        pen_x = 0.0
        parts = []
        for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
            svg = SVGPathPen(self.glyphs, ntos=num)
            dx = x + (pen_x + pos.x_offset) * scale
            dy = baseline - pos.y_offset * scale
            self.glyphs[self.order[info.codepoint]].draw(TransformPen(svg, (scale, 0, 0, -scale, dx, dy)))
            if svg.getCommands():
                parts.append(svg.getCommands())
            pen_x += pos.x_advance + track
        return " ".join(parts), (pen_x - track) * scale


def lockup(face, plate, x, y, word_color):
    """Mark plus name; returns (svg body, width)."""
    size = CAP_SHARE * plate / face.cap
    baseline = y + plate / 2 + CAP_SHARE * plate / 2
    word_x = x + plate + GAP_SHARE * plate
    d, width = face.text("ResultCrafter", size, word_x, baseline)
    body = (f'<g transform="translate({num(x)} {num(y)}) scale({num(plate / PLATE)})">{MARK}</g>\n'
            f'<path fill="{word_color}" d="{d}"/>')
    return body, word_x + width - x


def write(name, text):
    with open(HERE / name, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote", name)


def svg(width, height, body, label, note, display_scale=1.0):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {num(width)} {num(height)}" '
            f'width="{num(width * display_scale)}" height="{num(height * display_scale)}" role="img" aria-label="{label}">\n'
            f"<!-- {note} Written by assets/build.py; change it there, not here. -->\n{body}\n</svg>\n")


def render_png(svg_name, png_name, width, height):
    chrome = os.environ.get("CHROME") or next(
        (p for p in [r"C:\Program Files\Google\Chrome\Application\chrome.exe",
                     r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                     "/usr/bin/google-chrome", "/usr/bin/chromium",
                     "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"] if Path(p).exists()), None)
    if chrome is None:
        raise SystemExit("Chrome not found: set CHROME to its path")
    work = Path(tempfile.mkdtemp())
    try:
        page = work / "page.html"
        page.write_text(f'<!doctype html><html><body style="margin:0;background:transparent">'
                        f'<img src="{(HERE / svg_name).as_uri()}" width="{width}" height="{height}" '
                        f'style="display:block"></body></html>', encoding="utf-8")
        out = work / "out.png"
        subprocess.run([chrome, "--headless=new", "--disable-gpu", "--hide-scrollbars",
                        "--force-device-scale-factor=1", "--default-background-color=00000000",
                        "--allow-file-access-from-files", f"--user-data-dir={work / 'profile'}",
                        f"--screenshot={out}", f"--window-size={width},{height}", page.as_uri()],
                       check=True, capture_output=True, timeout=120)
        # On Windows the launcher returns before its child process has written the screenshot.
        deadline = time.monotonic() + 60
        while not (out.exists() and out.stat().st_size > 0):
            if time.monotonic() > deadline:
                raise SystemExit(f"Chrome wrote no screenshot for {svg_name}")
            time.sleep(0.25)
        time.sleep(1)
        shutil.copyfile(out, HERE / png_name)
        print("wrote", png_name)
    finally:
        shutil.rmtree(work, ignore_errors=True)


def main():
    semibold = Face(FONT_SEMIBOLD)
    regular = Face(FONT_REGULAR)

    write("resultcrafter-mark.svg",
          svg(PLATE, PLATE, MARK, "ResultCrafter", "The ResultCrafter mark: a return arrow that starts from a value."))

    for theme, color in (("light", INK), ("dark", PAPER)):
        body, width = lockup(semibold, PLATE, 0, 0, color)
        write(f"resultcrafter-lockup-{theme}.svg",
              svg(width, PLATE, body, "ResultCrafter",
                  f"The ResultCrafter lockup for {theme} pages: the mark and the name in Instrument Sans SemiBold "
                  "(assets/fonts/OFL.txt), the letters as outlines.", display_scale=0.5))

    # Social card: every part at least 108 px from each edge.
    w, h, margin = 1280, 640, 108
    plate = 136
    tag_size, sub_size = 56, 30
    tagline = "Return errors. Don\u2019t throw them."
    sub = "Result types for ASP.NET Core. Clean RFC 9457 errors in one call."
    block = plate + 44 + tag_size * semibold.cap + 58 + 0.25 * sub_size
    top = (h - block) / 2
    lock_body, lock_w = lockup(semibold, plate, margin, top, PAPER)
    tag_base = top + plate + 44 + tag_size * semibold.cap
    tag_d, tag_w = semibold.text(tagline, tag_size, margin, tag_base)
    sub_d, sub_w = regular.text(sub, sub_size, margin, tag_base + 58)
    for label, width in (("lockup", lock_w), ("tagline", tag_w), ("sub line", sub_w)):
        assert margin + width <= w - margin, f"{label} is too wide for the card: {width:.0f} px"
    assert top >= margin, "card block is too tall"
    body = (f'<rect width="{w}" height="{h}" fill="{NIGHT}"/>\n{lock_body}\n'
            f'<path fill="{AMBER}" d="{tag_d}"/>\n<path fill="{MUTED_ON_NIGHT}" d="{sub_d}"/>')
    write("social-card.svg", svg(w, h, body, "ResultCrafter: Return errors. Don't throw them.",
                                 "The GitHub social preview, 1280 x 640."))

    render_png("resultcrafter-mark.svg", "resultcrafter-icon.png", 128, 128)
    render_png("social-card.svg", "social-card.png", w, h)


if __name__ == "__main__":
    main()
