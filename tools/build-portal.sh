#!/usr/bin/env bash
# Build the internal dev portal: one rendered page per API + a catalog landing page.
# Output lands in docs/portal/ (gitignored, regenerated). `make portal` uploads it to
# the Blob static website. The catalog is the "self-discoverable" entry point; each API
# page is a full Redocly render with the x-source-system metadata visible per schema.
set -euo pipefail
cd "$(dirname "$0")/.."

OUT=docs/portal
mkdir -p "$OUT"

# One catalog row per spec: "<file>|<title>|<blurb>"
# The TypeSpec-emitted contract is the primary source of truth; legacy files
# are kept in the catalog for reference during migration.
APIS=(
  "spec/tsp-output/openapi.v1.yaml|ApiPlatform v1 (TypeSpec)|Canonical OpenAPI 3.1 contract emitted from TypeSpec — accounts, transactions, customers."
  "openapi/_legacy/accounts.v1.yaml|Accounts API (legacy)|Hand-authored reference: core-banking accounts & transactions."
  "openapi/_legacy/customers.v1.yaml|Customers API (legacy)|Hand-authored reference: core-banking customer profiles."
)

rows=""
for entry in "${APIS[@]}"; do
  spec="${entry%%|*}"; rest="${entry#*|}"; title="${rest%%|*}"; blurb="${rest#*|}"
  page="$(basename "${spec%.yaml}").html"      # e.g. accounts.v1.html
  echo ">> rendering $title -> $OUT/$page"
  npx redocly build-docs "$spec" -o "$OUT/$page" >/dev/null
  rows="$rows<a class=\"card\" href=\"$page\"><h2>$title</h2><p>$blurb</p><code>$spec</code></a>"
done

cat > "$OUT/index.html" <<HTML
<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Northwind API Platform — Internal Catalog</title>
<style>
  :root { color-scheme: dark; }
  body { margin:0; font:16px/1.5 system-ui,sans-serif; background:#0e1116; color:#e6edf3; }
  header { padding:2.5rem 2rem 1rem; border-bottom:1px solid #222a35; }
  h1 { margin:0 0 .25rem; font-size:1.6rem; }
  .sub { color:#8b97a7; }
  main { display:grid; gap:1rem; grid-template-columns:repeat(auto-fill,minmax(320px,1fr)); padding:2rem; max-width:1000px; }
  .card { display:block; padding:1.25rem 1.4rem; background:#161b22; border:1px solid #222a35; border-radius:12px; text-decoration:none; color:inherit; transition:border-color .15s,transform .15s; }
  .card:hover { border-color:#3b82f6; transform:translateY(-2px); }
  .card h2 { margin:0 0 .4rem; font-size:1.15rem; }
  .card p { margin:0 0 .6rem; color:#b3bdca; }
  .card code { font-size:.8rem; color:#7d8aa0; }
  footer { padding:1rem 2rem 3rem; color:#5b6675; font-size:.85rem; }
</style></head>
<body>
  <header>
    <h1>Northwind API Platform</h1>
    <div class="sub">Internal API catalog · design-first · canonical, vendor-agnostic contracts</div>
  </header>
  <main>$rows</main>
  <footer>Generated from the OpenAPI catalog. Each card opens the full reference, including the source-system mapping per schema.</footer>
</body></html>
HTML

echo ">> catalog written to $OUT/index.html"
