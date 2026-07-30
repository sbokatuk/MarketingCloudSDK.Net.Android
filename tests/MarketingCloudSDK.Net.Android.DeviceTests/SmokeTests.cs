using Com.Salesforce.Marketingcloud;

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

    private static readonly TaskCompletionSource<InitializationStatus> Initialized =
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
            // IllegalArgumentException before the SDK is ever reached otherwise. The Action
            // overload of Init is this repository's Additions code, so this check is also its
            // end-to-end proof.
            var config = MarketingCloudConfig.InvokeBuilder()
                .SetApplicationId("00000000-0000-4000-8000-000000000000")
                .SetAccessToken("devicetests-dummy-token0")
                .SetMarketingCloudServerUrl("https://localhost.invalid/")
                .SetMid("000000000")
                .Build(context);

            MarketingCloudSdk.Init(context, config, status =>
            {
                Reporter($"initialization status: {status}");
                Initialized.TrySetResult(status);
            });

            var completed = await Task.WhenAny(Initialized.Task, Task.Delay(TimeSpan.FromSeconds(90)));
            if (completed != Initialized.Task)
            {
                throw new TimeoutException("MarketingCloudSdk.Init never invoked its completion listener.");
            }
        }),

        new("request_sdk_hands_out_an_instance", async () =>
        {
            var ready = new TaskCompletionSource<MarketingCloudSdk>(TaskCreationOptions.RunContinuationsAsynchronously);
            MarketingCloudSdk.RequestSdk(sdk => ready.TrySetResult(sdk));

            var completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            if (completed != ready.Task)
            {
                throw new TimeoutException("MarketingCloudSdk.RequestSdk never called back with an instance.");
            }

            _ = ready.Task.Result.RegistrationManager
                ?? throw new InvalidOperationException("The ready SDK instance has no RegistrationManager.");
        }),
    ];
}
