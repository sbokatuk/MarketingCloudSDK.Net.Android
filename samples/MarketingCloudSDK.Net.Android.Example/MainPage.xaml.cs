using Com.Salesforce.Marketingcloud;

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

        // The Action overload is this package's addition - natively Init takes a listener
        // interface that needs a Java.Lang.Object subclass.
        MarketingCloudSdk.Init(context, config, status =>
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
