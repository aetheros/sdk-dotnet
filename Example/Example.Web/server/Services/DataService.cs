using Example.Types;

using System.Threading.Tasks;

namespace Example.Web.Server.Services
{
	public class DataService
	{
		readonly ModelContext _modelContext;

		public Meter GetMeter(string meterId) => _modelContext.Meters[meterId];

		public DataService(ModelContext modelContext)
		{
			_modelContext = modelContext;
		}

		public async Task Add(Data record) => await _modelContext.App.Application.AddContentInstance(_modelContext.App.DataContainer, record);
	}
}
