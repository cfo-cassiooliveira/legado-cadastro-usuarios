using System.Net;
using System.Text;

namespace Legado.Api.IntegrationTests.Infrastructure;

/// <summary>HttpMessageHandler que devolve sempre a mesma resposta (substitui serviços externos nos testes).</summary>
public sealed class StaticResponseHandler(HttpStatusCode status, string body = "{}") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
}
