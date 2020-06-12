using Mono.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GridNet.IoT.Client
{
	public abstract class UtilityBase
	{
		readonly OptionSet EmptyOptions = new OptionSet();

		public virtual OptionSet Options => EmptyOptions;

		protected bool verbose;
		protected bool help;

		public abstract Task Run(IList<string> args);

		protected abstract string Usage { get; }

		protected void Output(string message, bool showNonVerbose = true, string verboseMessage = null)
		{
			message = verbose ? verboseMessage ?? message : message;
			if (verbose || showNonVerbose)
				Console.Error.WriteLine(message);
		}

		protected void ShowError(string errorMessage, bool exit = true)
		{
			Console.Error.WriteLine($"error: {errorMessage}");
			if (exit)
				Environment.Exit(1);
		}

		protected void ShowUsage(string errorMessage = null, bool exit = true)
		{
			if (errorMessage != null)
				Console.Error.WriteLine($"error: {errorMessage}");
			Console.Error.WriteLine($"usage: {Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)} {this.GetType().Name} {this.Usage}");
			this.Options.WriteOptionDescriptions(Console.Error);
		}

		internal async Task Main(IEnumerable<string> args)
		{
			var rgExtra = this.Options.Parse(args.Skip(1));

			this.Options.Add("v|verbose", "Turn on verbose logging", v => verbose = true);
			this.Options.Add("h|?|help", "Display the help message", v => help = v != null);

			if (this.help)
				ShowUsage(exit: true);
			await this.Run(rgExtra);
		}
	}
}
