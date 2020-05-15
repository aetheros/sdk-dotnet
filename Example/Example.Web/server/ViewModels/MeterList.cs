using DotNetify;
using DotNetify.Security;

using Example.Types;
using Example.Web.Server.Services;
using Example.Web.Server.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace Example.Web.Server.ViewModels
{
	[Authorize]
	public class MeterList : BaseVM
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

		public ReactiveProperty<MeterListRow[]> Meters = new ReactiveProperty<MeterListRow[]>();

		public MeterList(ModelContext modelContext, MeterService meterService, DataService dataService)
		{
			Meters.SubscribeTo(meterService.Meters.Select(value =>
			{
				var meters = new Queue<MeterListRow>(Get<MeterListRow[]>("Meters")?.Reverse() ?? new MeterListRow[] { });

				var latestState = meterService.GetLatestContentInstance<State>(modelContext.App.StateContainer).Result;
				var stateStr = latestState != null ? latestState.Valve.Description() : "N/A";
				meters.Enqueue(new MeterListRow(value.MeterId, stateStr));

				//Subscribe to State to update a row and push updates to views
				value.State.Subscribe(newState =>
				{
					this.UpdateList("Meters", new MeterListRow(value.MeterId, newState));
					PushUpdates();
				});

				return meters.ToArray();
			})).SubscribedBy(AddInternalProperty<bool>("Update"), _ =>
			{
				PushUpdates();
				return true;
			});
		}
	}
}
