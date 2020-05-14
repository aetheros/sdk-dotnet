using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;

using GridNet.OneM2M.Types;

using static GridNet.IoT.Api.OneM2MConnection;
using GridNet.Bootstrap;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Security.Cryptography;
using System.Net.Http;

namespace GridNet.IoT.Api
{
	public class Application
	{
		public interface IConfig
		{
			public string AppId { get; }
			public string AppName { get; }
			public string CredentialId { get; }
			public Uri PoaUrl { get; }
		}

		public OneM2MConnection Connection { get; }
		public string AppId { get; }
		public string AeId { get; }
		//public string AeAppName { get; }
		public Uri? PoaUrl { get; set; }
		//public string AeUrl { get; }
		//public string MNCse { get; set; }
		//public string AeMnUrl { get; set; }

		public Application(OneM2MConnection con, string appId, string aeId, Uri? poaUrl = null)
		{
			Connection = con;
			AppId = appId;
			AeId = aeId;
			//AeUrl = aeUrl;

			PoaUrl = poaUrl;
			//AeAppName = aeAppName;
			//MNCse = mnCse;

			//AeMnUrl = $"{mnCse}/{AeId}";
		}

		//public string ResourceKey(string key) => $"{AeUrl}/{key}";
		public string ResourceKey(string key) => $"{AeId}/{key}";

		//public string MNResourceKey(string key) => $"{mnCse}/{AeId}/{key}";

		public async Task<ResponseContent> GetResponseAsync(RequestPrimitive body)
		{
			if (!body.To.StartsWith("/"))
				body.To = $"{AeId}/{body.To}";
			if (body.From == null)
				body.From = AeId;
			return await Connection.GetResponseAsync(body);
		}

		public Task<ResponseContent> GetPrimitiveAsync(string key, FilterCriteria? filterCriteria = null) =>
			GetResponseAsync(new RequestPrimitive
			{
				From = this.AeId,
				To = key,
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

		public async Task AddContentInstance(string key, object content) => await this.GetResponseAsync(new RequestPrimitive
		{
			To = key,
			Operation = Operation.Create,
			ResourceType = ResourceType.ContentInstance,
			PrimitiveContent = new PrimitiveContent
			{
				ContentInstance = new ContentInstance
				{
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
			catch (HttpStatusException e)
			{
				if (e.StatusCode != HttpStatusCode.NotFound)
					throw;

				//var toUrl = /*clientAppContainer ? AeMnUrl :*/ AeUrl;
				return (await GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Create,
					To = AeId,
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

		public async Task<T?> GetLatestContentInstance<T>(string containerKey)
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
					.SelectAsync(async url => await GetPrimitiveAsync(url))
					.OrderBy(s => s.ContentInstance?.CreationTime);

				return (await rcs
					.Select(rc => rc.ContentInstance?.GetContent<T>())
					.ToListAsync()).LastOrDefault();
			}
			catch (HttpStatusException e)
			{
				if (e.StatusCode != HttpStatusCode.NotFound)
					throw;

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
					subscriptionReference = discoverSubscriptions.URIList
						.AsParallel()
						.WithDegreeOfParallelism(4)
						.FirstOrDefault(sUrl =>
						{
							var subscription = GetPrimitiveAsync(sUrl)
								.Result
								.Subscription;

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


		public static async Task<Application> Register(OneM2MConnection.IConfig m2mConfig, IConfig appConfig, string inCse, Uri caUri)
		{
			var con = new OneM2MConnection(m2mConfig);

			var ae = await con.FindApplication(inCse, appConfig.AppId) ?? await con.RegisterApplication(appConfig, inCse);
			if (ae == null)
				throw new InvalidOperationException("Unable to register application");

			if (con.ClientCertificate == null)
			{
				var csrUri = new Uri(caUri, "CertificateSigning");
				var ccrUri = new Uri(caUri, "CertificateConfirm");

				var tokenId = ae.Labels?.FirstOrDefault(l => l.StartsWith("token="))?.Substring("token=".Length);
				if (string.IsNullOrWhiteSpace(tokenId))
					throw new InvalidDataException("registered AE is missing 'token' label");

				using (var privateKey = RSA.Create(4096))
				{
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
							Application = new Bootstrap.Application
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

					var certificateText = signingResponse.Response.X509Certificate;
					var signedCert = GridNetUtils.CreateX509Certificate(certificateText);

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
						Debug.Assert(confirmationResponse.Response.Status == CertificateSigningStatus.Accepted);
					}

					using (var pubPrivEphemeral = signedCert.CopyWithPrivateKey(privateKey))
					{
						await File.WriteAllBytesAsync(m2mConfig.CertificateFilename, pubPrivEphemeral.Export(X509ContentType.Cert));
					}

					con = new OneM2MConnection(m2mConfig.M2MUrl, signedCert);
				}
			}

			return new Application(con, appConfig.AppId, ae.AE_ID, appConfig.PoaUrl);
		}
	}
}
