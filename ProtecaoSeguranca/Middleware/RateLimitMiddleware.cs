using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System.Text;
using System.Threading.Tasks;

namespace ProtecaoSeguranca.Middleware
{
    /// <summary>
    /// Limita requisições por IP em janela fixa de 1 minuto.
    /// Atrás de proxy reverso (Traefik), o IP do cliente vem do X-Forwarded-For,
    /// mas só quando a conexão chega de uma rede confiável; caso contrário o header é ignorado.
    /// </summary>
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitMiddleware> _logger;
        private readonly LimitadorRequisicoes _limitador;

        public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;

            var opcoes = configuration.GetSection(RateLimitOptions.Secao).Get<RateLimitOptions>() ?? new RateLimitOptions();
            _limitador = new LimitadorRequisicoes(opcoes,
                rede => _logger.LogWarning("Rede confiável inválida ignorada: {Rede}", rede));
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var request = httpContext.Request;
            var ip = _limitador.ObterIp(httpContext.Connection.RemoteIpAddress, request.Headers["X-Forwarded-For"].ToString());

            if (HttpMethods.IsOptions(request.Method) || _limitador.Liberado(ip))
            {
                await _next(httpContext);
                return;
            }

            var resultado = _limitador.Registrar(ip, request.Path.Value);

            if (!resultado.Excedeu)
            {
                await _next(httpContext);
                return;
            }

            if (resultado.RegistrarLog)
                _logger.LogWarning(Registro.Deteccao(_limitador.Modo, "Rate limit excedido") + " IP: {Ip} {Metodo} {Path} Limite: {Limite}/min",
                    ip, request.Method, request.Path.Value, resultado.Limite);

            if (_limitador.Modo == ModoProtecao.SomenteLog)
            {
                await _next(httpContext);
                return;
            }

            var response = httpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            response.Headers["Retry-After"] = resultado.SegundosRestantes.ToString();
            response.ContentType = "application/json; charset=utf-8";
            await response.WriteAsync(Respostas.RateLimit, Encoding.UTF8);
        }
    }
}
