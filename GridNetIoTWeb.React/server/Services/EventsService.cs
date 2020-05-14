using GridNet.IoT.Types;

using System.Threading.Tasks;

namespace GridNet.IoT.Web.React.server.Services
{
	public class EventsService
	{
		readonly ModelContext _modelContext;

		public EventsService(ModelContext modelContext)
		{
			_modelContext = modelContext;
		}

		public async Task Add(Events record) => await _modelContext.App.Application.AddContentInstance(_modelContext.App.EventsContainer, record);
	}
}
