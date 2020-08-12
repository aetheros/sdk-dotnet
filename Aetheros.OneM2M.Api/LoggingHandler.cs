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

        void DumpHeaders(HttpHeaders headers)
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

            Trace.WriteLine($"{request.Method} {request.RequestUri} HTTP/{request.Version}");
            DumpHeaders(request.Headers);

            var content = request.Content;
            if (content != null)
                DumpHeaders(content.Headers);

            Trace.WriteLine("");
            if (content != null)
                Trace.WriteLine(content.ReadAsStringAsync().Result);

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
                Trace.WriteLine(content.ReadAsStringAsync().Result);

            return response;
        }
    }
}
