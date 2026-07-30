using Com.Salesforce.Marketingcloud;
using Com.Salesforce.Marketingcloud.Sfmcsdk;

namespace MarketingCloudSDK.Net.Android.Example;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnInitializeClicked(object? sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        var token = AccessTokenEntry.Text?.Trim();
        var serverUrl = ServerUrlEntry.Text?.Trim();
        var mid = MidEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(token) ||
            string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(mid))
        {
            AppendLog("Fill in all four MobilePush values first.");
            return;
        }

        // Android 13+ shows nothing without the runtime permission - the old sample forgot this
        // and push could never display.
        var granted = await Permissions.RequestAsync<Permissions.PostNotifications>();
        AppendLog($"POST_NOTIFICATIONS: {granted}");

        InitializeButton.IsEnabled = false;
        AppendLog("Initializing…");

        var context = global::Android.App.Application.Context;
        var config = MarketingCloudConfig.InvokeBuilder()
            .SetApplicationId(appId)
            .SetAccessToken(token)
            .SetMarketingCloudServerUrl(serverUrl)
            .SetMid(mid)
            .Build(context);

        // Configure the container, not the module: MarketingCloudConfig is an
        // EngagementModuleConfig, and SFMCSdk.Configure is what supplies the module the components
        // it initializes with. MarketingCloudSdk.Init, the pre-Unified-SDK entry point, hands the
        // module a null SFMCSdkComponents and dies in getEncryptionManager - RequestSdk below
        // would then never fire. The Action overload is the binding's, not the listener interface.
        var modules = new SFMCSdkModuleConfig.Builder
        {
            EngagementModuleConfig = config,
        }.Build();

        SFMCSdk.Configure(context, modules, status =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = $"Initialization: {status}";
                AppendLog($"Initialization completed: {status}");
                ContactKeyEntry.IsEnabled = true;
                ContactKeyButton.IsEnabled = true;
                TokenButton.IsEnabled = true;
            });
        });
    }

    private void OnAddTagClicked(object? sender, EventArgs e)
    {
        var tag = ContactKeyEntry.Text?.Trim();
        if (string.IsNullOrEmpty(tag))
        {
            AppendLog("Enter a tag first.");
            return;
        }

        MarketingCloudSdk.RequestSdk(sdk =>
        {
            // The registration editor batches changes and commits them in one network
            // round-trip. Contact-key WRITES moved to the unified identity in v11
            // (SFMCSdk.Identity, see SFMCSDK.Net.Android); the registration here carries tags,
            // attributes and the read-side of the identity.
            sdk.RegistrationManager.Edit()
                .AddTag(tag)
                .Commit();
            MainThread.BeginInvokeOnMainThread(() => AppendLog($"Tag '{tag}' committed."));
        });
    }

    private void OnShowRegistrationClicked(object? sender, EventArgs e)
    {
        MarketingCloudSdk.RequestSdk(sdk =>
        {
            var registration = sdk.RegistrationManager;
            var token = registration.SystemToken;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AppendLog($"Contact key: {registration.ContactKey ?? "(none)"}  Device id: {registration.DeviceId}");
                AppendLog(string.IsNullOrEmpty(token)
                    ? "No push token yet - is google-services.json configured?"
                    : $"Push token: {token}");
            });
        });
    }

    private void AppendLog(string line)
        => LogLabel.Text = $"{DateTime.Now:HH:mm:ss}  {line}\n{LogLabel.Text}";
}
