using Aetheros.Schema.OneM2M;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/*
cp Aetheros.Schema.OneM2M.cs Aetheros.Schema.OneM2M.cs.new
for i in AggregatedRequest AggregatedRequestRequest AggregatedResponse CSEBase Delivery OperationResult PrimitiveContent Request RequestPrimitive<TPrimitiveContent> ResponseContent ResponsePrimitive
do

	sed -i -E -e 's#(\spublic [A-Za-z0-9_.]*$i)\b#\1<TPrimitiveContent>#' Aetheros.Schema.OneM2M.cs.new
	sed -i -E -e 's#( : [A-Za-z0-9_.]*$i)#<TPrimitiveContent> : TPrimitiveContent where TPrimitiveContent : PrimitiveContent#' Aetheros.Schema.OneM2M.cs.new

done

*/
namespace Aetheros.OneM2M.Api
{
	public abstract class Connection : IDisposable
	{
		public virtual void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
		}

		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		public const string OneM2MResponseContentType = "application/vnd.onem2m-res+json";

		//[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]

		private protected const string _dateTimeFormat = "yyyyMMddTHHmmss.FFFFF";

		public abstract bool IsSecure { get; }

		public interface IConnectionConfiguration
		{
			Uri M2MUrl { get; }
			string? CertificateFilename { get; }
		}

		public class ConnectionConfiguration : IConnectionConfiguration
		{
			public required Uri M2MUrl { get; init; }
			public string? CertificateFilename { get; set; }
		}


		class TimeSpanMillisecondsConverter : JsonConverter<TimeSpan>
		{
			public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
			{
				writer.WriteValue((long)value.TotalMilliseconds);
			}

