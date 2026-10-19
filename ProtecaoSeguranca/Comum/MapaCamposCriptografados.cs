using ProtecaoSeguranca.Atributos;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ProtecaoSeguranca.Comum
{
    /// <summary>
    /// Onde estão os campos [Criptografado] no JSON de um tipo: propriedades de um objeto
    /// ou, para listas e arrays, o esquema de cada item.
    /// </summary>
    internal sealed class EsquemaJson
    {
        public Dictionary<string, CampoJson> Propriedades { get; set; }
        public EsquemaJson Item { get; set; }
    }

    internal sealed class CampoJson
    {
        public bool Criptografado { get; set; }
        public EsquemaJson Filho { get; set; }
    }

    internal static class MapaCamposCriptografados
    {
        private static readonly ConcurrentDictionary<Type, EsquemaJson> Cache = new ConcurrentDictionary<Type, EsquemaJson>();

        private static readonly HashSet<Type> TiposSimples = new HashSet<Type>
        {
            typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
            typeof(Guid), typeof(Uri), typeof(object), typeof(byte[])
        };

        public static EsquemaJson Obter(Type tipo)
        {
            if (tipo == null)
                return null;

            return Cache.GetOrAdd(tipo, t => Construir(t, new HashSet<Type>()));
        }

        private static EsquemaJson Construir(Type tipo, HashSet<Type> visitando)
        {
            tipo = Nullable.GetUnderlyingType(tipo) ?? tipo;

            if (tipo.IsPrimitive || tipo.IsEnum || TiposSimples.Contains(tipo) || typeof(IDictionary).IsAssignableFrom(tipo))
                return null;

            if (!visitando.Add(tipo))
                return null;

            try
            {
                var tipoItem = ObterTipoItem(tipo);
                if (tipoItem != null)
                {
                    var esquemaItem = Construir(tipoItem, visitando);
                    return esquemaItem == null ? null : new EsquemaJson { Item = esquemaItem };
                }

                var propriedades = new Dictionary<string, CampoJson>(new ComparadorNomeJson());

                foreach (var propriedade in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (propriedade.GetIndexParameters().Length > 0)
                        continue;

                    var criptografado = propriedade.IsDefined(typeof(CriptografadoAttribute), true);
                    var filho = criptografado ? null : Construir(propriedade.PropertyType, visitando);

                    if (criptografado || filho != null)
                        propriedades[NomeJson(propriedade)] = new CampoJson { Criptografado = criptografado, Filho = filho };
                }

                return propriedades.Count == 0 ? null : new EsquemaJson { Propriedades = propriedades };
            }
            finally
            {
                visitando.Remove(tipo);
            }
        }

        private static Type ObterTipoItem(Type tipo)
        {
            if (tipo.IsArray)
                return tipo.GetElementType();

            if (!typeof(IEnumerable).IsAssignableFrom(tipo))
                return null;

            var enumeravel = tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? tipo
                : tipo.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumeravel?.GetGenericArguments()[0] ?? typeof(object);
        }

        /// <summary>
        /// Nome da propriedade no JSON: respeita [JsonProperty] (Newtonsoft), [JsonPropertyName] (System.Text.Json)
        /// e [DataMember], lidos pelo nome do atributo para não depender das bibliotecas.
        /// </summary>
        private static string NomeJson(PropertyInfo propriedade)
        {
            foreach (var atributo in propriedade.GetCustomAttributes(true))
            {
                string nomePropriedade;
                switch (atributo.GetType().Name)
                {
                    case "JsonPropertyAttribute": nomePropriedade = "PropertyName"; break;
                    case "JsonPropertyNameAttribute":
                    case "DataMemberAttribute": nomePropriedade = "Name"; break;
                    default: continue;
                }

                if (atributo.GetType().GetProperty(nomePropriedade)?.GetValue(atributo) is string nome && nome.Length > 0)
                    return nome;
            }

            return propriedade.Name;
        }

        /// <summary>
        /// Compara nomes ignorando maiúsculas, '_' e '-', para casar "Id", "id", "user_id" e "userId"
        /// qualquer que seja a política de nomes do serializador da API.
        /// </summary>
        private sealed class ComparadorNomeJson : IEqualityComparer<string>
        {
            public bool Equals(string x, string y) =>
                string.Equals(Normalizar(x), Normalizar(y), StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(string nome) =>
                StringComparer.OrdinalIgnoreCase.GetHashCode(Normalizar(nome));

            private static string Normalizar(string nome) => nome.Replace("_", string.Empty).Replace("-", string.Empty);
        }
    }
}
