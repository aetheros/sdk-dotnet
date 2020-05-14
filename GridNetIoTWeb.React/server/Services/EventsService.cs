using GridNet.IoT.Types;
using GridNet.OneM2M.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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
