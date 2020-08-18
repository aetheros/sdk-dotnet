//#define USE_COAP

using Aetheros.OneM2M.Api;
using Aetheros.Schema.AOS;
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
using System.Linq;
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
	public class Example : UtilityBase
	{
#if USE_COAP
		Uri _poaUrl = new Uri("coap://127.0.0.1:15683/notify");
#else
		Uri _poaUrl = new Uri("http://10.0.2.15:5683");
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
			M2MUrl = new Uri("coap://127.0.0.1:8110"),
#else
			M2MUrl = new Uri("http://192.168.56.1:21300"),
#endif
		};

		string _AeAppId = "Nsdk-devAe-0.com.policynetiot.sdk";
		string _AeAppName = "sdk-devAe-0";
		string _AeCredential = "8992O4AAEXYWY95O";
		string _RegPath;
		string _MsPolicyPath;
		string _MsReadsPath;

		public Example()
		{
			_RegPath = "/PN_CSE";//"."
			_MsPolicyPath = $"{_RegPath}/metersvc/policies";
			_MsReadsPath = $"{_RegPath}/metersvc/reads";
		}

		const string MeterReadSubscriptionName = "metersvc-sampl-sub-01";
		const string MeterReadPolicyName = "metersvc-sampl-pol-01";

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
#if false
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
#endif

			Trace.TraceInformation("Invoking AE Registration API");

			var response = await _connection.GetResponseAsync(new RequestPrimitive
			{
				From = _AeCredential,
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

		async Task DeRegister()
		{
			await _application.DeleteAsync($"{_RegPath}/{_application.AppId}");
		}

		async Task<IObservable<NotificationNotificationEvent>> CreateSubscription()
		{
			await _application.EnsureContainerAsync(_MsReadsPath);
			await DeleteSubscription();

			Trace.TraceInformation("Invoking Create Subscription API");

			return await _application.ObserveAsync(
				_MsReadsPath,
				MeterReadSubscriptionName,
				new EventNotificationCriteria
				{
					NotificationEventType = new[] { NotificationEventType.CreateChild, },
				} 
			);
		}

		private async Task DeleteSubscription()
		{
			await _application.DeleteAsync($"{_MsReadsPath}/{MeterReadSubscriptionName}");
		}

		async Task CreateMeterReadPolicy()
		{
			await _application.EnsureContainerAsync(_MsPolicyPath);

			Trace.TraceInformation("Invoking Create Meter Read Policy API");

			await _application.AddContentInstanceAsync(
				_MsPolicyPath,
				MeterReadPolicyName,
				new MeterServicePolicy
				{
					MeterReadSchedule = new MeterReadSchedule
					{
						ReadingType = ReadingType.PowerQuality,
						TimeSchedule = new TimeSchedule
						{
							RecurrencePeriod = 120,
							ScheduleInterval = new ScheduleInterval
							{
								Start = DateTimeOffset.UtcNow,
								End = DateTimeOffset.UtcNow.AddDays(1),
							}
						}
					}
				}
			);
		}

		async Task CreateMeterRead()
		{
			await _application.EnsureContainerAsync(_MsReadsPath);

			Trace.TraceInformation("Invoking Create Meter Read Policy API");

			await _application.AddContentInstanceAsync(
				_MsReadsPath,
				new MeterRead
				{
					MeterSvcData = new MeterSvcData
					{
						PowerQuality = new PowerQualityData
						{
							VoltageA = 110.0f + ((float) new Random().NextDouble() - 0.5f) * 15.0f,
						},
						ReadTimeLocal = DateTimeOffset.UtcNow,
						Summations = new SummationData
						{
							ReactiveEnergyExportedA = 10,
						}
					}
				}
			);
		}

		async Task DeleteMeterReadPolicy()
		{
			await _application.DeleteAsync($"{_MsPolicyPath}/{MeterReadPolicyName}");
		}


		public override async Task Run(IList<string> args)
		{
#if USE_COAP
			// configure a oneM2M CoAP connection
			var connection = new CoapConnection(_connectionConfiguration);

			// start the POA CoAP server
			using var server = new CoAP.Server.CoapServer();
			server.AddEndPoint(new IPEndPoint(IPAddress.Any, _poaUrl.Port));
			server.Add(connection.CreateNotificationResource());
			server.Start();

			var hostTask = Task.Delay(Timeout.Infinite);  // TODO: terminate
#else
			// configure a oneM2M HTTP connection
			var connection = new HttpConnection(_connectionConfiguration);

			// start the POA web server
			var hostTask = WebHost.CreateDefaultBuilder()
				.UseUrls((_listenUrl ?? _poaUrl).ToString())
				.Configure(app => app.Map("/notify", builder => builder.Run(connection.HandleNotificationAsync)))
				.Build()
				.RunAsync();
#endif
			_connection = connection;

			// register the AE
			var ae = await Register();

			// configure the oneM2M applicaiton api
			_application = new Application(_connection, ae.App_ID, ae.AE_ID, "./", _poaUrl);

			// create a subscription
			var policyObservable =
				(await CreateSubscription())
				.Select(evt => evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<MeterRead>())
				.Where(meterRead => meterRead != null);

			using var eventSubscription = policyObservable.Subscribe(policy =>
			{
				var data = policy.MeterSvcData;
				Trace.TraceInformation($"new meter read: powerQuality: {data.PowerQuality.VoltageA}, readTimeLocal: {data.ReadTimeLocal}");
			});

			// create a meter read policy content instance
			await CreateMeterReadPolicy();

			// create a (fake) meter read content instance, triggering a notification
			await CreateMeterRead();

			try
			{
				// continue running the POA server
				await hostTask;
			}
			finally
			{
				await DeleteSubscription();
				await DeleteMeterReadPolicy();
				await DeRegister();
			}
		}
	}
}