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
			_pnClient.DefaultRequestHeaders.Add("Accept", OneM2MResponseContentType);
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

		public async Task<T> GetResponseAsync<T>(HttpRequestMessage request)
			where T : class, new()
		{
			using var response = await _pnClient.SendAsync(request);
			return await response.DeserializeAsync<T>() ??
				throw new InvalidDataException("The returned response did not match type 'ResponseContent'");
		}

		public override async Task<T> GetResponseAsync<T>(RequestPrimitive body)
		{
			using var request = GetRequest(body);
			return await GetResponseAsync<T>(request);
		}


		internal HttpRequestMessage GetRequest(RequestPrimitive body)
		{
			var args = GetRequestParameters(body);

			var method = body.Operation switch
			{
				Operation.Retrieve => HttpMethod.Get,
				Operation.Update => HttpMethod.Put,
				Operation.Delete => HttpMethod.Delete,
				_ => HttpMethod.Post,
			};

			var urlBuilder = new UriBuilder(_iotApiUrl)
			{
				Path = body.To,
				Query = string.Join("&", args.AllKeys.SelectMany(args.GetValues, (k, v) => $"{k}={Uri.EscapeDataString(v)}")),
			};

			var httpRequestMessage = new HttpRequestMessage(method, urlBuilder.ToString());

			var contentTypeHeader = new MediaTypeHeaderValue(OneM2MResponseContentType);

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

	public static class HtppConnectionExtensions
	{
		public static async Task<T> DeserializeAsync<T>(this HttpResponseMessage response)
			where T : class, new()
		{
			var body = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
				throw new Connection.HttpStatusException(response.StatusCode, response.ReasonPhrase);

			response.EnsureSuccessStatusCode();

			if (string.IsNullOrWhiteSpace(body))
				throw new InvalidDataException("An empty response was returned");

			return Connection.DeserializeJson<T>(body)
				?? throw new InvalidDataException($"The response did not match Type '{typeof(T).Name}'");
		}
	}
}
