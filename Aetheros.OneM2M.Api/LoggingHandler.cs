using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace Aetheros.OneM2M.Api
{
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public class TraceMessageHandler : MessageProcessingHandler
    {
        public TraceMessageHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        static void DumpHeaders(HttpHeaders headers)
        {
            foreach (var header in headers)
            {
                foreach (var value in header.Value)
                    Trace.WriteLine($"{header.Key}: {value}");
            }
        }

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Trace.WriteLine("\n>>>>>>>>>>>>>>>>");

            if (request.RequestUri == null)
                throw new System.ArgumentNullException("request.RequestUri");

            Trace.WriteLine($"{request.Method} {request.RequestUri.PathAndQuery} HTTP/{request.Version}");
            Trace.WriteLine($"Host: {request.RequestUri.Authority}");
            DumpHeaders(request.Headers);

            var content = request.Content;
            if (content != null)
                DumpHeaders(content.Headers);

            Trace.WriteLine("");
            if (content != null)
                Trace.WriteLine(content.ReadAsStringAsync(cancellationToken).Result);

            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            Trace.WriteLine("\n<<<<<<<<<<<<<<<<");

            Trace.WriteLine($"{(int) response.StatusCode} {response.ReasonPhrase}");
            DumpHeaders(response.Headers);

            var content = response.Content;
            if (content != null)
                DumpHeaders(content.Headers);

            Trace.WriteLine("");

            if (content != null)
                Trace.WriteLine(content.ReadAsStringAsync(cancellationToken).Result);

            return response;
        }
    }
}
