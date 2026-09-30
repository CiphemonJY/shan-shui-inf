"""Assemble web/index.html: upstream shan-shui-inf generator (run in a Web Worker) + screensaver driver.

The generator is this fork's root index.html (upstream https://github.com/LingDong-/shan-shui-inf, MIT).
"""
import pathlib
import re

root = pathlib.Path(__file__).parent
src = (root.parent / "index.html").read_text(encoding="utf-8")

# Generator = every <script> block before the viewer/UI code, minus the URL-arg parser
# (the worker seeds itself). The main block ends with DOM viewer code, which is cut.
blocks = re.findall(r"<script[^>]*>(.*?)</script>", src, re.S)
gen = [b for b in blocks[:6] if "parseArgs" not in b]
assert len(gen) == 5 and "function chunkloader" in gen[-1], "upstream layout changed"
cut = gen[-1].index('document.addEventListener("mousemove"')
gen[-1] = gen[-1][:cut]
gen = "\n".join(gen)
assert "document." not in gen
assert "</script" not in gen

page = (root / "template.html").read_text(encoding="utf-8")
out = root / "web" / "index.html"
out.parent.mkdir(exist_ok=True)
out.write_text(page.replace("/*GENERATOR*/", gen), encoding="utf-8", newline="\n")
print(f"wrote {out} ({out.stat().st_size // 1024} KB)")
