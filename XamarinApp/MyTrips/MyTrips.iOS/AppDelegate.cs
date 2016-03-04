﻿using System;
using System.Threading.Tasks;

using CoreSpotlight;
using Foundation;
using UIKit;

using MyTrips.Utils;
using MyTrips.Interfaces;
using MyTrips.iOS.Helpers;
using MyTrips.ViewModel;

using HockeyApp;
using MyTrips.DataStore.Abstractions;

namespace MyTrips.iOS
{
	[Register("AppDelegate")]
	public class AppDelegate : UIApplicationDelegate
	{
		public override UIWindow Window { get; set; }

		public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
		{
			ThemeManager.ApplyTheme();
			ViewModel.ViewModelBase.Init();

            ServiceLocator.Instance.Add<IAuthentication, Authentication>();
            ServiceLocator.Instance.Add<MyTrips.Utils.Interfaces.ILogger, MyTrips.Shared.PlatformLogger>();
//            ServiceLocator.Instance.Add<IHubIOT, IOTHub>();
            //            ServiceLocator.Instance.Add<IOBDDevice, OBDDevice>();
            Xamarin.Insights.Initialize(Logger.InsightsKey);
			if (!string.IsNullOrWhiteSpace(Logger.HockeyAppiOS))
			{
				Setup.EnableCustomCrashReporting(() =>
					{
						var manager = BITHockeyManager.SharedHockeyManager;
						manager.Configure(Logger.HockeyAppiOS);
						manager.StartManager();
						manager.Authenticator.AuthenticateInstallation();
						AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
							Setup.ThrowExceptionAsNative(e.ExceptionObject);
						TaskScheduler.UnobservedTaskException += (sender, e) =>
							Setup.ThrowExceptionAsNative(e.Exception);
					});
			}

			if (!Settings.Current.IsLoggedIn)
			{
				var viewController = UIStoryboard.FromName("Main", null).InstantiateViewController("loginViewController"); // Storyboard.InstantiateViewController("loginViewController");
				Window.RootViewController = viewController;
			}
			else
			{
				var tabBarController = Window.RootViewController as UITabBarController;
				tabBarController.SelectedIndex = 1;
			}

			return true;
		}

		#region Background Refresh

		// Minimum number of seconds between a background refresh
		// 15 minutes = 15 * 60 = 900 seconds
		private const double MINIMUM_BACKGROUND_FETCH_INTERVAL = 900;

		private void SetMinimumBackgroundFetchInterval()
		{
			UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(MINIMUM_BACKGROUND_FETCH_INTERVAL);
		}

		// Called whenever your app performs a background fetch
		public override async void PerformFetch(UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
		{
			// Do Background Fetch
			var downloadSuccessful = false;
			try
			{
				// Download data
				var manager = ServiceLocator.Instance.Resolve<IStoreManager>() as DataStore.Azure.StoreManager;
				if (manager != null)
				{
					await manager.SyncAllAsync(true);
					downloadSuccessful = true;
				}

			}
			catch (Exception ex)
			{
				Logger.Instance.Report(ex);			
			}

			// If you don't call this, your application will be terminated by the OS.
			// Allows OS to collect stats like data cost and power consumption
			if (downloadSuccessful)
			{
				completionHandler(UIBackgroundFetchResult.NewData);
			}
			else {
				completionHandler(UIBackgroundFetchResult.Failed);
			}
		}

		#endregion
		#region CoreSpotlight Search
		public override bool ContinueUserActivity(UIApplication application, NSUserActivity userActivity, UIApplicationRestorationHandler completionHandler)
		{
			if (userActivity.ActivityType == CSSearchableItem.ActionType)
			{
				var uuid = userActivity.UserInfo.ObjectForKey(CSSearchableItem.ActivityIdentifier);

				if (uuid == null)
					return true;

				// Navigate to trip
				var appDelegate = (AppDelegate)application.Delegate;
				var tabBarController = (TabBarController)appDelegate.Window.RootViewController;
				var navigationController = (UINavigationController)tabBarController.ViewControllers[0];
				var tripsViewController = (TripsTableViewController) navigationController.TopViewController;
				tripsViewController.NavigationController.PopToRootViewController(false);


				var trip = tripsViewController.ViewModel.Trips[Int32.Parse(uuid.ToString())];

				var currentTripVc = UIStoryboard.FromName("Main", null).InstantiateViewController("CURRENT_TRIP_STORYBIARD_IDENTIFIER") as CurrentTripViewController;
				currentTripVc.PastTripsDetailViewModel = new PastTripsDetailViewModel(trip);
				tripsViewController.NavigationController.PushViewController(currentTripVc, false);
			}

			return true;
		}
		#endregion
	}


    [Register("TripApplication")]
    public class TripApplication : UIApplication
    {
        public override void MotionBegan(UIEventSubtype motion, UIEvent evt)
        {
            if (motion == UIEventSubtype.MotionShake)
            {
                BITHockeyManager.SharedHockeyManager.FeedbackManager.ShowFeedbackComposeViewWithGeneratedScreenshot();
            }
        }
    }
}
