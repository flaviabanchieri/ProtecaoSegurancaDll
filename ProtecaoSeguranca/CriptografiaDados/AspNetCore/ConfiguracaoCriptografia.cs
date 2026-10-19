using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ProtecaoSeguranca.CriptografiaDados
{
    internal static class ConfiguracaoCriptografia
    {
        /// <summary>
        /// Na primeira requisição que usa [Criptografado], carrega as chaves do appsettings da API.
        /// APIs sem campos marcados nunca chegam aqui e não precisam de chave.
        /// </summary>
        public static void ConfigurarSeNecessario(HttpContext httpContext)
        {
            if (!Criptografia.Configurada)
                Criptografia.Configurar(httpContext.RequestServices.GetRequiredService<IConfiguration>());
        }
    }
}
