using ProtecaoSeguranca.Configuracao;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace ProtecaoSeguranca.Comum
{
    /// <summary>
    /// Contagem do rate limit por IP em janela fixa de 1 minuto, comum ao ASP.NET Core e ao ASP.NET.
    /// Atrás de proxy reverso (Traefik), o IP do cliente vem do X-Forwarded-For,
    /// mas só quando a conexão chega de uma rede confiável; caso contrário o header é ignorado.
    /// </summary>
    public class LimitadorRequisicoes
    {
        private const string ChaveGeral = "*";
        private static readonly long Janela = TimeSpan.FromMinutes(1).Ticks;

        private readonly int _limiteGeral;
        private readonly List<RegraRateLimit> _regras;
        private readonly HashSet<string> _ipsLiberados;
        private readonly List<RedeIp> _redesConfiaveis = new List<RedeIp>();
        private readonly ConcurrentDictionary<string, Contador> _contadores = new ConcurrentDictionary<string, Contador>();
        private long _ultimaLimpeza = DateTime.UtcNow.Ticks;

        /// <param name="opcoes">Limites configurados.</param>
        /// <param name="redeInvalida">Chamado para cada rede confiável com CIDR inválido (para log).</param>
        public LimitadorRequisicoes(RateLimitOptions opcoes, Action<string> redeInvalida)
        {
            Modo = opcoes.Modo;
            _limiteGeral = opcoes.LimitePorMinuto;
            _regras = (opcoes.Regras ?? RateLimitOptions.RegrasPadrao)
                .Where(r => !string.IsNullOrWhiteSpace(r.Path))
                .ToList();
            _ipsLiberados = new HashSet<string>(opcoes.IpsLiberados ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var cidr in opcoes.RedesConfiaveis ?? RateLimitOptions.RedesConfiaveisPadrao)
            {
                if (RedeIp.TryParse(cidr, out var rede))
                    _redesConfiaveis.Add(rede);
                else
                    redeInvalida?.Invoke(cidr);
            }
        }

        public ModoProtecao Modo { get; }

        public bool Liberado(string ip) => _ipsLiberados.Contains(ip);

        /// <summary>
        /// Conta a requisição do IP e indica se o limite da janela atual foi excedido.
        /// </summary>
        public ResultadoRateLimit Registrar(string ip, string path)
        {
            var regra = _regras.FirstOrDefault(r =>
                path != null && path.IndexOf(r.Path, StringComparison.OrdinalIgnoreCase) >= 0);
            var resultado = new ResultadoRateLimit { Limite = regra?.LimitePorMinuto ?? _limiteGeral };
            var agora = DateTime.UtcNow.Ticks;

            LimparExpirados(agora);

            var contador = _contadores.GetOrAdd($"{ip}|{regra?.Path ?? ChaveGeral}", _ => new Contador(agora));

            lock (contador)
            {
                if (agora - contador.InicioJanela >= Janela)
                {
                    contador.InicioJanela = agora;
                    contador.Quantidade = 0;
                    contador.Registrado = false;
                }

                contador.Quantidade++;
                resultado.Excedeu = contador.Quantidade > resultado.Limite;

                resultado.RegistrarLog = resultado.Excedeu && !contador.Registrado;
                if (resultado.RegistrarLog)
                    contador.Registrado = true;

                var segundosRestantes = (long)Math.Ceiling(TimeSpan.FromTicks(contador.InicioJanela + Janela - agora).TotalSeconds);
                resultado.SegundosRestantes = Math.Max(segundosRestantes, 1);
            }

            return resultado;
        }

        public string ObterIp(IPAddress remoto, string forwardedFor)
        {
            if (remoto == null)
                return "desconhecido";

            if (!EhConfiavel(remoto))
                return Formatar(remoto);

            // O proxy acrescenta o IP de quem o chamou no fim do header: lendo da direita para a
            // esquerda, o primeiro IP fora das redes confiáveis é o cliente. O que estiver à
            // esquerda dele foi enviado pelo próprio cliente e pode ser falsificado.
            var encaminhados = (forwardedFor ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            var cliente = remoto;
            for (var i = encaminhados.Length - 1; i >= 0; i--)
            {
                if (!IPAddress.TryParse(encaminhados[i].Trim(), out var ip))
                    break;

                cliente = ip;
                if (!EhConfiavel(ip))
                    break;
            }

            return Formatar(cliente);
        }

        private bool EhConfiavel(IPAddress ip) => _redesConfiaveis.Any(r => r.Contem(ip));

        private static string Formatar(IPAddress ip) =>
            ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();

        private void LimparExpirados(long agora)
        {
            var ultima = Interlocked.Read(ref _ultimaLimpeza);
            if (agora - ultima < Janela || Interlocked.CompareExchange(ref _ultimaLimpeza, agora, ultima) != ultima)
                return;

            foreach (var item in _contadores)
            {
                if (agora - item.Value.InicioJanela >= Janela)
                    _contadores.TryRemove(item.Key, out _);
            }
        }

        private class Contador
        {
            public Contador(long inicioJanela)
            {
                InicioJanela = inicioJanela;
            }

            public long InicioJanela;
            public int Quantidade;
            public bool Registrado;
        }
    }

    public class ResultadoRateLimit
    {
        public bool Excedeu { get; set; }

        public bool RegistrarLog { get; set; }

        public int Limite { get; set; }

        public long SegundosRestantes { get; set; }
    }
}
