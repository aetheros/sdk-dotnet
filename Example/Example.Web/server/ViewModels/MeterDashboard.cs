using DotNetify;
using DotNetify.Routing;

using Example.Types;
using Example.Web.Server.Services;
using Example.Web.Server.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using TaskTupleAwaiter;

namespace Example.Web.Server.ViewModels
{
	public class MeterDashboard : BaseVM, IRoutable
	{
		const string DateTimeFormat = "MM/dd/yyyy HH:mm:ss";

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

		public MeterDashboard(MeterService meterService, EventsService eventsService)
		{
			_meterService = meterService;
			SummationWindow = 1;

			(this).OnRouted((sender, e) =>
			{
				MeterId = e?.From?.Replace($"{nameof(MeterDashboard)}/", "");
				var app = _meterService.App;

				var loadTask = Task.Run(async () =>
				{
					var (meter, oldData, oldEvents) = await (
						_meterService.GetMeterAsync(MeterId),
						_meterService.GetOldSummationsAsync(MeterId, SummationWindow),
						_meterService.GetOldEventsAsync(MeterId)
					);
					if (meter.MeterId != this.MeterId)
						return null;

					this.OldData = oldData;
					this.OldEvents = oldEvents
						.Select(s => new MeterEventVM
						{
							EventTime = s.EventTime.ToLocalTime().ToString(DateTimeFormat),
							EventType = s.Event.Description()
						})
						.ToArray();

					Changed(nameof(OldData));
					Changed(nameof(OldEvents));

					return meter;
				});

				Func<object, bool> PushPropertyUpdates = _ =>
				{
					base.PushUpdates();
					return true;
				};

				AddProperty<Info>("Info")
					.SubscribeTo(Observable.Defer<Info>(async () =>
					{
						var meter = await loadTask;
						var latest = await _meterService.GetLatestContentInstanceAsync<Info>(meter.MeterUrl + app.InfoContainer);
						var connInfo = meter.Info
							.Publish(latest ?? new Info { MeterId = latest.MeterId });
						connInfo.Connect();
						return connInfo;
					}))
					.SubscribedBy(AddInternalProperty<bool>("UpdateI"), PushPropertyUpdates);


				AddProperty<string>("MeterState")
					.SubscribeTo(Observable.Defer<string>(async () =>
					{
						var meter = await loadTask;
						var latest = await _meterService.GetLatestContentInstanceAsync<State>(meter.MeterUrl + app.StateContainer);
						var latestValue = latest != null ? latest.Valve.Description() : "N/A";
						var connState = meter.State
							.Publish(latestValue);
						connState.Connect();
						return connState;
					}))
					.SubscribedBy(AddInternalProperty<bool>("UpdateS"), PushPropertyUpdates);


				AddProperty<CommandVModel>("MeterCommand")
					.SubscribeTo(Observable.Defer<CommandVModel>(async () =>
					{
						var meter = await loadTask;
						var latest = await _meterService.GetLatestContentInstanceAsync<Types.Command>(meter.MeterUrl + app.CommandContainer);
						var connCmd = meter.Command
							.Select(s => new CommandVModel
							{
								Action = s.Action.Description(),
								When = s.When.ToLocalTime().ToString(DateTimeFormat)
							})
							.Publish(new CommandVModel
							{
								Action = latest?.Action.Description(),
								When = latest?.When.ToLocalTime().ToString(DateTimeFormat)
							});
						connCmd.Connect();
						return connCmd;
					}))
					.SubscribedBy(AddInternalProperty<bool>("UpdateC"), PushPropertyUpdates);


				AddProperty<Config.MeterReadPolicy>("MeterReadPolicy")
					.SubscribeTo(Observable.Defer<Config.MeterReadPolicy>(async () =>
					{
						var meter = await loadTask;
						var latest = await _meterService.GetLatestContentInstanceAsync<Config.MeterReadPolicy>(meter.MeterUrl + app.ConfigContainer);
						var connPolicy = meter.MeterReadPolicy
							.Publish(latest);
						connPolicy.Connect();
						return connPolicy;
					}))
					.SubscribedBy(AddInternalProperty<bool>("UpdateP"), PushPropertyUpdates);


				this.Summations = AddProperty<Data>("Summations")
					.SubscribeTo(Observable.Defer(async () =>
					{
						var meter = await loadTask;
						return meter.Summations;
					}))
					.SubscribedBy(AddInternalProperty<bool>("Update"), PushPropertyUpdates);


				AddProperty<EventsVM>("MeterEvents")
					.SubscribeTo(Observable.Defer<EventsVM>(async () =>
					{
						var meter = await loadTask;
						return meter.Events
							.Select(s =>
								new EventsVM
								{
									MeterEvents = s.MeterEvents.Select(m => new MeterEventVM
									{
										EventTime = m.EventTime.ToLocalTime().ToString(DateTimeFormat),
										EventType = m.Event.Description()
									}).ToArray()
								}
							);
					}))
					.SubscribedBy(AddInternalProperty<bool>("UpdateE"), PushPropertyUpdates);
			});
		}

		public RoutingState RoutingState { get; set; }

		public Action<int> UpdateSummationWindow => summationWindow =>
		{
			Task.Run(async () =>
			{
				this.OldData = await _meterService.GetOldSummationsAsync(MeterId, summationWindow);
				Changed(nameof(OldData));
			});
		};

		public Action<SavedSendCommand> SendCommand => sendCommand =>
		{
			Task.Run(async () => await _meterService.AddCommandAsync(new Types.Command
			{
				Action = sendCommand.Action == "openValve" ? Actions.OpenValve : Actions.CloseValve,
				When = sendCommand.When
			}));
		};

		public Action<SavedSendConfigPolicy> SendMeterReadPolicy => sendPolicy =>
		{
			Task.Run(async () => await _meterService.AddMeterReadPolicyAsync(new Config.MeterReadPolicy
			{
				Name = $"{sendPolicy.ReadInterval} Read",
				Start = sendPolicy.Start,
				End = sendPolicy.End,
				ReadInterval = sendPolicy.ReadInterval
			}));
		};

		Random _random = new Random();

		public Action<bool> AddData => _ =>
		{
			Task.Run(async () =>
			{
				var app = _meterService.App;
				await app.Application.AddContentInstanceAsync(
					app.DataContainer,
					new Data
					{
						MeterId = this.MeterId,
						UOM = Data.Units.USGal,
						Summations = new[] {
							new Data.Summation {
								ReadTime = DateTimeOffset.UtcNow,
								Value = _random.NextDouble(),
							}
						}
					}
				);
			});
		};
	}
}

