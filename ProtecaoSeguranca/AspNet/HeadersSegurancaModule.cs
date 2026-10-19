using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System;
using System.Web;

namespace ProtecaoSeguranca.AspNet
{
    /// <summary>
    /// Adiciona headers de segurança a todas as respostas e remove headers que revelam a tecnologia do servidor.
    /// Headers já definidos pela aplicação não são sobrescritos. Valores configuráveis no appSettings
    /// com as chaves "Seguranca:HeadersSeguranca:*". O HSTS não é enviado em requisições locais.
    /// </summary>
    public class HeadersSegurancaModule : IHttpModule
    {
        private static readonly Lazy<HeadersSeguranca> Headers = new Lazy<HeadersSeguranca>(() =>
            new HeadersSeguranca(ConfiguracaoWebConfig.Ler<HeadersSegurancaOptions>(HeadersSegurancaOptions.Secao)));

        public void Init(HttpApplication app)
        {
            app.BeginRequest += (sender, e) =>
                ((HttpApplication)sender).Response.AddOnSendingHeaders(AplicarHeaders);
        }

        public void Dispose()
        {
        }

        private static void AplicarHeaders(HttpContext contexto)
        {
            var response = contexto.Response;
            var headersSeguranca = Headers.Value;

            var integrado = HttpRuntime.UsingIntegratedPipeline;

            foreach (var header in headersSeguranca.Obter(contexto.Request.IsLocal))
            {
                if (!integrado)
                    response.AppendHeader(header.Key, header.Value);
                else if (response.Headers[header.Key] == null)
                    response.Headers[header.Key] = header.Value;
            }

            if (integrado && headersSeguranca.RemoverHeadersServidor)
            {
                foreach (var nome in HeadersSeguranca.HeadersServidor)
                    response.Headers.Remove(nome);
            }
        }
    }
}
