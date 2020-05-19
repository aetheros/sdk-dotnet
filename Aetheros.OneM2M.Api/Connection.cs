using Aetheros.OneM2M.Binding;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aetheros.OneM2M.Api
{
	public abstract class Connection
	{
		public interface IConfig
		{
			public Uri M2MUrl { get; }
			public string CertificateFilename { get; }
		}

		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		public const string OneM2MResponseContentTYpe = "application/vnd.onem2m-res+json";
		//[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		private protected const string _dateTimeFormat = "yyyyMMddTHHmmss.FFFFF";

		static int _nextRequestId;
		readonly string _requestGuid = Guid.NewGuid().ToString("N");
		public string NextRequestId => $"{_requestGuid}/{Interlocked.Increment(ref _nextRequestId)}";

		static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore,
		};

		internal static JsonSerializer Serializer { get; }

		static Connection()
		{
			JsonSettings.Converters.Add(new Newtonsoft.Json.Converters.IsoDateTimeConverter
			{
				DateTimeStyles = DateTimeStyles.AssumeUniversal,
				DateTimeFormat = _dateTimeFormat
			});
			JsonSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());

			Serializer = JsonSerializer.CreateDefault(JsonSettings);
		}

		public async Task<AE?> FindApplicationAsync(string inCse, string appId)
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
					Attribute = Connection.GetAttributes<AE>(_ => _.App_ID == appId),
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

		public async Task<AE?> RegisterApplicationAsync(Application.IConfig appConfig)
		{
			var response = await GetResponseAsync(new RequestPrimitive
			{
				From = appConfig.CredentialId,
				To = appConfig.UrlPrefix,
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

		public abstract Task<ResponseContent> GetResponseAsync(RequestPrimitive body);

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

		public static T? DeserializeJson<T>(string str)
			where T : class => JsonConvert.DeserializeObject<T>(str, Connection.JsonSettings);

		public static ICollection<Aetheros.OneM2M.Binding.Attribute> GetAttributes<T>(params Expression<Func<T, object>>[] expressions) =>
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

				return new Aetheros.OneM2M.Binding.Attribute
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

			var serializer = JsonSerializer.CreateDefault(Connection.JsonSettings);
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

				request.FilterCriteria = fc;
			}

			return notification;
		}

		public static string SerializeJson(object obj) => JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented, JsonSettings);
	}

	public static class ApiExtensions
	{
		/// <summary>
		/// Deserialize the <see cref="ContentInstance.Content"/> of a <see cref="ContentInstance"/>.
		/// </summary>
		/// <typeparam name="T">The Type of the ContentInstance's Content</typeparam>
		/// <param name="this">The ContentInstance</param>
		/// <returns>The content of the ContentInstance</returns>
		public static T? GetContent<T>(this ContentInstance @this)
			where T : class
		{
			var json = @this.Content as JObject;
			return json?.ToObject<T>(Connection.Serializer);
		}

		internal static async Task<T> DeserializeAsync<T>(this HttpResponseMessage response)
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

		internal static async Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient @this, Uri uri, T body)
			where T : class
		{
			var bodyJson = Connection.SerializeJson(body);
			var requestBody = new StringContent(bodyJson, Encoding.UTF8, "application/json");

			return await @this.PostAsync(uri, requestBody);
		}

		internal static string ComputeHash(this HashAlgorithm @this, string str, Encoding? encoding = null)
		{
			var rgbClear = (encoding ?? Encoding.UTF8).GetBytes(str);   // TODO: BOM?
			var rgbHash = @this.ComputeHash(rgbClear);
			return Convert.ToBase64String(rgbHash);
		}
	}
}
