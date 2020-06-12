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
			var summations = await _modelContext.GetOldSummationsAsync(meterId, windowTimeSpan);
			Debug.WriteLine($"GetOldSummationsAsync({dataSummationWindow} minutes)... returned {summations.Count()} summations");
			return summations;
		}

		public async Task<IEnumerable<Events.MeterEvent>> GetOldEventsAsync(string meterId) => await _modelContext.GetOldEvents(meterId);

		public async Task<T> GetLatestContentInstanceAsync<T>(string containerKey)
			where T : class => await _modelContext.App.Application.GetLatestContentInstanceAsync<T>(containerKey);

		public async Task AddInfoAsync(Info record) => await _modelContext.App.Application.AddContentInstanceAsync(_modelContext.App.InfoContainer, record);

		public async Task AddStateAsync(State record) => await _modelContext.App.Application.AddContentInstanceAsync(_modelContext.App.StateContainer, record);

		public async Task AddCommandAsync(Command record) => await _modelContext.App.Application.AddContentInstanceAsync(_modelContext.App.CommandContainer, record);

		public async Task AddMeterReadPolicyAsync(Config.MeterReadPolicy record) => await _modelContext.App.Application.AddContentInstanceAsync(_modelContext.App.ConfigContainer, record);
	}
}
