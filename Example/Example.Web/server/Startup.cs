using DotNetify;
using DotNetify.Security;

using Example.Web.Server.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SpaServices.Webpack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

using System;
using System.IO;
using System.Text;

namespace Example.Web.Server
{
	public class Startup
	{
		readonly IConfiguration _config;

		public Startup(IConfiguration config)
		{
			_config = config;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddOptions();
			services.Configure<WebOptions>(_config);

			// Add OpenID Connect server to produce JWT access tokens.
			services.AddAuthenticationServer();

			services.AddMemoryCache();
			services.AddSignalR();
			services.AddDotNetify();

			services.AddTransient<ILiveDataService, MockLiveDataService>();
			services.AddSingleton<IEmployeeService, EmployeeService>();

			services.AddSingleton<MeterService>();
			services.AddSingleton<DataService>();
			services.AddSingleton<EventsService>();
			services.AddSingleton<ModelContext>();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.Map("/notify", builder =>
			{
				var modelContext = builder.ApplicationServices.GetService<ModelContext>();
				var api = ((Aetheros.OneM2M.Api.HttpConnection) modelContext.App.Api);
				builder.Run(api.HandleNotificationAsync);
			});

			app.UseAuthentication();
			app.UseWebSockets();
			app.UseDotNetify(config =>
			{
				// Middleware to do authenticate token in incoming request headers.
				config.UseJwtBearerAuthentication(new TokenValidationParameters
				{
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthServer.SecretKey)),
					ValidateIssuerSigningKey = true,
					ValidateAudience = false,
					ValidateIssuer = false,
					ValidateLifetime = true,
					ClockSkew = TimeSpan.FromSeconds(0)
				});

				// Filter to check whether user has permission to access view models with [Authorize] attribute.
				config.UseFilter<AuthorizeFilter>();
			});

			if (env.IsDevelopment())
			{
#pragma warning disable CS0618
				app.UseWebpackDevMiddleware(new WebpackDevMiddlewareOptions
				{
					HotModuleReplacement = true
				});
#pragma warning restore CS0618
			}

			app.UseFileServer();
			app.UseRouting();
			app.UseEndpoints(endpoints => endpoints.MapHub<DotNetifyHub>("/dotnetify"));

			app.Run(async (context) =>
			{
				var uri = context.Request.Path.ToUriComponent();
				if (uri.EndsWith(".map"))
					return;
				else if (uri.EndsWith("_hmr"))  // Fix HMR for deep links.
					context.Response.Redirect("/dist/__webpack_hmr");

				using var reader = new StreamReader(File.OpenRead("wwwroot/index.html"));
				await context.Response.WriteAsync(reader.ReadToEnd());
			});
		}
	}
}