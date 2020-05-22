using Aetheros.OneM2M.Api;
using Aetheros.OneM2M.Binding;

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
			var con = new HttpConnection(options.M2M);
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

			_startupTask = Task.Run(async () =>
			{
				await LoadObservableDataAsync();
			});

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

		async Task<IEnumerable<AE>> DiscoverAEsAsync()
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

			return await responseFilterContainers.URIList
				.ToAsyncEnumerable()
				.SelectAwait(async url => await App.Application.GetPrimitiveAsync(url))
				.Select(rc => rc.AE)
				.ToListAsync();
		}

		async Task LoadObservableDataAsync()
		{
			Debug.WriteLine("===========================ModelContext.LoadData()");

			var app = App.Application;

			//server app container subscriptions

			// device -> app
			var dataSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Data>(App.DataContainer));
			var eventSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Events>(App.EventsContainer));

			foreach (var deviceAE in await DiscoverAEsAsync())
			{
				var meterId = deviceAE.AE_ID;

				//**note**
				//no announced AEcan be found in dev8 and the below containers cannot be created as client app containers
				//workaround: created as server app containers
				//var meterUrl = $"{deviceAE.ResourceID}/";
				var meterUrl = "";

				// device
				var infoSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Info>(meterUrl + App.InfoContainer));
				var stateSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<State>(meterUrl + App.StateContainer));

				// app -> device
				var configSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Config.MeterReadPolicy>(meterUrl + App.ConfigContainer));
				var commandSubscription = Observable.Defer(async () => await app.ObserveContentInstanceCreationAsync<Command>(meterUrl + App.CommandContainer));

				_meters[meterId] = new Meter
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
			}
		}

		public async Task<IEnumerable<Data.Summation>> GetOldSummationsAsync(string meterId, TimeSpan summationWindow)
		{
			var utcNow = DateTimeOffset.UtcNow;
			var cutoffTime = utcNow - summationWindow;

			//get content instances created in the specified summation window + 7 days
			var dataRefs = (await App.Application.GetPrimitiveAsync(App.DataContainer, new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.ContentInstance },
				CreatedAfter = cutoffTime.AddDays(-1)
			})).URIList;

			if (dataRefs == null)
				return Array.Empty<Data.Summation>();

			var oldEvents =
				await dataRefs
				.Reverse()
				.ToAsyncEnumerable()
				.SelectAwait(async url => await App.Application.GetPrimitiveAsync(url))
				.Select(rc => rc.ContentInstance?.GetContent<Data>())
				.Where(d => d.MeterId == meterId
					&& d.Summations.Count > 0
					&& d.Summations.First().ReadTime > cutoffTime)
				.Reverse()
				.ToListAsync();

			return oldEvents
				.SelectMany(d => d.Summations)
				.OrderBy(s => s.ReadTime)
				.ToList();
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

			if (eventRefs == null)
				return Array.Empty<Events.MeterEvent>();

			var oldEvents =
				await eventRefs
				.Reverse()
				.ToAsyncEnumerable()
				.SelectAwait(async url => await App.Application.GetPrimitiveAsync(url))
				.Select(rc => rc.ContentInstance?.GetContent<Events>())
				.Where(d => d.MeterEvents.Count > 0
					&& d.MeterId == meterId)
				.Reverse()
				.ToListAsync();

			return oldEvents
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
