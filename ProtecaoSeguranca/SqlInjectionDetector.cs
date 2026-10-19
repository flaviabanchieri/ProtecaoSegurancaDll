using System;
using System.Text.RegularExpressions;

namespace ProtecaoSeguranca
{
    public static class SqlInjectionDetector
    {
        private const RegexOptions Opcoes = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

        private static readonly char[] Aspas = { '\'', '’', '‘', 'ʼ', 'ʹ', '＇' };

        private static readonly Regex Comentarios = new Regex(@"--|/\*|\*/", Opcoes, Timeout);

        private static readonly Regex ValoresCriptografados = new Regex(@"\bProtecaoSeguranca\.[A-Za-z0-9_-]+", Opcoes, Timeout);

        private static readonly Regex PontoEVirgulaComComando = new Regex(
            @";\s*(select|insert|update|delete|drop|exec|execute|waitfor|declare|truncate|alter|create|shutdown)\b", Opcoes, Timeout);

        private static readonly Regex PalavrasChave = new Regex(string.Join("|", new[]
        {
            @"\bunion\s+(all\s+)?select\b",
            @"\bselect\b.+\bfrom\b",
            @"\binsert\s+into\b",
            @"\bdelete\s+from\b",
            @"\bupdate\s+\w+\s+set\b",
            @"\b(drop|alter)\s+(table|database|view|procedure|function|user)\b",
            @"\btruncate\s+table\b",
            @"\bexec(ute)?(\s|\()",
            @"\bxp_\w+",
            @"\bsp_\w+",
            @"\bwaitfor\s+(delay|time)\b",
            @"\b(sleep|benchmark|pg_sleep)\s*\(",
            @"\bdbms_\w+",
            @"\butl_\w+",
            @"\bn?char\s*\(",
            @"\bchr\s*\(",
            @"\b(cast|convert)\s*\(",
            @"@@\w+",
            @"\binformation_schema\b",
            @"\bsys\.\w+",
            @"\bdeclare\s+@",
            // O lado direito não pode ser só '=' para não casar com o padding de Base64 ("+or+ab==")
            @"\bor\s+\S+?\s*(=|<|>|\blike\b)\s*[^\s=]",
            @"\band\s+\S+?\s*(=|<|>)\s*[^\s=]",
            @"(^|[\s(=,])0x[0-9a-f]{8,}"
        }), Opcoes, Timeout);

        /// <summary>
        /// Indica se o valor contém padrões de SQL Injection.
        /// </summary>
        /// <param name="valor">Valor recebido na requisição.</param>
        /// <param name="permitirPontoEVirgula">
        /// Quando true (textos livres no corpo), ';' só é bloqueado se seguido de comando SQL.
        /// </param>
        public static bool ContemSqlInjection(string valor, bool permitirPontoEVirgula = false)
        {
            if (string.IsNullOrEmpty(valor))
                return false;

            var texto = Normalizar(valor);

            if (texto.IndexOfAny(Aspas) >= 0)
                return true;

            if (permitirPontoEVirgula ? PontoEVirgulaComComando.IsMatch(texto) : texto.Contains(";"))
                return true;

            if (Comentarios.IsMatch(texto))
                return true;

            try
            {
                return PalavrasChave.IsMatch(texto);
            }
            catch (RegexMatchTimeoutException)
            {
                return true;
            }
        }

        private static string Normalizar(string valor)
        {
            var texto = valor;

            for (var i = 0; i < 3; i++)
            {
                string decodificado;
                try
                {
                    decodificado = Uri.UnescapeDataString(texto);
                }
                catch (UriFormatException)
                {
                    break;
                }

                if (decodificado == texto)
                    break;

                texto = decodificado;
            }

            texto = texto.Replace('+', ' ');

            try
            {
                return ValoresCriptografados.Replace(texto, "ProtecaoSeguranca");
            }
            catch (RegexMatchTimeoutException)
            {
                return texto;
            }
        }
    }
}
