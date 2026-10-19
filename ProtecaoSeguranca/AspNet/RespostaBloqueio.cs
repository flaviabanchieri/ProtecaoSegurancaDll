using System.Web;

namespace ProtecaoSeguranca.AspNet
{
    internal static class RespostaBloqueio
    {
        /// <summary>
        /// Escreve a resposta JSON e encerra a requisição sem executar a aplicação.
        /// </summary>
        public static void Escrever(HttpApplication app, int status, string json)
        {
            var response = app.Response;

            response.ClearContent();
            response.TrySkipIisCustomErrors = true;
            response.StatusCode = status;
            if (status == 429)
                response.StatusDescription = "Too Many Requests";
            response.ContentType = "application/json";
            response.Charset = "utf-8";
            response.Write(json);

            app.CompleteRequest();
        }
    }
}
