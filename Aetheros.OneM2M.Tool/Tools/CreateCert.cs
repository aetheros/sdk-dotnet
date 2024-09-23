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

//
// example:
//   dotnet run -- CreateCert -c http://policynet-m2m:21300/ -C https://policynet-ra:18090/ -i C123456789x0001 -t 00000000-0000-0000-0000-000000000000 -o cert.pem
//

namespace GridNet.IoT.Client.Tools
{
	[Description("create and sign a certificate")]
	public class CreateCert : UtilityBase
	{
		Uri _m2mUrl;
		Connection.ConnectionConfiguration _connectionConfiguration;

		string _AeId;
		Uri _CAUri;
		string _token;
		string _certificateFilename;
		const string _RegPath = "/PN_CSE";

		public CreateCert()
		{
		}

		public override OptionSet Options => new OptionSet
		{
			{ "c|cse=", "The URL to the CSE", v => _m2mUrl = new Uri(v, UriKind.Absolute) },
			{ "C|CA=", "The URL to the CA", v => _CAUri = new Uri(v, UriKind.Absolute) },
			{ "i|aeid=", "The existing AE Id", v => _AeId = v },
			{ "t|token=", "The AE's security Token", v => _token = v },
			{ "o|output=", "The filename of the certificate to sign", v => _certificateFilename = v },
		};

		protected override string Usage { get; } = "[<options>]";

		public override async Task Run(IList<string> args)
		{
			if (_m2mUrl == null)
				ShowUsage("CSE url is required", true);

			if (_CAUri == null)
				ShowUsage("CA url is required", true);

			if (string.IsNullOrWhiteSpace(_AeId))
				ShowUsage("AE ID is required", true);

			if (string.IsNullOrWhiteSpace(_token))
				ShowUsage("AE token is required", true);

			if (string.IsNullOrWhiteSpace(_certificateFilename))
				ShowUsage("Certificate Filename required", true);

			_connectionConfiguration = new Connection.ConnectionConfiguration{ M2MUrl = _m2mUrl };

			// configure a oneM2M CoAP connection
			var connection = new HttpConnection(_connectionConfiguration);

			Trace.WriteLine($"Looking for existing AE {_AeId}");
			AE ae;
			try
			{
				// look for existing AE
				ae = (await connection.GetPrimitiveAsync(_AeId, _AeId)).AE;
				Trace.WriteLine($"Found existing AE {ae.AE_ID}");
			}
			catch (Exception e)
			{
				Trace.WriteLine(e.Message);
				Trace.WriteLine($"Existing AE {_AeId} not found");
				return;
			}

			var cert = await Application.GenerateSigningCertificateAsync(
				_CAUri,
				ae,
				_certificateFilename
			);

			Trace.WriteLine(cert.ToString());
		}
	}
}
