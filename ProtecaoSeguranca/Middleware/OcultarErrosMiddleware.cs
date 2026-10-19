using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtecaoSeguranca.Comum;
using System;
using System.Linq;
using System.Text;
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
    /// Impede que detalhes de erros internos (ex.: mensagens do banco) cheguem ao cliente.
    /// Respostas 5xx têm os textos trocados por uma mensagem genérica com código de correlação,
    /// mantendo a estrutura JSON original; o erro real vai para o log.
    /// </summary>
    public class OcultarErrosMiddleware
    {
        private const int LimiteCaptura = 64 * 1024;

        private readonly RequestDelegate _next;
        private readonly ILogger<OcultarErrosMiddleware> _logger;
        private readonly AmbienteHospedagem _env;

        public OcultarErrosMiddleware(RequestDelegate next, ILogger<OcultarErrosMiddleware> logger, AmbienteHospedagem env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (_env.IsDevelopment())
            {
                await _next(httpContext);
                return;
            }

            var response = httpContext.Response;
            var original = response.Body;

            using (var interceptado = new RespostaInterceptadaStream(original, response, LimiteCaptura))
            {
                response.Body = interceptado;

                try
                {
                    await _next(httpContext);
                }
                catch (Exception ex)
                {
                    response.Body = original;

                    if (response.HasStarted)
                    {
                        _logger.LogError(ex, "Erro não tratado após o início da resposta. {Metodo} {Path}",
                            httpContext.Request.Method, httpContext.Request.Path.Value);
                        throw;
                    }

                    var id = Respostas.GerarIdErro();
                    _logger.LogError(ex, "Erro interno {Id}. {Metodo} {Path}{Query}",
                        id, httpContext.Request.Method, httpContext.Request.Path.Value, httpContext.Request.QueryString.Value);

                    response.StatusCode = StatusCodes.Status500InternalServerError;
                    await EscreverRespostaGenerica(response, id, null);
                    return;
                }
                finally
                {
                    response.Body = original;
                }

                if (interceptado.Capturou)
                {
                    var id = Respostas.GerarIdErro();
                    var corpoOriginal = Encoding.UTF8.GetString(interceptado.ObterCapturado());

                    _logger.LogError("Erro interno {Id}. {Metodo} {Path}{Query} Status: {Status} Resposta original: {Corpo}",
                        id, httpContext.Request.Method, httpContext.Request.Path.Value, httpContext.Request.QueryString.Value,
                        response.StatusCode, corpoOriginal);

                    await EscreverRespostaGenerica(response, id, corpoOriginal);
                }
            }
        }

        private static async Task EscreverRespostaGenerica(HttpResponse response, string id, string corpoOriginal)
        {
            var mensagem = Respostas.MensagemErroInterno(id);

            response.ContentLength = null;
            response.ContentType = "application/json; charset=utf-8";
            response.Headers[Respostas.HeaderCorrelacao] = id;

            await response.WriteAsync(SubstituirTextos(corpoOriginal, mensagem), Encoding.UTF8);
        }

        /// <summary>
        /// Mantém a estrutura do JSON original (o envelope que o front espera),
        /// trocando todos os valores texto pela mensagem genérica.
        /// </summary>
        private static string SubstituirTextos(string corpoOriginal, string mensagem)
        {
            var padrao = new JObject { ["mensagem"] = mensagem }.ToString(Formatting.None);

            if (string.IsNullOrWhiteSpace(corpoOriginal))
                return padrao;

            JToken json;
            try
            {
                json = JToken.Parse(corpoOriginal);
            }
            catch (JsonReaderException)
            {
                return padrao;
            }

            if (!(json is JContainer container))
                return JToken.FromObject(mensagem).ToString(Formatting.None);

            var textos = container.Descendants()
                .OfType<JValue>()
                .Where(v => v.Type == JTokenType.String)
                .ToList();

            foreach (var texto in textos)
                texto.Value = mensagem;

            return json.ToString(Formatting.None);
        }
    }
}
