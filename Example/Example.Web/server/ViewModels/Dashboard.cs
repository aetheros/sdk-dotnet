using DotNetify;
using DotNetify.Routing;
using DotNetify.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace Example.Web.Server
{
	[Authorize]
	public class Dashboard : BaseVM, IRoutable
	{
		private readonly IDisposable _subscription;

		public class Activity
		{
			public string PersonName { get; set; }
			public Route Route { get; set; }
			public string Status { get; set; }
		}

		public RoutingState RoutingState { get; set; }

		public string[] ServerUsageLabel => ["dns", "sql", "nethst", "w2k", "ubnt", "uat", "ftp", "smtp", "exch", "demo"];
		public string[] UtilizationLabel => ["Memory", "Disk", "Network"];

		public Dashboard(ILiveDataService liveDataService)
		{
			AddProperty<string>("Download").SubscribeTo(liveDataService.Download);
			AddProperty<string>("Upload").SubscribeTo(liveDataService.Upload);
			AddProperty<string>("Latency").SubscribeTo(liveDataService.Latency);
			AddProperty<int>("Users").SubscribeTo(liveDataService.Users);
			AddProperty<int[]>("Traffic").SubscribeTo(liveDataService.Traffic);
			AddProperty<int[]>("ServerUsage").SubscribeTo(liveDataService.ServerUsage);
			AddProperty<int[]>("Utilization").SubscribeTo(liveDataService.Utilization);

			AddProperty<Activity[]>("RecentActivities").SubscribeTo(liveDataService.RecentActivity.Select(value =>
			{
				var activities = new Queue<Activity>(Get<Activity[]>("RecentActivities")?.Reverse() ?? Array.Empty<Activity>());
				activities.Enqueue(new Activity
				{
					PersonName = value.PersonName,
					Status = value.Status,
					Route = this.Redirect(nameof(Form), value.Id.ToString())
				});

				if (activities.Count > 4)
					activities.Dequeue();

				return activities.Reverse().ToArray();
			}));

			// Regulate data update interval to no less than every 200 msecs.
			_subscription = Observable
				.Interval(TimeSpan.FromMilliseconds(200))
				.StartWith(0)
				.Subscribe(_ => PushUpdates());
		}

		public override void Dispose()
		{
			_subscription?.Dispose();
			base.Dispose();
		}
	}
}
