using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace Aetheros.OneM2M.Api
{
    public class DebugMessageHandler : MessageProcessingHandler
    {
        public DebugMessageHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        void DumpHeaders(HttpHeaders headers)
        {
            foreach (var header in headers)
                foreach (var value in header.Value)
                    Debug.WriteLine($"{header.Key}: {value}");
        }

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Debug.WriteLine(">>>>>>>>");

            Debug.WriteLine($"{request.Method} {request.RequestUri} HTTP/{request.Version}");
            DumpHeaders(request.Headers);

            var content = request.Content;
            if (content != null)
                DumpHeaders(content.Headers);

            Debug.WriteLine("");
            if (content != null)
                Debug.WriteLine(content.ReadAsStringAsync().Result);

            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            Debug.WriteLine("<<<<<<<<");

            Debug.WriteLine($"{(int) response.StatusCode} {response.ReasonPhrase}");
            DumpHeaders(response.Headers);

            var content = response.Content;
            if (content != null)
                DumpHeaders(content.Headers);

            Debug.WriteLine("");

            if (content != null)
                Debug.WriteLine(content.ReadAsStringAsync().Result);

            return response;
        }
    }
}
