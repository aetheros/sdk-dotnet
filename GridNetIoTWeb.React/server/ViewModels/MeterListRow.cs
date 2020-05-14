using DotNetify;
using DotNetify.Routing;
using GridNet.IoT.Types;
using GridNet.IoT.Web.React.server.Services;
using GridNet.IoT.Web.React.server.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace GridNet.IoT.Web.React.server.ViewModels
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
