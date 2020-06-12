using Aetheros.OneM2M.Binding;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Aetheros.OneM2M.Api
{
	public class HttpConnection : Connection
	{
		readonly Uri _iotApiUrl;
		readonly HttpClient _pnClient;

		public X509Certificate? ClientCertificate { get; }

		public HttpConnection(IConnectionConfiguration config)
			: this(config.M2MUrl, config.CertificateFilename) { }

		public HttpConnection(Uri m2mUrl, string certificateFilename)
			: this(m2mUrl, AosUtils.LoadCertificate(certificateFilename)) { }

		public HttpConnection(Uri m2mUrl, X509Certificate? certificate = null)
		{
			_iotApiUrl = m2mUrl;

			var handler = new HttpClientHandler
			{
#if false
				//enable this code if using proxy	
				Proxy = new System.Net.WebProxy("http://localhost.:8888")
					{
						BypassProxyOnLocal = false,
					},
#endif
				ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
			};

			if (certificate != null)
			{
				this.ClientCertificate = certificate;
				handler.ClientCertificates.Add(certificate);
			}

#if DEBUG
			var loggingHandler = new TraceMessageHandler(handler);
			_pnClient = new HttpClient(loggingHandler);
#else
			_pnClient = new HttpClient(handler);
#endif

			_pnClient.Timeout = TimeSpan.FromSeconds(300);
			_pnClient.DefaultRequestHeaders.Add("Accept", OneM2MResponseContentTYpe);
		}

		public async Task<ResponseContent> GetResponseAsync(HttpRequestMessage request)
		{
			using var response = await _pnClient.SendAsync(request);
			var responseContent = await response.DeserializeAsync<ResponseContent>() ??
				throw new InvalidDataException("The returned response did not match type 'ResponseContent'");

			if (response.Headers.TryGetValues("X-M2M-RSC", out IEnumerable<string> statusCodeHeaders))
			{
				var statusCodeHeader = statusCodeHeaders.FirstOrDefault();
				if (statusCodeHeader == null && Enum.TryParse<ResponseStatusCode>(statusCodeHeader, out ResponseStatusCode statusCode))
					responseContent.ResponseStatusCode = statusCode;
			}
			return responseContent;
		}

		public override async Task<T> GetResponseAsync<T>(RequestPrimitive body)
		{
			using var request = GetRequest(body);
			return await GetResponseAsync<T>(request);
		}

		public async Task<T> GetResponseAsync<T>(HttpRequestMessage request)
			where T : class, new()
		{
			using var response = await _pnClient.SendAsync(request);
			return await response.DeserializeAsync<T>() ??
				throw new InvalidDataException("The returned response did not match type 'ResponseContent'");
		}

		internal HttpRequestMessage GetRequest(RequestPrimitive body)
		{
			var args = new NameValueCollection();
			if (body.ResultContent != null)
				args["rcn"] = body.ResultContent.Value.ToString("d");

			if (body.ResultPersistence != null)
				args["rp"] = body.ResultPersistence;

			if (body.DeliveryAggregation != null)
				args["da"] = body.DeliveryAggregation.Value.ToString();

			if (body.DiscoveryResultType != null)
				args["drt"] = body.DiscoveryResultType.Value.ToString("d");

			if (body.RoleIDs != null)
				args.AddRange("rids", body.RoleIDs);

			if (body.TokenIDs != null)
				args["tids"] = body.TokenIDs;

			if (body.LocalTokenIDs != null)
				args.AddRange("ltids", body.LocalTokenIDs);

			if (body.TokenReqIndicator != null)
				args["tqi"] = body.TokenReqIndicator.Value.ToString();

			var rt = body.ResponseType;
			if (rt?.ResponseTypeValue != null)
				args["rt"] = rt.ResponseTypeValue.Value.ToString("d");

			var method = body.Operation switch
			{
				Operation.Retrieve => HttpMethod.Get,
				Operation.Update => HttpMethod.Put,
				Operation.Delete => HttpMethod.Delete,
				_ => HttpMethod.Post,
			};

			var fc = body.FilterCriteria;
			if (fc != null)
			{
				if (fc.CreatedBefore != null) args["crb"] = fc.CreatedBefore.Value.ToString(_dateTimeFormat);
				if (fc.CreatedAfter != null) args["cra"] = fc.CreatedAfter.Value.ToString(_dateTimeFormat);
				if (fc.ModifiedSince != null) args["ms"] = fc.ModifiedSince.Value.ToString(_dateTimeFormat);
				if (fc.UnmodifiedSince != null) args["us"] = fc.UnmodifiedSince.Value.ToString(_dateTimeFormat);
				if (fc.StateTagSmaller != null) args["sts"] = fc.StateTagSmaller.ToString();
				if (fc.StateTagBigger != null) args["stb"] = fc.StateTagBigger.ToString();
				if (fc.ExpireBefore != null) args["exb"] = fc.ExpireBefore.Value.ToString(_dateTimeFormat);
				if (fc.ExpireAfter != null) args["exa"] = fc.ExpireAfter.Value.ToString(_dateTimeFormat);
				if (fc.SizeAbove != null) args["sza"] = fc.SizeAbove.ToString();
				if (fc.SizeBelow != null) args["szb"] = fc.SizeBelow.ToString();
				if (fc.Limit != null) args["lim"] = fc.Limit.ToString();
				if (fc.FilterUsage != null) args["fu"] = fc.FilterUsage.Value.ToString("d");
				if (fc.FilterOperation != null) args["fo"] = fc.FilterOperation.Value ? "1" : "0";
				if (fc.ContentFilterSyntax != null) args["cfs"] = fc.ContentFilterSyntax.Value.ToString("d");
				if (fc.ContentFilterQuery != null) args["cfq"] = fc.ContentFilterQuery;
				if (fc.Level != null) args["lvl"] = fc.Level.ToString();
				if (fc.Offset != null) args["ofst"] = fc.Offset.ToString();

				if (fc.Attribute != null)
				{
					foreach (var attr in fc.Attribute)
					{
						if (attr.Value != null)
							args.Add(attr.Name, attr.Value.ToString());
					}
				}

				if (fc.ResourceType != null)
					args.AddRange("ty", fc.ResourceType.Select(ty => ty.ToString("d")));

				if (fc.SemanticsFilter != null)
					args.AddRange("smf", fc.SemanticsFilter);

				if (fc.Labels != null)
					args.AddRange("lbl", fc.Labels);

				if (fc.ContentType != null)
					args.AddRange("cty", fc.ContentType);
			}

			var urlBuilder = new UriBuilder(_iotApiUrl)
			{
				Path = body.To,
				Query = string.Join("&", args.AllKeys.SelectMany(args.GetValues, (k, v) => $"{k}={Uri.EscapeDataString(v)}")),
			};

			var httpRequestMessage = new HttpRequestMessage(method, urlBuilder.ToString());

			var contentTypeHeader = new MediaTypeHeaderValue(OneM2MResponseContentTYpe);

			//if (method == HttpMethod.Post || method == HttpMethod.Put)
			{
				if (body.ResourceType != null)
					contentTypeHeader.Parameters.Add(new NameValueHeaderValue("ty", ((int) body.ResourceType).ToString()));

				if (body.PrimitiveContent != null)
				{
					var bodyJson = SerializeJson(body.PrimitiveContent);
					httpRequestMessage.Content = new StringContent(bodyJson, Encoding.UTF8);
					httpRequestMessage.Content.Headers.ContentType = contentTypeHeader;
				}
			}

			httpRequestMessage.Headers.Add("X-M2M-Origin", body.From);
			httpRequestMessage.Headers.Add("X-M2M-RI", body.RequestIdentifier ?? NextRequestId);

			if (body.GroupRequestIdentifier != null)
				httpRequestMessage.Headers.Add("X-M2M-GID", body.GroupRequestIdentifier);

			if (body.OriginatingTimestamp != null)
				httpRequestMessage.Headers.Add("X-M2M-OT", body.OriginatingTimestamp.Value.ToString(_dateTimeFormat));

			if (body.ResultExpirationTimestamp != null)
				httpRequestMessage.Headers.Add("X-M2M-RST", body.ResultExpirationTimestamp);

			if (body.RequestExpirationTimestamp != null)
				httpRequestMessage.Headers.Add("X-M2M-RET", body.RequestExpirationTimestamp);

			if (body.OperationExecutionTime != null)
				httpRequestMessage.Headers.Add("X-M2M-OET", body.OperationExecutionTime);

			if (body.EventCategory != null)
				httpRequestMessage.Headers.Add("X-M2M-EC", body.EventCategory);

			if (body.ResponseType?.NotificationURI != null)
				httpRequestMessage.Headers.Add("X-M2M-RTU", string.Join("&", body.ResponseType.NotificationURI));

			return httpRequestMessage;
		}
	}
}
