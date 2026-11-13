"""Convert a CHANGELOG.md version section into flat Nexus changelog lines.

Each bullet becomes one line prefixed by its section, e.g. "Fixed: Detection of F4SE DLL support".
Usage: nexus_changelog.py <release-notes.md>  (writes to stdout)
"""

import re
import sys
from pathlib import Path

entries: list[str] = []
section = ""
last_is_entry = False

for raw in Path(sys.argv[1]).read_text(encoding="utf-8").splitlines():
	line = raw.rstrip()
	if not line.strip():
		last_is_entry = False
		continue

	if m := re.match(r"^###\s+(.+)$", line):
		section, last_is_entry = m.group(1).strip(), False
	elif m := re.match(r"^[-*]\s+(.+)$", line):
		text = m.group(1).strip()
		entries.append(f"{section}: {text}" if section else text)
		last_is_entry = True
	elif last_is_entry:  # wrapped continuation of the previous bullet
		entries[-1] += " " + line.strip()

print("\n".join(entries))
