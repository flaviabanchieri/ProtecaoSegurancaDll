using ProtecaoSeguranca.Configuracao;
using System;
using System.Collections.Generic;

namespace ProtecaoSeguranca.Comum
{
    /// <summary>
    /// Regras da proteção contra SQL Injection, comuns ao ASP.NET Core e ao ASP.NET.
    /// </summary>
    public static class RegrasSqlInjection
    {
        public const long TamanhoMaximoCorpo = 1024 * 1024;

        public static readonly HashSet<string> CamposIgnorados =
            new HashSet<string>(new[] { "password", "senha", "token" }, StringComparer.OrdinalIgnoreCase);

        public static readonly string[] MetodosComCorpo = { "POST", "PUT", "PATCH" };

        public static string ParaLog(string valor) =>
            valor != null && valor.Length > 200 ? valor.Substring(0, 200) : valor;
    }

    /// <summary>
    /// Mensagens de log comuns às proteções.
    /// </summary>
    public static class Registro
    {
        /// <summary>
        /// Início da mensagem de log de uma detecção, conforme o modo. No modo SomenteLog o prefixo
        /// [MONITORAMENTO] permite filtrar os eventos que não foram bloqueados.
        /// </summary>
        public static string Deteccao(ModoProtecao modo, string evento) =>
            modo == ModoProtecao.SomenteLog
                ? $"[MONITORAMENTO] {evento} (não bloqueado)."
                : $"{evento}, requisição bloqueada.";
    }

    /// <summary>
    /// Corpos JSON das respostas de bloqueio.
    /// </summary>
    public static class Respostas
    {
        public const string SqlInjection = "{\"mensagem\":\"Requisição contém caracteres ou termos não permitidos.\"}";
        public const string RateLimit = "{\"mensagem\":\"Muitas requisições. Tente novamente em instantes.\"}";
        public const string CriptografiaInvalida = "{\"mensagem\":\"Dado criptografado inválido.\"}";
        public const string ArquivoNaoPermitido = "{\"mensagem\":\"Arquivo não permitido.\"}";
        public const string ArquivoMuitoGrande = "{\"mensagem\":\"Arquivo excede o tamanho máximo permitido.\"}";
        public const string HeaderCorrelacao = "X-Correlation-Id";

        public static string MensagemErroInterno(string id) => $"Ocorreu um erro interno. Código: {id}";

        public static string GerarIdErro() => Guid.NewGuid().ToString("N").Substring(0, 12);
    }
}
