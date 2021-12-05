using Aetheros.Schema.OneM2M;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using CoAP.Net;
using CoAP;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Aetheros.OneM2M.Api
{
    public class CoapConnection<TPrimitiveContent> : Connection<TPrimitiveContent>
		where TPrimitiveContent : PrimitiveContent, new()
	{
		readonly Uri _iotApiUrl;
        readonly CoAP.CoapClient _pnClient;

		public X509Certificate? ClientCertificate { get; }

		public CoapConnection(Connection.IConnectionConfiguration config)
			: this(config.M2MUrl, config.CertificateFilename) { }

		public CoapConnection(Uri m2mUrl, string? certificateFilename)
			: this(m2mUrl, AosUtils.LoadCertificate(certificateFilename)) { }

		public CoapConnection(Uri m2mUrl, X509Certificate? certificate = null)
		{
			_iotApiUrl = m2mUrl;

			// TODO: certificate

			_pnClient = new CoapClient(m2mUrl);
			_pnClient.Timeout = 300 * 1000;
		}

		public async Task<ResponseContent<TPrimitiveContent>> GetResponseAsync(CoAP.Request request) => await GetResponseAsync<ResponseContent<TPrimitiveContent>>(request);

		public override async Task<T> GetResponseAsync<T>(RequestPrimitive<TPrimitiveContent> body)
		{
			var request = GetRequest(body);
			return await GetResponseAsync<T>(request);
		}

		public async Task<T> GetResponseAsync<T>(CoAP.Request request)
			where T : class, new()
		{
      Trace.WriteLine("\n>>>>>>>>>>>>>>>>");
			Trace.WriteLine($"{request.CodeString} {request.URI}");
			foreach (var option in request.GetOptions())
				Trace.WriteLine($"{option.Name}: ({option.Type}) {option.Value ?? option.StringValue}");
			if (request.PayloadSize > 0)
				Trace.WriteLine(request.PayloadString);
			Trace.WriteLine("");

			var response = await request.SendAsync(request, _pnClient.EndPoint);
			return await response.DeserializeAsync<T>() ??
				throw new InvalidDataException("The returned response did not match type 'ResponseContent'");
		}

		enum ContentFormats
		{
			Xml = 41,
			Json = 50,
			Cbor = 60,
		}


		enum OneM2mRequestOptions
		{
			FR = 256,
			RQI = 257,
			OT = 259,
			RQET = 260,
			RSET = 261,
			OET = 262,
			RTURI = 263,
			EC = 264,
			RSC = 265,
			GID = 266,
			TY = 267,
			CTO = 268,
			CTS = 269,
			ATI = 270,
			RVI = 271,
			VSI = 272,
			GTM = 273,
			AUS = 274,
			ASRI = 275,
			OMR = 276,
		}


		internal CoAP.Request GetRequest(RequestPrimitive<TPrimitiveContent> body)
		{
			var args = GetRequestParameters(body);

			var fc = body.FilterCriteria;
			if (fc != null)
			{
				if (fc.Attribute != null)
				{
					foreach (var attr in fc.Attribute)
					{
						if (attr.Value != null)
							args.Add("atr", $"{attr.Name},{attr.Value.ToString()}");
					}
				}
			}

			var method = body.Operation switch
			{
				Operation.Retrieve => CoAP.Method.GET,
				Operation.Update => CoAP.Method.PUT,
				Operation.Delete => CoAP.Method.DELETE,
				_ => CoAP.Method.POST,
			};

			var request = new CoAP.Request(method);
			request.AckTimeout = 10000 * 1000;

			request.URI = _pnClient.Uri;
			var to = body.To;
			var pathParts = to.Split("/", StringSplitOptions.RemoveEmptyEntries);
			
			if (to.StartsWith("//"))
				request.AddUriPath("_");
			else if (to.StartsWith("/"))
				request.AddUriPath("~");

			foreach (var pathPart in pathParts)
				request.AddUriPath(pathPart);

			foreach (var query in args.AllKeys.SelectMany(args.GetValues, (k, v) => $"{k}={/*Uri.EscapeDataString*/(v)}"))
				request.AddUriQuery(query);

			if (body.ResourceType != null)
				request.AddOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.TY, (int) body.ResourceType));

			if (body.PrimitiveContent != null)
			{
				var bodyJson = SerializeJson(body.PrimitiveContent);
				request.SetPayload(bodyJson, (int) ContentFormats.Json);
			}

			if (body.From != null)
				request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.FR, body.From));

			request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.RQI, body.RequestIdentifier ?? NextRequestId));

			if (body.GroupRequestIdentifier != null)
				request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.GID, body.GroupRequestIdentifier));

			if (body.OriginatingTimestamp != null)
				request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.OT, body.OriginatingTimestamp.Value.ToString(_dateTimeFormat)));

			if (body.ResultExpirationTimestamp != null)
				request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.RSET, body.ResultExpirationTimestamp));

			if (body.RequestExpirationTimestamp != null)
				request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.RQET, body.RequestExpirationTimestamp));

			if (body.OperationExecutionTime != null)
				request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.OET, body.OperationExecutionTime));

			if (body.EventCategory != null)
				request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.EC, body.EventCategory));

			if (body.ResponseType?.NotificationURI != null)
				request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.RTURI, string.Join("&", body.ResponseType.NotificationURI)));

			return request;
		}


		class NotifyResource : CoAP.Server.Resources.Resource
		{
            readonly Func<CoAP.Server.Resources.CoapExchange, Task> _postHandler;
			
			public NotifyResource(string name, Func<CoAP.Server.Resources.CoapExchange, Task> postHandler) : base(name) 
			{
				_postHandler = postHandler;
			}

			protected override void DoPost(CoAP.Server.Resources.CoapExchange exchange)
			{
				_postHandler(exchange).Wait();
			}
		}

		public CoAP.Server.Resources.Resource CreateNotificationResource(string name = "notify") =>
			new NotifyResource(name, this.HandleNotificationAsync);


		public async Task HandleNotificationAsync(CoAP.Server.Resources.CoapExchange exchange)
		{
			var request = exchange.Request;
			var body = request.PayloadString;

			Trace.WriteLine("\n!!!!!!!!!!!!!!!!");
			foreach (var option in request.GetOptions())
				Trace.WriteLine($"{option.Name}: ({option.Type}) {option.Value}");

			Trace.WriteLine("");
			if (body != null)
			{
				try
				{
					Trace.WriteLine(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(body), Formatting.Indented));
				}
				catch
				{
					Trace.WriteLine(body);
				}
			}

			var notification = ParseNotification(request);
			if (notification != null)
			{
				_notifications.OnNext(notification);

				var response = Response.CreateResponse(request, StatusCode.Content);
				/*
				foreach(var uri in notification.NotificationEvent.PrimitiveRepresentation.ResponseType?.NotificationURI ?? Array.Empty<string>())
					response.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.RTURI, uri));

				*/
				//response.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.RQI, notification.RequestIdentifier));
				exchange.Respond(response);
			}
		}

		Notification<TPrimitiveContent>? ParseNotification(CoAP.Request request)
		{
			var body = request.PayloadString;

			var notification = DeserializeJson<NotificationContent<TPrimitiveContent>>(body)?.Notification;
			if (notification == null)
			{
				Debug.WriteLine($"{nameof(ParseNotification)}: Invalid JSON");
				return null;
			}

			var serializer = JsonSerializer.CreateDefault(Connection.JsonSettings);
			var representation = ((Newtonsoft.Json.Linq.JObject) notification.NotificationEvent.Representation).ToObject<TPrimitiveContent>(serializer);
			if (representation == null)
			{
				Debug.WriteLine($"{nameof(ParseNotification)}: Invalid representation");
				return null;
			}
			notification.NotificationEvent.PrimitiveRepresentation = representation;

			/*
			var notificationPrimitive = notification.NotificationEvent.PrimitiveRepresentation = new TPrimitiveContent//new RequestPrimitive<TPrimitiveContent>
			{
				From = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.FR)?.StringValue,
				RequestIdentifier = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.RQI)?.StringValue,
				//GroupRequestIdentifier = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.GID)?.StringValue,
				OriginatingTimestamp = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.OT)?.Value as DateTimeOffset?,
				ResultExpirationTimestamp = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.RSET)?.StringValue,
				//RequestExpirationTimestamp = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.RQET)?.StringValue,
				//OperationExecutionTime = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.OET)?.StringValue,
				EventCategory = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.EC)?.StringValue,
		
				PrimitiveContent = representation
			};
			*/

#if false
			var optionNotificationUrl = request.GetFirstOption((CoAP.OptionType) OneM2mRequestOptions.RTURI)?.StringValue;
			if (!string.IsNullOrEmpty(optionNotificationUrl))
			{
				requestPrimitive.ResponseType = new ResponseTypeInfo
				{
					NotificationURI = optionNotificationUrl.Split('&'),
					//ResponseTypeValue = 
				};
			}
#endif

			//request.SetOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.RTURI, string.Join("&", body.ResponseType.NotificationURI)));

			return notification;
		}
	}

	public class CoapRequestException : Exception
	{
		public int StatusCode { get; }

		public CoapRequestException(int statusCode) : base (CoAP.Code.ToString(statusCode))
		{
			StatusCode = statusCode;
		}
	}

	public static class CoapExtensions
	{
		public static Task<CoAP.Response> SendTaskAsync(this CoapClient @this, CoAP.Request request)
		{
			var tcs = new TaskCompletionSource<CoAP.Response>();

			@this.SendAsync(request,
				response =>
				{
					tcs.SetResult(response);
				},
				failReason =>
				{
					tcs.SetException(new CoapRequestException(failReason == CoapClient.FailReason.Rejected ? 128 : 164));
				}
			);

			return tcs.Task;
		}

		public static Task<CoAP.Response> SendAsync(this CoAP.Request @this, CoAP.Request request, IEndPoint endPoint)
		{
			var tcs = new TaskCompletionSource<CoAP.Response>();

			request.Respond += (o, e) => tcs.SetResult(e.Response);
			request.Rejected += (o, e) => tcs.SetException(new CoapRequestException(128));
			request.TimedOut += (o, e) => tcs.SetException(new CoapRequestException(164));
			request.Send(endPoint ?? EndPointManager.Default);

			return tcs.Task;
		}

		public static async Task<T> DeserializeAsync<T>(this CoAP.Response response)
			where T : class, new()
		{
			if (!Code.IsSuccess(response.Code))
				throw new CoapRequestException(response.Code);

			var body = response.ResponseText;
			if (string.IsNullOrWhiteSpace(body))
				throw new InvalidDataException("An empty response was returned");

			return Connection.DeserializeJson<T>(body)
				?? throw new InvalidDataException($"The response did not match Type '{typeof(T).Name}'");
		}
	}

	public class CoapConnection : CoapConnection<PrimitiveContent>
	{
		public CoapConnection(Connection.IConnectionConfiguration config) : base(config) {}
		public CoapConnection(Uri m2mUrl, string certificateFilename) : base(m2mUrl, certificateFilename) {}
		public CoapConnection(Uri m2mUrl, X509Certificate? certificate = null) : base(m2mUrl, certificate) {}
	}
}
