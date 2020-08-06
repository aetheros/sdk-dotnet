#define USE_COAP

using Aetheros.OneM2M.Api;
using Aetheros.Schema.OneM2M;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using Mono.Options;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace GridNet.IoT.Client.Tools
{
	[Description("oneM2M demo")]
	public class Demo : UtilityBase
	{
#if USE_COAP
		Uri _poaUrl = new Uri("coap://10.0.2.2:5683");
#else
		Uri _poaUrl = new Uri("http://10.0.2.2:5683");
		Uri _listenUrl = new Uri($"http://0.0.0.0:5683");
#endif

		class ConnectionConfiguration : Connection.IConnectionConfiguration
		{
			public Uri M2MUrl { get; set; }
			public string CertificateFilename { get; set; }
		}

		readonly ConnectionConfiguration _connectionConfiguration = new ConnectionConfiguration
		{
#if USE_COAP
			//M2MUrl = new Uri("coap://192.168.56.1:8110/PN_CSE"),
			M2MUrl = new Uri("coap://192.168.56.1:8110"),
#else
			M2MUrl = new Uri("http://192.168.56.1:21300"),
#endif
		};

		string _AeAppId = "Nsdk-devAe-0.com.policynetiot.sdk";
		string _AeAppName = "sdk-devAe-0";
		string _AeCredential = "8992O4AAEXYWY95O";
		string _RegPath = ".";
		string _MsPolicyPath = "./metersvc/policies";
		string _MsReadsPath = "./metersvc/reads";

		const string MeterReadSubscriptionName = "metersvc-sampl-sub-01";
		private const string MeterReadPolicyName = "metersvc-sampl-pol-01";

		public override OptionSet Options => new OptionSet
		{
			{ "c|cse=", "The URL to the CSE", v => _connectionConfiguration.M2MUrl = new Uri(v, UriKind.Absolute) },
			{ "p|poa=", "The remote POA (point of access) url", v => _poaUrl = new Uri(v, UriKind.Absolute) },
#if !USE_COAP
			{ "l|listen=", "The local POA callback url", v => _listenUrl = new Uri(v, UriKind.Absolute) },
#endif
			{ "i|id=", "The App Id", v => _AeAppId = v },
			{ "n|name=", "The App Name", v => _AeAppName = v },
			{ "credential=", "The AE registration Credential", v => _AeCredential = v },
		};

		protected override string Usage { get; } = "[<options>]";

		Connection _connection;
		Application _application;

		async Task<AE> Register()
		{
			/*
			// find stale AEs
			var staleAEs = await _connection.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				From = "PN_CSE",
				To = "PN_CSE",
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.AE },
					Attribute = Connection.GetAttributes<AE>(_ => _.App_ID == _AeAppId),
				}
			});

			// delete stale AEs
			foreach (var url in staleAEs.URIList)
			{
				await _connection.GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Delete,
					From = "PN_CSE",
					To = url,
				});
			}
			*/

			Trace.TraceInformation("Invoking AE Registration API");

			var response = await _connection.GetResponseAsync(new RequestPrimitive
			{
				//From = _AeCredential,
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
				From = _AeCredential,
				To = $"{_RegPath}/{aeId}",
				Operation = Operation.Delete
			});
		}


		async Task<string> CreateSubscription()
		{
			await _application.EnsureContainerAsync(_MsReadsPath);

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

		private async Task DeleteSubscription(string aeId)
		{
			await _application.GetResponseAsync(new RequestPrimitive
			{
				From = aeId,
				To = $"{_MsReadsPath}/{MeterReadSubscriptionName}",
				Operation = Operation.Delete
			});
		}

		async Task CreateMeterReadPolicy()
		{
			await _application.EnsureContainerAsync(_MsPolicyPath);

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

		async Task DeleteMeterReadPolicy(string aeId)
		{
			await _application.GetResponseAsync(new RequestPrimitive
			{
				From = aeId,
				To = $"{_MsPolicyPath}/{MeterReadPolicyName}",
				Operation = Operation.Delete
			});
		}


		public override async Task Run(IList<string> args)
		{
#if USE_COAP
			_connection = new CoapConnection(_connectionConfiguration);

			using var server = new CoAP.Server.CoapServer(_poaUrl.Port);
			var notifyResource = new CoAP.Server.Resources.Resource("notify");
			server.Add(notifyResource);
			server.Start();
			var hostTask = Task.Delay(Timeout.Infinite);  // TODO: terminate
#else
			// configure a oneM2M connection
			_connection = new HttpConnection(_connectionConfiguration);

				// start the POA web server
			var hostTask = WebHost.CreateDefaultBuilder()
				.UseUrls((_listenUrl ?? _poaUrl).ToString())
				.Configure(app => app.Map("/notify", builder => builder.Run(context => _connection.HandleNotificationAsync(context.Request))))
				.Build()
				.RunAsync();
#endif

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
				DeleteSubscription(ae.AE_ID);
				DeleteMeterReadPolicy(ae.AE_ID);
				DeRegister(ae.AE_ID);
			}
		}
	}
}