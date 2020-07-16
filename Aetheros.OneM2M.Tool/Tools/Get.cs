using Aetheros.OneM2M.Api;
using Microsoft.AspNetCore.Http;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GridNet.IoT.Client.Tools
{
	[Description("OneM2M retrieve/discovery")]
	public class Get : UtilityBase
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
				if (string.IsNullOrWhiteSpace(_cert))
					ShowError($"Client Certificate (--cert) is required when using https");
				if (!File.Exists(_cert))
					ShowError($"Not Found: {_cert}");
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
				var certificate = AosUtils.LoadCertificate(_cert);
				handler.ClientCertificates.Add(certificate);
			}

			HttpClient client;
#if true //DEBUG
			var loggingHandler = new TraceMessageHandler(handler);
			client = new HttpClient(loggingHandler);
#else
			client = new HttpClient(handler);
#endif
			client.DefaultRequestHeaders.Add("Accept", HttpConnection.OneM2MResponseContentType);

			if (string.IsNullOrWhiteSpace(_rqi))
				_rqi = Guid.NewGuid().ToString("N");

			var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
			httpRequestMessage.Headers.Add("X-M2M-RI", _rqi);
			httpRequestMessage.Headers.Add("X-M2M-Origin", _org);

			var response = await client.SendAsync(httpRequestMessage);
			//response.EnsureSuccessStatusCode();

			var responseBody = await response.Content.ReadAsStringAsync();
			Console.WriteLine(responseBody);
		}
	}
}