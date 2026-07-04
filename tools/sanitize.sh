#!/usr/bin/env bash
# tools/sanitize.sh — two-layer publish-gate sanitize check
#
# Layer 1 (committed backstop, always runs):
#   Generic structural patterns — shape-only regexes, no proper nouns.
#   These run in CI and locally.
#
# Layer 2 (local dev / pre-publish, never in CI):
#   sanitize-denylist.private.txt — real institution/vendor/employer literals.
#   That file is git-ignored (*.private.txt) and absent in CI by design.
#
# Usage: bash tools/sanitize.sh
# Exits 0 if clean, 1 on any match.
#
# NB: Databricks is an intentionally-kept public-product connector (stub-default).
#     Generic structural patterns match shapes, not product names.

set -u

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PRIVATE_LIST="${REPO_ROOT}/sanitize-denylist.private.txt"
FOUND=0

# File types to scan (same set as before)
INCLUDES=(
  --include='*.md'
  --include='*.yaml' --include='*.yml'
  --include='*.json'
  --include='*.bicep'
  --include='*.cs'
)

# Paths and files to skip
EXCLUDES=(
  --exclude-dir='.git'
  --exclude-dir='node_modules'
  --exclude-dir='_orchestration'
  --exclude='*.private.*'
  --exclude='package-lock.json'
  --exclude='packages.lock.json'
)

# run_check LABEL PATTERN [GREP_FLAGS...]
# Runs grep -rnP GREP_FLAGS PATTERN over the repo tree.
# Prints any matches and sets FOUND=1 if any are found.
run_check() {
  local label="$1" pattern="$2"; shift 2
  local output
  if output=$(grep -rnP "$@" "$pattern" "${INCLUDES[@]}" "${EXCLUDES[@]}" "$REPO_ROOT" 2>/dev/null) && [[ -n "$output" ]]; then
    printf '%s\n' "$output"
    echo "!! sanitize: $label" >&2
    FOUND=1
  fi
}

# ── Layer 1: Generic structural patterns (committed; shape-only) ──────────────

# 12-digit account-number shape.
# Negative lookbehind (?<!-) excludes UUID tails, which are always preceded by a hyphen.
run_check "12-digit account-number shape" '(?<!-)\b\d{12}\b' -i

# SSN shape: NNN-NN-NNNN
run_check "SSN shape (NNN-NN-NNNN)" '\b\d{3}-\d{2}-\d{4}\b'

# VIN-shaped 17-character token: uppercase letters (excluding I/O/Q) plus digits.
# Checked case-sensitively to avoid matching camelCase identifiers in source code.
run_check "VIN-shaped 17-char token" '\b[A-HJ-NPR-Z0-9]{17}\b'

# Host:port service-account shape (e.g. svc.vendor.local:5432 or db.internal.net:1433)
run_check "host:port service-account shape" '\b\w+\.\w+\.(com|net|local):[0-9]{4,5}\b' -i

# ── Layer 2: Private literal denylist (git-ignored; not present in CI) ────────
if [[ -f "$PRIVATE_LIST" ]]; then
  echo ">> sanitize: loading private literal denylist ($(basename "$PRIVATE_LIST"))"
  while IFS= read -r line || [[ -n "$line" ]]; do
    # Skip blank lines and comment lines
    [[ -z "$line" || "${line:0:1}" == '#' ]] && continue
    run_check "private denylist match" "$line" -i
  done < "$PRIVATE_LIST"
fi

# ── Result ────────────────────────────────────────────────────────────────────
if [[ "$FOUND" -eq 0 ]]; then
  echo "sanitize clean"
  exit 0
else
  echo "" >&2
  echo "!! sanitize FAILED — remove or relocate the above before publishing" >&2
  exit 1
fi
