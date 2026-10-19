using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System;
using System.Net;
using System.Web;

namespace ProtecaoSeguranca.AspNet
{
    /// <summary>
    /// Limita requisições por IP em janela fixa de 1 minuto (429 com Retry-After).
    /// Limites configuráveis no appSettings com as chaves "Seguranca:RateLimit:*".
    /// </summary>
    public class RateLimitModule : IHttpModule
    {
        // O ASP.NET cria várias instâncias do HttpApplication: a contagem precisa ser única
        private static readonly Lazy<LimitadorRequisicoes> Limitador = new Lazy<LimitadorRequisicoes>(() =>
            new LimitadorRequisicoes(
                ConfiguracaoWebConfig.Ler<RateLimitOptions>(RateLimitOptions.Secao),
                rede => RegistroSeguranca.Aviso($"Rede confiável inválida ignorada: {rede}")));

        public void Init(HttpApplication app)
        {
            app.BeginRequest += Verificar;
        }

        public void Dispose()
        {
        }

        private static void Verificar(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var request = app.Request;
            var limitador = Limitador.Value;

            IPAddress.TryParse(request.UserHostAddress, out var remoto);
            var ip = limitador.ObterIp(remoto, request.Headers["X-Forwarded-For"]);

            if (request.HttpMethod == "OPTIONS" || limitador.Liberado(ip))
                return;

            var resultado = limitador.Registrar(ip, request.Path);
            if (!resultado.Excedeu)
                return;

            if (resultado.RegistrarLog)
                RegistroSeguranca.Aviso(Registro.Deteccao(limitador.Modo, "Rate limit excedido") +
                    $" IP: {ip} {request.HttpMethod} {request.Path} Limite: {resultado.Limite}/min");

            if (limitador.Modo == ModoProtecao.SomenteLog)
                return;

            app.Response.AppendHeader("Retry-After", resultado.SegundosRestantes.ToString());
            RespostaBloqueio.Escrever(app, 429, Respostas.RateLimit);
        }
    }
}
