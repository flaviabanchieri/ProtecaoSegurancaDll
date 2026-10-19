using ProtecaoSeguranca.Configuracao;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace ProtecaoSeguranca.AspNet
{
    /// <summary>
    /// Lê as opções do appSettings do web.config, com as mesmas chaves do appsettings.json do ASP.NET Core
    /// (ex.: "Seguranca:RateLimit:LimitePorMinuto"). Listas são separadas por vírgula e
    /// regras do rate limit no formato "path=limite" (ex.: "/autenticacao=5,/login=10") e
    /// regras do upload no formato "path=extensões|tamanho" (ex.: "/documentos=.pdf|.docx|5MB").
    /// </summary>
    internal static class ConfiguracaoWebConfig
    {
        private static readonly char[] Separadores = { ',', ';' };

        public static T Ler<T>(string secao) where T : new()
        {
            var opcoes = new T();

            foreach (var propriedade in typeof(T).GetProperties().Where(p => p.CanWrite))
            {
                var chave = secao + ":" + propriedade.Name;
                var valor = ConfigurationManager.AppSettings[chave];
                if (valor == null)
                    continue;

                var tipo = propriedade.PropertyType;

                if (tipo == typeof(string))
                    propriedade.SetValue(opcoes, valor);
                else if (tipo == typeof(int) && int.TryParse(valor, out var numero))
                    propriedade.SetValue(opcoes, numero);
                else if (tipo == typeof(bool) && bool.TryParse(valor, out var logico))
                    propriedade.SetValue(opcoes, logico);
                else if (tipo.IsEnum && Enum.GetNames(tipo).Contains(valor.Trim(), StringComparer.OrdinalIgnoreCase))
                    propriedade.SetValue(opcoes, Enum.Parse(tipo, valor.Trim(), true));
                else if (tipo == typeof(List<string>))
                    propriedade.SetValue(opcoes, Dividir(valor).ToList());
                else if (tipo == typeof(List<RegraRateLimit>))
                    propriedade.SetValue(opcoes, LerRegras(chave, valor));
                else if (tipo == typeof(List<RegraUpload>))
                    propriedade.SetValue(opcoes, LerRegrasUpload(chave, valor));
                else
                    RegistroSeguranca.Aviso($"Valor inválido no appSettings ignorado: {chave}");
            }

            return opcoes;
        }

        public static bool LerBool(string chave, bool padrao)
        {
            return bool.TryParse(ConfigurationManager.AppSettings[chave], out var valor) ? valor : padrao;
        }

        private static IEnumerable<string> Dividir(string valor) =>
            valor.Split(Separadores, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0);

        private static List<RegraRateLimit> LerRegras(string chave, string valor)
        {
            var regras = new List<RegraRateLimit>();

            foreach (var item in Dividir(valor))
            {
                var partes = item.Split('=');
                if (partes.Length == 2 && int.TryParse(partes[1].Trim(), out var limite))
                    regras.Add(new RegraRateLimit { Path = partes[0].Trim(), LimitePorMinuto = limite });
                else
                    RegistroSeguranca.Aviso($"Regra inválida ignorada em {chave}: {item}");
            }

            return regras;
        }

        /// <summary>
        /// "path=.pdf|.docx|5MB": extensões separadas por "|" e, opcionalmente, o tamanho máximo com sufixo MB.
        /// </summary>
        private static List<RegraUpload> LerRegrasUpload(string chave, string valor)
        {
            var regras = new List<RegraUpload>();

            foreach (var item in Dividir(valor))
            {
                var partes = item.Split('=');
                if (partes.Length != 2 || partes[0].Trim().Length == 0)
                {
                    RegistroSeguranca.Aviso($"Regra inválida ignorada em {chave}: {item}");
                    continue;
                }

                var regra = new RegraUpload { Path = partes[0].Trim(), ExtensoesPermitidas = new List<string>() };

                foreach (var parte in partes[1].Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
                {
                    if (parte.EndsWith("MB", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(parte.Substring(0, parte.Length - 2).Trim(), out var tamanho))
                        regra.TamanhoMaximoMb = tamanho;
                    else
                        regra.ExtensoesPermitidas.Add(parte);
                }

                regras.Add(regra);
            }

            return regras;
        }
    }
}
