using Aetheros.OneM2M.Api;
using Aetheros.OneM2M.Binding;

using GridNet.IoT.Types;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace GridNet.IoT.Client
{
	class Program
	{
		// environment specific parameters

		const string inCse = "/PN_CSE";

		static readonly Uri listenUrl = new Uri("http://0.0.0.0:44399");

		public static string CommandsContainerName = "commands";
		public static string ConfigContainerName = "config";

		static int _requestId;
		public static string NextRequestId => (_requestId++).ToString();

		public static ConcurrentDictionary<string, IObservable<RequestPrimitive>> mapSubscriptions = new ConcurrentDictionary<string, IObservable<RequestPrimitive>>();

		static Application _app;

		static readonly Uri caBaseUrl = new Uri("https://piersh-m2m.corp.grid-net.com:18091/");

		class ConnectionConfig : Connection.IConfig
		{
			public Uri M2MUrl { get; set; }
			public string CertificateFilename { get; set; }
		}

		class AppConfig : Application.IConfig
		{
			public string AppId { get; set; }
			public string AppName { get; set; }
			public string CredentialId { get; set; }
			public Uri PoaUrl { get; set; }
			public Uri PrivatePoaUrl { get; set; }
		}

		static Connection.IConfig _m2mConfig = new ConnectionConfig
		{
			M2MUrl = new Uri("https://api.piersh-m2m.corp.grid-net.com/"),
			CertificateFilename = @"../../client-cert.pfx"
		};

		static AppConfig _appConfig = new AppConfig
		{
			AppId = "Nsdk-devAe-0.com.policynetiot.sdk",
			AppName = "policynet.m2m",
			CredentialId = "B9JBEBPBYBJSU062",
			PoaUrl = new Uri("http://fw.dfcn.com:21371/notify"),
			PrivatePoaUrl = new Uri("http://0.0.0.0:44399"),
		};

		public static async Task Main(string[] args)
		{
			var con = new Connection(_m2mConfig);
			_app = new Application(con, _appConfig.AppId, "C4bb2f056000001", _appConfig.PoaUrl);

			var ciRefs = (await _app.GetPrimitiveAsync("data-cnt", new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.ContentInstance },
			})).URIList;

			_app = await Application.Register(_m2mConfig, _appConfig, inCse, caBaseUrl);

			var host = WebHost.CreateDefaultBuilder(args)
				.UseUrls(_appConfig.PrivatePoaUrl.ToString())
				.Configure(app =>
					app.Map("/notify", builder =>
						builder.Run(context => _app.Connection.HandleNotificationAsync(context.Request))
					)
				).Build();
			host.Start();

			await Run();
		}


		static async Task DoSubscriptions()
		{
			var discoverSubscriptions = await _app.GetPrimitiveAsync("data-cnt", new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.Subscription },
			});

#if true
			if (discoverSubscriptions?.URIList != null)
			{
				foreach (var url in discoverSubscriptions.URIList)
				{
					/*
					await _app.GetResponseAsync(new RequestPrimitive
					{
						Operation = Operation.Retrieve,
						To = url,
						//ResultContent = ResultContent.
					});
					*/

					await _app.GetResponseAsync(new RequestPrimitive
					{
						Operation = Operation.Delete,
						To = url,
					});
				}
			}
