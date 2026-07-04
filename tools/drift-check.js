#!/usr/bin/env node
/*
 * Drift gate: compares the RUNTIME OpenAPI (served at /openapi/v1.json) against the
 * EMITTED TypeSpec contract (spec/tsp-output/openapi.v1.yaml). It asserts the versioned
 * business surface (/v1/**) matches — no phantom routes (runtime not in the contract) and
 * no missing routes (contract not in the runtime). Infrastructure routes (/health,
 * /openapi*, root, webhooks) are excluded; they are not part of the published contract.
 *
 * Usage: node drift-check.js <runtime-openapi.json> <emitted-openapi.yaml>
 * Exit 0 = in sync, 1 = drift, 2 = usage/parse error.
 *
 * No external dependencies — runtime is JSON (parsed), emitted YAML is scanned for path keys.
 */
'use strict';
const fs = require('fs');

const INFRA = [/^\/health$/, /^\/openapi/, /^\/$/, /^\/v1$/, /^\/hooks(\/|$)/];

function isBusiness(p) {
  return p.startsWith('/v1/') && !INFRA.some((re) => re.test(p));
}

// Normalize path params: /v1/accounts/{accountId} -> /v1/accounts/{}
function normalize(p) {
  return p.replace(/\{[^}]+\}/g, '{}');
}

function runtimePaths(file) {
  const doc = JSON.parse(fs.readFileSync(file, 'utf8'));
  return Object.keys(doc.paths || {});
}

// Scan emitted YAML for the keys under the top-level `paths:` block.
function emittedPaths(file) {
  const lines = fs.readFileSync(file, 'utf8').split('\n');
  const out = [];
  let inPaths = false;
  for (const line of lines) {
    if (/^paths:\s*$/.test(line)) { inPaths = true; continue; }
    if (inPaths && /^\S/.test(line)) { inPaths = false; } // dedented to a new top-level key
    const m = inPaths && line.match(/^  (\/\S+):\s*$/);
    if (m) out.push(m[1]);
  }
  return out;
}

function setOf(paths) {
  return new Set(paths.filter(isBusiness).map(normalize));
}

function main() {
  const [, , runtimeFile, emittedFile] = process.argv;
  if (!runtimeFile || !emittedFile) {
    console.error('usage: drift-check.js <runtime-openapi.json> <emitted-openapi.yaml>');
    process.exit(2);
  }
  const runtime = setOf(runtimePaths(runtimeFile));
  const emitted = setOf(emittedPaths(emittedFile));

  const phantom = [...runtime].filter((p) => !emitted.has(p)); // runtime ∌ contract
  const missing = [...emitted].filter((p) => !runtime.has(p)); // contract ∌ runtime

  console.log(`runtime /v1 business paths: ${runtime.size}`);
  console.log(`emitted /v1 business paths: ${emitted.size}`);

  if (phantom.length === 0 && missing.length === 0) {
    console.log('DRIFT GATE: PASS — runtime surface matches the emitted contract.');
    process.exit(0);
  }
  if (phantom.length) console.error('PHANTOM routes (served but not in contract):\n  ' + phantom.join('\n  '));
  if (missing.length) console.error('MISSING routes (in contract but not served):\n  ' + missing.join('\n  '));
  console.error('DRIFT GATE: FAIL');
  process.exit(1);
}

main();
