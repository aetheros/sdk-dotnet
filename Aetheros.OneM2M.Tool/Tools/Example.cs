using Aetheros.OneM2M.Api;
using Aetheros.OneM2M.Binding;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using Mono.Options;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;

namespace GridNet.IoT.Client.Tools
{
	[Description("oneM2M demo")]
	public class Demo : UtilityBase
	{
		Uri _listenUrl;
		Uri _poaUrl = new Uri("http://192.168.225.34:5683");

		class ConnectionConfiguration : Connection.IConnectionConfiguration
		{
			public Uri M2MUrl { get; set; }
			public string CertificateFilename { get; set; }
		}

		readonly ConnectionConfiguration _connectionConfiguration = new ConnectionConfiguration
		{
			M2MUrl = new Uri("http://192.168.225.1:8100"),
		};

		string _AeAppId = "Nra1.com.aos.iot";
		string _AeAppName = "metersvc-smpl";
		string _AeCredential = "";
		string _RegPath = ".";
		string _MsPolicyPath = "./metersvc/policies";
		string _MsReadsPath = "./metersvc/reads";

		const string MeterReadSubscriptionName = "metersvc-sampl-sub-01";
		private const string MeterReadPolicyName = "metersvc-sampl-pol-01";

		public override OptionSet Options => new OptionSet
		{
			{ "c|cse=", "The URL to the CSE", v => _connectionConfiguration.M2MUrl = new Uri(v, UriKind.Absolute) },
			{ "p|poa=", "The remote POA (point of access) url", v => _poaUrl = new Uri(v, UriKind.Absolute) },
			{ "l|listen=", "The local POA callback url", v => _listenUrl = new Uri(v, UriKind.Absolute) },
			{ "i|id=", "The App Id", v => _AeAppId = v },
			{ "n|name=", "The App Name", v => _AeAppName = v },
			{ "credential=", "The AE registration Credential", v => _AeCredential = v },
		};

		protected override string Usage { get; } = "[<options>]";

		Connection _connection;
		Application _application;

		async Task<AE> Register()
		{
			Trace.TraceInformation("Invoking AE Registration API");

			var response = await _connection.GetResponseAsync(new RequestPrimitive
			{
				To = _RegPath,
				Operation = Operation.Create,
				ResourceType = ResourceType.AE,
				PrimitiveContent = new PrimitiveContent
				{
					AE = new AE
					{
						App_ID = _AeAppId,
						AppName = _AeAppName,
						PointOfAccess = new[] { _poaUrl.ToString() },
					}
				}
			});
			return response.AE;
		}

		async Task DeRegister(string aeId)
		{
			await _application.GetResponseAsync(new RequestPrimitive
			{
				To = $"{_RegPath}/{aeId}",
				Operation = Operation.Delete
			});
		}


		async Task<string> CreateSubscription()
		{
			Trace.TraceInformation("Invoking Create Subscription API");

			var subscriptionResponse = await _application.GetResponseAsync(new RequestPrimitive
			{
				To = _MsReadsPath,
				Operation = Operation.Create,
				ResourceType = ResourceType.Subscription,
				ResultContent = ResultContent.HierarchicalAddress,
				PrimitiveContent = new PrimitiveContent
				{
					Subscription = new Subscription
					{
						ResourceName = MeterReadSubscriptionName,
						EventNotificationCriteria = new EventNotificationCriteria
						{
							NotificationEventType = new[]
							{
								NotificationEventType.CreateChild,
								NotificationEventType.DeleteChild,
							},
						},
						NotificationContentType = NotificationContentType.AllAttributes,
						NotificationURI = new[] { _poaUrl.ToString() },
					}
				}
			});
			return subscriptionResponse?.URI;
		}

		private async Task DeleteSubscription()
		{
			await _application.GetResponseAsync(new RequestPrimitive
			{
				To = $"{MS_READS_PATH}/{MeterReadSubscriptionName}",
				Operation = Operation.Delete
			});
		}

		async Task CreateMeterReadPolicy()
		{
			Trace.TraceInformation("Invoking Create Meter Read Policy API");

			await _application.AddContentInstanceAsync(
				_MsPolicyPath,
				MeterReadPolicyName,
				new
				{
					read = new
					{
						rtype = "powerQuality",
						tsched = new
						{
							recper = 120,
							sched = new
							{
								end = default(DateTimeOffset?),
								start = new DateTimeOffset(2020, 1, 27, 0, 0, 0, TimeSpan.Zero)
							}
						}
					}
				}
			);
		}

		async Task DeleteMeterReadPolicy()
		{
			await _application.GetResponseAsync(new RequestPrimitive
			{
				To = $"{MS_READS_PATH}/{MeterReadPolicyName}",
				Operation = Operation.Delete
			});
		}


		public override async Task Run(IList<string> args)
		{
			// configure a oneM2M connection
			_connection = new HttpConnection(_connectionConfiguration);

				// start the POA web server
			var hostTask = WebHost.CreateDefaultBuilder()
				.UseUrls((_listenUrl ?? _poaUrl).ToString())
				.Configure(app => app.Map("/notify", builder => builder.Run(context => _connection.HandleNotificationAsync(context.Request))))
				.Build()
				.RunAsync();

			// register the AE
			var ae = await Register();

			// configure the oneM2M applicaiton api
			_application = new Application(_connection, ae.App_ID, ae.AE_ID, "./", _poaUrl);

			// create a subscription
			var subscriptionReference = await CreateSubscription();

			var subscriptionEventContent =
				from notification in _connection.Notifications
				where notification.SubscriptionReference == subscriptionReference
				let evt = notification.NotificationEvent
				where evt.NotificationEventType.Contains(NotificationEventType.CreateChild)
				select evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<dynamic>();

			using var eventSubscription = subscriptionEventContent.Subscribe(content => Trace.TraceInformation($"new meter read: {Convert.ToString(content)}"));

			// create a meter read policy content instance
			await CreateMeterReadPolicy();

			try
			{
				// continue running the POA server
				await hostTask;
			}
			finally
			{
				DeleteSubscription();
				DeleteMeterReadPolicy();
				DeRegister(ae.AE_ID);
			}
		}
	}
}