using Aetheros.OneM2M.Api;

using Mono.Options;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

// get -c C:\work\gridnet\m2msdk\AetherosOneM2MSDK\Aetheros.OneM2M.Tool\cert.pfx --from C4bb2f056000001 "https://api.piersh-m2m.corp.grid-net.com/PN_CSE/C4bb2f056000001/data-cnt?cra=20210526T014643.98874&fu=1&ty=4&rcn=5&lvl=2"
// Get -f C5eb5c0c2000006 "http://api.piersh-m2m.corp.grid-net.com:21300/PN_CSE/C5eb5c0c2000006/a/b/c?rcn=8&ty=4"
// Get --parallel=10000 -f C5eb5c0c2000006 "http://api.piersh-m2m.corp.grid-net.com:21300/testdev-piersh/foo/bar?rcn=8&ty=4"
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

		public override OptionSet Options => new OptionSet
		{
			{ "c|cert=", "The filename of the client certificate to use", v => _cert = v },
			{ "f|from=", "The Originator of the request", v => _org = v },
			{ "n|number=", "Number of duplicate requests", v => _count = long.Parse(v) },
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
				if (string.IsNullOrWhiteSpace(_cert))
					;//ShowError($"Client Certificate (--cert) is required when using https");
				else if (!File.Exists(_cert))
					ShowError($"Not Found: {_cert}");
			}

			if (string.IsNullOrWhiteSpace(_org))
                ShowError($"Originator is required (--from)");

			var hostUri = new Uri(uri.GetLeftPart(UriPartial.Authority));

			var handler = new HttpClientHandler
			{
				ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
			};

			if (_cert != null)
			{
				var certificate = AosUtils.LoadCertificate(_cert);
				handler.ClientCertificates.Add(certificate);
			}

			string rqiPrefix = Guid.NewGuid().ToString("N").Substring(0, 6);

			IEnumerable<Task<HttpResponseMessage>> Tasks()
			{
				for (long i = 0; i < _count; i++)
				{
#if true //DEBUG
					var loggingHandler = new TraceMessageHandler(handler);
					var client = verbose ? new HttpClient(loggingHandler) : new HttpClient();
#else
				var client = new HttpClient(handler);
#endif
					client.Timeout = TimeSpan.FromMinutes(5);
					client.DefaultRequestHeaders.Add("Accept", HttpConnection.OneM2MResponseContentType);

					var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
					var rqi = _rqi ?? $"{rqiPrefix}/{i}";
					if (!verbose)
						Console.WriteLine(rqi);
					httpRequestMessage.Headers.Add("X-M2M-RI", rqi);
					httpRequestMessage.Headers.Add("X-M2M-Origin", _org);
					yield return client.SendAsync(httpRequestMessage);
				}
			}

			//var responses = await tasks.WhenAll();
			//var response = await client.SendAsync(httpRequestMessage);
			//response.EnsureSuccessStatusCode();

			foreach (var task in Tasks())
			{
				var response = await task;
				if (verbose)
					Console.WriteLine("===========");
				var responseBody = await response.Content.ReadAsStringAsync();
				if (verbose)
				{
					Console.WriteLine($"{(int) response.StatusCode} {response.ReasonPhrase}");
					var headers = response.Headers;
					foreach (var header in headers)
						foreach (var value in header.Value)
							Console.WriteLine($"{header.Key}: {value}");
					Console.WriteLine("");
					Console.WriteLine(responseBody);
				}
			}
		}
	}
}