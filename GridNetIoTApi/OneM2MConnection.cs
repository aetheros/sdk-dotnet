using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GridNet.OneM2M.Types;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static GridNet.IoT.Api.OneM2MConnection;

namespace GridNet.IoT.Api
{
	public class OneM2MConnection
	{
		public interface IConfig
		{
			public Uri M2MUrl { get; }
			public string CertificateFilename { get; }
		}

		const string contentType = "application/vnd.onem2m-res+json";
		const string _dateTimeFormat = "yyyyMMddTHHmmss.FFFFF";

		readonly Uri _iotApiUrl;
		readonly HttpClient _pnClient;

		public X509Certificate? ClientCertificate { get; private set; }

		static int _nextRequestId;
		readonly string _requestGuid = Guid.NewGuid().ToString("N");
		public string NextRequestId => $"{_requestGuid}/{Interlocked.Increment(ref _nextRequestId)}";

		public static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore,
		};

		public static JsonSerializer Serializer { get; }

		static OneM2MConnection()
		{
			JsonSettings.Converters.Add(new Newtonsoft.Json.Converters.IsoDateTimeConverter
			{
				DateTimeStyles = DateTimeStyles.AssumeUniversal,
				DateTimeFormat = _dateTimeFormat
			});
			JsonSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter
			{
			});

			Serializer = JsonSerializer.CreateDefault(JsonSettings);
		}

		public OneM2MConnection(IConfig config)
			: this(config.M2MUrl, config.CertificateFilename) { }

		public OneM2MConnection(Uri m2mUrl, string certificateFilename)
			: this(m2mUrl, GridNetUtils.LoadCertificate(certificateFilename)) { }

		public OneM2MConnection(Uri m2mUrl, X509Certificate? certificate = null)
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
			var loggingHandler = new DebugMessageHandler(handler);
			_pnClient = new HttpClient(loggingHandler);
#else
			_pnClient = new HttpClient(handler);
