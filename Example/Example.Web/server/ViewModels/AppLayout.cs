using DotNetify;
using DotNetify.Routing;
using DotNetify.Security;

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Example.Web.Server
{
	[Authorize]
	public class AppLayout : BaseVM, IRoutable
	{
		private enum Route
		{
			Home,
			Dashboard,
			FormPage,
			TablePage,
			MeterList,
			//MeterDashboard,
		};

		public RoutingState RoutingState { get; set; }

		public object Menus => new List<object>()
		{
			new { Title = "Dashboard",    Icon = "assessment", Route = this.GetRoute(nameof(Route.Dashboard)) },
			new { Title = "Form Page",    Icon = "web",        Route = this.GetRoute(nameof(Route.FormPage), $"{nameof(Form)}/1") },
			new { Title = "Table Page",   Icon = "grid_on",    Route = this.GetRoute(nameof(Route.TablePage)) },
			new { Title = "Meters",       Icon = "grid_on",    Route = this.GetRoute(nameof(Route.MeterList)) },
		};

		public string UserName { get; set; }
		public string UserAvatar { get; set; }

		public AppLayout(IPrincipalAccessor principalAccessor)
		{
			var userIdentity = principalAccessor.Principal.Identity as ClaimsIdentity;

			UserName = userIdentity.Name;
			UserAvatar = userIdentity.Claims.FirstOrDefault(i => i.Type == ClaimTypes.Uri)?.Value;

			this.RegisterRoutes("/", new List<RouteTemplate>
			{
				new RouteTemplate(nameof(Route.Home)) { UrlPattern = "", ViewUrl = nameof(Route.Dashboard) },
				new RouteTemplate(nameof(Route.Dashboard)),
				new RouteTemplate(nameof(Route.FormPage)) { UrlPattern = $"{nameof(Form)}(/:id)" },
				new RouteTemplate(nameof(Route.TablePage)),
				new RouteTemplate(nameof(Route.MeterList)),
				//new RouteTemplate(nameof(Route.MeterDashboard)) { UrlPattern = $"{MeterDashboardPath}(/:id)" },
			});
		}
	}
}
