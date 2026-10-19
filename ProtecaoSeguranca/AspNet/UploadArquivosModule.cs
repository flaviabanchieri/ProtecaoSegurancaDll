using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System;
using System.Linq;
using System.Web;

namespace ProtecaoSeguranca.AspNet
{
    /// <summary>
    /// Recusa arquivos enviados em multipart/form-data com extensão não permitida, tamanho acima do limite (413),
    /// conteúdo que não corresponde à extensão ou código de servidor embutido (400).
    /// Regras configuráveis no appSettings com as chaves "Seguranca:UploadArquivos:*".
    /// </summary>
    public class UploadArquivosModule : IHttpModule
    {
        private static readonly Lazy<ValidadorArquivos> Validador = new Lazy<ValidadorArquivos>(() =>
            new ValidadorArquivos(ConfiguracaoWebConfig.Ler<UploadArquivosOptions>(UploadArquivosOptions.Secao)));

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

            if (!RegrasSqlInjection.MetodosComCorpo.Contains(request.HttpMethod, StringComparer.OrdinalIgnoreCase) ||
                request.ContentType == null ||
                !request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                return;

            var validador = Validador.Value;
            HttpPostedFile recusado = null;
            string campo = null;

            var arquivos = request.Files;
            var recusa = validador.ValidarQuantidade(arquivos.Count);

            for (var i = 0; recusa == null && i < arquivos.Count; i++)
            {
                var arquivo = arquivos[i];
                var conteudo = arquivo.InputStream;
                try
                {
                    recusa = validador.Validar(request.Path, arquivo.FileName, arquivo.ContentLength, conteudo);
                }
                finally
                {
                    conteudo.Position = 0;
                }

                if (recusa != null)
                {
                    recusado = arquivo;
                    campo = arquivos.AllKeys[i];
                }
            }

            if (recusa == null)
                return;

            RegistroSeguranca.Aviso(Registro.Deteccao(validador.Modo, "Upload de arquivo recusado") +
                $" IP: {request.UserHostAddress} X-Forwarded-For: {request.Headers["X-Forwarded-For"]} " +
                $"{request.HttpMethod} {request.Path} Campo: {campo} Arquivo: {ValidadorArquivos.ParaLog(recusado?.FileName)} Motivo: {recusa.Motivo}");

            if (validador.Modo == ModoProtecao.Bloquear)
                RespostaBloqueio.Escrever(app, recusa.TamanhoExcedido ? 413 : 400, recusa.Resposta);
        }
    }
}
