using System;

namespace Aetheros.OneM2M.Api.Registration
{
	using Newtonsoft.Json;

	using System.Diagnostics;
	using System.Xml.Serialization;

	public enum CertificateSigningStatus
	{
		Accepted = 0,
		GrantedWithMods = 1,
		Rejection = 2,
		Waiting = 3,        // the request  has not yet been processed
		RevocationWarning = 4,  // this message contains a warning that a revocation is imminent
		RevocationNotification = 5 //notification that a revocation has occurred
	}

	[Serializable]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class CertificateSigningRequestBody
	{
		[JsonProperty("pnm2m:signreq")]
		[XmlElement("signreq")]
		public CertificateSigningRequest? Request { get; set; }
	}

	[Serializable]
	[JsonObject("signreq")]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class CertificateSigningRequest
	{
		[JsonProperty("device")]
		[XmlElement("device")]
		public Application? Application { get; set; }

		[JsonProperty("xcsr")]
		[XmlElement("xcsr")]
		public string? X509Request { get; set; }
	}



	[Serializable]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class CertificateSigningResponseBody
	{
		[JsonProperty("pnm2m:signresp")]
		[XmlElement("signresp")]
		public CertificateSigningResponse? Response { get; set; }
	}

	[Serializable]
	[JsonObject("signresp")]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class CertificateSigningResponse
	{
		[JsonProperty("confirmtxnid")]
		[XmlElement("confirmtxnid")]
		public string? TransactionId { get; set; }

		[JsonProperty("clientcert")]
		[XmlElement("clientcert")]
		public string? X509Certificate { get; set; }

		[JsonProperty("status")]
		[XmlElement("status")]
		public CertificateSigningStatus Success { get; set; }
	}

	[Serializable]
	[JsonObject("device")]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class Application
	{
		[JsonProperty("wanaddr")]
		[XmlElement("wanaddr")]
		public string? AeId { get; set; }

		[JsonProperty("tokenid")]
		[XmlElement("tokenid")]
		public string? TokenId { get; set; }
	}




	[Serializable]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class ConfirmationRequestBody
	{
		[JsonProperty("pnm2m:confirmreq")]
		[XmlElement("confirmreq")]
		public ConfirmationRequest? Request { get; set; }
	}

	[Serializable]
	[JsonObject("pnm2m:confirmreq")]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class ConfirmationRequest
	{
		[JsonProperty("certhash")]
		[XmlElement("certhash")]
		public string? CertificateHash { get; set; }

		[JsonProperty("certid")]
		[XmlElement("certid")]
		public CertificateId? CertificateId { get; set; }

		[JsonProperty("txnid")]
		[XmlElement("txnid")]
		public string? TransactionId { get; set; }
	}

	[Serializable]
	[JsonObject("certid")]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class CertificateId
	{
		[JsonProperty("issuer")]
		[XmlElement("issuer")]
		public string? Issuer { get; set; }

		[JsonProperty("serial")]
		[XmlElement("serial")]
		public string? SerialNumber { get; set; }
	}




	[Serializable]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class ConfirmationResponseBody
	{
		[JsonProperty("pnm2m:confirmresp")]
		[XmlElement("confirmresp")]
		public ConfirmationResponse? Response { get; set; }
	}

	[Serializable]
	[JsonObject("pnm2m:pnm2m:confirmresp")]
	[DebuggerStepThrough]
	[System.ComponentModel.DesignerCategory("code")]
	public partial class ConfirmationResponse
	{
		[JsonProperty("cacertpem")]
		[XmlElement("cacertpem")]
		public string? Certificate { get; set; }

		[JsonProperty("newtokenid")]
		[XmlElement("newtokenid")]
		public string? NewTokenId { get; set; }

		[JsonProperty("status")]
		[XmlElement("status")]
		public CertificateSigningStatus Status { get; set; }

		[JsonProperty("certid")]
		[XmlElement("certid")]
		public CertificateId? CertificateId { get; set; }
	}
}