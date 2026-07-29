using System;

namespace Com.Salesforce.Marketingcloud
{
	/// <summary>
	/// Bridges a C# delegate to <c>MarketingCloudSdk.InitializationListener</c>. Without this
	/// every consumer has to write their own <c>Java.Lang.Object</c> listener subclass - the old
	/// sfmc-net-bindings-mobile consumers carried exactly that boilerplate.
	/// </summary>
	public sealed class InitializationListener : Java.Lang.Object, MarketingCloudSdk.IInitializationListener
	{
		private readonly Action<InitializationStatus> onComplete;

		public InitializationListener(Action<InitializationStatus> onComplete)
			=> this.onComplete = onComplete ?? throw new ArgumentNullException(nameof(onComplete));

		public void Complete(InitializationStatus status) => onComplete(status);
	}

	/// <summary>Bridges a C# delegate to <c>MarketingCloudSdk.WhenReadyListener</c>.</summary>
	public sealed class WhenReadyListener : Java.Lang.Object, MarketingCloudSdk.IWhenReadyListener
	{
		private readonly Action<MarketingCloudSdk> onReady;

		public WhenReadyListener(Action<MarketingCloudSdk> onReady)
			=> this.onReady = onReady ?? throw new ArgumentNullException(nameof(onReady));

		public void Ready(MarketingCloudSdk sdk) => onReady(sdk);
	}

	public partial class MarketingCloudSdk
	{
		/// <summary>
		/// <see cref="Init(Android.Content.Context, MarketingCloudConfig, IInitializationListener)"/>
		/// with a C# delegate in place of the listener interface. The delegate is invoked once,
		/// with the terminal status.
		/// </summary>
		public static void Init(Android.Content.Context context, MarketingCloudConfig config, Action<InitializationStatus> onComplete)
			=> Init(context, config, new InitializationListener(onComplete));

		/// <summary>
		/// <see cref="RequestSdk(IWhenReadyListener)"/> with a C# delegate. The delegate runs
		/// once the SDK reaches its operational state, with the ready instance.
		/// </summary>
		public static void RequestSdk(Action<MarketingCloudSdk> onReady)
			=> RequestSdk(new WhenReadyListener(onReady));
	}
}
