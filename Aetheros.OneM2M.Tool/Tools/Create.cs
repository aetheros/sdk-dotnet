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
	[Description("create a OneM2M resource")]
	public class Create : UtilityBase
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

			var m2mUrl = $"{uri.Scheme}://{uri.Authority}";
			HttpConnection client = new HttpConnection(new Uri(m2mUrl));

			//while (true) {
				try {
					Console.Write(">");
					Console.ReadLine();
					Console.WriteLine("");

					var response = await client.GetResponseAsync(
						new RequestPrimitive
						{
							From = _org,
							To = uri.AbsolutePath,
							Operation = Operation.Create,
							ResourceType = ResourceType.ContentInstance,
							//ResultContent = resultContent,
							PrimitiveContent = new PrimitiveContent()
							{
								Container = new Aetheros.Schema.OneM2M.Container {
									ResourceName = "860112043190752"
								}
								/*
								ContentInstance = new ContentInstance
								{
									Content = await Console.In.ReadToEndAsync()
								}*/
							}
						}
					);
				}
				catch{}
			//}
		}
	}
}