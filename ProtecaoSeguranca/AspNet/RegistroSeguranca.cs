using System;
using System.Diagnostics;

namespace ProtecaoSeguranca.AspNet
{
    public enum NivelRegistro
    {
        Aviso,
        Erro
    }

    /// <summary>
    /// Destino dos logs dos módulos no ASP.NET (.NET Framework), que não tem ILogger.
    /// Por padrão escreve no System.Diagnostics.Trace; para usar o log da aplicação,
    /// trocar o destino no Application_Start do Global.asax:
    /// <code>RegistroSeguranca.Destino = (nivel, mensagem, ex) => log.Warn(mensagem, ex);</code>
    /// </summary>
    public static class RegistroSeguranca
    {
        public static Action<NivelRegistro, string, Exception> Destino { get; set; } = EscreverTrace;

        internal static void Aviso(string mensagem) => Escrever(NivelRegistro.Aviso, mensagem, null);

        internal static void Erro(string mensagem, Exception ex) => Escrever(NivelRegistro.Erro, mensagem, ex);

        private static void Escrever(NivelRegistro nivel, string mensagem, Exception ex)
        {
            try
            {
                Destino?.Invoke(nivel, mensagem, ex);
            }
            catch
            {
            }
        }

        private static void EscreverTrace(NivelRegistro nivel, string mensagem, Exception ex)
        {
            var texto = "[ProtecaoSeguranca] " + mensagem + (ex != null ? Environment.NewLine + ex : string.Empty);

            if (nivel == NivelRegistro.Erro)
                Trace.TraceError(texto);
            else
                Trace.TraceWarning(texto);
        }
    }
}
