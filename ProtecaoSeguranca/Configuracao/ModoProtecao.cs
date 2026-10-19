namespace ProtecaoSeguranca.Configuracao
{
    /// <summary>
    /// O que uma proteção faz ao detectar uma requisição suspeita.
    /// </summary>
    public enum ModoProtecao
    {
        /// <summary>Registra no log e bloqueia a requisição (padrão).</summary>
        Bloquear,

        /// <summary>
        /// Só registra no log, com o prefixo [MONITORAMENTO], e deixa a requisição seguir.
        /// Usado para medir falsos positivos em produção antes de ligar o bloqueio.
        /// </summary>
        SomenteLog
    }
}
