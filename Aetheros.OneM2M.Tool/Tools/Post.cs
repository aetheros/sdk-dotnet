using Aetheros.OneM2M.Api;
using Aetheros.Schema.OneM2M;
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

// get -c C:\work\gridnet\m2msdk\AetherosOneM2MSDK\Aetheros.OneM2M.Tool\cert.pfx --from C5eb5c0c2000006 "https://api.piersh-m2m.corp.grid-net.com/PN_CSE/C4bb2f056000001/data-cnt?cra=20210526T014643.98874&fu=1&ty=4&rcn=5&lvl=2"

namespace GridNet.IoT.Client.Tools
{
	[Description("OneM2M create")]
	public class Post : UtilityBase
	{
		string _rqi;
		string _org;
		string _cert;
		string _method;
		string _conentType = "application/vnd.onem2m-res+json";
		ResourceType? _resourceType;

		public override OptionSet Options => new OptionSet
		{
			{ "c|cert=", "The filename of the client certificate to use", v => _cert = v },
			{ "m|method=", "The HTTP Method to use (POST)", v => _method = v },
			{ "f|from=", "The Originator of the request", v => _org = v },
			{ "b|body=", "The Content-Type of the body", v => _conentType = v },
			{ "t|type=", "The ResourceType of the new resource", v => _resourceType = Enum.Parse<ResourceType>(v) },
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

			HttpMethod method = HttpMethod.Post;
			switch(_method?.ToUpper()) {
				case "POST":
					method = HttpMethod.Post;
					break;
				case "PUT":
					method = HttpMethod.Put;
					break;
				case null:
					break;
				default:
					ShowError($"Unsupported method: {_method}");
					break;
			}

			if (method == HttpMethod.Post) {
				if (_resourceType == null)
					ShowError("--type is required");
			}

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
			client.Timeout = TimeSpan.FromMinutes(5);
			client.DefaultRequestHeaders.Add("Accept", Connection<Aetheros.Schema.OneM2M.PrimitiveContent>.OneM2MResponseContentType);


			var httpRequestMessage = new HttpRequestMessage(method, uri);
			httpRequestMessage.Headers.Add("X-M2M-RI", _rqi ?? Guid.NewGuid().ToString("N"));
			httpRequestMessage.Headers.Add("X-M2M-Origin", _org);

			using var stdin = Console.OpenStandardInput();
			httpRequestMessage.Content = new StreamContent(stdin);
			httpRequestMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_conentType);
			if (_resourceType != null)
				httpRequestMessage.Content.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("ty", ((int) _resourceType).ToString()));

			var response = await client.SendAsync(httpRequestMessage);

			var responseBody = await response.Content.ReadAsStringAsync();
			Console.WriteLine(responseBody);
		}
	}
}