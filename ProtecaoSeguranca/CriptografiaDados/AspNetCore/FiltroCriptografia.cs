using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtecaoSeguranca.Atributos;
using ProtecaoSeguranca.Comum;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
#if !NETCOREAPP3_0_OR_GREATER
using Microsoft.AspNetCore.Http.Internal;
#endif

namespace ProtecaoSeguranca.CriptografiaDados
{
    /// <summary>
    /// Filtro MVC global registrado pelo UsePortalSeguranca. Em qualquer endpoint:
    /// - entrada: propriedades [Criptografado] do corpo e parâmetros [Criptografado] (rota/query) são
    ///   descriptografados antes do model binding; valor puro ou adulterado é recusado com 400;
    /// - saída: propriedades [Criptografado] do objeto retornado são criptografadas.
    /// Endpoints sem tipos marcados passam direto, sem custo de leitura do corpo.
    /// </summary>
    internal sealed class FiltroCriptografia : IAsyncResourceFilter, IAsyncResultFilter
    {
        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var campoInvalido = await Descriptografar(context);

            if (campoInvalido != null)
            {
                var httpContext = context.HttpContext;
                httpContext.RequestServices.GetService<ILogger<FiltroCriptografia>>()?.LogWarning(
                    "Dado criptografado inválido ou não criptografado, requisição bloqueada. IP: {Ip} {Metodo} {Path} Campo: {Campo}",
                    httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Method,
                    httpContext.Request.Path.Value, campoInvalido);

                context.Result = new ContentResult
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Content = Respostas.CriptografiaInvalida,
                    ContentType = "application/json; charset=utf-8"
                };
                return;
            }

            await next();
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var valor = (context.Result as ObjectResult)?.Value ?? (context.Result as JsonResult)?.Value;
            var esquema = MapaCamposCriptografados.Obter(valor?.GetType());

            if (esquema == null)
            {
                await next();
                return;
            }

            ConfiguracaoCriptografia.ConfigurarSeNecessario(context.HttpContext);

            var response = context.HttpContext.Response;
            var original = response.Body;

            using (var buffer = new MemoryStream())
            {
                response.Body = buffer;
                try
                {
                    await next();
                }
                finally
                {
                    response.Body = original;
                }

                buffer.Position = 0;

                var json = response.ContentType != null && response.ContentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!json || buffer.Length == 0)
                {
                    await buffer.CopyToAsync(original);
                    return;
                }

                string corpo;
                using (var leitor = new StreamReader(buffer, Encoding.UTF8))
                {
                    corpo = await leitor.ReadToEndAsync();
                }

                var bytes = Encoding.UTF8.GetBytes(TransformadorJson.CifrarCampos(corpo, esquema));
                response.ContentLength = bytes.Length;
                await original.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        /// <summary>Devolve o campo inválido, ou null se tudo foi descriptografado.</summary>
        private static async Task<string> Descriptografar(ResourceExecutingContext context)
        {
            EsquemaJson esquemaCorpo = null;

            foreach (var parametro in context.ActionDescriptor.Parameters)
            {
                var info = (parametro as ControllerParameterDescriptor)?.ParameterInfo;

                if (info != null && info.IsDefined(typeof(CriptografadoAttribute), true))
                {
                    ConfiguracaoCriptografia.ConfigurarSeNecessario(context.HttpContext);

                    var nome = parametro.BindingInfo?.BinderModelName ?? parametro.Name;
                    if (!DecifrarRotaOuQuery(context, nome))
                        return nome;
                }
                else if (parametro.BindingInfo?.BindingSource == BindingSource.Body)
                {
                    esquemaCorpo = MapaCamposCriptografados.Obter(parametro.ParameterType);
                }
            }

            if (esquemaCorpo == null)
                return null;

            ConfiguracaoCriptografia.ConfigurarSeNecessario(context.HttpContext);

            var request = context.HttpContext.Request;
            var corpo = await LerCorpo(request);
            if (string.IsNullOrWhiteSpace(corpo))
                return null;

            var novo = TransformadorJson.DecifrarCampos(corpo, esquemaCorpo, out var campo);
            if (novo == null)
                return campo;

            TrocarCorpo(request, novo);
            return null;
        }

        private static bool DecifrarRotaOuQuery(ResourceExecutingContext context, string nome)
        {
            var request = context.HttpContext.Request;

            if (context.RouteData.Values.TryGetValue(nome, out var valorRota) && valorRota is string textoRota)
            {
                if (!TransformadorJson.TentarDecifrarTexto(textoRota, out var original))
                    return false;

                context.RouteData.Values[nome] = original;
#if NETCOREAPP3_0_OR_GREATER
                request.RouteValues[nome] = original;
#endif
                return true;
            }

            if (!request.Query.ContainsKey(nome))
                return true;

            var itens = new List<KeyValuePair<string, string>>();
            foreach (var item in request.Query)
            {
                var marcado = string.Equals(item.Key, nome, StringComparison.OrdinalIgnoreCase);
                foreach (var valor in item.Value)
                {
                    var novo = valor;
                    if (marcado && !TransformadorJson.TentarDecifrarTexto(valor, out novo))
                        return false;

                    itens.Add(new KeyValuePair<string, string>(item.Key, novo));
                }
            }

            request.QueryString = QueryString.Create(itens);
            return true;
        }

        private static async Task<string> LerCorpo(HttpRequest request)
        {
            if (request.ContentLength == 0)
                return null;

#if NETCOREAPP3_0_OR_GREATER
            request.EnableBuffering();
#else
            request.EnableRewind();
#endif
            string corpo;
            using (var leitor = new StreamReader(request.Body, Encoding.UTF8, true, 4096, leaveOpen: true))
            {
                corpo = await leitor.ReadToEndAsync();
            }
            request.Body.Position = 0;
            return corpo;
        }

        private static void TrocarCorpo(HttpRequest request, string corpo)
        {
            var bytes = Encoding.UTF8.GetBytes(corpo);
            request.Body = new MemoryStream(bytes);
            request.ContentLength = bytes.Length;
        }
    }
}
