using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Aetheros.OneM2M.Api
{
	internal static class AosUtils
	{
		public static void AddRange(this NameValueCollection @this, string key, IEnumerable<string> values)
		{
			foreach (var value in values)
				@this.Add(key, value);
		}

		public static async Task<IEnumerable<T>> WhenAll<T>(this IEnumerable<Task<T>> @this) => await Task.WhenAll(@this);

		public static string Join(this IEnumerable<string> @this, string seprator = ", ") =>
			string.Join(seprator, @this);

		public static DateTimeOffset? ParseNullableDateTimeOffset(this string @this) =>
			DateTimeOffset.TryParse(@this, out var offset) ? (DateTimeOffset?) offset : null;

		public static TEnum? ParseNullableEnum<TEnum>(this string @this) where TEnum : struct =>
			Enum.TryParse(@this, out TEnum value) ? (TEnum?) value : null;

		public static X509Certificate2? LoadCertificate(string certificateFilename)
		{
			if (!File.Exists(certificateFilename))
				return null;
			return new X509Certificate2(X509Certificate.CreateFromCertFile(certificateFilename));
		}

		public static IObservable<T> AsyncFinally<T>(this IObservable<T> source, Func<Task> action) =>
			source
				.Materialize()
				.SelectMany(async n =>
				{
					switch (n.Kind)
					{
						case NotificationKind.OnCompleted:
						case NotificationKind.OnError:
							await action();
							return n;
						case NotificationKind.OnNext:
							return n;
						default:
							throw new NotImplementedException();
					}
				})
				.Dematerialize();

		public static T? DeserializeObject<T>(this JsonSerializer @this, string value)
			where T : class
		{
			using (var stringReader = new StringReader(value))
			using (var jsonReader = new JsonTextReader(stringReader))
				return @this.Deserialize<T>(jsonReader);
		}

		public static string ToPemString(this CertificateRequest request, X509SignatureGenerator? generator = null)
		{
			var pkcs10 = generator == null ? request.CreateSigningRequest() : request.CreateSigningRequest(generator);
			return "-----BEGIN CERTIFICATE REQUEST-----\r\n"
				+ pkcs10.ToFormattedBase64String()
				+ "-----END CERTIFICATE REQUEST-----";
		}

		public static string ToFormattedBase64String(this byte[] @this, int lineLength = 64)
		{
			var builder = new StringBuilder();
			var base64 = Convert.ToBase64String(@this);
			for (var offset = 0; offset < base64.Length;)
			{
				int lineEnd = Math.Min(offset + lineLength, base64.Length);
				builder.Append(base64, offset, lineEnd - offset).AppendLine();
				offset = lineEnd;
			}
			return builder.ToString();
		}

		public static X509Certificate2 CreateX509Certificate(string str)
		{
			var temp = Path.GetTempFileName();
			File.WriteAllText(temp, str);
			try
			{
				return new X509Certificate2(X509Certificate.CreateFromCertFile(temp));
			}
			finally
			{
				File.Delete(temp);
			}
		}
	}

#if false
	static class AsyncEnumerableExtensions
	{
		public static IAsyncEnumerable<U> SelectAsync<T, U>(this IAsyncEnumerable<T> source, Func<T, Task<U>> fn) => new SelectAsyncEnumerable<T, U>(source, fn);

		class SelectAsyncEnumerable<T, U> : IAsyncEnumerable<U>
		{
			readonly IAsyncEnumerable<T> _source;
			readonly Func<T, Task<U>> _func;

			public SelectAsyncEnumerable(IAsyncEnumerable<T> source, Func<T, Task<U>> func)
			{
				_source = source;
				_func = func;
			}

			public IAsyncEnumerator<U> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new SelectAsyncEnumerator(_source.GetAsyncEnumerator(), _func, cancellationToken);

			class SelectAsyncEnumerator : IAsyncEnumerator<U>
			{
				readonly IAsyncEnumerator<T> _source;
				readonly Func<T, Task<U>> _func;
				readonly CancellationToken _cancellationToken;

				public SelectAsyncEnumerator(IAsyncEnumerator<T> source, Func<T, Task<U>> func, CancellationToken cancellationToken)
				{
					_source = source;
					_func = func;
					_cancellationToken = cancellationToken;
				}

				public ValueTask DisposeAsync() => _source.DisposeAsync();

				public async ValueTask<bool> MoveNextAsync()
				{
					if (!await _source.MoveNextAsync(_cancellationToken))
						return false;
					Current = await _func(_source.Current);
					return true;
				}

				public U Current { get; private set; }
			}
		}
	}
#endif
}
