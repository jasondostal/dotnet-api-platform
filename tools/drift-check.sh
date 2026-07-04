#!/usr/bin/env bash
# Drift gate runner: boots the API, captures its runtime OpenAPI, and compares it to the
# emitted TypeSpec contract via drift-check.js. Used locally and in the ADO pipeline.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EMITTED="$ROOT/spec/tsp-output/openapi.v1.yaml"
RUNTIME="$(mktemp /tmp/runtime-openapi.XXXXXX.json)"
PORT="${DRIFT_PORT:-5081}"

if [[ ! -f "$EMITTED" ]]; then
  echo "emitted contract not found: $EMITTED (run 'make spec' first)" >&2
  exit 2
fi

echo "Building API…"
dotnet build "$ROOT/src/ApiPlatform.Api/ApiPlatform.Api.csproj" -c Release --nologo >/dev/null

echo "Starting API on :$PORT…"
ASPNETCORE_URLS="http://127.0.0.1:$PORT" \
  dotnet run --project "$ROOT/src/ApiPlatform.Api/ApiPlatform.Api.csproj" -c Release --no-build --no-launch-profile >/dev/null 2>&1 &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true' EXIT

# Wait for health
for i in $(seq 1 30); do
  if curl -fsS "http://127.0.0.1:$PORT/health" >/dev/null 2>&1; then break; fi
  sleep 1
done

curl -fsS "http://127.0.0.1:$PORT/openapi/v1.json" -o "$RUNTIME"
echo "Captured runtime OpenAPI -> $RUNTIME"

node "$ROOT/tools/drift-check.js" "$RUNTIME" "$EMITTED"
