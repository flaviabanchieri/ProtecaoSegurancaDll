using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProtecaoSeguranca.Configuracao;
using ProtecaoSeguranca.CriptografiaDados;
using ProtecaoSeguranca.Middleware;
using System;
using System.Linq;

namespace ProtecaoSeguranca.Extensions
{
    public static class PortalSegurancaExtensions
    {
        /// <summary>
        /// Registra as proteções do ProtecaoSeguranca na ordem correta:
        /// headers de segurança (se habilitado), ocultar erros 5xx, rate limit (se habilitado),
        /// validação de upload (se habilitado) e proteção contra SQL Injection; e liga a criptografia das propriedades [Criptografado] (se habilitado).
        /// Registrar depois do UseCors/UseAuthentication e antes do UseMvc.
        /// </summary>
        /// <example>app.UsePortalSeguranca(o => o.UsarRateLimit = true);</example>
        public static IApplicationBuilder UsePortalSeguranca(this IApplicationBuilder app,
            Action<PortalSegurancaOptions> configurar = null)
        {
            var opcoes = new PortalSegurancaOptions();
            configurar?.Invoke(opcoes);

            if (opcoes.UsarHeadersSeguranca)
                app.UseHeadersSeguranca();

            app.UseOcultarErros();

            if (opcoes.UsarRateLimit)
                app.UseRateLimit();

            if (opcoes.UsarValidacaoUpload)
                app.UseValidacaoUpload();

            if (opcoes.UsarCriptografia)
                app.UseCriptografia();

            return app.UseProtecaoSqlInjection();
        }

        /// <summary>
        /// Liga a criptografia das propriedades e parâmetros marcados com [Criptografado] em todos os endpoints MVC:
        /// descriptografa na entrada e criptografa na saída. Sem nenhum campo marcado não faz nada e não exige chave.
        /// Chaves na seção "Seguranca:Criptografia".
        /// </summary>
        public static IApplicationBuilder UseCriptografia(this IApplicationBuilder app)
        {
            // Filtro global do MVC: os endpoints são montados na primeira requisição, depois deste ponto
            var mvc = app.ApplicationServices.GetService<IOptions<MvcOptions>>()?.Value;

            if (mvc != null && !mvc.Filters.OfType<FiltroCriptografia>().Any())
                mvc.Filters.Add(new FiltroCriptografia());

            return app;
        }

        /// <summary>
        /// Adiciona headers de segurança (CSP, HSTS, X-Frame-Options...) e remove headers que revelam o servidor.
        /// Valores configuráveis na seção "Seguranca:HeadersSeguranca".
        /// </summary>
        public static IApplicationBuilder UseHeadersSeguranca(this IApplicationBuilder app)
        {
            return app.UseMiddleware<HeadersSegurancaMiddleware>();
        }

        /// <summary>
        /// Troca o corpo de respostas 5xx por mensagem genérica com código de correlação; o erro real vai para o log.
        /// </summary>
        public static IApplicationBuilder UseOcultarErros(this IApplicationBuilder app)
        {
            return app.UseMiddleware<OcultarErrosMiddleware>();
        }

        /// <summary>
        /// Recusa arquivos enviados com extensão não permitida, acima do tamanho máximo (413), com conteúdo
        /// que não corresponde à extensão ou com código de servidor embutido (400).
        /// Regras configuráveis na seção "Seguranca:UploadArquivos".
        /// </summary>
        public static IApplicationBuilder UseValidacaoUpload(this IApplicationBuilder app)
        {
            return app.UseMiddleware<UploadArquivosMiddleware>();
        }

        /// <summary>
        /// Limita requisições por IP (429). Limites configuráveis na seção "Seguranca:RateLimit".
        /// </summary>
        public static IApplicationBuilder UseRateLimit(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RateLimitMiddleware>();
        }
    }
}
