using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

using GridNet.OneM2M.Types;

using GridNet.IoT.Types;

using GridNet.IoT.Api;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using Newtonsoft.Json;

namespace GridNet.IoT.Tools
{
	class Program
	{
		// environment specific parameters

		// add hosts file entry api.gridwide-lab3 -> 132.197.247.60
		// api.dev8.usw1.aws.corp.grid-net.com -> 192.168.10.161
		static readonly Uri m2mUrl = new Uri("https://api.dev8.usw1.aws.corp.grid-net.com");
		//const string aeCredential = "SOMHIFX1X1BQYYJP";

		static readonly Uri poaUrl = new Uri("http://fw.dfcn.com:21371/notify");
		const string inCse = "/PN_CSE";
		//const string mnCse = "/352327090000150";

		const string aeAppName = "sdk-devAe-0";
		const string aeAppId = "Nsdk-devAe-0.com.policynetiot.sdk";
		const string aeId = "Cce488036000003";

		static readonly Uri listenUrl = new Uri("http://0.0.0.0:44399");

		public static string CommandsContainerName = "commands";
		public static string ConfigContainerName = "config";

		static int _requestId;
		public static string NextRequestId => (_requestId++).ToString();

		public static ConcurrentDictionary<string, IObservable<RequestPrimitive>> mapSubscriptions = new ConcurrentDictionary<string, IObservable<RequestPrimitive>>();

		static readonly GridNet.IoT.Api.OneM2MConnection _api = new GridNet.IoT.Api.OneM2MConnection(m2mUrl, @"../../test.cer");
		static readonly Random _random = new Random();

		public static async Task Main(string[] args)
		{
			var host = WebHost.CreateDefaultBuilder()
				.UseUrls(listenUrl.ToString())
				.Configure(app => app.Map("/notify", NotifyHandler)).Build();
			host.Start();

			await Run();
		}

		static async Task AddInfo() {
			await _app.AddContentInstance("info-cnt", new Info { MeterId = aeId });
		}

		static async Task AddData()
		{
			var count = 0;
			while (count < 2)
			{
				var summations = new Data.Summation[] { new Data.Summation { ReadTime = DateTimeOffset.UtcNow, Value = _random.NextDouble() * 20 } };

				var createContentInstanceResponse = await _app.GetResponseAsync(new RequestPrimitive
				{
					To = "data-cnt",
					Operation = Operation.Create,
					ResourceType = ResourceType.ContentInstance,
					PrimitiveContent = new PrimitiveContent
					{
						ContentInstance = new ContentInstance
						{
							Content = new Data
							{
								MeterId = aeId,
								UOM = Data.Units.USGal,
								Summations = summations
							}
						}
					}
				});

				await Task.Delay(TimeSpan.FromSeconds(6));
				count++;
			}
		}

		static async Task DoSubscriptions()
		{
			var discoverSubscriptions = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = aeId,
				/*
				ResponseType = new ResponseTypeInfo
				{
					ResponseTypeValue = ResponseType.NonBlockingRequestAsynch
				},
				*/
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = new[] { ResourceType.Subscription },
				}
			});

			var subscription = await _app.ObserveAsync("data-cnt");

			using (var sub = subscription.Subscribe(
				evt =>
				{
					var dataStr = evt.PrimitiveRepresentation.PrimitiveContent?.ContentInstance?.Content.ToString();
					if (dataStr != null)
					{
						var data = JsonConvert.DeserializeObject<Data>(dataStr, Api.OneM2MConnection.JsonSettings);
					}
					Console.WriteLine("data added notification: " + dataStr);
				},
				ex =>
				{
				},
				() => { }))
			{
				var count = 0;
				while (count < 2)
				{
					var summations = new Data.Summation[] { new Data.Summation { ReadTime = DateTimeOffset.UtcNow.AddMinutes(-5), Value = _random.NextDouble() * 20 }, new Data.Summation { ReadTime = DateTimeOffset.UtcNow, Value = _random.NextDouble() * 20 } };

					var createContentInstanceResponse = await _app.GetResponseAsync(new RequestPrimitive
					{
						Operation = Operation.Create,
						To = "data-cnt",
						ResourceType = ResourceType.ContentInstance,
						PrimitiveContent = new PrimitiveContent
						{
							ContentInstance = new ContentInstance
							{
								Content = new Data
								{
									MeterId = aeId,
									UOM = Data.Units.USGal,
									Summations = summations
								}
							}
						}
					});

					await Task.Delay(TimeSpan.FromSeconds(5));
					count++;
				}


			}

			//if (discoverSubscriptions?.URIList != null)
			//{
			//	foreach (var url in discoverSubscriptions.URIList)
			//	{
			//		/*
			//		await _app.GetResponseAsync(new RequestPrimitive
			//		{
			//			Operation = Operation.Retrieve,
			//			To = url,
			//			//ResultContent = ResultContent.
			//		});
			//		*/

			//		await _app.GetResponseAsync(new RequestPrimitive
			//		{
			//			Operation = Operation.Delete,
			//			To = url,
			//		});
			//	}
			//}
		}

		static async Task Run()
		{
			//var commandContainer = await _app.EnsureContainerAsync("command-cnt", true);
#if false
			AddInfo().Wait();
#endif
#if false
			AddData().Wait();
#endif
			//DoSubscriptions().Wait();
#if false
			var discoverSubscriptions = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = aeId,
				/*
				ResponseType = new ResponseTypeInfo
				{
					ResponseTypeValue = ResponseType.NonBlockingRequestAsynch
				},
				*/
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = (int)ResourceType.Subscription,
				}
			});

			var responseFilterContainers = await _app.GetResponseAsync(new RequestPrimitive
			{
				Operation = Operation.Retrieve,
				To = "/PN_CSE",
				ResultContent = ResultContent.ChildResourceReferences,
				FilterCriteria = new FilterCriteria
				{
					FilterUsage = FilterUsage.Discovery,
					ResourceType = (int)ResourceType.AE,
					Attribute = Api.GetAttributes<AE>(_ => _.AppName == aeAppName),
					//Attribute = Api.GetAttributes<AE>(_ => _.AE_ID == aeId),
				}
			});

			var deviceAEs =
				(await responseFilterContainers.ResourceRefList
				.SelectAsync(async rr => await _app.GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Retrieve,
					To = rr.Value,
				})))
				.Select(rc => rc.AE)
				.ToList();
