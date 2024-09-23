using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GridNet.IoT.Client
{
	public static class Program
	{
		public class UtilityInfo
		{
			public Type Type { get; set; }
			public string Description { get; set; }
			public PropertyInfo OptionsProperty { get; set; }
			public MethodInfo Run { get; set; }
		}

		public static IEnumerable<UtilityInfo> Utilities =>
			from type in Assembly.GetExecutingAssembly().GetTypes()
			where
				!type.IsAbstract &&
				typeof(UtilityBase).IsAssignableFrom(type)
			let prop = type.GetProperty("Options", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance)
			where
				prop != null &&
				prop.PropertyType == typeof(OptionSet)
			let miRun = type.GetMethod("Run", new[] { typeof(IList<string>) })
			where
				miRun != null
			select new UtilityInfo
			{
				Type = type,
				OptionsProperty = prop,
				Description = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>().Description,
				Run = miRun
			};

		static void DumpOptions()
		{
			Console.Error.WriteLine($"usage: {Process.GetCurrentProcess().ProcessName} <command> [<options>]");
			DumpCommands();
		}

		static void DumpCommands()
		{
			Console.Error.WriteLine("available commands:");
			foreach (var info in Utilities)
				Console.Error.WriteLine($"    {info.Type.Name}: {info.Description}");
		}

		public static async Task Main(string[] args)
		{
			Trace.Listeners.Clear();
			Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));

			if (args.Length == 0)
			{
				DumpOptions();
				return;
			}

			var utility = CreateUtility(args[0]);
			if (utility == null)
			{
				Console.Error.WriteLine(args[0] + ": no such command");
				DumpCommands();
				return;
			}

			await utility.Main(args);
		}

		public static UtilityBase CreateUtility(string strUtil)
		{
			var info = (
				from util in Utilities
				where util.Type.Name.Equals(strUtil, StringComparison.InvariantCultureIgnoreCase)
				select util
			).FirstOrDefault();

			if (info == null)
				return null;

			return Activator.CreateInstance(info.Type) as UtilityBase;
		}
	}
}