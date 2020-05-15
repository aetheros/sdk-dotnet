using Example.Types;

using System.Threading.Tasks;

namespace Example.Web.Server.Services
{
	public class EventsService
	{
		readonly ModelContext _modelContext;

		public EventsService(ModelContext modelContext)
		{
			_modelContext = modelContext;
		}

		public async Task Add(Events record) => await _modelContext.App.Application.AddContentInstanceAsync(_modelContext.App.EventsContainer, record);
	}
}
