using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if NETCOREAPP3_0_OR_GREATER
using Microsoft.Extensions.Hosting;
using AmbienteHospedagem = Microsoft.AspNetCore.Hosting.IWebHostEnvironment;
#else
using Microsoft.AspNetCore.Hosting;
using AmbienteHospedagem = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
#endif

namespace ProtecaoSeguranca.Middleware
{
    /// <summary>
    /// Adiciona headers de segurança a todas as respostas (inclusive erros e bloqueios dos outros middlewares)
    /// e remove headers que revelam a tecnologia do servidor.
    /// Headers já definidos pela API não são sobrescritos.
    /// </summary>
    public class HeadersSegurancaMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HeadersSeguranca _headersSeguranca;
        private readonly List<KeyValuePair<string, string>> _headers;

        public HeadersSegurancaMiddleware(RequestDelegate next, IConfiguration configuration, AmbienteHospedagem env)
        {
            _next = next;

            var opcoes = configuration.GetSection(HeadersSegurancaOptions.Secao).Get<HeadersSegurancaOptions>()
                ?? new HeadersSegurancaOptions();
            _headersSeguranca = new HeadersSeguranca(opcoes);
            _headers = _headersSeguranca.Obter(env.IsDevelopment()).ToList();
        }

        public Task Invoke(HttpContext httpContext)
        {
           
            httpContext.Response.OnStarting(AplicarHeaders, httpContext.Response);
            return _next(httpContext);
        }

        private Task AplicarHeaders(object estado)
        {
            var headers = ((HttpResponse)estado).Headers;

            foreach (var header in _headers)
            {
                if (!headers.ContainsKey(header.Key))
                    headers[header.Key] = header.Value;
            }

            if (_headersSeguranca.RemoverHeadersServidor)
            {
                foreach (var nome in HeadersSeguranca.HeadersServidor)
                    headers.Remove(nome);
            }

            return Task.CompletedTask;
        }
    }
}
