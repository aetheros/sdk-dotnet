using Aetheros.OneM2M.Binding;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using CoAP.Net;
using CoAP;
using CoAP.Util;
using System.Net;

namespace Aetheros.OneM2M.Api
{
	public class CoapConnection : Connection
	{
		readonly Uri _iotApiUrl;
		CoAP.CoapClient _pnClient;

		public X509Certificate? ClientCertificate { get; }

		public CoapConnection(IConnectionConfiguration config)
			: this(config.M2MUrl, config.CertificateFilename) { }

		public CoapConnection(Uri m2mUrl, string certificateFilename)
			: this(m2mUrl, AosUtils.LoadCertificate(certificateFilename)) { }

		public CoapConnection(Uri m2mUrl, X509Certificate? certificate = null)
		{
			_iotApiUrl = m2mUrl;

#if false
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
#endif

			_pnClient = new CoapClient(m2mUrl);
			_pnClient.Timeout = 300 * 1000;
		}

		public async Task<ResponseContent> GetResponseAsync(CoAP.Request request)
		{
			//var response = await _pnClient.SendTaskAsync(request);
			var response = await request.SendAsync(request, _pnClient.EndPoint);
			var responseContent = await response.DeserializeAsync<ResponseContent>() ??
				throw new InvalidDataException("The returned response did not match type 'ResponseContent'");

			// TODO:
			//responseContent.ResponseStatusCode = response.StatusCode;
			return responseContent;
		}

		public override async Task<T> GetResponseAsync<T>(RequestPrimitive body)
		{
			var request = GetRequest(body);
			return await GetResponseAsync<T>(request);
		}

		public async Task<T> GetResponseAsync<T>(CoAP.Request request)
			where T : class, new()
		{
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


		internal CoAP.Request GetRequest(RequestPrimitive body)
		{
			var args = GetRequestParameters(body);

			var method = body.Operation switch
			{
				Operation.Retrieve => CoAP.Method.GET,
				Operation.Update => CoAP.Method.PUT,
				Operation.Delete => CoAP.Method.DELETE,
				_ => CoAP.Method.POST,
			};

			var request = new CoAP.Request(method);
			request.AckTimeout = 10000 * 1000;
			//request.URI = urlBuilder.Uri;

			//request.AddUriPath(body.To);
			request.URI = _pnClient.Uri;
			foreach (var pathPart in body.To.Split("/", StringSplitOptions.RemoveEmptyEntries))
				if (pathPart != ".")
					request.AddUriPath(pathPart);

			foreach (var query in args.AllKeys.SelectMany(args.GetValues, (k, v) => $"{k}={Uri.EscapeDataString(v)}"))
				request.AddUriQuery(query);

			//if (method == HttpMethod.Post || method == HttpMethod.Put)
			{
				if (body.ResourceType != null)
					request.AddOption(Option.Create((CoAP.OptionType) OneM2mRequestOptions.TY, (int) body.ResourceType));

				if (body.PrimitiveContent != null)
				{
					var bodyJson = SerializeJson(body.PrimitiveContent);
					request.SetPayload(bodyJson, (int) ContentFormats.Json);
				}
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
}
