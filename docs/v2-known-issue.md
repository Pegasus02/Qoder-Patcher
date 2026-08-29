# v2 Known Issue: Request Still Uses Qoder Gateway

## Symptom

The Provider and models appear in Qoder CN, and the model can be added, but a task fails with:

```text
自定义模型服务异常
Failed to generate custom pool
```

## Evidence

The Qoder Runtime log for the failed request showed:

```text
model_config.url = ""
custom_model.provider = "bailian"
custom_model.model = "gpt-5.6-sol"
```

The request target was Qoder's inference gateway rather than the configured CPA endpoint. Qoder's server then returned HTTP 400 with `Failed to generate custom pool`.

## Cause

The injected catalog entry reused an official Provider key to pass desktop validation. The desktop model-save path persisted that official Provider key but omitted the injected URL. The patched `byokEntryToModelEntry` URL conversion therefore never received the custom URL and could not rewrite it.

This is not evidence of CPA downtime or an API-key authentication failure; the failed request did not reach CPA.

## v2.1 resolution

v2.1 avoids the failing cloud-pool path rather than trying to make the Qoder gateway reach a LAN service:

1. Match a BYOK request to a model declared in the local patch configuration.
2. Reuse the API Key already loaded by Qoder for that BYOK model.
3. Construct an `external-openai` target with the configured LAN base URL and model metadata.
4. Let Qoder's built-in OpenAI-compatible SSE transport send the request directly.
5. Throw locally when required direct-route data is missing, preventing cloud fallback.

This design means a model saved under v2 can normally be reused; deletion and recreation should not be necessary. Installation-level CPA verification is still required before calling the issue fully closed.
