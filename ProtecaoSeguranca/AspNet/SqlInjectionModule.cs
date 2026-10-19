using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace ProtecaoSeguranca.AspNet
{
    /// <summary>
    /// Bloqueia com 400 requisições com padrões de SQL Injection no path, query string ou corpo (JSON/formulário).
    /// </summary>
    public class SqlInjectionModule : IHttpModule
    {
        private static readonly Lazy<ModoProtecao> Modo = new Lazy<ModoProtecao>(() =>
            ConfiguracaoWebConfig.Ler<SqlInjectionOptions>(SqlInjectionOptions.Secao).Modo);

        public void Init(HttpApplication app)
        {
            app.BeginRequest += Validar;
        }

        public void Dispose()
        {
        }

        private static void Validar(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var request = app.Request;

            if (request.HttpMethod == "OPTIONS")
                return;

            var suspeito = ValidarPath(request)
                ?? ValidarColecao(request.Unvalidated.QueryString, false)
                ?? ValidarCorpo(request);

            if (suspeito == null)
                return;

            RegistroSeguranca.Aviso(Registro.Deteccao(Modo.Value, "Possível SQL Injection") +
                $" IP: {request.UserHostAddress} X-Forwarded-For: {request.Headers["X-Forwarded-For"]} " +
                $"{request.HttpMethod} {request.Path} Campo: {suspeito.Campo} Valor: {RegrasSqlInjection.ParaLog(suspeito.Valor)}");

            if (Modo.Value == ModoProtecao.Bloquear)
                RespostaBloqueio.Escrever(app, 400, Respostas.SqlInjection);
        }

        private static Suspeita ValidarPath(HttpRequest request)
        {
            return SqlInjectionDetector.ContemSqlInjection(request.Path) ? new Suspeita("path", request.Path) : null;
        }

        /// <summary>
        /// Query string ou formulário. Usa Request.Unvalidated para não disparar a validação
        /// de requisição do ASP.NET (HttpRequestValidationException) antes da hora.
        /// </summary>
        private static Suspeita ValidarColecao(NameValueCollection colecao, bool corpo)
        {
            foreach (var chave in colecao.AllKeys)
            {
                if (corpo && chave != null && RegrasSqlInjection.CamposIgnorados.Contains(chave))
                    continue;

                if (SqlInjectionDetector.ContemSqlInjection(chave, corpo))
                    return new Suspeita(chave, chave);

                foreach (var valor in colecao.GetValues(chave) ?? new string[0])
                {
                    if (SqlInjectionDetector.ContemSqlInjection(valor, corpo))
                        return new Suspeita(chave, valor);
                }
            }

            return null;
        }

        private static Suspeita ValidarCorpo(HttpRequest request)
        {
            if (!RegrasSqlInjection.MetodosComCorpo.Contains(request.HttpMethod, StringComparer.OrdinalIgnoreCase))
                return null;

            if (request.ContentLength > RegrasSqlInjection.TamanhoMaximoCorpo)
                return null;

            var tipo = request.ContentType ?? string.Empty;

            if (tipo.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) ||
                tipo.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                return ValidarColecao(request.Unvalidated.Form, true);

            if (tipo.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                return ValidarJson(request);

            return null;
        }

        private static Suspeita ValidarJson(HttpRequest request)
        {
            var stream = request.InputStream;
            string corpo;
            using (var leitor = new StreamReader(stream, request.ContentEncoding ?? Encoding.UTF8, true, 4096, leaveOpen: true))
            {
                corpo = leitor.ReadToEnd();
            }
            stream.Position = 0;

            if (string.IsNullOrWhiteSpace(corpo) || corpo.Length > RegrasSqlInjection.TamanhoMaximoCorpo)
                return null;

            object json;
            try
            {
                json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(corpo);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            return ValidarValorJson(json, "$");
        }

        private static Suspeita ValidarValorJson(object valor, string caminho)
        {
            switch (valor)
            {
                case string texto:
                    return SqlInjectionDetector.ContemSqlInjection(texto, permitirPontoEVirgula: true)
                        ? new Suspeita(caminho, texto)
                        : null;

                case IDictionary<string, object> objeto:
                    foreach (var propriedade in objeto)
                    {
                        if (RegrasSqlInjection.CamposIgnorados.Contains(propriedade.Key))
                            continue;

                        if (SqlInjectionDetector.ContemSqlInjection(propriedade.Key, permitirPontoEVirgula: true))
                            return new Suspeita(propriedade.Key, propriedade.Key);

                        var resultado = ValidarValorJson(propriedade.Value, caminho + "." + propriedade.Key);
                        if (resultado != null)
                            return resultado;
                    }
                    return null;

                case object[] lista:
                    for (var i = 0; i < lista.Length; i++)
                    {
                        var resultado = ValidarValorJson(lista[i], $"{caminho}[{i}]");
                        if (resultado != null)
                            return resultado;
                    }
                    return null;

                default:
                    return null;
            }
        }

        private class Suspeita
        {
            public Suspeita(string campo, string valor)
            {
                Campo = campo;
                Valor = valor;
            }

            public string Campo { get; }
            public string Valor { get; }
        }
    }
}
