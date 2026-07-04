# Architecture diagrams

`dotnet-api-platform-architecture.drawio` is a 7-page draw.io file:

| Page | View |
|------|------|
| 00 · Future State | the whole system at a glance — icons only |
| 01 · Ecosystem | the paved road: engine → factory → flagship across GitHub / ADO / Azure |
| 02 · Runtime | the modular monolith on Container Apps |
| 03 · The Guards | the ordered request pipeline + three enforcement layers |
| 04 · Contract-first & CI/CD | TypeSpec-is-law flow and both pipelines |
| 05 · Scaling Ladder | rungs 0–6 with explicit climb signals |
| A1 · Appendix | dense wiring view of the whole system |

## Editing

The `.drawio` file is **generated** — edit `gen.py` and re-run instead of editing XML:

```bash
# one-time: rebuild the icon style index from the upstream icon libraries
git clone --depth 1 https://github.com/dwarfered/azure-architecture-icons-for-drawio.git
python3 build-icon-index.py azure-architecture-icons-for-drawio

python3 gen.py   # emits dotnet-api-platform-architecture.drawio
```

Icons come from [dwarfered/azure-architecture-icons-for-drawio](https://github.com/dwarfered/azure-architecture-icons-for-drawio)
(official Azure service icons as draw.io libraries), plus two web extras in `extra-icons/`
(NuGet mark via Simple Icons; package glyph via VS Code codicons). Drop any SVG into
`extra-icons/` and reference it in `gen.py` as `'X:<filename>'`.

## Exporting

draw.io desktop's CLI renders pages headlessly. Note: `--page-index` is silently ignored in
current builds — split the mxfile into single-page files first:

```bash
python3 - <<'EOF'
import xml.etree.ElementTree as ET
t = ET.parse('dotnet-api-platform-architecture.drawio')
for i, d in enumerate(t.getroot()):
    m = ET.Element('mxfile', t.getroot().attrib); m.append(d)
    ET.ElementTree(m).write(f'_page{i}.drawio', xml_declaration=True, encoding='utf-8')
EOF
for i in 0 1 2 3 4 5 6; do
  "/Applications/draw.io.app/Contents/MacOS/draw.io" -x -f png --scale 2 -o page-$i.png _page$i.drawio
done
```

## Visual language

Solid gray = required / always fires · long dash = optional / config-gated ·
dotted gray = stub / future seam · orange dash = scale signal · red = security control ·
purple dash = governance / audit. Boundary boxes carry their platform's icon in the corner.
