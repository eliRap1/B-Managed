"""Pull plain Hebrew text out of the Driver-moodle book v3 so we can mirror its structure."""
import re, sys
from xml.etree import ElementTree as ET

ns = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
tree = ET.parse(r"C:\Users\eli08\AppData\Local\Temp\v3unpack\word\document.xml")
root = tree.getroot()
out  = []
for p in root.iter("{http://schemas.openxmlformats.org/wordprocessingml/2006/main}p"):
    style = p.find("./w:pPr/w:pStyle", ns)
    sval = style.get("{http://schemas.openxmlformats.org/wordprocessingml/2006/main}val") if style is not None else ""
    runs = "".join(t.text or "" for t in p.iter("{http://schemas.openxmlformats.org/wordprocessingml/2006/main}t"))
    if not runs.strip():
        continue
    if sval.startswith("Heading"):
        lvl = re.findall(r"\d+", sval)
        n = int(lvl[0]) if lvl else 1
        out.append(("#" * n) + " " + runs.strip())
    else:
        out.append(runs.strip())
with open(r"D:\yudb\_v3_text.md", "w", encoding="utf-8") as f:
    f.write("\n\n".join(out))
print("wrote", len(out), "paragraphs")
