using DotNetify;
using DotNetify.Routing;
using DotNetify.Security;

using Example.Types;
using Example.Web.Server.Services;
using Example.Web.Server.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Example.Web.Server.ViewModels
{
	[Authorize]
	public class MeterList : BaseVM, DotNetify.Routing.IRoutable
	{
		public string Meters_itemKey => nameof(Meter.MeterId);

		private bool _showNotification;
		public bool ShowNotification
		{
			get
			{
				var value = _showNotification;
				_showNotification = false;
				return value;
			}
			set
			{
				_showNotification = value;
				Changed(nameof(ShowNotification));
			}
		}

		public RoutingState RoutingState { get; set; }

		public ReactiveProperty<MeterListRow[]> Meters = new ReactiveProperty<MeterListRow[]>();


		public MeterList(ModelContext modelContext, MeterService meterService, DataService dataService)
		{
			this.RegisterRoutes(nameof(MeterList), new List<RouteTemplate>
			{
				new RouteTemplate(nameof(MeterDashboard)) { UrlPattern = $"{nameof(MeterDashboard)}(/:id)" },
			});
			this.OnRouted((sender, e) => System.Diagnostics.Debug.WriteLine(e.From));

			var objLock = new object();

			async Task<MeterListRow[]> GetRows(Meter meter)
			{
				Debug.WriteLine($">> GetRows {meter.MeterId}");

				var latestState = await meterService.GetLatestContentInstanceAsync<State>(meter.MeterUrl + modelContext.App.StateContainer);
				var stateStr = latestState != null ? latestState.Valve.Description() : "N/A";

				var route = this.GetRoute(nameof(MeterDashboard), $"{nameof(MeterDashboard)}/{meter.MeterId}");

				//Subscribe to State to update a row and push updates to views
				meter.State.Subscribe(newState =>
				{
					Debug.WriteLine($">> UpdateList {meter.MeterId}");

					this.UpdateList("Meters", new MeterListRow(meter.MeterId, newState, this.GetRoute(nameof(MeterDashboard), $"{nameof(MeterDashboard)}/{meter.MeterId}")));
					PushUpdates();

					Debug.WriteLine($"<< UpdateList {meter.MeterId}");
				});

				Debug.WriteLine($"<< GetRows {meter.MeterId}");

				lock (objLock)
				{
					var meters = new List<MeterListRow>(Get<MeterListRow[]>("Meters") ?? new MeterListRow[] { });
					meters.Add(new MeterListRow(meter.MeterId, stateStr, route));
					return meters.ToArray();
				}
			}

			var meterListRows = meterService.Meters.Select(l => Observable.FromAsync(() => GetRows(l)));

			AddProperty<MeterListRow[]>("Meters")
				.SubscribeTo(Observable.Concat(meterListRows))
				.SubscribedBy(AddInternalProperty<bool>("Update"), _ =>
				{
					PushUpdates();
					return true;
				});
		}
	}
}
