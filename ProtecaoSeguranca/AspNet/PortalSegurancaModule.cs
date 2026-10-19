using System.Web;

namespace ProtecaoSeguranca.AspNet
{
    /// <summary>
    /// Registra as proteções na ordem correta, como o UsePortalSeguranca do ASP.NET Core:
    /// headers de segurança (se habilitado), ocultar erros, rate limit (se habilitado), validação de upload (se habilitado) e SQL Injection.
    /// Escolhas no appSettings: "Seguranca:UsarHeadersSeguranca" (padrão true), "Seguranca:UsarRateLimit" (padrão false)
    /// e "Seguranca:UsarValidacaoUpload" (padrão false).
    /// Para escolher proteção por proteção, registrar os módulos individuais no web.config em vez deste.
    /// </summary>
    public class PortalSegurancaModule : IHttpModule
    {
        public void Init(HttpApplication app)
        {
            if (ConfiguracaoWebConfig.LerBool("Seguranca:UsarHeadersSeguranca", true))
                new HeadersSegurancaModule().Init(app);

            new OcultarErrosModule().Init(app);

            if (ConfiguracaoWebConfig.LerBool("Seguranca:UsarRateLimit", false))
                new RateLimitModule().Init(app);

            if (ConfiguracaoWebConfig.LerBool("Seguranca:UsarValidacaoUpload", false))
                new UploadArquivosModule().Init(app);

            new SqlInjectionModule().Init(app);
        }

        public void Dispose()
        {
        }
    }
}