#endif

			_pnClient.DefaultRequestHeaders.Add("Accept", contentType);
		}

		public async Task<AE?> FindApplication(string inCse, string appId)
		{
			var response = await GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				From = inCse,
				To = inCse,
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.AE },
					Attribute = OneM2MConnection.GetAttributes<AE>(_ => _.App_ID == appId),
				}
			});

			var aeUrl = response.URIList?.FirstOrDefault();
			if (aeUrl == null)
				return null;

			var response2 = await GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				From = inCse,
				To = aeUrl
			});

			return response2.AE;
		}

		public async Task<AE?> RegisterApplication(Application.IConfig appConfig, string inCse)
		{
			var response = await GetResponseAsync(new RequestPrimitive
			{
				From = appConfig.CredentialId,
				To = inCse,
				Operation = Operation.Create,
				ResourceType = ResourceType.AE,
				ResultContent = ResultContent.Attributes,
				PrimitiveContent = new PrimitiveContent
				{
					AE = new AE
					{
						App_ID = appConfig.AppId,
						AppName = appConfig.AppName,
						PointOfAccess = appConfig.PoaUrl == null ? null : new[] { appConfig.PoaUrl.ToString() }
					}
				}
			});

			return response?.AE;
		}

		public async Task<ResponseContent?> GetPrimitiveAsync(string key, FilterCriteria? filterCriteria = null) =>
			await GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = key,
				FilterCriteria = filterCriteria
			});

		public async Task<ResponseContent> GetResponseAsync(RequestPrimitive body)
		{
			using (var request = GetRequest(body))
				return await GetResponseAsync(request);
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

		public async Task<ResponseContent> GetResponseAsync(HttpRequestMessage request)
		{
			using (var response = await _pnClient.SendAsync(request))
				return await response.DeserializeAsync<ResponseContent>() ??
					throw new InvalidDataException("The returned response did not match type 'ResponseContent'");
		}

		public static T? DeserializeJson<T>(string str)
			where T : class => JsonConvert.DeserializeObject<T>(str, OneM2MConnection.JsonSettings);


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

			HttpMethod method;
			switch (body.Operation)
			{
				case Operation.Create:
				case Operation.Notify:
				default:
					method = HttpMethod.Post;
					break;
				case Operation.Retrieve:
					method = HttpMethod.Get;
					break;
				case Operation.Update:
					method = HttpMethod.Put;
					break;
				case Operation.Delete:
					method = HttpMethod.Delete;
					break;
			}

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
						if (attr.Value != null)
							args.Add(attr.Name, attr.Value.ToString());
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

			var contentTypeHeader = new MediaTypeHeaderValue(contentType);

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

		public static ICollection<GridNet.OneM2M.Types.Attribute> GetAttributes<T>(params Expression<Func<T, object>>[] expressions) =>
			expressions.Select(expr =>
			{
				var body = expr.Body;
				if (body.NodeType == ExpressionType.Convert)
					body = ((UnaryExpression) body).Operand;  // throws

				var equalityExpr = (BinaryExpression) body;
				var left = (MemberExpression) equalityExpr.Left;

				var member = left.Member;
				var memberName = member.Name;
				var jsonPropertyAttribute = member.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault() as JsonPropertyAttribute;
				if (jsonPropertyAttribute?.PropertyName != null)
					memberName = jsonPropertyAttribute.PropertyName;

				var right = equalityExpr.Right;
				var rightLambda = Expression.Lambda(right);
				var compiledExpression = rightLambda.Compile();
				var result = compiledExpression.DynamicInvoke();

				return new GridNet.OneM2M.Types.Attribute
				{
					Name = memberName,
					Value = result,
				};
			}).ToList();

		readonly Subject<Notification> _notifications = new Subject<Notification>();
		public IObservable<Notification> Notifications => _notifications;

		public async Task HandleNotificationAsync(HttpRequest req)
		{
			var requestPrimitive = await ParseNotificationAsync(req);
			if (requestPrimitive != null)
				_notifications.OnNext(requestPrimitive);
		}

		async Task<Notification?> ParseNotificationAsync(HttpRequest req)
		{
			using (var bodyStream = new StreamReader(req.Body, true))
			{
				var body = await bodyStream.ReadToEndAsync();
				return ParseNotification(body, req.Headers, req.Query);
			}
		}


		Notification? ParseNotification(string body, IHeaderDictionary headers, IQueryCollection query)
		{
			var notificationContent = DeserializeJson<NotificationContent>(body);
			if (notificationContent == null)
				return null;

			var notification = notificationContent.Notification;
			if (notification == null)
				return null;

			var serializer = JsonSerializer.CreateDefault(OneM2MConnection.JsonSettings);
			var representation = ((Newtonsoft.Json.Linq.JObject) notification.NotificationEvent.Representation).ToObject<PrimitiveContent>(serializer);

			var request = notification.NotificationEvent.PrimitiveRepresentation = new RequestPrimitive
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
					request.ResponseType = new ResponseTypeInfo
					{
						ResponseTypeValue = responseType.FirstOrDefault()?.ParseNullableEnum<ResponseType>(),
						NotificationURI = notificationURI.Join("&")?.Split('&')?.ToArray(),
					};
				}

				FilterCriteria? fc = null;
				FilterCriteria FC() => fc ?? (fc = new FilterCriteria());

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

				if (query["cfq"].Any())
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

				request.FilterCriteria = fc;
			}

			return notification;
		}

		public static string SerializeJson(object obj) => JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented, JsonSettings);
	}


	public static class ApiExtensions
	{
		public static T? GetContent<T>(this ContentInstance @this)
			where T : class
		{
			var json = @this.Content as JObject;
			return json?.ToObject<T>(OneM2MConnection.Serializer);
		}

		public static async Task<T> DeserializeAsync<T>(this HttpResponseMessage response)
			where T : class, new()
		{
			var content = response.Content;
			var body = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
				throw new HttpStatusException(response.StatusCode, response.ReasonPhrase);

			response.EnsureSuccessStatusCode();

			if (string.IsNullOrWhiteSpace(body))
				throw new InvalidDataException("An empty response was returned");

			return OneM2MConnection.DeserializeJson<T>(body)
				?? throw new InvalidDataException($"The response did not match Type '{typeof(T).Name}'"); ;
		}

		public static async Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient @this, Uri uri, T body)
			where T : class
		{
			var bodyJson = OneM2MConnection.SerializeJson(body);
			var requestBody = new StringContent(bodyJson, Encoding.UTF8, "application/json");

			return await @this.PostAsync(uri, requestBody);
		}

		public static string ComputeHash(this HashAlgorithm @this, string str, Encoding? encoding = null)
		{
			var rgbClear = (encoding ?? Encoding.UTF8).GetBytes(str);	// TODO: BOM?
			var rgbHash = @this.ComputeHash(rgbClear);
			return Convert.ToBase64String(rgbHash);
		}
	}
}