#endif
			//var testConfig = (await _app.ObserveAsync(_"config-cnt"));
			var latestPolicy = _app.GetLatestContentInstance<Config.MeterReadPolicy>("config-cnt").Result;


			//var dataContainer = await _app.EnsureContainerAsync("data-cnt");

			//var summations = new Data.Summation[] { new Data.Summation { ReadTime = DateTimeOffset.UtcNow.AddMinutes(-5), Value = _random.NextDouble() * 20 }, new Data.Summation { ReadTime = DateTimeOffset.UtcNow, Value = _random.NextDouble() * 20 } };

			//var createContentInstanceResponse = await _app.GetResponseAsync(new RequestPrimitive
			//{
			//	Operation = Operation.Create,
			//	To = _"data-cnt",
			//	ResourceType = ResourceType.ContentInstance,
			//	PrimitiveContent = new PrimitiveContent
			//	{
			//		ContentInstance = new ContentInstance
			//		{
			//			Content = new Data
			//			{
			//				MeterId = aeId,
			//				UOM = Data.Units.USGal,
			//				Summations = summations
			//			}
			//		}
			//	}
			//});

			var responseDataContainer = (await _app.GetPrimitiveAsync("data-cnt", new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.ContentInstance },
			})).URIList;

			var responseDataContainer2 = (await _app.GetPrimitiveAsync("data-cnt", new FilterCriteria
			{
				FilterUsage = FilterUsage.Discovery,
				ResourceType = new[] { ResourceType.ContentInstance },
				CreatedAfter = DateTimeOffset.UtcNow.AddDays(-14)
			})).URIList;


			var contInsts = await responseDataContainer2
				.ToAsyncEnumerable()
				.SelectAsync(async url => await _app.GetPrimitiveAsync(url))
				.Select(rc => rc.ContentInstance)
				.ToListAsync();


#if false
			foreach(var cin in responseDataContainer)
			{
				_app.GetResponseAsync(new RequestPrimitive
				{
					Operation = Operation.Delete,
					To = cin,
				}).Wait();
			}
#endif
			var dataSummations = await responseDataContainer
				.ToAsyncEnumerable()
				.SelectAsync(async url => await _app.GetPrimitiveAsync(url))
				.Select(rc => JsonConvert.DeserializeObject<Data>(rc.ContentInstance.Content.ToString(), Api.OneM2MConnection.JsonSettings))
				.ToListAsync();
			//.Select(rc => JsonConvert.DeserializeObject<Data>(rc.ContentInstance.Content.ToString(), new JsonSerializerSettings
			// {
			//	 Error = delegate (object sender, ErrorEventArgs args)
			//	 {
			//		 errors.Add(args.ErrorContext.Error.Message);
			//		 args.ErrorContext.Handled = true;
			//	 },
			//	 Converters = { new IsoDateTimeConverter() }
			// }));

			//foreach (var deviceAE in deviceAEs)
			//{
			//	var containers = (await _app.GetResponseAsync(new RequestPrimitive
			//	{
			//		Operation = Operation.Retrieve,
			//		To = deviceAE.ResourceID,
			//		ResultContent = ResultContent.ChildResourceReferences,
			//		FilterCriteria = new FilterCriteria
			//		{
			//			FilterUsage = FilterUsage.Discovery,
			//			ResourceType = (int)ResourceType.Container,
			//		}
			//	})).ResourceRefList;

			//	foreach (var container in containers)
			//	{

			//	}
			//}



		}

		static void NotifyHandler(IApplicationBuilder app) =>
			app.Run(context => _api.HandleNotificationAsync(context.Request));
	}
}
