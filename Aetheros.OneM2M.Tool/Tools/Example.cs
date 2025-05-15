//#define USE_COAP

using Aetheros.OneM2M.Api;
using Aetheros.Schema.AOS;
using Aetheros.Schema.OneM2M;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mono.Options;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GridNet.IoT.Client.Tools
{
	[System.ComponentModel.Description("oneM2M demo")]
	public class Example : UtilityBase
	{
#if USE_COAP
		Uri _m2mUrl = new Uri("coap://127.0.0.1:8110");
		Uri _poaUrl = new Uri("coap://127.0.0.1:15683/notify");
		readonly Uri _listenUrl = null;
#else
		Uri _m2mUrl = new Uri("http://policynet-fw:21300/");
		Uri _poaUrl = new Uri("http://10.0.3.3:44346/notify");
		Uri _listenUrl = new Uri($"http://0.0.0.0:44346");
#endif

		class ConnectionConfiguration : Connection.IConnectionConfiguration
		{
			public Uri M2MUrl { get; init; }
		  public string CertificateFilename { get; set; }
		}

		ConnectionConfiguration _connectionConfiguration;

		string _AeId = "";
		string _AeAppId = "Rdemo.com.aos.iot";
		string _AeAppName = "metersvc-smpl";
		readonly string _RegPath = "/PN_CSE";
		readonly string _MsReadsPath = $"reads";
		string _AeCredential;

		public Example()
		{
		}

		const string MeterReadSubscriptionName = "sdk-sampl-sub-01";

		const string meterReadAclName = "meter-read-acl";


		public override OptionSet Options => new OptionSet
		{
			{ "c|cse=", "The URL to the CSE", v => _m2mUrl = new Uri(v, UriKind.Absolute) },
			{ "p|poa=", "The remote POA (point of access) url", v => _poaUrl = new Uri(v, UriKind.Absolute) },
#if !USE_COAP
			{ "l|listen=", "The local POA callback url", v => _listenUrl = new Uri(v, UriKind.Absolute) },
#endif
			{ "a|app=", "The App Id", v => _AeAppId = v },
			{ "n|name=", "The App Name", v => _AeAppName = v },
			{ "i|id=", "The existing AE Id", v => _AeId = v },
			{ "credential=", "The AE registration Credential", v => _AeCredential = v },
		};

		protected override string Usage { get; } = "[<options>]";

		Application _application;


		(Connection<PrimitiveContent>, Task) startCoapConnection()
		{
			CoAP.Log.LogManager.Level = CoAP.Log.LogLevel.Warning;
			// configure a oneM2M CoAP connection
			var connection = new CoapConnection(_connectionConfiguration);

			// start the POA CoAP server
			var coapServer = new CoAP.Server.CoapServer()	;
			coapServer.AddEndPoint(new IPEndPoint(IPAddress.Any, _poaUrl.Port));
			coapServer.Add(connection.CreateNotificationResource());
			coapServer.Start();

			var listenerTask = Task.Delay(Timeout.Infinite)
				.ContinueWith(task =>
				{
					// stop the server
					coapServer.Stop();
					coapServer.Dispose();
				});

			return (connection, listenerTask);
		}

		(Connection<PrimitiveContent>, Task) startHttpConnection()
		{
			// configure a oneM2M HTTP connection
			var connection = new HttpConnection(_connectionConfiguration);

			// start the POA web server
			var listenerTask = WebHost.CreateDefaultBuilder()
				.UseUrls((_listenUrl ?? _poaUrl).ToString())
				.Configure(app => app.MapWhen(
					ctx => ctx.Request.Method == "POST"	// listen for POST requests
						&& ctx.Request.Path == "/notify"	// on the /notify path
						&& ctx.Request.ContentType == "application/vnd.onem2m-ntfy+json",	// with JSON content
					builder => builder.Run(connection.HandleNotificationAsync)
				))
				.Build()
				.RunAsync();

			return (connection, listenerTask);
		}


		async Task<AE> EnsureRegistered(Connection<PrimitiveContent> connection)
		{
			if (!string.IsNullOrEmpty(_AeId))
			{
				try
				{
					Trace.WriteLine($"Looking for existing AE '{_AeId}'");
					var ae = await connection.FindApplicationAsync(_AeId);
					Trace.WriteLine($"Using existing AE '{ae.AE_ID}'");
					return ae;
				}
				catch (Exception ex)
				{
					Trace.TraceError($"Failed to find AE '{_AeId}': {ex.Message}");
				}
			}

			Trace.WriteLine($"Registering new AE '{_AeAppId}/{_AeAppName}'");
			return await connection.RegisterApplicationAsync(
				new ApplicationConfiguration
				{
					AppId = _AeAppId,
					AppName = _AeAppName,
					CredentialId = _AeCredential,
					PoaUrl = _poaUrl,
				}
			);
		}

		private async Task<AccessControlPolicy> EnsureMeterReadAcl()
		{
			Trace.WriteLine($"Looking for existing ACL '{meterReadAclName}'");
			var acl = (await _application.TryGetPrimitiveAsync(meterReadAclName))?.AccessControlPolicy;
			if (acl == null)
			{
				Trace.WriteLine($"Creating ACL '{meterReadAclName}'");
				acl = (await _application.CreateResourceAsync(
					"",
					ResourceType.AccessControlPolicy,
					pc =>
					{
						pc.AccessControlPolicy = new AccessControlPolicy
						{
							ResourceName = meterReadAclName,
							Privileges =
							[
								new AccessControlRule
								{
									AccessControlOriginators = ["app-id@" + _AeAppId],
									AccessControlOperations = AccessControlOperations.Item1, // Create
								}
							],
							SelfPrivileges =
							[
								new AccessControlRule
								{
									AccessControlOriginators = ["app-id@" + _AeAppId],
									AccessControlOperations = AccessControlOperations.Item63, // All
								}
							]
						};
						return pc;
					}
				)).AccessControlPolicy;
			}
			return acl;
		}

		private async Task<Container> EnsureReadContainer(string aclUri)
		{
			// create the read container
			var container = await _application.EnsureContainerAsync(_MsReadsPath, aclUri);

			if (container.MaxInstanceAge == null || container.MaxInstanceAge != 0)
			{
				// set the max instance age to 0 to prevent the server from persisting the content instances
				await _application.UpdateResourceAsync(
					_MsReadsPath,
					pc =>
					{
						pc.Container = new Container
						{
							MaxInstanceAge = 0,	// 0 = do not persist content instances, only forward to the POA
						};
						return pc;
					}
				);
			}

			return container;
		}

		async Task CreateMeterRead()
		{
			Trace.WriteLine("Invoking Create Meter Read API");

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

		private async Task DeleteReadsSubscription()
		{
			await _application.DeleteAsync($"{_MsReadsPath}/{MeterReadSubscriptionName}");
		}

		async Task DeRegister()
		{
			await _application.DeleteAsync("");
		}

		public override async Task Run(IList<string> args)
		{
			_connectionConfiguration = new ConnectionConfiguration { M2MUrl = _m2mUrl };

			// start the onem2m connection and notification listener
			var (connection, listenerTask) = _m2mUrl.Scheme.StartsWith("coap")
				? startCoapConnection()
				: startHttpConnection();

			// register the AE
			var ae = await EnsureRegistered(connection);

			// configure the oneM2M applicaiton api
			_application = new Application(connection, ae, _RegPath);

			// create an access control policy to allow mn-AE's with the same appId to write to our container
			var acl = await EnsureMeterReadAcl();
			var aclUri = _application.ToAbsolute(acl.ResourceName);

			var container = await EnsureReadContainer(aclUri);

			// create the subscription
			var policyObservable = await _application.ObserveContentInstanceAsync<MeterRead>(
				_MsReadsPath,
				MeterReadSubscriptionName,
				batchSize: 100
			);

			// listen for notifications on the subscription
			using var eventSubscription = policyObservable.SubscribeAsync(async policy =>
			{
				var data = policy.MeterSvcData;
				Trace.WriteLine($"new meter read:");
				Trace.WriteLine($"\tpowerQuality: {data.PowerQuality.VoltageA}");
				Trace.WriteLine($"\treadTimeLocal: {data.ReadTimeLocal}");
			});

			try
			{
				using var meterReadTask = Task.Run(async () =>
				{
					// create some (fake) meter read content instance, triggering notifications
					while (true)
					{
						await Task.Delay(TimeSpan.FromSeconds(1));
						await CreateMeterRead();
					}
				});

				// continue running the POA server
				await Task.WhenAny(listenerTask, meterReadTask);
			}
			finally
			{
				await DeleteReadsSubscription();
				//await DeRegister();
			}
		}

	}
}
