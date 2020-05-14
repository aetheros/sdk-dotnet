using System.Threading.Tasks;
using GridNet.OneM2M.Types;
using GridNet.IoT.Types;
using Microsoft.Extensions.Options;

namespace GridNet.IoT.Web.React.server.Services
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
