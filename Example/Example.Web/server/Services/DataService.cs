using Example.Types;

using System.Threading.Tasks;

namespace Example.Web.Server.Services
{
	public class DataService
	{
		readonly ModelContext _modelContext;

		public async Task<Meter> GetMeter(string meterId) => await _modelContext.GetMeterAsync(meterId);

		public DataService(ModelContext modelContext)
		{
			_modelContext = modelContext;
		}

		public async Task Add(Data record) => await _modelContext.App.Application.AddContentInstanceAsync(_modelContext.App.DataContainer, record);
	}
}
