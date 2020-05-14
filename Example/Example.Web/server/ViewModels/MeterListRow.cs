using DotNetify;
using DotNetify.Routing;
using Example.Types;
using Example.Web.Server.Services;
using Example.Web.Server.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

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
