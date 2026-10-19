using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System;
using System.IO;
#if !NETCOREAPP3_0_OR_GREATER
using Microsoft.AspNetCore.Http.Internal;
#endif
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtecaoSeguranca.Middleware
{
    public class SqlInjectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SqlInjectionMiddleware> _logger;
        private readonly ModoProtecao _modo;

        public SqlInjectionMiddleware(RequestDelegate next, ILogger<SqlInjectionMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _modo = (configuration.GetSection(SqlInjectionOptions.Secao).Get<SqlInjectionOptions>() ?? new SqlInjectionOptions()).Modo;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var request = httpContext.Request;

            if (HttpMethods.IsOptions(request.Method))
            {
                await _next(httpContext);
                return;
            }

            var suspeito = ValidarPath(request)
                ?? ValidarQueryString(request)
                ?? await ValidarCorpo(request);

            if (suspeito != null)
            {
                RegistrarTentativa(httpContext, suspeito.Value.Campo, suspeito.Value.Valor);

                if (_modo == ModoProtecao.Bloquear)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    httpContext.Response.ContentType = "application/json; charset=utf-8";
                    await httpContext.Response.WriteAsync(Respostas.SqlInjection, Encoding.UTF8);
                    return;
                }
            }

            await _next(httpContext);
        }

        private static (string Campo, string Valor)? ValidarPath(HttpRequest request)
        {
            var path = request.Path.HasValue ? request.Path.Value : null;

            return SqlInjectionDetector.ContemSqlInjection(path) ? ("path", path) : ((string, string)?)null;
        }

        private static (string Campo, string Valor)? ValidarQueryString(HttpRequest request)
        {
            foreach (var item in request.Query)
            {
                if (SqlInjectionDetector.ContemSqlInjection(item.Key))
                    return (item.Key, item.Key);

                foreach (var valor in item.Value)
                {
                    if (SqlInjectionDetector.ContemSqlInjection(valor))
                        return (item.Key, valor);
                }
            }

            return null;
        }

        private static async Task<(string Campo, string Valor)?> ValidarCorpo(HttpRequest request)
        {
            if (!RegrasSqlInjection.MetodosComCorpo.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
                return null;

            if (request.ContentLength > RegrasSqlInjection.TamanhoMaximoCorpo)
                return null;

            if (request.HasFormContentType)
                return await ValidarFormulario(request);

            if (request.ContentType != null && request.ContentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                return await ValidarJson(request);

            return null;
        }

        private static async Task<(string Campo, string Valor)?> ValidarFormulario(HttpRequest request)
        {
            IFormCollection form;
            try
            {
                form = await request.ReadFormAsync();
            }
            catch (InvalidDataException)
            {
                return null;
            }

            foreach (var item in form)
            {
                if (RegrasSqlInjection.CamposIgnorados.Contains(item.Key))
                    continue;

                if (SqlInjectionDetector.ContemSqlInjection(item.Key, permitirPontoEVirgula: true))
                    return (item.Key, item.Key);

                foreach (var valor in item.Value)
                {
                    if (SqlInjectionDetector.ContemSqlInjection(valor, permitirPontoEVirgula: true))
                        return (item.Key, valor);
                }
            }

            return null;
        }

        private static async Task<(string Campo, string Valor)?> ValidarJson(HttpRequest request)
        {
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

            if (string.IsNullOrWhiteSpace(corpo) || corpo.Length > RegrasSqlInjection.TamanhoMaximoCorpo)
                return null;

            JToken json;
            try
            {
                json = JToken.Parse(corpo);
            }
            catch (JsonReaderException)
            {
                return null;
            }

            return ValidarToken(json);
        }

        private static (string Campo, string Valor)? ValidarToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    foreach (var propriedade in ((JObject)token).Properties())
                    {
                        if (RegrasSqlInjection.CamposIgnorados.Contains(propriedade.Name))
                            continue;

                        if (SqlInjectionDetector.ContemSqlInjection(propriedade.Name, permitirPontoEVirgula: true))
                            return (propriedade.Name, propriedade.Name);

                        var resultado = ValidarToken(propriedade.Value);
                        if (resultado != null)
                            return resultado;
                    }
                    return null;

                case JTokenType.Array:
                    foreach (var item in token.Children())
                    {
                        var resultado = ValidarToken(item);
                        if (resultado != null)
                            return resultado;
                    }
                    return null;

                case JTokenType.String:
                    var valor = token.Value<string>();
                    return SqlInjectionDetector.ContemSqlInjection(valor, permitirPontoEVirgula: true)
                        ? (token.Path, valor)
                        : ((string, string)?)null;

                default:
                    return null;
            }
        }

        private void RegistrarTentativa(HttpContext httpContext, string campo, string valor)
        {
            var request = httpContext.Request;
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var forwardedFor = request.Headers["X-Forwarded-For"].ToString();

            _logger.LogWarning(
                Registro.Deteccao(_modo, "Possível SQL Injection") + " IP: {Ip} X-Forwarded-For: {ForwardedFor} {Metodo} {Path} Campo: {Campo} Valor: {Valor}",
                ip, forwardedFor, request.Method, request.Path.Value, campo, RegrasSqlInjection.ParaLog(valor));
        }
    }
}
