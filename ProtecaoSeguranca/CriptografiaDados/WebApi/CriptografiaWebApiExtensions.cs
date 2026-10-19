using ProtecaoSeguranca.CriptografiaDados;
using System.Linq;
using System.Web.Http;

namespace ProtecaoSeguranca.AspNet
{
    public static class CriptografiaWebApiExtensions
    {
        /// <summary>
        /// Liga a criptografia das propriedades e parâmetros marcados com [Criptografado] em todos os endpoints
        /// do Web API 2: descriptografa na entrada e criptografa na saída. Chamar no WebApiConfig.Register.
        /// Chaves no appSettings: "Seguranca:Criptografia:Chave".
        /// </summary>
        /// <example>config.UsarCriptografia();</example>
        public static HttpConfiguration UsarCriptografia(this HttpConfiguration config)
        {
            if (!config.Filters.Any(f => f.Instance is FiltroCriptografia))
                config.Filters.Add(new FiltroCriptografia());

            return config;
        }
    }
}
