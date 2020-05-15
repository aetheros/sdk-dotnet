using DotNetify.Routing;

namespace Example.Web.Server.ViewModels
{
	public class MeterListRow : IRoutable
	{
		public string MeterId { get; }
		public string MeterState { get; }
		public RoutingState RoutingState { get; set; }

		public MeterListRow(string id, string state)
		{
			MeterId = id;
			MeterState = state;
		}
	}
}
