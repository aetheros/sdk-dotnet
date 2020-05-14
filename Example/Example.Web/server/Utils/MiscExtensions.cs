using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Example.Web.Server.Utils
{
	public static class MiscExtensions
	{
		public static string Description(this Enum element)
		{
			var value = element.ToString();
			var type = element.GetType();
			//Use reflection to try and get the description attribute for the enumeration
			var field = type.GetField(value);
			if (field != null)
			{
				var descAttributes = (DisplayAttribute[]) field.GetCustomAttributes(typeof(DisplayAttribute), false);
				if (descAttributes != null && descAttributes.Length > 0)
					return descAttributes[0].GetName();
			}
			return value;
		}
	}
}
