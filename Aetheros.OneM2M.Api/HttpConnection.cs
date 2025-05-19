using Aetheros.Schema.OneM2M;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Aetheros.OneM2M.Api
{
	public class HttpConnection<TPrimitiveContent> : Connection<TPrimitiveContent>
		where TPrimitiveContent : PrimitiveContent, new()
	{
		readonly Uri _iotApiUrl;
		readonly HttpClient _pnClient;

		public X509Certificate2? ClientCertificate { get; }

		public override bool IsSecure => _iotApiUrl.Scheme == Uri.UriSchemeHttps;

		public HttpConnection(Connection.IConnectionConfiguration config)
			: this(config.M2MUrl, config.CertificateFilename) { }

		public HttpConnection(Uri m2mUrl, string? certificateFilename)
			: this(m2mUrl, AosUtils.LoadCertificateWithKey(certificateFilename)) { }

		public HttpConnection(Uri m2mUrl, X509Certificate2? certificate = null)
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
				ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
				{
					return true;
				},
			};

			if (certificate != null)
			{
				Trace.Assert(certificate.HasPrivateKey, "The certificate must have a private key");
				this.ClientCertificate = certificate;
				handler.ClientCertificates.Add(certificate);
			}

#if DEBUG
			var loggingHandler = new TraceMessageHandler(handler);
			_pnClient = new HttpClient(loggingHandler);
#else
			_pnClient = new HttpClient(handler);
#endif
			//_pnClient.DefaultRequestVersion = HttpVersion.Version11;

			_pnClient.Timeout = TimeSpan.FromSeconds(300);
			_pnClient.DefaultRequestHeaders.Add("Accept", OneM2MResponseContentType);
			//_pnClient.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");
		}

		public async Task<ResponseContent<TPrimitiveContent>> GetResponseAsync(HttpRequestMessage request)
		{
			using var response = await _pnClient.SendAsync(request);
			var responseContent = await response.DeserializeAsync<ResponseContent<TPrimitiveContent>>() ??
				throw new InvalidDataException("The returned response did not match type 'ResponseContent'");

			if (response.Headers.TryGetValues("X-M2M-RSC", out IEnumerable<string>? statusCodeHeaders))
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

		public override async Task<T> GetResponseAsync<T>(RequestPrimitive<TPrimitiveContent> body)
		{
			using var request = GetRequest(body);
			return await GetResponseAsync<T>(request);
		}


		internal HttpRequestMessage GetRequest(RequestPrimitive<TPrimitiveContent> body)
		{
			var args = GetRequestParameters(body);

			var fc = body.FilterCriteria;
			if (fc != null)
			{
				foreach (var attr in fc.Attribute)
				{
					if (attr.Value != null)
						args.Add(attr.Name, attr.Value.ToString());
				}
			}

			var method = body.Operation switch
			{
				Operation.Retrieve => HttpMethod.Get,
				Operation.Update => HttpMethod.Put,
				Operation.Delete => HttpMethod.Delete,
				_ => HttpMethod.Post,
			};

			var url = body.To;
			if (url.StartsWith("//"))
				url = "/_/" + url.Substring(2);
			else if (url.StartsWith("/"))
				url = "/~/" + url.Substring(1);
			else
				url = "/" + url;

			var urlBuilder = new UriBuilder(_iotApiUrl)
			{
				Path = url,
				Query = string.Join("&", args.AllKeys.SelectMany(args.GetValues, (k, v) => $"{k}={Uri.EscapeDataString(v)}")),
			};

			var httpRequestMessage = new HttpRequestMessage(method, urlBuilder.ToString());

			var contentTypeHeader = new MediaTypeHeaderValue(OneM2MResponseContentType);

			//if (method == HttpMethod.Post || method == HttpMethod.Put)
			{
				if (body.ResourceType != null)
					contentTypeHeader.Parameters.Add(new NameValueHeaderValue("ty", ((int)body.ResourceType).ToString()));

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

		public async Task HandleNotificationAsync(HttpContext context)
		{
			var request = context.Request;
			using var bodyStream = new StreamReader(request.Body, true);
			var body = (await bodyStream.ReadToEndAsync())!;

			Trace.WriteLine("\n!!!!!!!!!!!!!!!!");
			Trace.WriteLine($"{request.Method} {request.Path}?{request.QueryString} {request.Protocol}");
			foreach (var header in request.Headers)
			{
				foreach (var value in header.Value)
					Trace.WriteLine($"{header.Key}: {value}");
			}

			Trace.WriteLine("");
			if (body == null)
			{
				Debug.WriteLine($"{nameof(HandleNotificationAsync)}: empty body");
				return;
			}

			Trace.WriteLine(body);
			Trace.Flush();

			foreach (var notification in ParseNotifications(body))
			{
				_notifications.OnNext(notification);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				_pnClient?.Dispose();
			base.Dispose(disposing);
		}
		
	}

	public class HttpStatusException : Exception
	{
		public HttpStatusCode StatusCode { get; }
		public string ReasonPhrase { get; }

		public HttpStatusException(HttpStatusCode statusCode, string reasonPhrase)
			: base($"Http Status {statusCode}: {reasonPhrase}")
		{
			this.StatusCode = statusCode;
			this.ReasonPhrase = reasonPhrase;
		}
	}

	public static class HttpConnectionExtensions
	{
		public static async Task<T> DeserializeAsync<T>(this HttpResponseMessage response)
			where T : class, new()
		{
			var body = await response.Content.ReadAsStringAsync();

			if (response.Headers.TryGetValues("X-M2M-RSC", out IEnumerable<string>? statusCodeHeaders))
			{
				var statusCodeHeader = statusCodeHeaders.FirstOrDefault();
				if (Enum.TryParse<ResponseStatusCode>(statusCodeHeader, out ResponseStatusCode statusCode))
				{
					if (statusCode >= ResponseStatusCode.BadRequest)
					{
						string msg = null;
						try
						{
							var errorResponse = Connection.DeserializeJson<ResponseContent<PrimitiveContent>>(body);
							msg = errorResponse?.DebugInfo;
						}
						catch (Exception e)
						{
							// ignore
						}
						throw new OneM2MException(statusCode, msg ?? statusCode.ToString());
					}
				}
			}

			if (!response.IsSuccessStatusCode)
				throw new HttpStatusException(response.StatusCode, response.ReasonPhrase ?? "Unknown Error");

			response.EnsureSuccessStatusCode();

			if (string.IsNullOrWhiteSpace(body))
				throw new InvalidDataException("An empty response was returned");

			return Connection.DeserializeJson<T>(body)
				?? throw new InvalidDataException($"The response did not match Type '{typeof(T).Name}'");
		}
	}

	public class HttpConnection : HttpConnection<PrimitiveContent>, IDisposable
	{
		public HttpConnection(Connection.IConnectionConfiguration config) : base(config) { }
		public HttpConnection(Uri m2mUrl, string certificateFilename) : base(m2mUrl, certificateFilename) { }
		public HttpConnection(Uri m2mUrl, X509Certificate2? certificate = null) : base(m2mUrl, certificate) { }
	}
}
