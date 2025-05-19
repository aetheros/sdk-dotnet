//#define USE_COAP
#define USE_SECURE_CONNECTION

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
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace GridNet.IoT.Client.Tools
{
	[System.ComponentModel.Description("oneM2M demo")]
	public class Example : UtilityBase
	{
#if USE_COAP
#if USE_SECURE_CONNECTION
		Uri _m2mUrl = new Uri("coaps://127.0.0.1:8111");
#else
		Uri _m2mUrl = new Uri("coap://127.0.0.1:8110");
#endif
		Uri _raUrl = new Uri("coap://127.0.0.1:18090/");
		Uri _poaUrl = new Uri("coap://127.0.0.1:15683/notify");
		readonly Uri _listenUrl = null;
#else
#if USE_SECURE_CONNECTION
		Uri _m2mUrl = new Uri("https://policynet-cse:21301/");
#else
		Uri _m2mUrl = new Uri("http://policynet-cse:21300/");
#endif
		Uri _raUrl = new Uri("https://policynet-fw:18090/");
		
		Uri _listenUrl = new Uri($"http://0.0.0.0:44346");
		Uri _poaUrl = new Uri("http://10.0.3.3:44346/notify");
#endif

		class ConnectionConfiguration : Connection.IConnectionConfiguration
		{
			public Uri M2MUrl { get; set; }
		  public string CertificateFilename { get; set; }
		}

		string _certFilename;
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
			{ "c|cse=", "The CSE URL", v => _m2mUrl = new Uri(v, UriKind.Absolute) },
			{ "p|poa=", "The remote POA (point of access) URL", v => _poaUrl = new Uri(v, UriKind.Absolute) },
#if !USE_COAP
			{ "l|listen=", "The local POA callback URL", v => _listenUrl = new Uri(v, UriKind.Absolute) },
#endif
			{ "a|app=", "The App Id", v => _AeAppId = v },
			{ "n|name=", "The App Name", v => _AeAppName = v },
			{ "i|id=", "The existing AE Id", v => _AeId = v },
			{ "credential=", "The AE registration Credential", v => _AeCredential = v },
			{ "cert=", "The filename of the client certificate to use", v => _certFilename = v },
			{ "ra=", "The RA URL", v => _raUrl = new Uri(v, UriKind.Absolute) },
		};

		protected override string Usage { get; } = "[<options>]";

		Application _application;


		CoapConnection createCoapConnection()
		{
			CoAP.Log.LogManager.Level = CoAP.Log.LogLevel.Warning;
			return new CoapConnection(_connectionConfiguration);
		}

		Task startCoapListener(CoapConnection connection)
		{
			var coapServer = new CoAP.Server.CoapServer();
			coapServer.AddEndPoint(new IPEndPoint(IPAddress.Any, _poaUrl.Port));
			coapServer.Add(connection.CreateNotificationResource());
			coapServer.Start();

			return Task.Delay(Timeout.Infinite)
				.ContinueWith(task =>
				{
					coapServer.Stop();
					coapServer.Dispose();
				});
		}

		HttpConnection createHttpConnection()
		{
			return new HttpConnection(_connectionConfiguration);
		}

		Task startHttpListener(HttpConnection connection)
		{
			return WebHost.CreateDefaultBuilder()
				.UseUrls((_listenUrl ?? _poaUrl).ToString())
				.Configure(app => app.MapWhen(
					ctx => ctx.Request.Method == "POST"
						&& ctx.Request.Path == "/notify"
						&& ctx.Request.ContentType == "application/vnd.onem2m-ntfy+json",
					builder => builder.Run(connection.HandleNotificationAsync)
				))
				.Build()
				.RunAsync();
		}

		async Task<AE> EnsureRegistered()
		{
			X509Certificate2 cert = AosUtils.LoadCertificateWithKey(_connectionConfiguration.CertificateFilename);
			if (cert != null)
			{
				if (!cert.HasPrivateKey)
					ShowError($"Certificate '{_connectionConfiguration.CertificateFilename}' does not have a private key");

				var certAeId = cert.ExtractedAeId();
				if (certAeId == null)
					ShowError($"Failed to extract AE Id from certificate '{_connectionConfiguration.CertificateFilename}'");

				if (!string.IsNullOrEmpty(_AeId))
					ShowError($"Cannot specify both AE Id '{_AeId}' and certificate '{_connectionConfiguration.CertificateFilename}'");

				_AeId = certAeId;
			}

			// Always use a separate connection for registration
			Connection<PrimitiveContent> registrationConnection =
				_m2mUrl.Scheme.StartsWith("coap")
				? new CoapConnection(_connectionConfiguration)
				: new HttpConnection(_connectionConfiguration);

			AE? ae = null;
			if (!string.IsNullOrEmpty(_AeId) && (!registrationConnection.IsSecure || cert != null))
			{
				try
				{
					Trace.WriteLine($"Looking for existing AE '{_AeId}'");
					ae = await registrationConnection.FindApplicationAsync(_AeId);
				}
				catch (Exception ex)
				{
					if (cert != null)
						ShowError($"Failed to find AE '{_AeId}' using certificate '{_connectionConfiguration.CertificateFilename}': {ex.Message}");
					Trace.TraceError($"Failed to find AE '{_AeId}': {ex.Message}");
				}
			}

			if (ae == null)
			{
				if (string.IsNullOrWhiteSpace(_AeAppName))
					ShowError($"Missing AE registration App Name");
				if (string.IsNullOrWhiteSpace(_AeAppId))
					ShowError($"Missing AE registration App Id");
				if (string.IsNullOrWhiteSpace(_AeCredential))
					ShowError($"Missing AE registration credential");

				Trace.WriteLine($"Registering new AE '{_AeAppId}/{_AeAppName}'");
				ae = await registrationConnection.RegisterApplicationAsync(
					new ApplicationConfiguration
					{
						AppId = _AeAppId,
						AppName = _AeAppName,
						CredentialId = _AeCredential,
						PoaUrl = _poaUrl,
					}
				);
			}

			if (cert == null && registrationConnection.IsSecure)
			{
				if (string.IsNullOrWhiteSpace(_connectionConfiguration.CertificateFilename))
					_connectionConfiguration.CertificateFilename = $"{ae.AE_ID}.pem";

				cert = await Application.GenerateSigningCertificateAsync(_raUrl, ae, _connectionConfiguration.CertificateFilename);
				if (cert == null)
					ShowError($"Failed to generate certificate for AE '{_AeId}'");
				else
					Trace.WriteLine($"Generated certificate for AE '{_AeId}' and saved to '{_connectionConfiguration.CertificateFilename}'");
			}

			return ae;
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
			_connectionConfiguration = new ConnectionConfiguration { M2MUrl = _m2mUrl, CertificateFilename = _certFilename };

			var ae = await EnsureRegistered();

			// Now create the main connection and listener for notifications
			Connection<PrimitiveContent> connection;
			Task listenerTask;
			if (_m2mUrl.Scheme.StartsWith("coap"))
			{
				var coapConnection = createCoapConnection();
				listenerTask = startCoapListener(coapConnection);
				connection = coapConnection;
			}
			else
			{
				var httpConnection = createHttpConnection();
				listenerTask = startHttpListener(httpConnection);
				connection = httpConnection;
			}

			// configure the oneM2M application api
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
				await DeRegister();
			}
		}

	}
}
