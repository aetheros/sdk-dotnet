using Aetheros.OneM2M.Api;
using Microsoft.AspNetCore.Http;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

// get -c C:\work\gridnet\m2msdk\AetherosOneM2MSDK\Aetheros.OneM2M.Tool\cert.pfx --from C5eb5c0c2000006 "https://api.piersh-m2m.corp.aetheros.com/PN_CSE/C4bb2f056000001/data-cnt?cra=20210526T014643.98874&fu=1&ty=4&rcn=5&lvl=2"

namespace GridNet.IoT.Client.Tools
{
	[Description("delete a OneM2M resource")]
	public class Delete : UtilityBase
	{
		string _rqi;
		string _org;
		string _cert;

		public override OptionSet Options => new OptionSet
		{
			{ "c|cert=", "The filename of the client certificate to use", v => _cert = v },
			{ "f|from=", "The Originator of the request", v => _org = v },
			{ "r|requestIdentifier=", "The Request Identifier to use", v => _rqi = v },
		};

		protected override string Usage { get; } = "[<options>] <url>";

		public override async Task Run(IList<string> args)
		{
			if (args.Count != 1)
				ShowUsage(exit: true);

			if (!Uri.TryCreate(args[0], UriKind.Absolute, out Uri uri))
				ShowError($"Invalid url: {args[0]}");

			if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
			{
				if (!string.IsNullOrWhiteSpace(_cert))
				{
					if (!File.Exists(_cert))
						ShowError($"Not Found: {_cert}");
				}
			}

			if (string.IsNullOrWhiteSpace(_org))
				ShowError($"Originator is required (--from)");

			var hostUri = new Uri (uri.GetLeftPart(UriPartial.Authority));

			var handler = new HttpClientHandler
			{
				ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
			};

			if (_cert != null)
			{
				var certificate = AosUtils.LoadCertificateWithKey(_cert);
				if (certificate != null)
					handler.ClientCertificates.Add(certificate);
			}

			HttpClient client;
#if true //DEBUG
			var loggingHandler = new TraceMessageHandler(handler);
			client = new HttpClient(loggingHandler);
#else
			client = new HttpClient(handler);
#endif
			client.DefaultRequestHeaders.Add("Accept", Connection<Aetheros.Schema.OneM2M.PrimitiveContent>.OneM2MResponseContentType);
			client.Timeout = TimeSpan.FromMinutes(5);


			var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, uri);
			httpRequestMessage.Headers.Add("X-M2M-RI", _rqi ?? Guid.NewGuid().ToString("N"));
			httpRequestMessage.Headers.Add("X-M2M-Origin", _org);
			var response = await client.SendAsync(httpRequestMessage);
		}
	}
}