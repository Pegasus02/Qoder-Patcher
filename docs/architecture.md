# v2.1 Architecture

## Target files

v2.1 reads the original `resources/app.asar` only to verify its known SHA-256. It modifies only:

```text
resources/app.asar.unpacked/
  node_modules/@qoder-ai/qoder-cn-agent-sdk/
  dist/_worker/qoder-worker-runtime.obf.mjs
```

## Injection points

The v2.1 patch injects six runtime changes:

1. Import synchronous `node:fs` access for the local JSON configuration.
2. Add configuration, base-URL normalization, and direct-target helpers.
3. Rewrite a saved BYOK model URL from `uiBaseUrl` to `upstreamBaseUrl` during model-entry conversion.
4. Replace one returned official Provider entry with the configured display name and model list.
5. Skip Qoder's remote BYOK validation when the request URL matches the configured UI URL.
6. Before inference, convert a matching BYOK request into an `external-openai` transport target.

## Why two URLs exist

The unmodified Qoder desktop UI only accepts HTTPS custom URLs. A local or LAN OpenAI-compatible endpoint may expose HTTP. The intended design is therefore:

```text
Qoder UI stores uiBaseUrl (HTTPS-shaped)
        ↓
Worker recognizes that exact URL
        ↓
Worker uses upstreamBaseUrl for inference
```

The desktop serialization path still omits the URL and keeps the replaced official Provider key. v2.1 therefore does not rely on those serialized fields. It matches `custom_model.model` against the local configuration, reads the API Key from Qoder's in-memory BYOK request, and creates this transport target:

```text
providerId : qoder-cn-patcher
adapter    : openai-compatible
baseUrl    : normalized upstreamBaseUrl
apiKey     : custom_model.parameters.api_key
model      : metadata from the local JSON configuration
```

Qoder's built-in external transport then posts directly to `<baseUrl>/chat/completions`. A configured model with a missing Key or invalid upstream URL fails locally instead of falling back to the Qoder gateway.

## Backup model

Each clean Apply operation stores:

- the original Worker Runtime;
- its SHA-256;
- the unchanged `app.asar` SHA-256;
- target paths and patch version in `manifest.json`.

Backups are local and excluded from Git. An installed v2 patch is upgraded by locating a backup whose Runtime hash matches the supported original, generating v2.1 from that original, and retaining the same restore point.
