namespace ProtecaoSeguranca.Configuracao
{
    /// <summary>
    /// Valores dos headers de segurança, lidos da seção "Seguranca:HeadersSeguranca" do appsettings (opcional).
    /// Cada header com valor vazio ("") não é enviado.
    /// </summary>
    public class HeadersSegurancaOptions
    {
        public const string Secao = "Seguranca:HeadersSeguranca";

        /// <summary>Impede que o navegador "adivinhe" o tipo do conteúdo (MIME sniffing).</summary>
        public string XContentTypeOptions { get; set; } = "nosniff";

        /// <summary>Impede que a resposta seja exibida dentro de iframe (clickjacking).</summary>
        public string XFrameOptions { get; set; } = "DENY";

        /// <summary>Não envia a URL de origem para outros sites.</summary>
        public string ReferrerPolicy { get; set; } = "no-referrer";

        /// <summary>
        /// Restrição máxima, adequada para APIs que só devolvem JSON.
        /// Páginas HTML servidas pela API (ex.: Swagger UI) precisam de uma política mais permissiva.
        /// </summary>
        public string ContentSecurityPolicy { get; set; } = "default-src 'none'; frame-ancestors 'none'";

        /// <summary>
        /// Obriga o navegador a usar HTTPS por 1 ano. Não é enviado em Development.
        /// Sem includeSubDomains por padrão, para não afetar subdomínios que ainda usam HTTP.
        /// </summary>
        public string StrictTransportSecurity { get; set; } = "max-age=31536000";

        /// <summary>Desliga recursos do navegador que uma API não usa.</summary>
        public string PermissionsPolicy { get; set; } = "camera=(), microphone=(), geolocation=(), payment=()";

        /// <summary>Remove headers que revelam a tecnologia do servidor (Server, X-Powered-By, X-AspNet-Version...).</summary>
        public bool RemoverHeadersServidor { get; set; } = true;
    }
}
