#define USE_COAP

using Aetheros.OneM2M.Api;
using Aetheros.Schema.OneM2M;

using Mono.Options;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace GridNet.IoT.Client.Tools
{
	[Description("oneM2M demo")]
	public class TestDev : UtilityBase
	{
		Uri _poaUrl = new Uri("coap://127.0.0.1:15683/notify");

		Uri _m2mUrl;
		Connection.ConnectionConfiguration _connectionConfiguration;

		string _AeId = "";
		string _AeAppId = "Nra1.com.aos.iot";
		string _AeAppName = "metersvc-smpl";

		readonly string _RegPath = ".";
		readonly string _MsPolicyPath = $"config-cnt";
		readonly string _MsCommandsPath = $"command-cnt";
		readonly string _ReadsContainerName = "data-cnt";

		public TestDev()
		{
		}

		const string MeterReadSubscriptionName = "sdk-sampl-sub-01";
		const string MeterReadPolicyName = "sdk-sampl-pol-01";

		public override OptionSet Options => new OptionSet
		{
			{ "c|cse=", "The URL to the CSE", v => _m2mUrl = new Uri(v, UriKind.Absolute) },
			{ "p|poa=", "The remote POA (point of access) url", v => _poaUrl = new Uri(v, UriKind.Absolute) },
#if !USE_COAP
			{ "l|listen=", "The local POA callback url", v => _listenUrl = new Uri(v, UriKind.Absolute) },
#endif
			{ "i|id=", "The App Id", v => _AeAppId = v },
			{ "n|name=", "The App Name", v => _AeAppName = v },
			{ "a|ae=", "The existing AE Id", v => _AeId = v },
			//{ "credential=", "The AE registration Credential", v => _AeCredential = v },
		};

		protected override string Usage { get; } = "[<options>]";

		Application _application;


		async Task<AE> Register(Connection<PrimitiveContent> connection)
		{
			if (!string.IsNullOrEmpty(_AeId))
			{
				// look for existing AE
				var ae = (await connection.TryGetPrimitiveAsync(_AeId, _AeId)).AE;
				if (ae != null) {
					Trace.WriteLine($"Using existing AE {ae.AE_ID}");
					return ae;
				}
			}

			// register a new AE
			Trace.WriteLine("Invoking AE Registration API");
			return await connection.RegisterApplicationAsync(
				new ApplicationConfiguration
				{
					AppId = _AeAppId,
					AppName = _AeAppName,
					CredentialId = null,
					PoaUrl = _poaUrl,
				}
			);
		}

		async Task CreateMeterRead(string readsContainer)
		{
			var summation = new global::Example.Types.Data.Summation
			{
				ReadTime = DateTimeOffset.UtcNow,
				Value = new Random().NextDouble() * 100,
			};

			Console.WriteLine($"Create Read: @ {summation.ReadTime} = {summation.Value}");

			await _application.AddContentInstanceAsync(
				readsContainer,
				new global::Example.Types.Data
				{
					MeterId = _application.AeId,
					UOM = global::Example.Types.Data.Units.USGal,
					Summations = [summation]
				}
			);
		}

		public override async Task Run(IList<string> args)
		{
			if (_m2mUrl == null)
				ShowUsage("CSE url is required", true);

			_connectionConfiguration = new Connection.ConnectionConfiguration{ M2MUrl = _m2mUrl };

			CoAP.Log.LogManager.Level = CoAP.Log.LogLevel.Warning;

			// configure a oneM2M CoAP connection
			var connection = new CoapConnection(_connectionConfiguration);

			// start the POA CoAP server
			var coapServer = new CoAP.Server.CoapServer();
			coapServer.AddEndPoint(new IPEndPoint(IPAddress.Any, _poaUrl.Port));
			coapServer.Add(connection.CreateNotificationResource());
			coapServer.Start();


			// initialize the connection
			var ae = await Register(connection);
			var cseId = "/PN_CSE";
			_application = new Application(connection, ae, cseId);


			// create our containers
			await _application.EnsureContainerAsync(_MsPolicyPath);
			await _application.EnsureContainerAsync(_MsCommandsPath);


			// discover the in-ae
			var inAeUrl = (await _application.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = "/PN_CSE/",
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = [ResourceType.AE],
					Attribute = Connection<PrimitiveContent>.GetAttributes<AE>(_ => _.AppName == _AeAppName),
				}
			})).URIList.SingleOrDefault();

			if (inAeUrl == null)
				ShowError($"Unable to find in-AE {_AeAppName}");


			var tsReadInterval = TimeSpan.FromDays(15);
			var updatePolicySource = new CancellationTokenSource();
			var lockPolicyUpdate = new object();


			// fetch the most recent policy, initialize the read interval
			var policy = await _application.GetLatestContentInstanceAsync<global::Example.Types.Config.MeterReadPolicy>(_MsPolicyPath);
			if (policy != null)
				tsReadInterval = TimeSpan.Parse(policy.ReadInterval);


			// subscribe to the policy container
			var policyObservable = await _application.ObserveContentInstanceAsync<global::Example.Types.Config.MeterReadPolicy>(_MsPolicyPath, MeterReadSubscriptionName);
			using var eventSubscription = policyObservable.Subscribe(policy =>
			{
				lock (lockPolicyUpdate)
				{
					tsReadInterval = TimeSpan.Parse(policy.ReadInterval);

					Console.WriteLine($"New Read Interval: {tsReadInterval}");

					// 
					var oldTokenSource = updatePolicySource;
					updatePolicySource = new CancellationTokenSource();
					oldTokenSource.Cancel();
				}
			});


			while (true)
			{
				var timeStart = DateTimeOffset.UtcNow;

				await CreateMeterRead($"{inAeUrl}/{_ReadsContainerName}");

				try
				{
					await Task.Delay(DateTimeOffset.UtcNow + tsReadInterval - timeStart, updatePolicySource.Token);
				}
				catch (TaskCanceledException) {}	// ignore the wake-up exception
			}
		}
	}
}
