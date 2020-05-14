using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DotNetify;
using DotNetify.Routing;
using GridNet.IoT.Types;
using GridNet.IoT.Web.React.server.Services;
using GridNet.IoT.Web.React.server.Utils;

namespace GridNet.IoT.Web.React.server.ViewModels
{
	public class MeterDashboard : BaseVM, IRoutable
	{
		readonly MeterService _meterService;

		public class SummationVM
		{
			public DateTimeOffset ReadTime;
			public double Value;
		}

		public class SavedSendCommand
		{
			public string MeterId { get; set; }
			public string Action { get; set; }
			public DateTimeOffset When { get; set; }
		}

		public class SavedSendConfigPolicy
		{
			public string MeterId { get; set; }
			public string Name { get; set; }
			public DateTimeOffset Start { get; set; }
			public DateTimeOffset? End { get; set; }
			public string ReadInterval { get; set; }
		}

		public class EventsVM
		{
			public ICollection<MeterEventVM> MeterEvents { get; set; }
		}
		public class MeterEventVM
		{
			public string EventTime { get; set; }
			public string EventType { get; set; }
		}

		public class CommandVModel
		{
			public string Action { get; set; }
			public string When { get; set; }
		}

		public Actions ActionType
		{
			get => Get<Actions>();
			set => Set(value);
		}
		public string MeterId { get; private set; }
		public int SummationWindow { get; set; }

		public ReactiveProperty<Data> Summations;

		public IEnumerable<Data.Summation> OldData { get; set; }

		public IEnumerable<MeterEventVM> OldEvents { get; set; }

		async Task LoadInitialData(string meterId)
		{
			OldData = await _meterService.GetOldSummations(MeterId, SummationWindow);
			OldEvents = (await _meterService.GetOldEvents(MeterId)).Select(s => new MeterEventVM
			{
				EventTime = s.EventTime.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss"),
				EventType = s.Event.Description()
			}).ToArray();
		}

		public MeterDashboard(MeterService meterService, EventsService eventsService)
		{
			_meterService = meterService;
			SummationWindow = 1;

			(this).OnRouted((sender, e) =>
			{
				MeterId = e?.From?.Replace($"{dotnetify_react_template.AppLayout.MeterDashboardPath}/", "");
				var meter = _meterService.GetMeter(MeterId);

				var context = _meterService.GetContext();

				LoadInitialData(MeterId).Wait();

				var latestInfo = _meterService.GetLatestContentInstance<Info>(context.App.InfoContainer).Result;
				var connInfo = meter.Info
					.Publish(latestInfo ?? new Info { MeterId = MeterId });
				connInfo.Connect();

				AddProperty<Info>("Info")
					.SubscribeTo(connInfo)
					.SubscribedBy(AddInternalProperty<bool>("UpdateI"), _ =>
					{
						base.PushUpdates();
						return true;
					});

				var latestState = _meterService.GetLatestContentInstance<State>(context.App.StateContainer).Result;
				var connState = meter.State
					.Publish(latestState != null ? latestState.Valve.Description() : "N/A");
				connState.Connect();

				AddProperty<string>("MeterState")
					.SubscribeTo(connState)
					.SubscribedBy(AddInternalProperty<bool>("UpdateS"), _ =>
					{
						base.PushUpdates();
						return true;
					});

				var latestCommand = _meterService.GetLatestContentInstance<Types.Command>(context.App.CommandContainer).Result;
				var connCmd = meter.Command
					.Select(s => new CommandVModel
					{
						Action = s.Action.Description(),
						When = s.When.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss")
					})
					.Publish(new CommandVModel
					{
						Action = latestCommand?.Action.Description(),
						When = latestCommand?.When.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss")
					});
				connCmd.Connect();

				AddProperty<CommandVModel>("MeterCommand")
					.SubscribeTo(connCmd)
					.SubscribedBy(AddInternalProperty<bool>("UpdateC"), _ =>
					{
						base.PushUpdates();
						return true;
					});

				var latestPolicy = _meterService.GetLatestContentInstance<Config.MeterReadPolicy>(context.App.ConfigContainer).Result;
				var connPolicy = meter.MeterReadPolicy
					.Publish(latestPolicy);
				connPolicy.Connect();

				AddProperty<Config.MeterReadPolicy>("MeterReadPolicy")
					.SubscribeTo(connPolicy)
					.SubscribedBy(AddInternalProperty<bool>("UpdateP"), _ =>
					{
						base.PushUpdates();
						return true;
					});

				Summations = AddProperty<Data>("Summations")
					.SubscribeTo(meter.Summations)
					.SubscribedBy(AddInternalProperty<bool>("Update"), _ =>
					{
						base.PushUpdates();
						return true;
					});

				AddProperty<EventsVM>("MeterEvents")
					.SubscribeTo(meter.Events.Select(s =>
					new EventsVM
					{
						MeterEvents = s.MeterEvents.Select(m => new MeterEventVM
						{
							EventTime = m.EventTime.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss"),
							EventType = m.Event.Description()
						}).ToArray()
					}))
					.SubscribedBy(AddInternalProperty<bool>("UpdateE"), _ =>
					{
						base.PushUpdates();
						return true;
					});
			});
		}

		public RoutingState RoutingState { get; set; }

		public Action<int> UpdateSummationWindow => summationWindow =>
		{
			this.OldData = _meterService.GetOldSummations(MeterId, summationWindow).Result;
			_meterService.GetOldSummations(MeterId, summationWindow).Wait();

			Changed(nameof(OldData));
		};

		public Action<SavedSendCommand> SendCommand => sendCommand =>
		{
			_meterService.AddCommand(new Types.Command
			{
				Action = sendCommand.Action == "openValve" ? Actions.OpenValve : Actions.CloseValve,
				When = sendCommand.When
			}).Wait();
		};

		public Action<SavedSendConfigPolicy> SendMeterReadPolicy => sendPolicy =>
		{
			_meterService.AddMeterReadPolicy(new Config.MeterReadPolicy
			{
				name = $"{sendPolicy.ReadInterval} Read",
				Start = sendPolicy.Start,
				End = sendPolicy.End,
				ReadInterval = sendPolicy.ReadInterval
			}).Wait();
		};
	}
}
