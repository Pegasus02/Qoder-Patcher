# v2 Architecture

## Target files

v2 reads the original `resources/app.asar` only to verify its known SHA-256. It modifies only:

```text
resources/app.asar.unpacked/
  node_modules/@qoder-ai/qoder-cn-agent-sdk/
  dist/_worker/qoder-worker-runtime.obf.mjs
```

## Injection points

The v2 baseline injects five runtime changes:

1. Import synchronous `node:fs` access for the local JSON configuration.
2. Add a configuration reader and URL-rewrite helper.
3. Rewrite a saved BYOK model URL from `uiBaseUrl` to `upstreamBaseUrl` during model-entry conversion.
4. Replace one returned official Provider entry with the configured display name and model list.
5. Skip Qoder's remote BYOK validation when the request URL matches the configured UI URL.

## Why two URLs exist

The unmodified Qoder desktop UI only accepts HTTPS custom URLs. A local or LAN OpenAI-compatible endpoint may expose HTTP. The intended design is therefore:

```text
Qoder UI stores uiBaseUrl (HTTPS-shaped)
        ↓
Worker recognizes that exact URL
        ↓
Worker uses upstreamBaseUrl for inference
```

The current desktop serialization path omits the URL before the patched model-entry converter runs, which is the v2 end-to-end limitation.

## Backup model

Each Apply operation stores:

- the original Worker Runtime;
- its SHA-256;
- the unchanged `app.asar` SHA-256;
- target paths and patch version in `manifest.json`.

Backups are local and excluded from Git.
