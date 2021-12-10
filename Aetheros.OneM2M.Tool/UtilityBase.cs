using Mono.Options;
using System;
using System.Collections.Generic;
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
		OptionSet _options;

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

		protected static void ShowError(string errorMessage, bool exit = true)
		{
			Console.Error.WriteLine($"{Path.GetFileNameWithoutExtension(Environment.ProcessPath)}: {errorMessage}");
			if (exit)
				Environment.Exit(1);
		}

		protected void ShowUsage(string errorMessage = null, bool exit = true)
		{
			if (errorMessage != null) {
				Console.Error.WriteLine($"{Path.GetFileNameWithoutExtension(Environment.ProcessPath)}: {errorMessage}");
				Console.Error.WriteLine();
			}
			Console.Error.WriteLine($"usage: {Path.GetFileNameWithoutExtension(Environment.ProcessPath)} {this.GetType().Name} {this.Usage}");
			_options.WriteOptionDescriptions(Console.Error);
			if (exit)
				Environment.Exit(1);
		}

		internal async Task Main(IEnumerable<string> args)
		{
			_options = this.Options;
			_options.Add("v|verbose", "Turn on verbose logging", v => verbose = true);
			_options.Add("h|?|help", "Display the help message", v => help = v != null);

			var rgExtra = _options.Parse(args.Skip(1));

			if (this.help)
				ShowUsage(exit: true);
			await this.Run(rgExtra);
		}
	}
}
