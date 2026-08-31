using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using QoderCN.GatewayManager;

internal static class NativeEngineTests
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string BuildV1135Fixture()
    {
        return string.Join("\n", new string[] {
            PatcherEngine.ImportAnchor_v1135,
            PatcherEngine.ConverterAnchor_v1135,
            PatcherEngine.ModelUrlAnchor,
            PatcherEngine.CatalogAnchor_v1135,
            PatcherEngine.ValidationAnchor_v1135,
            PatcherEngine.InferenceRouteAnchor_v1135,
            PatcherEngine.StartupBYOKAnchor_v1135,
            PatcherEngine.ModelListAnchor_v1135,
            PatcherEngine.GetModelAnchor
        });
    }

    private static string BuildV1131Fixture()
    {
        return string.Join("\n", new string[] {
            PatcherEngine.ImportAnchor_v1131,
            PatcherEngine.ConverterAnchor_v1131,
            PatcherEngine.ModelUrlAnchor,
            PatcherEngine.CatalogAnchor_v1131,
            PatcherEngine.ValidationAnchor_v1131,
            PatcherEngine.InferenceRouteAnchor_v1131,
            PatcherEngine.StartupBYOKAnchor_v1131,
            PatcherEngine.ModelListAnchor_v1131,
            PatcherEngine.GetModelAnchor
        });
    }

    private static string BuildOriginalFixture()
    {
        return BuildV1135Fixture();
    }

    private static void TestProfileAndSecretStorage(string root)
    {
        string profile = Path.Combine(root, "profile.json");
        ProviderConfig config = new ProviderConfig();
        config.apiKey = "test-secret-that-must-not-appear-in-json";
        config.models.Add(new ModelItem { id = "model-a", efforts = new string[] { "low", "high" }, supportsDisabled = true });
        config.SaveToFile(profile);
        string json = File.ReadAllText(profile, Encoding.UTF8);
        Assert(json.IndexOf("test-secret", StringComparison.Ordinal) < 0, "Profile JSON contains the API key.");
        Assert(json.IndexOf("\"apiKey\"", StringComparison.Ordinal) < 0, "Profile JSON contains an apiKey property.");

        ProviderConfig loaded = ProviderConfig.LoadFromFile(profile);
        Assert(loaded.models.Count == 1 && loaded.models[0].efforts.Length == 2, "Model efforts were not preserved.");
        Assert(loaded.models[0].supportsDisabled == true, "supportsDisabled was not preserved.");

        SecretStore.Save(profile, config.apiKey);
        Assert(SecretStore.Load(profile) == config.apiKey, "DPAPI secret round trip failed.");
        SecretStore.Delete(profile);
        Assert(SecretStore.Load(profile) == "", "DPAPI secret deletion failed.");
    }

    private static void TestMultiProviderWorkspaceAndProviderSecrets(string root)
    {
        string wsPath = Path.Combine(root, "workspace.json");
        GatewayWorkspace ws = new GatewayWorkspace();

        ProviderItem p1 = new ProviderItem { id = "prov-1", name = "CPA Gateway", baseUrl = "http://192.168.50.241:8317/v1" };
        p1.models.Add(new ModelItem { id = "claude-3-7-sonnet", displayName = "Claude 3.7", reasoning = true, vision = true, selectedForInjection = true });
        p1.models.Add(new ModelItem { id = "gpt-4o", displayName = "GPT-4o", reasoning = false, vision = true, selectedForInjection = false });
        ws.providers.Add(p1);

        ProviderItem p2 = new ProviderItem { id = "prov-2", name = "Local Ollama", baseUrl = "http://127.0.0.1:11434/v1" };
        p2.models.Add(new ModelItem { id = "deepseek-r1:32b", displayName = "DeepSeek R1 32B", reasoning = true, vision = false, selectedForInjection = true });
        ws.providers.Add(p2);

        ws.selectedModelKeys.Add("claude-3-7-sonnet");
        ws.selectedModelKeys.Add("deepseek-r1:32b");

        ws.SaveToFile(wsPath);
        string rawJson = File.ReadAllText(wsPath, Encoding.UTF8);
        Assert(!rawJson.Contains("apiKey"), "Workspace JSON leaked API key field.");

        GatewayWorkspace loaded = GatewayWorkspace.LoadFromFile(wsPath);
        Assert(loaded.providers.Count == 2, "Failed to load 2 providers.");
        Assert(loaded.providers[0].models.Count == 2, "Provider 1 models count mismatch.");
        Assert(loaded.providers[1].models.Count == 1, "Provider 2 models count mismatch.");

        // Test Provider-Scoped DPAPI Keys
        SecretStore.SaveProviderKey("prov-1", "sk-prov-1-secret-key");
        SecretStore.SaveProviderKey("prov-2", "sk-prov-2-secret-key");

        Assert(SecretStore.LoadProviderKey("prov-1") == "sk-prov-1-secret-key", "Provider 1 secret mismatch.");
        Assert(SecretStore.LoadProviderKey("prov-2") == "sk-prov-2-secret-key", "Provider 2 secret mismatch.");

        // Test Runtime Compilation
        HashSet<string> activeKeys = new HashSet<string>(loaded.selectedModelKeys);
        ProviderConfig compiled = loaded.CompileToRuntimeConfig(activeKeys);
        Assert(compiled.models.Count == 2, "Compiled runtime models count mismatch (expected 2 active models).");
        Assert(compiled.models[0].upstreamBaseUrl == "http://192.168.50.241:8317/v1", "Model 1 did not inherit provider 1 baseUrl.");
        Assert(compiled.models[1].upstreamBaseUrl == "http://127.0.0.1:11434/v1", "Model 2 did not inherit provider 2 baseUrl.");

        SecretStore.DeleteProviderKey("prov-1");
        SecretStore.DeleteProviderKey("prov-2");
        Assert(SecretStore.LoadProviderKey("prov-1") == "", "Provider 1 deletion failed.");
    }

    private static void TestLegacySecretFallbackAndMigration(string root)
    {
        string profile = Path.Combine(root, "legacy-profile.json");
        File.WriteAllText(profile, "{}", new UTF8Encoding(false));

        byte[] legacyEntropy = Encoding.UTF8.GetBytes("QoderCN-GatewayManager/3.0.1/API-Key");
        byte[] plaintext = Encoding.UTF8.GetBytes("sk-legacy-301-secret");
        byte[] legacyProtected = ProtectedData.Protect(plaintext, legacyEntropy, DataProtectionScope.CurrentUser);

        // Manually write secret with legacy entropy
        string normalized = Path.GetFullPath(profile).Trim().ToUpperInvariant();
        byte[] digest;
        using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        StringBuilder name = new StringBuilder();
        foreach (byte b in digest) name.Append(b.ToString("x2"));
        string secretStoreDir = Environment.GetEnvironmentVariable("QODER_CN_SECRET_STORE_DIR");
        Directory.CreateDirectory(secretStoreDir);
        string secretFile = Path.Combine(secretStoreDir, name + ".bin");
        File.WriteAllBytes(secretFile, legacyProtected);

        // Load via SecretStore should gracefully decrypt with fallback and upgrade to v3.2.0 entropy
        string loaded = SecretStore.Load(profile);
        Assert(loaded == "sk-legacy-301-secret", "Legacy v3.0.1 secret fallback load failed.");

        // Verify it was re-saved with v3.2.0 entropy by unprotecting with v3.2.0 entropy directly
        byte[] updatedBytes = File.ReadAllBytes(secretFile);
        byte[] v320Entropy = Encoding.UTF8.GetBytes("QoderCN-GatewayManager/3.2.0/API-Key");
        byte[] unprot = ProtectedData.Unprotect(updatedBytes, v320Entropy, DataProtectionScope.CurrentUser);
        Assert(Encoding.UTF8.GetString(unprot) == "sk-legacy-301-secret", "Secret was not automatically upgraded to v3.2.0 entropy.");
    }

    private static void TestPatchRoutingAndFallbackChain()
    {
        string patched1135 = PatcherEngine.PatchRuntimeText(BuildV1135Fixture());
        Assert(patched1135.Contains(PatcherEngine.PatchMarker), "Current v3.2.0 patch marker is missing in v1135.");
        Assert(PatcherEngine.PatchMarker == "QODER_CN_OAI_PATCH_V3_2_0", "Patch marker version mismatch.");
        Assert(patched1135.Contains("custom-openai-provider-v3.2.0.json"), "v3.2.0 runtime configuration fallback is missing.");
        Assert(patched1135.Contains("custom-openai-provider-v3.1.0.json"), "v3.1.0 runtime configuration fallback is missing.");
        Assert(patched1135.Contains("process.env[\"QODER_CN_KEY_\"+(n.providerId||\"\")]||process.env.QODER_CN_CUSTOM_PROVIDER_API_KEY||t?.parameters?.api_key"), "Multi-provider API key lookup is missing.");
        Assert(!patched1135.Contains("qcv30target(A){try{"), "Direct-route errors are still swallowed by an outer try/catch.");
        Assert(!patched1135.Contains("api_key:e.apiKey") && !patched1135.Contains("api_key:qcc.apiKey"), "Injected model metadata still reads API keys from JSON.");

        string patched1131 = PatcherEngine.PatchRuntimeText(BuildV1131Fixture());
        Assert(patched1131.Contains(PatcherEngine.PatchMarker), "Current v3.2.0 patch marker is missing in v1131.");
        Assert(patched1131.Contains("function XxA(A){/*" + PatcherEngine.PatchMarker), "v1131 converter marker missing.");
    }

    private static void TestUpgradeChainDetection()
    {
        string fixture = BuildOriginalFixture();
        string v320Patched = PatcherEngine.PatchRuntimeText(fixture);

        // Applying to already patched runtime should throw
        bool threwAlreadyPatched = false;
        try { PatcherEngine.PatchRuntimeText(v320Patched); }
        catch (InvalidOperationException ex) { threwAlreadyPatched = ex.Message.Contains("already installed"); }
        Assert(threwAlreadyPatched, "Applying v3.2.0 on top of existing v3.2.0 did not throw expected exception.");

        // Applying on older v3.1.0 patch should detect older patch and instruct restore first
        string v310Simulated = fixture + "\n// QODER_CN_OAI_PATCH_V3_1_0\n";
        bool threwOlderDetected = false;
        try { PatcherEngine.PatchRuntimeText(v310Simulated); }
        catch (InvalidOperationException ex) { threwOlderDetected = ex.Message.Contains("older v2.x") || ex.Message.Contains("runtime patch is present"); }
        Assert(threwOlderDetected, "Applying v3.2.0 directly over v3.1.0 without restore baseline was not prevented.");
    }

    private static BackupManifest CreateManifest(string backupRoot, string installDir, string id, string content)
    {
        string dir = Path.Combine(backupRoot, id);
        Directory.CreateDirectory(dir);
        string backup = Path.Combine(dir, "qoder-worker-runtime.obf.mjs");
        File.WriteAllText(backup, content, new UTF8Encoding(false));
        BackupManifest manifest = new BackupManifest
        {
            backupId = id,
            createdAt = DateTime.UtcNow.ToString("o"),
            installDir = Path.GetFullPath(installDir),
            runtimePath = Path.Combine(installDir, PatcherEngine.RuntimeRelativePath),
            runtimeBackup = backup,
            runtimeSha256 = PatcherEngine.GetFileSha256(backup),
            patchVersion = "3.1.0"
        };
        manifest.Save(Path.Combine(dir, "manifest.json"));
        return manifest;
    }

    private static void TestBackupIsolation(string root)
    {
        string backupRoot = Path.Combine(root, "backups");
        string installA = Path.Combine(root, "install-a");
        string installB = Path.Combine(root, "install-b");
        string runtimeA = Path.Combine(installA, PatcherEngine.RuntimeRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimeA));
        File.WriteAllText(runtimeA, "patched-a", new UTF8Encoding(false));

        CreateManifest(backupRoot, installA, "backup-a", "original-a");
        System.Threading.Thread.Sleep(20);
        CreateManifest(backupRoot, installB, "backup-b", "original-b");

        BackupManifest selected = PatcherEngine.GetLatestBackup(backupRoot, installA);
        Assert(selected.backupId == "backup-a", "Restore selected a backup from another installation.");

        bool rejected = false;
        try { PatcherEngine.GetLatestBackup(backupRoot, installA, "backup-b"); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected, "A specific cross-install backup was not rejected.");

        PatcherEngine.RestorePatch(installA, backupRoot);
        Assert(File.ReadAllText(runtimeA, Encoding.UTF8) == "original-a", "Restore did not use the matching installation backup.");
    }

    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "qoder-native-tests-" + Guid.NewGuid().ToString("N"));
        string oldSecretStore = Environment.GetEnvironmentVariable("QODER_CN_SECRET_STORE_DIR");
        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("QODER_CN_SECRET_STORE_DIR", Path.Combine(root, "secrets"));
            
            Console.WriteLine("[RUN] Testing profile and DPAPI secret storage isolation...");
            TestProfileAndSecretStorage(root);

            Console.WriteLine("[RUN] Testing multi-provider workspace and provider-scoped DPAPI keys...");
            TestMultiProviderWorkspaceAndProviderSecrets(root);

            Console.WriteLine("[RUN] Testing legacy secret fallback and migration...");
            TestLegacySecretFallbackAndMigration(root);

            Console.WriteLine("[RUN] Testing v3.2.0 patch routing and fallback resolution chain...");
            TestPatchRoutingAndFallbackChain();

            Console.WriteLine("[RUN] Testing upgrade chain detection...");
            TestUpgradeChainDetection();

            Console.WriteLine("[RUN] Testing backup isolation and restore verification...");
            TestBackupIsolation(root);

            Console.WriteLine("[OK] All native engine P0 acceptance tests passed!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[FAIL] Test error: " + ex.Message + "\n" + ex.StackTrace);
            return 1;
        }
        finally
        {
            Environment.SetEnvironmentVariable("QODER_CN_SECRET_STORE_DIR", oldSecretStore);
            if (Directory.Exists(root))
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
