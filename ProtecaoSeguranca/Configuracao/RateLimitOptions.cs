using System.Collections.Generic;

namespace ProtecaoSeguranca.Configuracao
{
    /// <summary>
    /// Limites do rate limit, lidos da seção "Seguranca:RateLimit" do appsettings (opcional).
    /// </summary>
    public class RateLimitOptions
    {
        public const string Secao = "Seguranca:RateLimit";

        public static readonly List<RegraRateLimit> RegrasPadrao = new List<RegraRateLimit>
        {
            new RegraRateLimit { Path = "/autenticacao", LimitePorMinuto = 5 }
        };

        public ModoProtecao Modo { get; set; } = ModoProtecao.Bloquear;

        /// <summary>Limite geral por IP para requisições que não se encaixam em nenhuma regra.</summary>
        public int LimitePorMinuto { get; set; } = 100;

        /// <summary>
        /// Regras específicas por trecho de path (a primeira que casar vale).
        /// Sem valor no appsettings, usa <see cref="RegrasPadrao"/>.
        /// </summary>
        public List<RegraRateLimit> Regras { get; set; }

        /// <summary>IPs que não passam pelo rate limit (ex.: servidores internos).</summary>
        public List<string> IpsLiberados { get; set; }

        public static readonly List<string> RedesConfiaveisPadrao = new List<string>
        {
           
        };

        /// <summary>
        /// Redes dos proxies reversos (ex.: Traefik) em CIDR. Só quando a conexão vem delas
        /// o IP do cliente é lido do X-Forwarded-For. Sem valor no appsettings, usa <see cref="RedesConfiaveisPadrao"/>.
        /// </summary>
        public List<string> RedesConfiaveis { get; set; }
    }

    public class RegraRateLimit
    {
        /// <summary>Trecho do path, sem diferenciar maiúsculas (ex.: "/autenticacao").</summary>
        public string Path { get; set; }

        public int LimitePorMinuto { get; set; }
    }
}
