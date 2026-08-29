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

## v2.1 direction

The next implementation should:

1. Persist a real `provider: custom` model instead of impersonating `bailian`.
2. Populate both `custom_model.url` and `model_config.url` during Headless session policy construction.
3. Preserve `format: openai` and the selected model id.
4. Add a runtime guard that rejects a custom-model request if the resolved target remains Qoder's gateway.
5. Require deletion and recreation of models saved by v2.
6. Verify the resolved target in logs before sending an end-to-end test prompt.
