using Com.Salesforce.Marketingcloud;

// Both packages ship an InitializationStatus; the alias keeps the unified SDK's types
// distinguishable from this package's without a using-directive collision.
using Sfmc = Com.Salesforce.Marketingcloud.Sfmcsdk;

namespace MarketingCloudSDK.Net.Android.DeviceTests;

/// <summary>One check: a name and something that throws when the SDK misbehaves.</summary>
public sealed record SmokeTest(string Name, Func<Task> Execute);

/// <summary>
/// Drives the real, packed bindings on a real Android runtime. No credentials: the configuration
/// below points at no tenant, so nothing registers anywhere - what is being proven is that the
/// native classes across all seven shipped .aars (plus the SFMCSDK.Net.Android dependency's two)
/// are present, the JNI surface works, and configuration reaches the SDK.
/// </summary>
public static class SmokeTests
{
    /// <summary>Where progress lines go; MainActivity points this at logcat.</summary>
    public static Action<string> Reporter { get; set; } = _ => { };

    private static readonly TaskCompletionSource<Sfmc.IInitializationStatus> Initialized =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static readonly SmokeTest[] All =
    [
        new("native_classes_are_present_in_every_shipped_aar", () =>
        {
            // Class.forName is the same lookup JNI performs; a missing .aar fails here with
            // ClassNotFoundException naming it, rather than three checks later with something
            // baffling. One marker per .aar this package ships, plus one per .aar arriving
            // through the SFMCSDK.Net.Android dependency.
            //
            // The three-argument overload, and the application's own loader: the one-argument
            // Class.forName resolves against the *caller's* class loader, and a call arriving
            // over JNI has no application frame to take one from - so every lookup goes to the
            // boot class loader, which knows nothing about the app's dex files and fails every
            // marker with "Class not found using the boot class loader". initialize: false
            // because presence is the question here, not static initialization.
            var loader = global::Android.App.Application.Context.ClassLoader
                ?? throw new InvalidOperationException("The application context has no class loader.");

            foreach (var marker in new[]
            {
                "com.salesforce.marketingcloud.MarketingCloudSdk",                    // marketingcloudsdk
                "com.salesforce.marketingcloud.pushfeature.BuildConfig",              // pushfeaturemodule
                "com.salesforce.marketingcloud.pushmodels.BuildConfig",               // pushmodelsmodule
                "com.salesforce.marketingcloud.inappmessagingfeature.BuildConfig",    // inappmessagingfeaturemodule
                "com.salesforce.marketingcloud.inappmessaging.models.AppConfigEvent", // inappmessagingmodelsmodule
                "com.salesforce.marketingcloud.UrlHandler",                           // common
                "com.salesforce.marketingcloud.legacycrypto.OldSdkHash",              // legacy-crypto
                "com.salesforce.marketingcloud.sfmcsdk.SFMCSdk",                      // sfmcsdk (dependency)
                "com.salesforce.marketingcloud.internal.util.PermissionUtils",        // common-internal (dependency)
            })
            {
                Java.Lang.Class.ForName(marker, initialize: false, loader);
            }

            return Task.CompletedTask;
        }),

        new("init_invokes_the_completion_listener", async () =>
        {
            var context = global::Android.App.Application.Context;

            // Dummy values shaped like real ones; the SDK accepts them and fails server-side,
            // which is the isolation these checks want. "Shaped like" is not cosmetic:
            // MarketingCloudConfig.Builder.build validates the applicationId against a version-4
            // UUID regex and requires the accessToken to be exactly 24 characters, and throws
            // IllegalArgumentException before the SDK is ever reached otherwise.
            var config = MarketingCloudConfig.InvokeBuilder()
                .SetApplicationId("00000000-0000-4000-8000-000000000000")
                .SetAccessToken("devicetests-dummy-token0")
                .SetMarketingCloudServerUrl("https://localhost.invalid/")
                .SetMid("000000000")
                .Build(context);

            // SFMCSdk.configure, not MarketingCloudSdk.init. In the Unified SDK generation the
            // push module is a *module*: its public two-argument init hands the module a null
            // SFMCSdkComponents and initialization dies with a NullPointerException out of
            // SFMCSdkComponents.getEncryptionManager. Configuring the container is what supplies
            // those components and then drives the engagement module's own init - and
            // MarketingCloudConfig is an EngagementModuleConfig precisely so it can be handed
            // over this way. Consumers must do the same, so the smoke test does too.
            var modules = new Sfmc.SFMCSdkModuleConfig.Builder
            {
                EngagementModuleConfig = config,
            }.Build();

            Sfmc.SFMCSdk.Configure(context, modules, status =>
            {
                Reporter($"initialization status: {status}");
                Initialized.TrySetResult(status);
            });

            var completed = await Task.WhenAny(Initialized.Task, Task.Delay(TimeSpan.FromSeconds(90)));
            if (completed != Initialized.Task)
            {
                throw new TimeoutException("SFMCSdk.Configure never invoked its completion listener.");
            }
        }),

        new("request_sdk_hands_out_an_instance", async () =>
        {
            // MarketingCloudSdk.RequestSdk with a C# delegate is this repository's Additions
            // code, so this check is also its end-to-end proof. The listener only fires once the
            // engagement module reaches its operational state, which is why the configure check
            // above has to run - and succeed - first.
            var ready = new TaskCompletionSource<MarketingCloudSdk>(TaskCreationOptions.RunContinuationsAsynchronously);
            MarketingCloudSdk.RequestSdk(sdk => ready.TrySetResult(sdk));

            var completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(60)));
            if (completed != ready.Task)
            {
                throw new TimeoutException("MarketingCloudSdk.RequestSdk never called back with an instance.");
            }

            _ = ready.Task.Result.RegistrationManager
                ?? throw new InvalidOperationException("The ready SDK instance has no RegistrationManager.");
        }),
    ];
}
