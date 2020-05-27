using DotNetify.Routing;

namespace Example.Web.Server.ViewModels
{
	public class MeterListRow : IRoutable
	{
		public string MeterId { get; }
		public string MeterState { get; }
		public Route Route { get; }

		public RoutingState RoutingState { get; set; }

		public MeterListRow(string id, string state, Route route)
		{
			MeterId = id;
			MeterState = state;
			Route = route;
		}
	}
}
