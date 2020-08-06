using System;

namespace Example.Web.Server.Services
{
	[Serializable]
	public class WebOptions
	{
		public WebOptions()
		{
		}

		public string DataContainer { get; set; } = "data-cnt";
		public string EventsContainer { get; set; } = "events-cnt";
		public string InfoContainer { get; set; } = "info-cnt";
		public string StateContainer { get; set; } = "state-cnt";
		public string ConfigContainer { get; set; } = "config-cnt";
		public string CommandContainer { get; set; } = "command-cnt";

		public ConnectionConfig M2M { get; set; }
		public AppConfig AE { get; set; }

		public Uri CAUrl { get; set; }

		[Serializable]
		public class ConnectionConfig : Aetheros.OneM2M.Api.Connection.IConnectionConfiguration
		{
			public Uri M2MUrl { get; set; }
			public string CertificateFilename { get; set; } = "cert.pfx";
		}

		[Serializable]
		public class AppConfig : Aetheros.OneM2M.Api.Application.IApplicationConfiguration
		{
			public string AppId { get; set; }
			public string AppName { get; set; }
			public string CredentialId { get; set; }
			public Uri PoaUrl { get; set; }
			public Uri PrivatePoaUrl { get; set; }
			public string UrlPrefix { get; set; }
		}
	}
}
