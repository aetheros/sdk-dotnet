using Example.Types;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Example.Web.Server.Services
{
	public class MeterService
	{
		readonly ModelContext _modelContext;

		public async Task<Meter> GetMeterAsync(string meterId) => await _modelContext.GetMeterAsync(meterId);
		public IObservable<Meter> Meters { get; }
		public MyApplication App => _modelContext.App;

		public MeterService(ModelContext modelContext)
		{
			_modelContext = modelContext;

			Meters = Observable.Defer(async () => (await _modelContext.GetMeters()).Select(m => m.Value).ToObservable());
		}

		public async Task<IEnumerable<Data.Summation>> GetOldSummationsAsync(string meterId, int dataSummationWindow)
		{
			Debug.WriteLine($"GetOldSummationsAsync({dataSummationWindow} minutes)...");
			var windowTimeSpan = TimeSpan.FromMinutes(dataSummationWindow);
			try
			{
				var summations = await _modelContext.GetOldSummationsAsync(meterId, windowTimeSpan);
				Debug.WriteLine($"GetOldSummationsAsync({dataSummationWindow} minutes)... returned {summations.Count()} summations");
				return summations;
			}
			catch
			{
				return Array.Empty<Data.Summation>();
			}
		}

		public async Task<IEnumerable<Events.MeterEvent>> GetOldEventsAsync(string meterId)
		{
			try
			{
				return await _modelContext.GetOldEvents(meterId);
			}
			catch
			{
				return Array.Empty<Events.MeterEvent>();
			}
		}

		public async Task<T> GetLatestContentInstanceAsync<T>(string containerKey)
			where T : class => await _modelContext.App.Application.GetLatestContentInstanceAsync<T>(containerKey);

		public async Task AddInfoAsync(Info record) =>
			await _modelContext.App.Application.AddContentInstanceAsync(_modelContext.App.InfoContainer, record);

		public async Task AddStateAsync(State record) =>
			await _modelContext.App.Application.AddContentInstanceAsync(_modelContext.App.StateContainer, record);

		public async Task AddCommandAsync(Meter meter, Command record) =>
			await _modelContext.App.Application.AddContentInstanceAsync(meter.MeterUrl + _modelContext.App.CommandContainer, record);

		public async Task AddMeterReadPolicyAsync(Meter meter, Config.MeterReadPolicy record) =>
			await _modelContext.App.Application.AddContentInstanceAsync(meter.MeterUrl + _modelContext.App.ConfigContainer, record);
	}
}
