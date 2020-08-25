using Aetheros.OneM2M.Api;
using Aetheros.Schema.OneM2M;

using Example.Types;
using Example.Web.Server.Utils;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Example.Web.Server.Services
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

	public partial class ModelContext : IDisposable
	{
		public MyApplication App { get; }

		Dictionary<string, Meter> _meters = new Dictionary<string, Meter>();

		Task _startupTask;

		public async Task<IReadOnlyDictionary<string, Meter>> GetMeters()
		{
			await _startupTask;
			return _meters;
		}

		public async Task<Meter> GetMeterAsync(string id)
		{
			await _startupTask;
			return _meters[id];
		}

		public ModelContext(IOptions<WebOptions> opts)
		{
			var options = opts.Value;
			 var app = Application.RegisterAsync(options.M2M, options.AE, options.AE.UrlPrefix, options.CAUrl).Result;

			this.App = new MyApplication
			{
				Application = app,
				DataContainer = options.DataContainer,
				EventsContainer = options.EventsContainer,
				StateContainer = options.StateContainer,
				InfoContainer = options.InfoContainer,
				ConfigContainer = options.ConfigContainer,
				CommandContainer = options.CommandContainer,
			};

			_startupTask = Task.Run(async () => await LoadObservableDataAsync());

			_cts = new System.Threading.CancellationTokenSource();
#if false
			Task.Run(async () =>
			{
				var random = new Random();
				while (!_cts.IsCancellationRequested)
				{
					await Task.Delay(1000 + random.Next(5000));
					var rg = (await GetMeters()).Values.ToList();
					var meter = rg[random.Next(rg.Count)];

					await this.App.Application.AddContentInstanceAsync(this.App.DataContainer, new Data
					{
						MeterId = meter.MeterId,
						UOM = Data.Units.USGal,
						Summations = new[] {
							new Data.Summation {
								ReadTime = DateTimeOffset.UtcNow,
								Value = random.NextDouble(),
							}
						},
					});
				}
			}, _cts.Token);
#endif
		}

		CancellationTokenSource _cts;

		public void Dispose()
		{
			_cts.Cancel();
		}

		async Task<IEnumerable<AEAnnc>> DiscoverAEsAsync()
		{
			Debug.WriteLine("===========================ModelContext.DiscoverAEs()");

			//**note**
			//cannot find announced AE in dev8
			var responseFilterContainers = await App.Application.GetPrimitiveAsync("/PN_CSE", new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.AEAnnc },
				Attribute = Connection.GetAttributes<AE>(_ => _.App_ID == App.Application.AppId),
			});

			return await responseFilterContainers.URIList
				.ToAsyncEnumerable()
				.SelectAwait(async url => await App.Application.GetPrimitiveAsync(url))
				.Select(rc => rc.AEAnnc)
				.ToListAsync();
		}

		public string DataContainer => $"~/{App.DataContainer}";
		public string EventsContainer => $"~/{App.EventsContainer}";

		async Task LoadObservableDataAsync()
		{
			Debug.WriteLine("===========================ModelContext.LoadData()");

			var app = App.Application;

			//server app container subscriptions

			// device -> app
			var dataSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Data>(this.DataContainer));
			var eventSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Events>(this.EventsContainer));

			var meters = (await DiscoverAEsAsync()).Select(deviceAE =>
			{
				var meterId = deviceAE.AE_ID;

				//**note**
				//no announced AEcan be found in dev8 and the below containers cannot be created as client app containers
				//workaround: created as server app containers
				//var meterUrl = $"{deviceAE.ResourceID}/";
				var meterUrl = $"{deviceAE.Link}/";

				var poaUrl = $"{app.UrlPrefix}/{app.AeId}";

				// device
				var infoSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Info>(meterUrl + App.InfoContainer, poaUrl));
				var stateSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<State>(meterUrl + App.StateContainer, poaUrl));

				// app -> device
				var configSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Config.MeterReadPolicy>(meterUrl + App.ConfigContainer, poaUrl));
				var commandSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Command>(meterUrl + App.CommandContainer, poaUrl));

				return new Meter
				{
					MeterId = meterId,
					MeterUrl = meterUrl,

					// device -> app
					Summations = dataSubscription.Where(d => d.MeterId == meterId),
					Events = eventSubscription.Where(e => e.MeterId == meterId),

					// device
					Info = infoSubscription.Where(i => i.MeterId == meterId),
					State = stateSubscription.Select(s => s.Valve.Description()),

					// app -> device
					MeterReadPolicy = configSubscription,
					Command = commandSubscription,
				};
			});

			foreach (var meter in meters)
				_meters[meter.MeterId] = meter;
		}

		public async Task<IEnumerable<Data.Summation>> GetOldSummationsAsync(string meterId, TimeSpan summationWindow)
		{
			var utcNow = DateTimeOffset.UtcNow;
			var cutoffTime = utcNow - summationWindow;

			// TODO: filter by creator

			//get content instances created in the specified summation window + 7 days
			var childResources = (await App.Application.GetChildResourcesAsync<PrimitiveContent>(
				this.DataContainer,
				new FilterCriteria
				{
					ResourceType = new[] { ResourceType.ContentInstance },
					//CreatedAfter = cutoffTime,
				}
			)).Container.ContentInstance;

			return childResources
				.Select(ci => ci.GetContent<Data>())
				.Where(d => d.MeterId == meterId
					&& d.Summations.Count > 0
					&& d.Summations.First().ReadTime > cutoffTime)
				.SelectMany(d => d.Summations)
				.OrderBy(s => s.ReadTime)
				.ToList();
		}

		public async Task<IEnumerable<Events.MeterEvent>> GetOldEvents(string meterId)
		{
			//get content instances created in the specified summation window + 7 days
			var childResources = (await App.Application.GetChildResourcesAsync<PrimitiveContent>(
				this.EventsContainer,
				new FilterCriteria
				{
					//FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.ContentInstance },
					CreatedAfter = DateTimeOffset.UtcNow.AddDays(-30),
				}
			)).Container.ContentInstance;

			return
				childResources
				.Select(ci => ci.GetContent<Events>())
				.Where(d => d.MeterEvents.Count > 0 && d.MeterId == meterId)
				.SelectMany(d => d.MeterEvents)
				.OrderByDescending(s => s.EventTime)
				.ToList();
		}
	}

	public class Meter
	{
		public string MeterId { get; set; }
		public IObservable<Info> Info { get; set; }
		public IObservable<string> State { get; set; }
		public IObservable<Command> Command { get; set; }
		public IObservable<Config.MeterReadPolicy> MeterReadPolicy { get; set; }
		public IObservable<Events> Events { get; set; }
		public IObservable<Data> Summations { get; set; }
		public string MeterUrl { get; internal set; }

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
