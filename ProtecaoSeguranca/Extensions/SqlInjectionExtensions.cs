using Microsoft.AspNetCore.Builder;
using ProtecaoSeguranca.Middleware;

namespace ProtecaoSeguranca.Extensions
{
    public static class SqlInjectionExtensions
    {
        /// <summary>
        /// Bloqueia com 400 requisições com padrões de SQL Injection no path, query string ou corpo.
        /// Registrar depois do UseCors e antes do UseMvc.
        /// </summary>
        public static IApplicationBuilder UseProtecaoSqlInjection(this IApplicationBuilder app)
        {
            return app.UseMiddleware<SqlInjectionMiddleware>();
        }
    }
}
