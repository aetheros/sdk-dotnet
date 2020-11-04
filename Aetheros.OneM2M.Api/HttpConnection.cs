using Aetheros.Schema.OneM2M;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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

		public X509Certificate? ClientCertificate { get; }

		public HttpConnection(Connection.IConnectionConfiguration config)
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

		public async Task<ResponseContent<TPrimitiveContent>> GetResponseAsync(HttpRequestMessage request)
		{
			using var response = await _pnClient.SendAsync(request);
			var responseContent = await response.DeserializeAsync<ResponseContent<TPrimitiveContent>>() ??
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
			if (body != null)
				Trace.WriteLine(body);

			Trace.Flush();

			var requestPrimitive = ParseNotification(body, request.Headers, request.Query);
			if (requestPrimitive != null)
				_notifications.OnNext(requestPrimitive);
		}

		Notification<TPrimitiveContent>? ParseNotification(string body, IHeaderDictionary headers, IQueryCollection query)
		{
			var notificationContent = Connection.DeserializeJson<NotificationContent<TPrimitiveContent>>(body);
			if (notificationContent == null)
				return null;

			var notification = notificationContent.Notification;
			if (notification == null)
				return null;

			var serializer = JsonSerializer.CreateDefault(Connection.JsonSettings);
			var representation = ((Newtonsoft.Json.Linq.JObject) notification.NotificationEvent.Representation).ToObject<TPrimitiveContent>(serializer);

			var requestPrimitive = notification.NotificationEvent.PrimitiveRepresentation = new RequestPrimitive<TPrimitiveContent>
			{
				From = headers["X-M2M-Origin"].FirstOrDefault(),
				RequestIdentifier = headers["X-M2M-RI"].FirstOrDefault(),
				GroupRequestIdentifier = headers["X-M2M-GID"].FirstOrDefault(),
				OriginatingTimestamp = headers["X-M2M-OT"].FirstOrDefault()?.ParseNullableDateTimeOffset(),
				ResultExpirationTimestamp = headers["X-M2M-RST"].FirstOrDefault(),
				RequestExpirationTimestamp = headers["X-M2M-RET"].FirstOrDefault(),
				OperationExecutionTime = headers["X-M2M-OET"].FirstOrDefault(),
				EventCategory = headers["X-M2M-EC"].FirstOrDefault(),

				PrimitiveContent = representation
			};

			if (query.Any())
			{
				var notificationURI = headers["X-M2M-RTU"];
				var responseType = query["rt"];
				if (notificationURI.Count > 0 || responseType.Count > 0)
				{
					requestPrimitive.ResponseType = new ResponseTypeInfo
					{
						ResponseTypeValue = responseType.FirstOrDefault()?.ParseNullableEnum<ResponseType>(),
						NotificationURI = notificationURI.Join("&")?.Split('&')?.ToArray(),
					};
				}

				FilterCriteria? fc = null;
				FilterCriteria FC() => fc ??= new FilterCriteria();

				if (DateTimeOffset.TryParse(query["crb"].FirstOrDefault(), out DateTimeOffset crb))
					FC().CreatedBefore = crb;

				if (DateTimeOffset.TryParse(query["cra"].FirstOrDefault(), out DateTimeOffset cra))
					FC().CreatedAfter = cra;

				if (DateTimeOffset.TryParse(query["ms"].FirstOrDefault(), out DateTimeOffset ms))
					FC().ModifiedSince = ms;

				if (long.TryParse(query["sts"].FirstOrDefault(), out long sts))
					FC().StateTagSmaller = sts;

				if (long.TryParse(query["stb"].FirstOrDefault(), out long stb))
					FC().StateTagBigger = stb;

				if (DateTimeOffset.TryParse(query["exb"].FirstOrDefault(), out DateTimeOffset exb))
					FC().ExpireBefore = exb;

				if (DateTimeOffset.TryParse(query["exa"].FirstOrDefault(), out DateTimeOffset exa))
					FC().ExpireAfter = exa;

				var resourceTypes = query["ty"].SelectMany(str => str.Split(",")).Select(str => Enum.Parse<ResourceType>(str)).ToList();
				if (resourceTypes.Count > 0)
					FC().ResourceType = resourceTypes;

				if (long.TryParse(query["sza"].FirstOrDefault(), out long sza))
					FC().SizeAbove = sza;

				if (long.TryParse(query["szb"].FirstOrDefault(), out long szb))
					FC().SizeBelow = szb;

				if (long.TryParse(query["lim"].FirstOrDefault(), out long lim))
					FC().Limit = lim;

				if (Enum.TryParse<FilterUsage>(query["fu"].FirstOrDefault(), out FilterUsage fu))
					FC().FilterUsage = fu;

				var fo = query["fo"].FirstOrDefault();
				if (fo == "1" || "true".Equals(fo, StringComparison.InvariantCultureIgnoreCase))
					FC().FilterOperation = true;
				else if (fo == "0" || "false".Equals(fo, StringComparison.InvariantCultureIgnoreCase))
					FC().FilterOperation = false;

				if (Enum.TryParse<ContentFilterSyntax>(query["cfs"].FirstOrDefault(), out ContentFilterSyntax cfs))
					FC().ContentFilterSyntax = cfs;

				if (query["cfq"].Count > 0)
					FC().ContentFilterQuery = query["cfq"].FirstOrDefault();

				if (long.TryParse(query["lvl"].FirstOrDefault(), out long lvl))
					FC().Level = lvl;

				if (long.TryParse(query["ofst"].FirstOrDefault(), out long ofst))
					FC().Offset = ofst;

				/* TODO:
				if (fc.Attribute != null)
					foreach (var attr in fc.Attribute)
						args.Add(attr.Name, attr.Value.ToString());

				if (fc.SemanticsFilter != null)
					args.AddRange("smf", fc.SemanticsFilter);

				if (fc.Labels != null)
					args.AddRange("lbl", fc.Labels);

				if (fc.ContentType != null)
					args.AddRange("cty", fc.ContentType);
				*/

				requestPrimitive.FilterCriteria = fc;
			}

			return notification;
		}		
	}

	public static class HttpConnectionExtensions
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

	public class HttpConnection : HttpConnection<PrimitiveContent>
	{
		public HttpConnection(Connection.IConnectionConfiguration config) : base(config) {}
		public HttpConnection(Uri m2mUrl, string certificateFilename) : base(m2mUrl, certificateFilename) {}
		public HttpConnection(Uri m2mUrl, X509Certificate? certificate = null) : base(m2mUrl, certificate) {}
	}
}
