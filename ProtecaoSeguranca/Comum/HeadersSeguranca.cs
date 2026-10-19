using ProtecaoSeguranca.Configuracao;
using System.Collections.Generic;
using System.Linq;

namespace ProtecaoSeguranca.Comum
{
    /// <summary>
    /// Headers de segurança a adicionar e headers do servidor a remover, comum ao ASP.NET Core e ao ASP.NET.
    /// </summary>
    public class HeadersSeguranca
    {
        public static readonly string[] HeadersServidor =
        {
            "Server", "X-Powered-By", "X-AspNet-Version", "X-AspNetMvc-Version"
        };

        private readonly List<KeyValuePair<string, string>> _comHsts;
        private readonly List<KeyValuePair<string, string>> _semHsts;

        public HeadersSeguranca(HeadersSegurancaOptions opcoes)
        {
            RemoverHeadersServidor = opcoes.RemoverHeadersServidor;

            var headers = new Dictionary<string, string>
            {
                ["X-Content-Type-Options"] = opcoes.XContentTypeOptions,
                ["X-Frame-Options"] = opcoes.XFrameOptions,
                ["Referrer-Policy"] = opcoes.ReferrerPolicy,
                ["Content-Security-Policy"] = opcoes.ContentSecurityPolicy,
                ["Permissions-Policy"] = opcoes.PermissionsPolicy
            };

            _semHsts = headers.Where(h => !string.IsNullOrWhiteSpace(h.Value)).ToList();

            _comHsts = new List<KeyValuePair<string, string>>(_semHsts);
            if (!string.IsNullOrWhiteSpace(opcoes.StrictTransportSecurity))
                _comHsts.Add(new KeyValuePair<string, string>("Strict-Transport-Security", opcoes.StrictTransportSecurity));
        }

        public bool RemoverHeadersServidor { get; }

        /// <summary>
        /// Headers a adicionar. Em desenvolvimento o HSTS fica de fora, porque faria o navegador
        /// exigir HTTPS em localhost para qualquer projeto.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Obter(bool desenvolvimento) =>
            desenvolvimento ? _semHsts : _comHsts;
    }
}
