namespace ProtecaoSeguranca.Configuracao
{
    /// <summary>
    /// Opções da proteção contra SQL Injection, lidas da seção "Seguranca:SqlInjection" do appsettings (opcional).
    /// </summary>
    public class SqlInjectionOptions
    {
        public const string Secao = "Seguranca:SqlInjection";

        public ModoProtecao Modo { get; set; } = ModoProtecao.Bloquear;
    }
}