			public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue, bool hasExistingValue, JsonSerializer serializer)
			{
				return TimeSpan.FromMilliseconds((long)reader.Value);
			}
		}


		protected static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			DateParseHandling = DateParseHandling.DateTimeOffset,
			DateFormatString = 	"yyyyMMdd'T'HHmmss.FFFFFFFK",
			DefaultValueHandling = DefaultValueHandling.Ignore,
			Converters = { new TimeSpanMillisecondsConverter() }
		};

		internal static JsonSerializer Serializer { get; }

		static Connection()
		{
			JsonSettings.Converters.Add(new Newtonsoft.Json.Converters.IsoDateTimeConverter
			{
				DateTimeStyles = DateTimeStyles.AssumeUniversal,
				DateTimeFormat = _dateTimeFormat
			});
			//JsonSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());

			Serializer = JsonSerializer.CreateDefault(JsonSettings);
		}


		
		public static T? DeserializeJson<T>(string str)
			where T : class => JsonConvert.DeserializeObject<T>(str, Connection.JsonSettings);

		public static T? DeserializeJson<T>(JToken token)
			where T : class => token.ToObject<T>(Serializer);

		public static string SerializeJson(object obj) => JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented, JsonSettings);


		public static ICollection<Aetheros.Schema.OneM2M.Attribute> GetAttributes<T>(params Expression<Func<T, object>>[] expressions) =>
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

				return new Aetheros.Schema.OneM2M.Attribute
				{
					Name = memberName,
					Value = result,
				};
			}).ToList();


	}

	public class OneM2MException : Exception
	{
		public OneM2MException(ResponseStatusCode responseStatusCode)
		: base(responseStatusCode.ToString())
		{
			this.StatusCode = responseStatusCode;
		}
		public OneM2MException(ResponseStatusCode responseStatusCode, string message)
		: base(message)
		{
			this.StatusCode = responseStatusCode;
		}
		public OneM2MException(ResponseStatusCode responseStatusCode, string message, Exception innerException)
		: base(message, innerException)
		{
			this.StatusCode = responseStatusCode;
		}
		protected OneM2MException(SerializationInfo info, StreamingContext context) : base(info, context) { }

		public ResponseStatusCode StatusCode { get; set; }
	}

	public abstract class Connection<TPrimitiveContent> : Connection
		where TPrimitiveContent : PrimitiveContent, new()
{
	static int _nextRequestId;
	readonly string _requestGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
	public string NextRequestId => $"{_requestGuid}/{Interlocked.Increment(ref _nextRequestId)}";

	public async Task<AE?> FindApplicationAsync(string aeId) => (await GetPrimitiveAsync(aeId, aeId)).AE;

	public async Task<AE> RegisterApplicationAsync(ApplicationConfiguration appConfig)
	{
		var response = await GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
		{
			From = appConfig.CredentialId,
			To = appConfig.CseId,
			Operation = Operation.Create,
			ResourceType = ResourceType.AE,
			ResultContent = ResultContent.Attributes,
			PrimitiveContent = new TPrimitiveContent
			{
				AE = new AE
				{
					App_ID = appConfig.AppId,
					AppName = appConfig.AppName,
					ResourceName = appConfig.AppName,
					PointOfAccess = appConfig.PoaUrl == null ? null : new[] { appConfig.PoaUrl.ToString() }
				}
			}
		});

		return response.AE;
	}

	public async Task<ResponseContent<TPrimitiveContent>> GetPrimitiveAsync(string from, string to, FilterCriteria? filterCriteria = null) =>
		await GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
		{
			Operation = Operation.Retrieve,
			From = from,
			To = to,
			FilterCriteria = filterCriteria
		});


	public async Task<ResponseContent<TPrimitiveContent>?> TryGetPrimitiveAsync(string from, string to, FilterCriteria? filterCriteria = null)
	{
		try
		{
			return await GetPrimitiveAsync(from, to, filterCriteria);
		}
		catch (OneM2MException e) when (e.StatusCode == ResponseStatusCode.NotFound)
		{
			return null;
		}
	}

	public async Task<ResponseContent<TPrimitiveContent>> GetChildResourcesAsync(string from, string to, FilterCriteria? filterCriteria = null) =>
		await GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
		{
			Operation = Operation.Retrieve,
			From = from,
			To = to,
			ResultContent = ResultContent.ChildResources,
			FilterCriteria = filterCriteria
		});

	public async Task<ResponseContent<TPrimitiveContent>> GetResponseAsync(RequestPrimitive<TPrimitiveContent> body) => await GetResponseAsync<ResponseContent<TPrimitiveContent>>(body);

	public abstract Task<T> GetResponseAsync<T>(RequestPrimitive<TPrimitiveContent> body) where T : class, new();

	protected static NameValueCollection GetRequestParameters(RequestPrimitive<TPrimitiveContent> body)
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

			if (fc.ResourceType != null)
				args.AddRange("ty", fc.ResourceType.Select(ty => ty.ToString("d")));

			if (fc.SemanticsFilter != null)
				args.AddRange("smf", fc.SemanticsFilter);

			if (fc.Labels != null)
				args.AddRange("lbl", fc.Labels);

			if (fc.ContentType != null)
				args.AddRange("cty", fc.ContentType);
		}

		return args;
	}

	// TODO: make this connection-type-agnostic
	protected readonly Subject<Notification<TPrimitiveContent>> _notifications = new Subject<Notification<TPrimitiveContent>>();
	public IObservable<Notification<TPrimitiveContent>> Notifications => _notifications;


	public static IEnumerable<Notification<TPrimitiveContent>> ParseNotifications(string body)
	{
		var serializer = JsonSerializer.CreateDefault(Connection.JsonSettings);
		void FixNotification(Notification<TPrimitiveContent> notification)
		{
			var representation = ((Newtonsoft.Json.Linq.JObject)notification.NotificationEvent.Representation).ToObject<TPrimitiveContent>(serializer);
			if (representation != null)
				notification.NotificationEvent.PrimitiveRepresentation = representation;
		}

		var aggregatedNotificationContent = Connection.DeserializeJson<AggregatedNotificationContent<TPrimitiveContent>>(body);
		if (aggregatedNotificationContent != null && aggregatedNotificationContent.AggregatedNotification != null)
		{
			foreach (var notification in aggregatedNotificationContent.AggregatedNotification.Notification)
			{
				FixNotification(notification);
				yield return notification;
			}
		}
		else
		{
			var notificationContent = Connection.DeserializeJson<NotificationContent<TPrimitiveContent>>(body);
			if (notificationContent == null)
			{
				Debug.WriteLine($"{nameof(ParseNotifications)}: invalid json");
				yield break;
			}

			var notification = notificationContent.Notification;
			if (notification == null)
			{
				Debug.WriteLine($"{nameof(ParseNotifications)}: missing notification");
				yield break;
			}

			yield return notification;
		}
	}
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
			var json = @this.Content as JToken;
			return json?.ToObject<T>(Connection.Serializer);
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

		public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> enumerable) where T : class
		{
				return enumerable.Where(e => e != null).Select(e => e!);
		}
		public static IObservable<T> WhereNotNull<T>(this IObservable<T?> enumerable) where T : class
		{
				return enumerable.Where(e => e != null).Select(e => e!);
		}
	}
}
