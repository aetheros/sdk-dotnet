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
	[Description("register a new IN-AE resource")]
	public class RegisterAE : UtilityBase
	{
		Uri _m2mUrl;
		Connection.ConnectionConfiguration _connectionConfiguration;

		string _AeId;
		string _AeAppId;
		string _AeAppName;
		string _AeCredential;
		Uri _poaUrl;
		const string _RegPath = "/PN_CSE";

		public RegisterAE()
		{
		}

		public override OptionSet Options => new OptionSet
		{
			{ "c|cse=", "The URL to the CSE", v => _m2mUrl = new Uri(v, UriKind.Absolute) },
			{ "a|appid=", "The App Id", v => _AeAppId = v },
			{ "n|name=", "The App Name", v => _AeAppName = v },
			{ "p|poa=", "The remote POA (point of access) url", v => _poaUrl = new Uri(v, UriKind.Absolute) },
			{ "i|aeid=", "The existing AE Id", v => _AeId = v },
			{ "credential=", "The AE registration Credential", v => _AeCredential = v },
		};

		protected override string Usage { get; } = "[<options>]";

		public override async Task Run(IList<string> args)
		{
			if (_m2mUrl == null)
				ShowUsage("CSE url is required", true);

			if (string.IsNullOrWhiteSpace(_AeAppId))
				ShowUsage("App Id is required", true);

			if (string.IsNullOrWhiteSpace(_AeAppName))
				ShowUsage("App Name is required", true);

			_connectionConfiguration = new Connection.ConnectionConfiguration{ M2MUrl = _m2mUrl };

			// configure a oneM2M CoAP connection
			var connection = new HttpConnection(_connectionConfiguration);

			if (!string.IsNullOrEmpty(_AeId))
			{
				Trace.WriteLine($"Looking for existing AE {_AeId}");
				try
				{
					// look for existing AE
					var ae = (await connection.GetPrimitiveAsync(_AeId, _AeId)).AE;
					Trace.WriteLine($"Found existing AE {ae.AE_ID}");
					return;
				}
				catch (Exception e)
				{
					Trace.WriteLine(e.Message);
				}
				Trace.WriteLine($"Existing AE {_AeId} not found");
			}

			if (_poaUrl == null)
				ShowUsage("POA is required", true);

			// register a new AE
			Trace.WriteLine("Invoking AE Registration API");
			await connection.RegisterApplicationAsync(new ApplicationConfiguration
			{
				AppId = _AeAppId,
				AppName = _AeAppName,
				CredentialId = _AeCredential,
				PoaUrl = _poaUrl,
			});
		}
	}
}
