using Aetheros.OneM2M.Api.Registration;
using Aetheros.Schema.OneM2M;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Aetheros.OneM2M.Api
{
	public interface IApplicationConfiguration
	{
		string? AppId { get; }
		string? AppName { get; }
		string? CredentialId { get; }
		Uri? PoaUrl { get; }
		string? UrlPrefix { get; }
	}

	public class Application<TPrimitiveContent>
		where TPrimitiveContent : PrimitiveContent, new()
	{
		public Connection<TPrimitiveContent> Connection { get; }
		public string AppId { get; }
		public string AeId { get; }
		public Uri? PoaUrl { get; set; }
		public string UrlPrefix { get; set; }

		public Application(Connection<TPrimitiveContent> con, string appId, string aeId, string urlPrefix, Uri? poaUrl = null)
		{
			Connection = con;
			AppId = appId;
			AeId = aeId;
			PoaUrl = poaUrl;
			UrlPrefix = urlPrefix;
		}

		
		public Application(Connection<TPrimitiveContent> con, string aeId, IApplicationConfiguration config)
		{
			Connection = con;
			AppId = config.AppId ?? throw new ArgumentNullException("config.AppId");
			AeId = aeId;
			PoaUrl = config.PoaUrl;
			UrlPrefix = config.UrlPrefix ?? throw new ArgumentNullException("config.UrlPrefix");
		}

		
		

		public string ResourceKey(string key) => $"{AeId}/{key}";


		public async Task<T> GetResponseAsync<T>(RequestPrimitive<TPrimitiveContent> body)
			where T : class, new()
		{
			if (body.To == null)
				body.To = AeId;
			else if (!body.To.StartsWith("/"))
				body.To = $"{AeId}/{body.To}";

			if (body.From == null)
				body.From = AeId;
			return await Connection.GetResponseAsync<T>(body);
		}

		public async Task<ResponseContent<TPrimitiveContent>> GetResponseAsync(RequestPrimitive<TPrimitiveContent> body) => await GetResponseAsync<ResponseContent<TPrimitiveContent>>(body);

		public async Task<T> GetChildResourcesAsync<T>(string key, FilterCriteria? filterCriteria = null)
			where T : class, new() =>
			await GetResponseAsync<T>(new RequestPrimitive<TPrimitiveContent>
			{
				Operation = Operation.Retrieve,
				To = key,
				ResultContent = ResultContent.ChildResources,
				FilterCriteria = filterCriteria
			});

		public async Task<ResponseContent<TPrimitiveContent>> GetPrimitiveAsync(
			string key,
			FilterCriteria? filterCriteria = null,
			ResultContent? resultContent = null,
			DiscResType? discoveryResultType = null
		) =>
			await GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
			{
				From = this.AeId,
				To = key,
				ResultContent = resultContent,
				Operation = Operation.Retrieve,
				FilterCriteria = filterCriteria,
				DiscoveryResultType = discoveryResultType,
			});

		public async Task<ResponseContent<TPrimitiveContent>> CreateResourceAsync(string url, ResourceType resourceType, Func<TPrimitiveContent, TPrimitiveContent> setter, ResultContent? resultContent = null) =>
			await GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
			{
				From = this.AeId,
				To = url,
				Operation = Operation.Create,
				ResourceType = resourceType,
				ResultContent = resultContent,
				PrimitiveContent = setter(new TPrimitiveContent())
			});

		public async Task<ResponseContent<TPrimitiveContent>> UpdateResourceAsync(string url, Func<TPrimitiveContent, TPrimitiveContent> setter) =>
			await GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
			{
				From = this.AeId,
				To = url,
				Operation = Operation.Update,
				PrimitiveContent = setter(new TPrimitiveContent())
			});

		/*
		public async Task<T> GetPrimitiveAsync<T>(string key, Func<TPrimitiveContent, T> selector, FilterCriteria? filterCriteria = null)
			=> selector(await GetPrimitiveAsync(key, filterCriteria));
		*/


		public async Task DeleteAsync(params string[] urls) => await DeleteAsync((IEnumerable<string>) urls);

		public async Task DeleteAsync(IEnumerable<string> urls)
		{
			foreach (var url in urls)
			{
				try
				{
					await GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
					{
						Operation = Operation.Delete,
						To = url,
					});
				}
				catch (Connection.HttpStatusException e) when (e.StatusCode == HttpStatusCode.NotFound) { }
				catch (CoapRequestException e) when (e.StatusCode == 132) { }
			}
		}

		public async Task<ContentInstance> AddContentInstanceAsync(string key, object content) => await AddContentInstanceAsync(key, null, content);

		public async Task<ContentInstance> AddContentInstanceAsync(string key, string? resourceName, object content) =>
			(await this.GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
			{
				To = key,
				Operation = Operation.Create,
				ResourceType = ResourceType.ContentInstance,
				PrimitiveContent = new TPrimitiveContent
				{
					ContentInstance = new ContentInstance
					{
						ResourceName = resourceName,
						Content = content
					}
				}
			})).ContentInstance;

		public async Task<Container?> EnsureContainerAsync(string name /*, bool clientAppContainer = false*/)
		{
			if (name == "." || name == "/")
				return null;

			try
			{
				return (await GetPrimitiveAsync(name)).Container;
			}
			catch (Connection.HttpStatusException e) when (e.StatusCode == HttpStatusCode.NotFound) { }
			catch (CoapRequestException e) when (e.StatusCode == 132) { }

			string? parentName = null;

			int ichLast = name.LastIndexOf('/');
			if (ichLast > 0)
			{
				parentName = name.Substring(0, ichLast);
				name = name.Substring(ichLast + 1);
				if (!string.IsNullOrWhiteSpace(parentName) && parentName != ".")
					await EnsureContainerAsync(parentName);
			}

			var container = (await GetResponseAsync(new RequestPrimitive<TPrimitiveContent>
			{
				Operation = Operation.Create,
				To = parentName,
				ResourceType = ResourceType.Container,
				PrimitiveContent = new TPrimitiveContent
				{
					Container = new Container
					{
						ResourceName = name
					}
				}
			})).Container;

			return container;
		}

		public async Task<T?> GetLatestContentInstanceAsync<T>(string containerKey)
			where T : class
		{
			try
			{
				var response = (await GetPrimitiveAsync(containerKey, new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.ContentInstance },
				}));
				var ciRefs = response.URIList;

				if (ciRefs == null || !ciRefs.Any())
					return null;

				var rcs = ciRefs
					.ToAsyncEnumerable()
					.SelectAwait(async url => await GetPrimitiveAsync(url))
					.OrderBy(s => s.ContentInstance?.CreationTime);

				return (await rcs
					.Select(rc => rc.ContentInstance?.GetContent<T>())
					.ToListAsync()).LastOrDefault();
			}
			catch (Connection.HttpStatusException e) when (e.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}
		}

		readonly ConcurrentDictionary<string, Task<IObservable<NotificationNotificationEvent<TPrimitiveContent>>>> _eventSubscriptions = new ConcurrentDictionary<string, Task<IObservable<NotificationNotificationEvent<TPrimitiveContent>>>>();

		public async Task<IObservable<NotificationNotificationEvent<TPrimitiveContent>>> ObserveNotificationAsync(
			string url,
			string? subscriptionName = null,
			EventNotificationCriteria? criteria = null,
			string? poaUrl = null,
			bool deleteAfterFinalClose = false)
		{
			if (poaUrl == null)
			{
				poaUrl = this.PoaUrl?.ToString();

				if (poaUrl == null)
					throw new InvalidOperationException("Cannot Observe without valid PoaUrl");
			}

			// TODO: differentiate criteria
			return await _eventSubscriptions.GetOrAdd(url, async key =>
			{
				var filterDiscoveryCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.Subscription }
				};
				if (subscriptionName != null)
					filterDiscoveryCriteria.Attribute = Connection<TPrimitiveContent>.GetAttributes<Subscription>(_ => _.ResourceName == subscriptionName);

				string? subscriptionReference = null;

				var discoverSubscriptions = await GetPrimitiveAsync(url, filterDiscoveryCriteria);
				if (discoverSubscriptions?.URIList != null)
				{
					subscriptionReference = await discoverSubscriptions.URIList
						//.AsParallel().WithDegreeOfParallelism(4)
						.ToAsyncEnumerable()
						.FirstOrDefaultAwaitAsync(async sUrl =>
						{
							var primitive = await GetPrimitiveAsync(sUrl);
							var subscription = primitive.Subscription;

							return subscription.NotificationURI != null
								&& subscription.NotificationURI.Any(n => poaUrl.Equals(n, StringComparison.OrdinalIgnoreCase));
						});

					//work around for CSE timeout issue - remove subscriptions with different poaUrls
					if (subscriptionReference != null)
						await DeleteAsync(discoverSubscriptions.URIList.Except(new string[] { subscriptionReference }).ToArray());
				}

				//create subscription only if can't find subscription with the same notification url
				if (subscriptionReference != null)
				{
					Debug.WriteLine($"Using existing subscription {key} : {subscriptionReference}");
				}
				else
				{
					var subscriptionResponse = await CreateResourceAsync(
						url,
						ResourceType.Subscription,
						pc => {
							pc.Subscription = new Subscription
							{
								ResourceName = subscriptionName,
								EventNotificationCriteria = criteria ?? _defaultEventNotificationCriteria,
								NotificationContentType = NotificationContentType.AllAttributes,
								NotificationURI = new[] { poaUrl },
							};
							return pc;
						},
						resultContent: ResultContent.HierarchicalAddress
					);

					subscriptionReference = subscriptionResponse.URI ?? throw new ProtocolViolationException("CreateResourceAsync succeeded but did not return a URI");
					Debug.WriteLine($"Created Subscription {key} : {subscriptionReference}");
				}

				return Connection.Notifications
					.Where(n => n.SubscriptionReference == subscriptionReference)
					.Select(n => n.NotificationEvent)
					.Finally(() =>
					{
						if (deleteAfterFinalClose)
							DeleteAsync(subscriptionReference).Wait();
					})
					.Publish()
					.RefCount();
			});
		}


		public async Task<IObservable<TPrimitiveContent>> ObserveAsync(
			string url,
			string? subscriptionName = null,
			EventNotificationCriteria? criteria = null,
			string? poaUrl = null,
			bool deleteAfterFinalClose = false
			) =>
				(await ObserveNotificationAsync(url, subscriptionName, criteria: criteria, poaUrl: poaUrl, deleteAfterFinalClose: deleteAfterFinalClose))
				.Select(evt => evt.PrimitiveRepresentation)
				.WhereNotNull();

		static readonly EventNotificationCriteria _defaultEventNotificationCriteria = new EventNotificationCriteria
		{
			NotificationEventType = new[] { NotificationEventType.CreateChild },
		};

		public async Task<IObservable<TContent>> ObserveContentInstanceAsync<TContent>(
			string containerName,
			string? subscriptionName = null,
			string? poaUrl = null,
			bool deleteAfterFinalClose = false
			)
			where TContent : class
		{
			var container = await this.EnsureContainerAsync(containerName);
			return (await this.ObserveAsync(containerName, subscriptionName, poaUrl: poaUrl, deleteAfterFinalClose: deleteAfterFinalClose))
				//.Where(evt => evt.NotificationEventType == NotificationEventType.CreateChild)
				.Select(pc => pc.ContentInstance?.GetContent<TContent>())
				.WhereNotNull();
		}





		// TODO: find a proper place for this
		public static async Task<Application<TPrimitiveContent>> RegisterAsync(Connection.IConnectionConfiguration m2mConfig, IApplicationConfiguration appConfig, string inCse, Uri? caUri = null)
		{
			var urlPrefix = appConfig.UrlPrefix ?? throw new ArgumentNullException("appConfig.UrlPrefix");

			var con = new HttpConnection<TPrimitiveContent>(m2mConfig);

			var appId = appConfig.AppId ?? throw new ArgumentNullException("appConfig.AppId");
			var ae = await con.FindApplicationAsync(inCse, appId) ?? await con.RegisterApplicationAsync(appConfig);
			if (ae == null)
				throw new InvalidOperationException("Unable to register application");

			if (con.ClientCertificate == null && caUri != null)
			{
				var certificateFilename = m2mConfig.CertificateFilename ?? throw new ArgumentNullException("m2mConfig.CertificateFilename");
				var m2mUrl = m2mConfig.M2MUrl ?? throw new ArgumentNullException("m2mConfig.M2MUrl");

				var csrUri = new Uri(caUri, "CertificateSigning");
				var ccrUri = new Uri(caUri, "CertificateConfirm");

				var tokenId = ae.Labels?.FirstOrDefault(l => l.StartsWith("token="))?.Substring("token=".Length);
				if (string.IsNullOrWhiteSpace(tokenId))
					throw new InvalidDataException("registered AE is missing 'token' label");

				using var privateKey = RSA.Create(4096);
				var certificateRequest = new CertificateRequest(
					$"CN={appConfig.AppId}",
					privateKey,
					HashAlgorithmName.SHA256,
					RSASignaturePadding.Pkcs1);

				var sanBuilder = new SubjectAlternativeNameBuilder();
				sanBuilder.AddDnsName(appConfig.AppId);
				sanBuilder.AddDnsName(ae.AE_ID);
				certificateRequest.CertificateExtensions.Add(sanBuilder.Build());

				//Debug.WriteLine(certificateRequest.ToPemString());
				var signingRequest = new CertificateSigningRequestBody
				{
					Request = new CertificateSigningRequest
					{
						Application = new Aetheros.OneM2M.Api.Registration.Application
						{
							AeId = ae.AE_ID,
							TokenId = tokenId
						},
						X509Request = certificateRequest.ToPemString(),
					}
				};

				using var handler = new HttpClientHandler
				{
					ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
				};
				using var client = new HttpClient(handler);

				CertificateSigningResponseBody signingResponse;
				using (var httpSigningResponse = await client.PostJsonAsync(csrUri, signingRequest))
					signingResponse = await httpSigningResponse.DeserializeAsync<CertificateSigningResponseBody>();
				if (signingResponse.Response == null)
					throw new InvalidDataException("CertificateSigningResponse does not contain a response");
				if (signingResponse.Response.X509Certificate == null)
					throw new InvalidDataException("CertificateSigningResponse does not contain a certificate");

				var signedCert = AosUtils.CreateX509Certificate(signingResponse.Response.X509Certificate);

				var confirmationRequest = new ConfirmationRequestBody
				{
					Request = new ConfirmationRequest
					{
						CertificateHash = Convert.ToBase64String(signedCert.GetCertHash(HashAlgorithmName.SHA256)),
						CertificateId = new CertificateId
						{
							Issuer = signedCert.Issuer,
							SerialNumber = int.Parse(signedCert.SerialNumber, System.Globalization.NumberStyles.HexNumber).ToString()
						},
						TransactionId = signingResponse.Response.TransactionId,
					}
				};

				using (var httpConfirmationResponse = await client.PostJsonAsync(ccrUri, confirmationRequest))
				{
					var confirmationResponse = await httpConfirmationResponse.DeserializeAsync<ConfirmationResponseBody>();
					if (confirmationResponse.Response == null)
						throw new InvalidDataException("Invalid ConfirmationResponse");
					Debug.Assert(confirmationResponse.Response.Status == CertificateSigningStatus.Accepted);
				}

				using (var pubPrivEphemeral = signedCert.CopyWithPrivateKey(privateKey))
					await File.WriteAllBytesAsync(m2mConfig.CertificateFilename, pubPrivEphemeral.Export(X509ContentType.Cert));

				con = new HttpConnection<TPrimitiveContent>(m2mUrl, signedCert);
			}

			return new Application<TPrimitiveContent>(con, appConfig.AppId, ae.AE_ID, urlPrefix, appConfig.PoaUrl);
		}		
	}

	public class Application : Application<PrimitiveContent>
	{

		public class Configuration : IApplicationConfiguration
		{
			public string? AppId { get; set; }

			public string? AppName { get; set; }

			public string? CredentialId { get; set; }

			public Uri? PoaUrl { get; set; }

			public string? UrlPrefix { get; set; }
			public string? AEId { get; set; }
		}

		public Application(Connection<PrimitiveContent> con, string appId, string aeId, string urlPrefix, Uri? poaUrl = null) : base(con, appId, aeId, urlPrefix, poaUrl) {}
		public Application(Connection<PrimitiveContent> con, string aeId, IApplicationConfiguration config) : base (con, aeId, config) {}
	}
}
