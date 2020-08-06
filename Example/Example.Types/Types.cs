using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Example.Types
{
	[JsonObject("info")]
	public class Info
	{
		[Key]
		[JsonProperty("meterId")]
		public string? MeterId { get; set; }
	}

	public class State
	{
		public enum ValveState
		{
			[Display(Name = "Open")]
			[EnumMember(Value = "open")]
			Open = 1,

			[Display(Name = "Closed")]
			[EnumMember(Value = "closed")]
			Closed,
		}

		[JsonProperty("valve")]
		public ValveState Valve { get; set; }
	}

	public enum Actions
	{
		[Display(Name = "Open Valve")]
		[EnumMember(Value = "openValve")]
		OpenValve = 1,

		[Display(Name = "Close Valve")]
		[EnumMember(Value = "closeValve")]
		CloseValve,
	}

	[JsonObject("commands")]
	public class Command
	{
		[JsonProperty("action")]
		public Actions Action { get; set; }

		[JsonProperty("when")]
		public DateTimeOffset When { get; set; }
	}

	[JsonObject("config")]
	public class Config
	{
		[JsonObject("meterReadPolicy")]
		public class MeterReadPolicy
		{
			[Key]
			[JsonProperty("id")]
			public string? Id { get; set; }

			[JsonProperty("name")]
			public string? Name { get; set; }

			[JsonProperty("start")]
			public DateTimeOffset Start { get; set; }

			[JsonProperty("end")]
			public DateTimeOffset? End { get; set; }

			[JsonProperty("readInterval")]
			public string? ReadInterval { get; set; }
		}
	}

	[JsonObject("events")]
	public class Events
	{
		public enum EventTypes
		{
			[Display(Name = "Leak Detected")]
			[EnumMember(Value = "leakDetected")]
			LeakDetected = 1,

			[Display(Name = "Backflow Detected")]
			[EnumMember(Value = "backflowDetected")]
			BackflowDetected,
		}

		public class MeterEvent
		{
			[JsonProperty("eventTime")]
			public DateTimeOffset EventTime { get; set; }

			[JsonProperty("event")]
			public EventTypes Event { get; set; }
		}

		[Key]
		[JsonProperty("meterId")]
		public string? MeterId { get; set; }

		[JsonProperty("events")]
		public ICollection<MeterEvent> MeterEvents { get; set; } = Array.Empty<MeterEvent>();
	}

	[JsonObject("data")]
	public class Data
	{
		public enum Units
		{
			USGal = 1,
		}

		[Key]
		[JsonProperty("meterId")]
		public string? MeterId { get; set; }

		[JsonProperty("uom")]
		public Units UOM { get; set; }

		
		[JsonObject("summations")]
		public class Summation
		{
			[JsonProperty("readTime")]
			public DateTimeOffset ReadTime { get; set; }

			[JsonProperty("value")]
			public double Value { get; set; }
		}

		[JsonProperty("summations")]
		public ICollection<Summation> Summations { get; set; } = Array.Empty<Summation>();
	}
}
