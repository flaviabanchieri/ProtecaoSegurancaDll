using ProtecaoSeguranca.Comum;
using System;
using System.Web;

namespace ProtecaoSeguranca.AspNet
{
    /// <summary>
    /// Troca a página de erro de exceções não tratadas por uma mensagem JSON genérica com código de correlação
    /// (header X-Correlation-Id); o erro real vai para o log.
    /// Segue o customErrors do web.config: onde ele mostra detalhes (ex.: RemoteOnly acessando localmente),
    /// o erro original é mantido.
    /// O Web API trata as próprias exceções e não passa por aqui; ele já esconde os detalhes de clientes
    /// remotos por padrão (IncludeErrorDetailPolicy.LocalOnly).
    /// </summary>
    public class OcultarErrosModule : IHttpModule
    {
        public void Init(HttpApplication app)
        {
            app.Error += TratarErro;
        }

        public void Dispose()
        {
        }

        private static void TratarErro(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var contexto = app.Context;
            var ex = contexto.Server.GetLastError();

            if (ex == null || !contexto.IsCustomErrorEnabled)
                return;

            if (ex is HttpException http && http.GetHttpCode() < 500)
                return;

            var request = contexto.Request;

            if (contexto.Response.HeadersWritten)
            {
                RegistroSeguranca.Erro($"Erro não tratado após o início da resposta. {request.HttpMethod} {request.Path}", ex);
                return;
            }

            var id = Respostas.GerarIdErro();
            RegistroSeguranca.Erro($"Erro interno {id}. {request.HttpMethod} {request.Path}{request.Url.Query}", ex);

            contexto.Server.ClearError();
            app.Response.AppendHeader(Respostas.HeaderCorrelacao, id);

            var corpo = "{\"mensagem\":\"" + Respostas.MensagemErroInterno(id) + "\"}";
            RespostaBloqueio.Escrever(app, 500, corpo);
        }
    }
}
