using Aetheros.OneM2M.Api.Registration;
using Aetheros.OneM2M.Binding;

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
	public class Application
	{
		public interface IApplicationConfiguration
		{
			public string AppId { get; }
			public string AppName { get; }
			public string CredentialId { get; }
			public Uri PoaUrl { get; }
			public string UrlPrefix { get; }
		}

		public Connection Connection { get; }
		public string AppId { get; }
		public string AeId { get; }
		//public string AeAppName { get; }
		public Uri? PoaUrl { get; set; }
		//public string AeUrl { get; }
		//public string MNCse { get; set; }
		//public string AeMnUrl { get; set; }
		public string UrlPrefix { get; set; }

		public Application(Connection con, string appId, string aeId, string urlPrefix, Uri? poaUrl = null)
		{
			Connection = con;
			AppId = appId;
			AeId = aeId;
			//AeUrl = aeUrl;

			PoaUrl = poaUrl;
			UrlPrefix = urlPrefix;
			//AeAppName = aeAppName;
			//MNCse = mnCse;

			//AeMnUrl = $"{mnCse}/{AeId}";
		}

		//public string ResourceKey(string key) => $"{AeUrl}/{key}";
		public string ResourceKey(string key) => $"{AeId}/{key}";

		//public string MNResourceKey(string key) => $"{mnCse}/{AeId}/{key}";

		public async Task<T> GetResponseAsync<T>(RequestPrimitive body)
			where T : class, new()
		{
			if (body.To == null)
				body.To = $"/{UrlPrefix}{AeId}";
			else if (!body.To.StartsWith("/"))
				body.To = $"/{UrlPrefix}{AeId}/{body.To}";

			if (body.From == null)
				body.From = AeId;
			return await Connection.GetResponseAsync<T>(body);
		}

		public async Task<ResponseContent> GetResponseAsync(RequestPrimitive body) => await GetResponseAsync<ResponseContent>(body);

		public async Task<Resources> GetChildResourcesAsync(string key, FilterCriteria? filterCriteria = null) =>
			await GetResponseAsync<Resources>(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = key,
				ResultContent = ResultContent.ChildResources,
				FilterCriteria = filterCriteria
			});

		public Task<ResponseContent> GetPrimitiveAsync(string key, FilterCriteria? filterCriteria = null, ResultContent? resultContent = null) =>
			GetResponseAsync(new RequestPrimitive
			{
				From = this.AeId,
				To = key,
				ResultContent = resultContent,
				Operation = Operation.Retrieve,
				FilterCriteria = filterCriteria
			});

		public async Task DeleteAsync(IEnumerable<string> urls)
		{
			foreach (var url in urls)
			{
				await GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Delete,
					To = url,
				});
			}
		}

		public async Task AddContentInstanceAsync(string key, object content) => await AddContentInstanceAsync(key, null, content);

		public async Task AddContentInstanceAsync(string key, string? resourceName, object content) => await this.GetResponseAsync(new RequestPrimitive
		{
			To = key,
			Operation = Operation.Create,
			ResourceType = ResourceType.ContentInstance,
			PrimitiveContent = new PrimitiveContent
			{
				ContentInstance = new ContentInstance
				{
					ResourceName = resourceName,
					Content = content
				}
			}
		});

		public async Task<Container> EnsureContainerAsync(string name /*, bool clientAppContainer = false*/)
		{
			try
			{
				return (await GetPrimitiveAsync(name)).Container;
			}
			catch (Connection.HttpStatusException e) when (e.StatusCode == HttpStatusCode.NotFound)
			{
				return (await GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Create,
					//To = "~",
					ResourceType = ResourceType.Container,
					PrimitiveContent = new PrimitiveContent
					{
						Container = new Container
						{
							ResourceName = name
						}
					}
				})).Container;
			}
		}

		public async Task<T?> GetLatestContentInstanceAsync<T>(string containerKey)
			where T : class
		{
			try
			{
				var ciRefs = (await GetPrimitiveAsync(containerKey, new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.ContentInstance },
				})).URIList;

				if (ciRefs == null)
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

		readonly ConcurrentDictionary<string, Task<IObservable<NotificationNotificationEvent>>> _eventSubscriptions = new ConcurrentDictionary<string, Task<IObservable<NotificationNotificationEvent>>>();

		public async Task<IObservable<NotificationNotificationEvent>> ObserveAsync(string url)
		{
			if (this.PoaUrl == null)
				throw new InvalidOperationException("Cannot Observe without valid PoaUrl");

			return await _eventSubscriptions.GetOrAdd(url, async key =>
			{
				var discoverSubscriptions = await GetPrimitiveAsync(url, new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.Subscription }
				});

				string? subscriptionReference = null;
				var poaUrl = PoaUrl.ToString();

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
					if (discoverSubscriptions?.URIList != null)
						await DeleteAsync(discoverSubscriptions.URIList.Except(new string[] { subscriptionReference }));
				}

				//create subscription only if can't find subscription with the same notification url
				if (subscriptionReference == null)
				{
					var subscriptionResponse = await GetResponseAsync(new RequestPrimitive
					{
						Operation = Operation.Create,
						To = url,
						ResourceType = ResourceType.Subscription,
						ResultContent = ResultContent.HierarchicalAddress,
						PrimitiveContent = new PrimitiveContent
						{
							Subscription = new Subscription
							{
								NotificationURI = new[] { poaUrl },
							}
						}
					});

					subscriptionReference = subscriptionResponse?.URI;
				}

				Debug.WriteLine($"Created Subscription {subscriptionReference} = {key}");

				return Connection.Notifications
					.Where(n => n.SubscriptionReference == subscriptionReference)
					.Select(n => n.NotificationEvent)
					.Finally(() =>
					{
						//Debug.WriteLine($"Removing Subscription {subscriptionReference} = {key}");

						//if (!_eventSubscriptions.TryRemove(key, out var old))
						//{
						//	Debug.WriteLine($"Subscription {key} missing");
						//}

						//GetResponseAsync(new RequestPrimitive
						//{
						//	Operation = Operation.Delete,
						//	To = subscriptionReference,
						//}).Wait();
					})
					.Publish()
					.RefCount();
			});
		}

		public async Task<IObservable<TContent>> ObserveContentInstanceCreationAsync<TContent>(string containerName)
			where TContent : class
		{
			var container = await this.EnsureContainerAsync(containerName);
			return (await this.ObserveAsync(containerName))
				.Where(evt => evt.NotificationEventType.Contains(NotificationEventType.CreateChild))
				.Select(evt => evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.GetContent<TContent>())
				.Where(content => content != null) as IObservable<TContent>;
		}


		// TODO: find a proper place for this
		public static async Task<Application> RegisterAsync(Connection.IConnectionConfiguration m2mConfig, IApplicationConfiguration appConfig, string inCse, Uri caUri)
		{
			var con = new HttpConnection(m2mConfig);

			var ae = await con.FindApplicationAsync(inCse, appConfig.AppId) ?? await con.RegisterApplicationAsync(appConfig);
			if (ae == null)
				throw new InvalidOperationException("Unable to register application");

			if (con.ClientCertificate == null)
			{
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

				con = new HttpConnection(m2mConfig.M2MUrl, signedCert);
			}

			return new Application(con, appConfig.AppId, ae.AE_ID, appConfig.UrlPrefix, appConfig.PoaUrl);
		}
	}
}