#endif

			var subscription = await _app.ObserveAsync("foo");

			/*
			await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Create,
				To = "foo",
				ResourceType = ResourceType.Subscription,
				PrimitiveContent = new PrimitiveContent
				{
					Subscription = new Subscription
					{
						NotificationURI = new[] { poaUrl.ToString() },
					}
				}
			});
			*/

			using (var sub = subscription.Subscribe(
				evt =>
				{
					//int k = 3;
				},
				Console.WriteLine,
				() => Console.WriteLine("Completed")))
			{
				var createContentInstanceResponse = await _app.GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Create,
					To = "foo",
					ResourceType = ResourceType.ContentInstance,
					PrimitiveContent = new PrimitiveContent
					{
						ContentInstance = new ContentInstance
						{
							Content = new Command
							{
								Action = Actions.CloseValve,
								When = DateTime.UtcNow
							}
						}
					}
				});

				await Task.Delay(TimeSpan.FromSeconds(5));
			}

			/*
			await subscription.ForEachAsync(n =>
			{
				int k = 3;
			});
			*/

			await Task.Delay(TimeSpan.FromDays(1));
		}

		static async Task Run()
		{
			var discoverSubscriptions = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = _app.AeId,
				//ResponseType = new ResponseTypeInfo
				//{
				//	NotificationURI = new[] { poaUrl.ToString() },
				//	//ResponseTypeValue = ResponseType.NonBlockingRequestAsynch
				//	//ResponseTypeValue = ResponseType.BlockingRequest
				//},
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.Subscription },
				}
			});
			await Task.Delay(TimeSpan.FromDays(1));

			var dataContainer = await _app.EnsureContainerAsync("data-cnt");
			var eventsContainer = await _app.EnsureContainerAsync("events-cnt");
			var fooContainer = await _app.EnsureContainerAsync("foo");

			await DoSubscriptions();
			await Task.Delay(TimeSpan.FromDays(1));

			/*
			// async create container
			await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Create,
				To = _aeUrl,
				ResourceType = ResourceType.Container,
				ResponseType = new ResponseTypeInfo
				{
					NotificationURI = new[] { poaUrl.ToString() },
					ResponseTypeValue = ResponseType.NonBlockingRequestAsynch
				},
				PrimitiveContent = new PrimitiveContent
				{
					Container = new Container
					{
						ResourceName = "blah"
					}
				}
			});
			await Task.Delay(TimeSpan.FromDays(1));
			*/

			/*
			// async discover subscriptions
			var discoverSubscriptions = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = aeId,
				ResponseType = new ResponseTypeInfo
				{
					NotificationURI = new[] { poaUrl.ToString() },
					ResponseTypeValue = ResponseType.NonBlockingRequestAsynch
				},
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = ResourceType.Subscription,
				}
			});
			await Task.Delay(TimeSpan.FromDays(1));
			*/



			var retrieveFoo = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = "foo",
				ResultContent = ResultContent.ChildResourceReferences,
				ResponseType = new ResponseTypeInfo
				{
					ResponseTypeValue = ResponseType.NonBlockingRequestAsynch,
					NotificationURI = new[] {
						_appConfig.PoaUrl.ToString()
					}
				},
				/*
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = ResourceType.AEAnnc,
					Attribute = Api.GetAttributes<AE>(_ => _.AppName == aeAppName),
					//Attribute = Api.GetAttributes<AE>(_ => _.AE_ID == aeId),
				},
				*/
			});


			await Task.Delay(TimeSpan.FromDays(1));



			var responseFilterContainers = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = "/PN_CSE",
				ResultContent = ResultContent.ChildResourceReferences,
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.AEAnnc },
					Attribute = Connection.GetAttributes<AE>(_ => _.AppName == _appConfig.AppName),
					//Attribute = Api.Api.GetAttributes<AE>(_ => _.AE_ID == aeId),
				}
			});

			var deviceAEs = await responseFilterContainers.ResourceRefList
				.ToAsyncEnumerable()
				.SelectAsync(async rr => await _app.GetPrimitiveAsync(rr.Value))
				.Select(rc => rc.AEAnnc)
				.ToListAsync();

			foreach (var deviceAE in deviceAEs)
			{
				var containers = (await _app.GetPrimitiveAsync(deviceAE.Link, new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.Container },
				})).URIList;

				foreach (var container in containers)
				{
					await _app.GetPrimitiveAsync(container, new FilterCriteria
					{
						FilterUsage = FilterUsage.Discovery,
						ResourceType = new[] { ResourceType.ContentInstance },
					});
				}
			}

			return;

			/* test concurrent requests
			Task.WaitAll(
				Enumerable.Range(0, 20).Select(i =>
					_app.GetResponseAsync(new RequestPrimitive
					{
						Operation = Operation.Retrieve,
						To = aeId,
						RequestIdentifier = i.ToString()
					})
				).ToArray()
			);
			*/

			/*
			foreach (var url in responseFilterContainers.URIList.Skip(1))
			{
				var ae = await _app.GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Retrieve,
					To = url,
				});

			}
			*/

			await Task.Delay(TimeSpan.FromDays(1));

			// discover AEs
			var discoverSubscriptions2 = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = "foo",//inCse,
						   //To = inCse,//ResourceKey("foo"),//,
				ResultContent = ResultContent.ChildResourceReferences,
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.Subscription },
					//ResourceType = ResourceType.ContentInstance,
					//Attribute = Api.GetAttributes<Resource>(_ => _.ParentID == "asdasd")
					//Attribute = Api.GetAttributes<AE>(_ => _.AppName == aeAppName),
				}
			});

			/*
			var ciResponse = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = ResourceKey(CommandsContainerName),
			});
			*/

			/*
			// delete a resource
			await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Delete,
				To = "/PN_CSE/PN_CSE/sdk-devAe-0/config"
			});
			*/

			/*
			await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = _CommandsContainerName,
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					//ResourceType = ResourceType.ContentInstance,
					Attribute = Api.Api.GetAttributes<ContentInstance>(_ => _.ParentID == inCse)
					//Attribute = Api.Api.GetAttributes<AE>(_ => _.AppName == aeAppName),
				}
			});
			*/

