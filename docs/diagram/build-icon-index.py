#!/usr/bin/env python3
"""Rebuild icon-index.json from the dwarfered draw.io Azure icon libraries.

Usage:
    git clone --depth 1 https://github.com/dwarfered/azure-architecture-icons-for-drawio.git
    python3 build-icon-index.py azure-architecture-icons-for-drawio
Then: python3 gen.py  (emits dotnet-api-platform-architecture.drawio)
"""
import json, html, glob, re, os, sys

root = sys.argv[1] if len(sys.argv) > 1 else 'azure-architecture-icons-for-drawio'
index = {}
for f in glob.glob(os.path.join(root, 'azure-public-service-icons', '*.xml')):
    base = os.path.basename(f)
    if base.startswith('000'):
        continue
    raw = open(f).read().strip()
    raw = raw[len('<mxlibrary>'):-len('</mxlibrary>')]
    for e in json.loads(raw):
        title = e.get('title', '')
        xml = html.unescape(e.get('xml', ''))
        m = re.search(r'style="([^"]+)"', xml)
        if m and title:
            index[title] = {'style': m.group(1), 'w': e.get('w', 48), 'h': e.get('h', 48), 'lib': base}
json.dump(index, open('icon-index.json', 'w'))
print(f'{len(index)} icons indexed')
