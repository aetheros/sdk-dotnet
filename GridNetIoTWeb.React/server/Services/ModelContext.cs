using Aetheros.OneM2M.Api;
using Aetheros.OneM2M.Binding;

using GridNet.IoT.Types;
using GridNet.IoT.Web.React.server.Utils;

using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace GridNet.IoT.Web.React.server.Services
{
	public class MyApplication
	{
		public Application Application { get; set; }

		public string DataContainer { get; set; }
		public string EventsContainer { get; set; }
		public string InfoContainer { get; set; }
		public string StateContainer { get; set; }
		public string ConfigContainer { get; set; }
		public string CommandContainer { get; set; }


		public Connection Api => Application.Connection;
	}

	public partial class ModelContext
	{
		public MyApplication App { get; }

		public readonly Dictionary<string, Meter> Meters = new Dictionary<string, Meter>();
		List<AE> _deviceAEs;

		public ModelContext(IOptions<WebOptions> opts)
		{
			var options = opts.Value;
			var con = new Connection(options.M2M);
			var ae = con.FindApplication(options.InCse, options.AE.AppId).Result;
			var app = Application.Register(options.M2M, options.AE, options.InCse, options.CAUrl).Result;
			//var app = new Application(con, ae.App_ID, ae.AE_ID, options.PoaUrl);
			//if (app == null)
			//	app = api.RegisterApplication(options.InCse, credentialId, options.AE.AppId, options.PoaUrl);
			//app.PoaUrl = options.PoaUrl;

			this.App = new MyApplication
			{
				Application = app,
				DataContainer = options.DataContainer,
				EventsContainer = options.EventsContainer,
				InfoContainer = options.InfoContainer,
				StateContainer = options.StateContainer,
				ConfigContainer = options.ConfigContainer,
				CommandContainer = options.CommandContainer,
			};

			DiscoverAEs().Wait();

			LoadObservableData().Wait();
		}

		public async Task DiscoverAEs()
		{
			Debug.WriteLine("===========================ModelContext.DiscoverAEs()");

			//**note**
			//cannot find announced AE in dev8
			var responseFilterContainers = await App.Application.GetPrimitiveAsync("/PN_CSE", new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.AE },
				Attribute = Connection.GetAttributes<AE>(_ => _.App_ID == App.Application.AppId),
			});

			_deviceAEs = await responseFilterContainers.URIList
				.ToAsyncEnumerable()
				.SelectAsync(async url => await App.Application.GetPrimitiveAsync(url))
				.Select(rc => rc.AE)
				.ToListAsync();
		}

		public async Task LoadObservableData()
		{
			Debug.WriteLine("===========================ModelContext.LoadData()");

			var app = App.Application;
			var dataContainer = await app.EnsureContainerAsync(App.DataContainer);
			var eventsContainer = await app.EnsureContainerAsync(App.EventsContainer);

			//server app container subscriptions
			var dataSubscription = (await app.ObserveAsync(App.DataContainer))
				.Where(evt => evt.NotificationEventType.Contains(NotificationEventType.CreateChild))
				.Select(evt => evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<Data>())
				.Where(data => data != null);

			var eventSubscription = (await app.ObserveAsync(App.EventsContainer))
				.Where(evt => evt.NotificationEventType.Contains(NotificationEventType.CreateChild))
				.Select(evt => evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<Events>())
				.Where(events => events != null);

			foreach (var deviceAE in _deviceAEs)
			{
				//**note**
				//no announced AEcan be found in dev8 and the below containers cannot be created as client app containers
				//workaround: created as server app containers
				var infoContainer = await app.EnsureContainerAsync(App.InfoContainer);
				var stateContainer = await app.EnsureContainerAsync(App.StateContainer);
				var configContainer = await app.EnsureContainerAsync(App.ConfigContainer);
				var commandContainer = await app.EnsureContainerAsync(App.CommandContainer);

				var infoSubscription = (await app.ObserveAsync(App.InfoContainer))
					.Where(evt => evt.NotificationEventType.Contains(NotificationEventType.CreateChild))
					.Select(evt => evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<Info>())
					.Where(info => info != null);

				var stateSubscription = (await app.ObserveAsync(App.StateContainer))
					.Where(evt => evt.NotificationEventType.Contains(NotificationEventType.CreateChild))
					.Select(evt => evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<State>())
					.Where(state => state != null);

				var commandSubscription = (await app.ObserveAsync(App.CommandContainer))
					.Where(evt => evt.NotificationEventType.Contains(NotificationEventType.CreateChild))
					.Select(evt => evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<Command>())
					.Where(command => command != null);

				var configSubscription = (await app.ObserveAsync(App.ConfigContainer))
					.Where(evt => evt.NotificationEventType.Contains(NotificationEventType.CreateChild))
					.Select(evt => evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<Types.Config.MeterReadPolicy>())
					.Where(config => config != null);

				var meterId = deviceAE.AE_ID;

				Meters[meterId] = new Meter
				{
					MeterId = meterId,
					MeterReadPolicy = configSubscription,
					Command = commandSubscription,

					Info = infoSubscription.Where(i => i.MeterId == meterId),
					State = stateSubscription.Select(s => s.Valve.Description()),
					Summations = dataSubscription.Where(d => d.MeterId == meterId),
					Events = eventSubscription.Where(e => e.MeterId == meterId),
				};
			}
		}

		public async Task<IEnumerable<Data.Summation>> GetOldSummations(string meterId, TimeSpan summationWindow)
		{
			var utcNow = DateTimeOffset.UtcNow;
			var cutoffTime = utcNow - summationWindow;

			//get content instances created in the specified summation window + 7 days
			var dataRefs = (await App.Application.GetPrimitiveAsync(App.DataContainer, new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.ContentInstance },
				CreatedAfter = cutoffTime.AddDays(-7)
			})).URIList;

			var oldSummations = dataRefs == null ? new List<Data>() :
				await dataRefs
				.Reverse()
				.ToAsyncEnumerable()
				.SelectAsync(async url => await App.Application.GetPrimitiveAsync(url))
				.Select(rc => rc.ContentInstance?.GetContent<Data>())
				.Where(d => d.Summations.Any())
				.Where(d => d.Summations.First().ReadTime > cutoffTime)
				.Reverse()
				.ToListAsync();

			return oldSummations
				.Where(d => d.MeterId == meterId)
				.SelectMany(d => d.Summations)
				.OrderBy(s => s.ReadTime).ToArray();
		}

		public async Task<IEnumerable<Events.MeterEvent>> GetOldEvents(string meterId)
		{
			//get content instances created in the past 30 days
			var eventRefs = (await App.Application.GetPrimitiveAsync(App.EventsContainer, new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.ContentInstance },
				CreatedAfter = DateTimeOffset.UtcNow.AddDays(-30)
			})).URIList;

			var oldEvents = eventRefs == null ? new List<Events>() :
				await eventRefs
				.Reverse()
				.ToAsyncEnumerable()
				.SelectAsync(async url => await App.Application.GetPrimitiveAsync(url))
				.Select(rc => rc.ContentInstance?.GetContent<Events>())
				.Where(d => d.MeterEvents.Any())
				.Reverse()
				.ToListAsync();

			return oldEvents
				.Where(d => d.MeterId == meterId)
				.SelectMany(d => d.MeterEvents)
				.OrderByDescending(s => s.EventTime)
				.ToArray();
		}
	}

	public class Meter
	{
		public string MeterId { get; set; }
		public IObservable<Info> Info { get; set; }
		public IObservable<string> State { get; set; }
		public IObservable<Command> Command { get; set; }
		public IObservable<Types.Config.MeterReadPolicy> MeterReadPolicy { get; set; }
		public IObservable<Events> Events { get; set; }
		public IObservable<Data> Summations { get; set; }

		public Meter() { }

		public Meter(Meter meter, IObservable<string> newState)
		{
			this.MeterId = meter.MeterId;
			this.Info = meter.Info;
			this.State = newState;
			this.Command = meter.Command;
			this.MeterReadPolicy = meter.MeterReadPolicy;
			this.Events = meter.Events;
			this.Summations = meter.Summations;
		}
	}
}
