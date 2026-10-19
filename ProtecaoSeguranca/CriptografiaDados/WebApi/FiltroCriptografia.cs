using ProtecaoSeguranca.AspNet;
using ProtecaoSeguranca.Atributos;
using ProtecaoSeguranca.Comum;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace ProtecaoSeguranca.CriptografiaDados
{
    /// <summary>
    /// Filtro global do Web API 2, registrado com config.UsarCriptografia(). Em qualquer endpoint:
    /// - entrada (antes do model binding): propriedades [Criptografado] do corpo e parâmetros [Criptografado]
    ///   (rota/query) são descriptografados; valor puro ou adulterado é recusado com 400;
    /// - saída: propriedades [Criptografado] do objeto retornado são criptografadas.
    /// Endpoints sem tipos marcados passam direto.
    /// </summary>
    internal sealed class FiltroCriptografia : IAuthorizationFilter, IActionFilter
    {
        // Cache do Web API para os pares da query string; precisa ser descartado ao trocar a URL
        private const string CacheQuery = "MS_QueryNameValuePairs";

        public bool AllowMultiple => false;

        public async Task<HttpResponseMessage> ExecuteAuthorizationFilterAsync(HttpActionContext actionContext,
            CancellationToken cancellationToken, Func<Task<HttpResponseMessage>> continuation)
        {
            var campoInvalido = await Descriptografar(actionContext);
            if (campoInvalido == null)
                return await continuation();

            var request = actionContext.Request;
            var ip = request.Properties.TryGetValue("MS_HttpContext", out var contexto)
                ? (contexto as HttpContextBase)?.Request.UserHostAddress
                : null;

            RegistroSeguranca.Aviso("Dado criptografado inválido ou não criptografado, requisição bloqueada. " +
                $"IP: {ip} {request.Method} {request.RequestUri.AbsolutePath} Campo: {campoInvalido}");

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(Respostas.CriptografiaInvalida, Encoding.UTF8, "application/json")
            };
        }

        public async Task<HttpResponseMessage> ExecuteActionFilterAsync(HttpActionContext actionContext,
            CancellationToken cancellationToken, Func<Task<HttpResponseMessage>> continuation)
        {
            var resposta = await continuation();

            if (resposta == null || !(resposta.Content is ObjectContent conteudo))
                return resposta;

            var esquema = MapaCamposCriptografados.Obter(conteudo.Value?.GetType());
            var tipoMidia = conteudo.Headers.ContentType?.MediaType;

            if (esquema == null || tipoMidia == null || tipoMidia.IndexOf("json", StringComparison.OrdinalIgnoreCase) < 0)
                return resposta;

            var json = await conteudo.ReadAsStringAsync();
            resposta.Content = new StringContent(TransformadorJson.CifrarCampos(json, esquema), Encoding.UTF8, "application/json");
            return resposta;
        }

        /// <summary>Devolve o campo inválido, ou null se tudo foi descriptografado.</summary>
        private static async Task<string> Descriptografar(HttpActionContext actionContext)
        {
            EsquemaJson esquemaCorpo = null;

            foreach (var binding in actionContext.ActionDescriptor.ActionBinding.ParameterBindings)
            {
                var parametro = binding.Descriptor;

                if (parametro.GetCustomAttributes<CriptografadoAttribute>().Any())
                {
                    if (!DecifrarRotaOuQuery(actionContext, parametro.ParameterName))
                        return parametro.ParameterName;
                }
                else if (binding.WillReadBody)
                {
                    esquemaCorpo = MapaCamposCriptografados.Obter(parametro.ParameterType);
                }
            }

            var request = actionContext.Request;
            if (esquemaCorpo == null || request.Content == null || request.Content.Headers.ContentLength == 0)
                return null;

            var corpo = await request.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(corpo))
                return null;

            var novo = TransformadorJson.DecifrarCampos(corpo, esquemaCorpo, out var campo);
            if (novo == null)
                return campo;

            TrocarCorpo(request, novo);
            return null;
        }

        private static bool DecifrarRotaOuQuery(HttpActionContext actionContext, string nome)
        {
            var valoresRota = actionContext.ControllerContext.RouteData.Values;

            if (valoresRota.TryGetValue(nome, out var valorRota) && valorRota is string textoRota)
            {
                if (!TransformadorJson.TentarDecifrarTexto(textoRota, out var original))
                    return false;

                valoresRota[nome] = original;
                return true;
            }

            var request = actionContext.Request;
            var pares = request.GetQueryNameValuePairs().ToList();
            if (!pares.Any(p => string.Equals(p.Key, nome, StringComparison.OrdinalIgnoreCase)))
                return true;

            var partes = new string[pares.Count];
            for (var i = 0; i < pares.Count; i++)
            {
                var valor = pares[i].Value;
                if (string.Equals(pares[i].Key, nome, StringComparison.OrdinalIgnoreCase) &&
                    !TransformadorJson.TentarDecifrarTexto(valor, out valor))
                    return false;

                partes[i] = Uri.EscapeDataString(pares[i].Key) + "=" + Uri.EscapeDataString(valor ?? string.Empty);
            }

            request.RequestUri = new UriBuilder(request.RequestUri) { Query = string.Join("&", partes) }.Uri;
            request.Properties.Remove(CacheQuery);
            return true;
        }

        private static void TrocarCorpo(HttpRequestMessage request, string corpo)
        {
            var original = request.Content;
            var novo = new StringContent(corpo, Encoding.UTF8);

            novo.Headers.Remove("Content-Type");
            foreach (var header in original.Headers.Where(h => !string.Equals(h.Key, "Content-Length", StringComparison.OrdinalIgnoreCase)))
                novo.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (novo.Headers.ContentType != null)
                novo.Headers.ContentType.CharSet = "utf-8";

            request.Content = novo;
        }
    }
}
