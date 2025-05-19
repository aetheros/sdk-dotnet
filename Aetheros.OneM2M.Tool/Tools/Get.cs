using Aetheros.OneM2M.Api;

using Mono.Options;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

// get -c C:\work\gridnet\m2msdk\AetherosOneM2MSDK\Aetheros.OneM2M.Tool\cert.pfx --from C4bb2f056000001 "https://api.piersh-m2m.corp.aetheros.com/PN_CSE/C4bb2f056000001/data-cnt?cra=20210526T014643.98874&fu=1&ty=4&rcn=5&lvl=2"
// Get -f C5eb5c0c2000006 "http://api.piersh-m2m.corp.aetheros.com:21300/PN_CSE/C5eb5c0c2000006/a/b/c?rcn=8&ty=4"
// Get --parallel=10000 -f C5eb5c0c2000006 "http://api.piersh-m2m.corp.aetheros.com:21300/testdev-piersh/foo/bar?rcn=8&ty=4"
// /PN_CSE/testdev-piersh/aeA5eb35b7600000f

namespace GridNet.IoT.Client.Tools
{
	[Description("OneM2M retrieve/discovery")]
	public class Get : UtilityBase
	{
		string _rqi;
		string _org;
		string _cert;
		int _parallel = 1;
		long _count = 1;
		string _AeCredential;

		public override OptionSet Options => new OptionSet
		{
			{ "c|cert=", "The filename of the client certificate to use", v => _cert = v },
			{ "f|from=", "The Originator of the request", v => _org = v },
			{ "n|number=", "Number of duplicate requests", v => _count = long.Parse(v) },
			{ "r|requestIdentifier=", "The Request Identifier to use", v => _rqi = v },
			{ "credential=", "The AE registration Credential", v => _AeCredential = v },
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
			Console.WriteLine($"Host: {hostUri}");


			var connection = new HttpConnection(new Connection.ConnectionConfiguration { M2MUrl = hostUri });
			var aeId = "Cpolicynet.m2m2";
			var ae2 = await connection.FindApplicationAsync(aeId);

			if (ae2 == null) {
				ShowError($"AE not found: {aeId}");

				ae2 = await connection.RegisterApplicationAsync(new ApplicationConfiguration {
					AppId = "Nra1.com.aetheros.policynet.m2m",
					AppName = "policynet.m2m",
					CredentialId = _AeCredential,
					//PoaUrl = _poaUrl,
				});
			}

			var handler = new HttpClientHandler
			{
				ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
			};

			if (!string.IsNullOrWhiteSpace(_cert))
			{
				var certificate = AosUtils.LoadCertificateWithKey(_cert);
				handler.ClientCertificates.Add(certificate);
			}

			HttpClient client;
#if true //DEBUG
			var loggingHandler = new TraceMessageHandler(handler);
			client = new HttpClient(loggingHandler);
#else
			client = new HttpClient(handler);
#endif
			client.Timeout = TimeSpan.FromMinutes(5);
			client.DefaultRequestHeaders.Add("Accept", Connection<Aetheros.Schema.OneM2M.PrimitiveContent>.OneM2MResponseContentType);

			var tasks = Enumerable.Range(0, _parallel).Select(i => {
				var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
				httpRequestMessage.Headers.Add("X-M2M-RI", _rqi ?? Guid.NewGuid().ToString("N"));
				httpRequestMessage.Headers.Add("X-M2M-Origin", _org);
				return client.SendAsync(httpRequestMessage);
			});

			//var responses = await tasks.WhenAll();
			//var response = await client.SendAsync(httpRequestMessage);
			//response.EnsureSuccessStatusCode();

			foreach (var task in tasks)
			{
				var response = await task;
				Console.WriteLine("===========");
				var responseBody = await response.Content.ReadAsStringAsync();
				Console.WriteLine(responseBody);
			}
		}
	}
}