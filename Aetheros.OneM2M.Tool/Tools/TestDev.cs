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
	public class TestDev : UtilityBase
	{
		Uri _poaUrl = new Uri("coap://127.0.0.1:15683/notify");

		readonly Connection.ConnectionConfiguration _connectionConfiguration = new Connection.ConnectionConfiguration
		{
			M2MUrl = new Uri("coap://127.0.0.1:8110"),
		};

		string _AeId = "";
		string _AeAppId = "Nra1.com.aos.iot";
		string _AeAppName = "metersvc-smpl";

		string _AeCredential = "";//"8992O4AAEXYWY95O";
		string _RegPath = ".";
		string _MsPolicyPath = $"~/config-cnt";
		string _MsCommandsPath = $"~/command-cnt";

		string _ReadsContainerName = "data-cnt";

		public TestDev()
		{
		}

		const string MeterReadSubscriptionName = "sdk-sampl-sub-01";
		const string MeterReadPolicyName = "sdk-sampl-pol-01";

		public override OptionSet Options => new OptionSet
		{
			{ "c|cse=", "The URL to the CSE", v => _connectionConfiguration.M2MUrl = new Uri(v, UriKind.Absolute) },
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


		async Task<AE> Register(Connection connection)
		{
#if false
			// find existing AE
			var existingAeUrls = (await connection.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				From = _RegPath,
				To = _RegPath,
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.AE },
					Attribute = Connection.GetAttributes<AE>(_ => _.App_ID == _AeAppId),
				}
			})).URIList;

			if (existingAeUrls.Any())
			{
				var ae = await connection.GetPrimitiveAsync(existingAeUrls.First());
				if (ae != null)
				{
					// TODO: fix POA
					return ae.AE;
				}
			}
#endif

			if (!string.IsNullOrEmpty(_AeId))
			{
				try
				{
					var ae = (await connection.GetPrimitiveAsync(_AeId, _AeId)).AE;
					Trace.TraceInformation($"Using existing AE {ae.AE_ID}");
					return ae;
				}
				catch { }
			}

			Trace.TraceInformation("Invoking AE Registration API");

			var response = await connection.GetResponseAsync(new RequestPrimitive
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
					Summations = new[] { summation }
				}
			);
		}

		public override async Task Run(IList<string> args)
		{
			CoAP.Log.LogManager.Level = CoAP.Log.LogLevel.Warning;
			// configure a oneM2M CoAP connection
			var connection = new CoapConnection(_connectionConfiguration);

			// start the POA CoAP server
			var coapServer = new CoAP.Server.CoapServer();
			coapServer.AddEndPoint(new IPEndPoint(IPAddress.Any, _poaUrl.Port));
			coapServer.Add(connection.CreateNotificationResource());
			coapServer.Start();

			var ae = await Register(connection);
			_application = new Application(connection, ae.App_ID, ae.AE_ID, "./", _poaUrl);

			//await _application.EnsureContainerAsync(_MsPolicyPath);
			//await _application.EnsureContainerAsync(_MsCommandsPath);


			var inAeUrl = (await _application.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = "/PN_CSE/",
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.AE },
					Attribute = Connection.GetAttributes<AE>(_ => _.AppName == _AeAppName),
				}
			})).URIList.SingleOrDefault();

			if (inAeUrl == null)
				this.ShowError($"Unable to find in-AE {_AeAppName}");

			var inAe = (await _application.GetPrimitiveAsync(inAeUrl)).AE;

			var tsReadInterval = TimeSpan.FromDays(15);
			var updatePolicySource = new CancellationTokenSource();
			var lockPolicyUpdate = new object();

			var policy = await _application.GetLatestContentInstanceAsync<global::Example.Types.Config.MeterReadPolicy>(_MsPolicyPath);
			if (policy != null)
				tsReadInterval = TimeSpan.Parse(policy.ReadInterval);

			var policyObservable = await _application.ObserveAsync<global::Example.Types.Config.MeterReadPolicy>(_MsPolicyPath, MeterReadSubscriptionName);
			using var eventSubscription = policyObservable.Subscribe(policy =>
			{
				lock (lockPolicyUpdate)
				{
					tsReadInterval = TimeSpan.Parse(policy.ReadInterval);

					Console.WriteLine($"New Read Interval: {tsReadInterval}");

					var oldTokenSource = updatePolicySource;
					updatePolicySource = new CancellationTokenSource();
					oldTokenSource.Cancel();
				}
			});



			try
			{
				while (true)
				{
					var timeStart = DateTimeOffset.UtcNow;

					await CreateMeterRead($"{inAeUrl}/{_ReadsContainerName}");

					try
					{
						await Task.Delay(DateTimeOffset.UtcNow + tsReadInterval - timeStart, updatePolicySource.Token);
					}
					catch (TaskCanceledException) {}
				}
			}
			finally
			{
				//await DeleteSubscription();
				//await DeleteMeterReadPolicy();
				//await DeRegister();
			}
		}
	}
}