#if false
			foreach (var value in Enum.GetValues(typeof(ResourceType)).OfType<ResourceType>())
			{
				Debug.WriteLine(value);
				await _app.GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Retrieve,
					To = inCse,
					RequestIdentifier = NextRequestId,
					FilterCriteria = new FilterCriteria
					{
						FilterUsage = FilterUsage.Discovery,
						ResourceType = (int) value,
						//Attribute = Api.GetAttributes<AE>(_ => _.AppName == aeAppName),
					}
				});
			}
#endif

			/*
			var commandsContainer = await EnsureContainer(CommandsContainerName);
			var configContainer = await EnsureContainer(ConfigContainerName);

			await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Create,
				To = ResourceKey(CommandsContainerName),
				ResourceType = ResourceType.ContentInstance,
				PrimitiveContent = new PrimitiveContent
				{
					ContentInstance = new ContentInstance
					{
						Content = new Command
						{
							Action = Actions.CloseValve,
							When = DateTime.UtcNow
						}
					}
				}
			});
			*/

#if false
			if (false)
			{
				var requestBody = new RequestPrimitive
				{
					Operation = Operation.Create,
					From = aeCredential,
					To = inCse,
					RequestIdentifier = NextRequestId,
					ResourceType = ResourceType.AE,
					ResultContent = ResultContent.Attributes,
					PrimitiveContent = new PrimitiveContent
					{
						AE = new AE
						{
							App_ID = aeAppId,
							AppName = aeAppName,
							PointOfAccess = new[] { poaUrl.ToString() }
						}
					}
				};

				var response = await _api.GetResponseAsync(requestBody);
				AE ae = response.AE;
				AE_ID = ae.AE_ID;
			}
#endif

			if (false)
			{
				var response = await _app.Connection.GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Retrieve,
					//From = aeCredential,
					To = inCse,
					//ResourceType = ResourceType.AE,
					FilterCriteria = new FilterCriteria
					{
						FilterUsage = FilterUsage.Discovery,
						ResourceType = new[] { ResourceType.AE },
						Attribute = Connection.GetAttributes<AE>(_ => _.App_ID == _appConfig.AppId),
					}
				});

				IEnumerable<string> urls = null;

				{
					var responseFilterAEs = await _app.GetResponseAsync(new RequestPrimitive
					{
						Operation = Operation.Retrieve,
						To = inCse,
						FilterCriteria = new FilterCriteria
						{
							FilterUsage = FilterUsage.Discovery,
							ResourceType = new[] { ResourceType.AE },
							Attribute = Connection.GetAttributes<AE>(_ => _.AppName == _appConfig.AppName),
						}
					});
					urls = responseFilterAEs.URIList;
				}

				{
					var aes = await urls
						.ToAsyncEnumerable()
						.SelectAsync(async url =>
						{
							var responseGetAE = await _app.GetPrimitiveAsync(url);
							return responseGetAE.AE;
						})
						.ToListAsync();
				}
			}

			{
				/*
				var responseFilterContainers = await _app.GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Retrieve,
					To = "/PN_CSE/PN_CSE/sdk-devAe-0",
					FilterCriteria = new FilterCriteria
					{
						FilterUsage = FilterUsage.Discovery,
						ResourceType = ResourceType.Container,
						//Attribute = Api.GetAttributes<AE>(_ => _.AppName == aeAppName),
						//Attribute = Api.GetAttributes<AE>(_ => _.AE_ID == aeId),
					}
				});
				*/

				/*
				if (response == null)
				{
					var createBody = new RequestPrimitive
					{
						Operation = Operation.Create,
						To = urls[0],
						RequestIdentifier = "12345",
						ResourceType = ResourceType.Container,
						PrimitiveContent = new PrimitiveContent
						{
							Container = new Container
							{
								ResourceName = "foo"
							}
						}
					};
					var createResponse = await _app.GetResponseAsync(createBody);
				}
				*/

				var qq = await _app.GetPrimitiveAsync("/PN_CSE/PN_CSE/sdk-devAe-0/foo");
			}
		}
	}


}
