using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System;
using System.Globalization;
using System.Security.Cryptography;
#if NETFRAMEWORK
using ProtecaoSeguranca.AspNet;
#else
using Microsoft.Extensions.Configuration;
#endif

namespace ProtecaoSeguranca
{
    /// <summary>
    /// Criptografia de dados entregues ao front. Normalmente usada pelas propriedades marcadas
    /// com [Criptografado]; também pode ser chamada direto.
    /// Os valores gerados começam com "ProtecaoSeguranca." e mudam a cada chamada, mesmo para o mesmo dado.
    /// </summary>
    public static class Criptografia
    {
        private static volatile MotorCriptografia _motor;
#if NETFRAMEWORK
        private static readonly object Trava = new object();
#endif

        /// <summary>Define as chaves. Lança InvalidOperationException se a chave faltar ou for inválida.</summary>
        public static void Configurar(CriptografiaOptions opcoes)
        {
            _motor = new MotorCriptografia(opcoes);
        }

#if !NETFRAMEWORK
        /// <summary>
        /// Define as chaves a partir da seção "Seguranca:Criptografia" do appsettings.
        /// As propriedades [Criptografado] fazem isso sozinhas na primeira requisição; chamar na inicialização
        /// só é necessário para usar Criptografar/Descriptografar antes disso.
        /// </summary>
        public static void Configurar(IConfiguration configuracao)
        {
            Configurar(configuracao.GetSection(CriptografiaOptions.Secao).Get<CriptografiaOptions>() ?? new CriptografiaOptions());
        }
#endif

        internal static bool Configurada => _motor != null;

        /// <summary>Gera uma chave nova (32 bytes aleatórios em base64) para "Seguranca:Criptografia:Chave".</summary>
        public static string GerarChave()
        {
            var bytes = new byte[32];
            using (var aleatorio = RandomNumberGenerator.Create())
            {
                aleatorio.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        public static string Criptografar(string valor) =>
            valor == null ? null : Motor.Cifrar(TipoValor.Texto, valor);

        public static string Criptografar(int valor) =>
            Motor.Cifrar(TipoValor.Inteiro, valor.ToString(CultureInfo.InvariantCulture));

        public static string Criptografar(long valor) =>
            Motor.Cifrar(TipoValor.Longo, valor.ToString(CultureInfo.InvariantCulture));

        public static string Criptografar(Guid valor) =>
            Motor.Cifrar(TipoValor.Guid, valor.ToString("D"));

        /// <summary>
        /// Devolve o valor original como texto (números em formato invariante).
        /// Lança FormatException se o valor foi adulterado ou não foi criptografado com uma das chaves configuradas.
        /// </summary>
        public static string Descriptografar(string texto)
        {
            if (!TentarDescriptografar(texto, out var valor))
                throw new FormatException("Dado criptografado inválido.");

            return valor;
        }

        public static bool TentarDescriptografar(string texto, out string valor)
        {
            valor = null;
            if (!Motor.TentarDecifrar(texto, out var tipo, out var original))
                return false;

            valor = tipo == TipoValor.Json ? TransformadorJson.ParaTexto(original) : original;
            return true;
        }

        internal static string Cifrar(TipoValor tipo, string valor) => Motor.Cifrar(tipo, valor);

        internal static bool TentarDecifrar(string texto, out TipoValor tipo, out string valor) =>
            Motor.TentarDecifrar(texto, out tipo, out valor);

        private static MotorCriptografia Motor
        {
            get
            {
                var motor = _motor;
                if (motor != null)
                    return motor;

#if NETFRAMEWORK
                lock (Trava)
                {
                    if (_motor == null)
                        _motor = new MotorCriptografia(ConfiguracaoWebConfig.Ler<CriptografiaOptions>(CriptografiaOptions.Secao));

                    return _motor;
                }
#else
                throw new InvalidOperationException(
                    "Criptografia não configurada. Chame Criptografia.Configurar(Configuration) na inicialização " +
                    "(as propriedades [Criptografado] configuram sozinhas na primeira requisição).");
#endif
            }
        }
    }
}
