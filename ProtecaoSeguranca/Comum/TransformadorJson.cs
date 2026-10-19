using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ProtecaoSeguranca.Comum
{
    /// <summary>
    /// Cifra e decifra os campos [Criptografado] de um JSON, percorrendo-o junto com o esquema do tipo .NET.
    /// </summary>
    internal static class TransformadorJson
    {
        /// <summary>Saída: os campos marcados viram strings "ProtecaoSeguranca..."; o resto do JSON fica como está.</summary>
        public static string CifrarCampos(string json, EsquemaJson esquema)
        {
            JToken token;
            try
            {
                token = Ler(json);
            }
            catch (JsonReaderException)
            {
                return json;
            }

            CifrarCampos(token, esquema);
            return token.ToString(Formatting.None);
        }

        /// <summary>
        /// Entrada: cada campo marcado precisa vir cifrado (ou nulo/vazio/zero, para cadastros sem id).
        /// Devolve null e o caminho do campo quando algum valor está puro ou adulterado.
        /// </summary>
        public static string DecifrarCampos(string json, EsquemaJson esquema, out string campoInvalido)
        {
            campoInvalido = null;

            JToken token;
            try
            {
                token = Ler(json);
            }
            catch (JsonReaderException)
            {
                return json;
            }

            campoInvalido = DecifrarCampos(token, esquema);
            return campoInvalido == null ? token.ToString(Formatting.None) : null;
        }

        /// <summary>Valor de rota ou query string: decifrado, ou vazio/zero, que passam como estão.</summary>
        public static bool TentarDecifrarTexto(string texto, out string valor)
        {
            valor = texto;
            if (EhVazio(texto))
                return true;

            if (!TentarDecifrarValor(texto, out var token))
                return false;

            valor = ParaTexto(token);
            return true;
        }

        public static string ParaTexto(string json) => ParaTexto(Ler(json));

        private static string ParaTexto(JToken token) =>
            token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);

        private static bool EhVazio(string texto) => string.IsNullOrEmpty(texto) || texto == "0";

        private static bool EhVazio(JToken token) =>
            token.Type == JTokenType.Null ||
            (token.Type == JTokenType.String && (string)token == string.Empty) ||
            (token.Type == JTokenType.Integer && (long)token == 0);

        private static void CifrarCampos(JToken token, EsquemaJson esquema)
        {
            if (token is JArray lista && esquema.Item != null)
            {
                foreach (var item in lista)
                    CifrarCampos(item, esquema.Item);
            }
            else if (token is JObject objeto && esquema.Propriedades != null)
            {
                foreach (var propriedade in objeto.Properties().ToList())
                {
                    if (!esquema.Propriedades.TryGetValue(propriedade.Name, out var campo))
                        continue;

                    if (campo.Criptografado)
                    {
                        if (propriedade.Value.Type != JTokenType.Null)
                            propriedade.Value = new JValue(Criptografia.Cifrar(TipoValor.Json, propriedade.Value.ToString(Formatting.None)));
                    }
                    else if (campo.Filho != null)
                    {
                        CifrarCampos(propriedade.Value, campo.Filho);
                    }
                }
            }
        }

        private static string DecifrarCampos(JToken token, EsquemaJson esquema)
        {
            if (token is JArray lista && esquema.Item != null)
            {
                foreach (var item in lista)
                {
                    var erro = DecifrarCampos(item, esquema.Item);
                    if (erro != null)
                        return erro;
                }
            }
            else if (token is JObject objeto && esquema.Propriedades != null)
            {
                foreach (var propriedade in objeto.Properties().ToList())
                {
                    if (!esquema.Propriedades.TryGetValue(propriedade.Name, out var campo))
                        continue;

                    if (campo.Criptografado)
                    {
                        if (EhVazio(propriedade.Value))
                            continue;

                        if (propriedade.Value.Type != JTokenType.String || !TentarDecifrarValor((string)propriedade.Value, out var valor))
                            return propriedade.Path;

                        propriedade.Value = valor;
                    }
                    else if (campo.Filho != null)
                    {
                        var erro = DecifrarCampos(propriedade.Value, campo.Filho);
                        if (erro != null)
                            return erro;
                    }
                }
            }

            return null;
        }

        private static bool TentarDecifrarValor(string texto, out JToken valor)
        {
            valor = null;
            if (!Criptografia.TentarDecifrar(texto, out var tipo, out var original))
                return false;

            switch (tipo)
            {
                case TipoValor.Json:
                    valor = Ler(original);
                    break;
                case TipoValor.Inteiro:
                case TipoValor.Longo:
                    valor = new JValue(long.Parse(original, CultureInfo.InvariantCulture));
                    break;
                default:
                    valor = new JValue(original);
                    break;
            }

            return true;
        }

        /// <summary>
        /// Lê sem converter datas nem trocar decimais por double, para devolver o JSON
        /// com os valores exatamente como a API os escreveu.
        /// </summary>
        private static JToken Ler(string json)
        {
            using (var leitor = new JsonTextReader(new StringReader(json))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal
            })
            {
                return JToken.ReadFrom(leitor);
            }
        }
    }
}
