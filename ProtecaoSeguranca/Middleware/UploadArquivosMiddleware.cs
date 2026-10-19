using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProtecaoSeguranca.Comum;
using ProtecaoSeguranca.Configuracao;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtecaoSeguranca.Middleware
{
    /// <summary>
    /// Recusa arquivos enviados em multipart/form-data com extensão não permitida, tamanho acima do limite,
    /// conteúdo que não corresponde à extensão ou código de servidor embutido (ex.: um .php disfarçado de imagem).
    /// </summary>
    public class UploadArquivosMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UploadArquivosMiddleware> _logger;
        private readonly ValidadorArquivos _validador;

        public UploadArquivosMiddleware(RequestDelegate next, ILogger<UploadArquivosMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _validador = new ValidadorArquivos(
                configuration.GetSection(UploadArquivosOptions.Secao).Get<UploadArquivosOptions>() ?? new UploadArquivosOptions());
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var request = httpContext.Request;

            if (!RegrasSqlInjection.MetodosComCorpo.Contains(request.Method, StringComparer.OrdinalIgnoreCase) ||
                request.ContentType == null ||
                !request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                await _next(httpContext);
                return;
            }

            IFormCollection form;
            try
            {
                // O formulário fica em cache no HttpContext: o model binding reaproveita a leitura
                form = await request.ReadFormAsync();
            }
            catch (InvalidDataException)
            {
                await _next(httpContext);
                return;
            }

            var recusa = Validar(request, form.Files, out var arquivo);

            if (recusa != null)
            {
                RegistrarTentativa(httpContext, arquivo, recusa);

                if (_validador.Modo == ModoProtecao.Bloquear)
                {
                    httpContext.Response.StatusCode = recusa.TamanhoExcedido
                        ? StatusCodes.Status413PayloadTooLarge
                        : StatusCodes.Status400BadRequest;
                    httpContext.Response.ContentType = "application/json; charset=utf-8";
                    await httpContext.Response.WriteAsync(recusa.Resposta, Encoding.UTF8);
                    return;
                }
            }

            await _next(httpContext);
        }

        private ArquivoRecusado Validar(HttpRequest request, IFormFileCollection arquivos, out IFormFile recusado)
        {
            recusado = null;

            var quantidade = _validador.ValidarQuantidade(arquivos.Count);
            if (quantidade != null)
                return quantidade;

            foreach (var arquivo in arquivos)
            {
                ArquivoRecusado recusa;
                using (var conteudo = arquivo.OpenReadStream())
                {
                    recusa = _validador.Validar(request.Path.Value, arquivo.FileName, arquivo.Length, conteudo);
                }

                if (recusa != null)
                {
                    recusado = arquivo;
                    return recusa;
                }
            }

            return null;
        }

        private void RegistrarTentativa(HttpContext httpContext, IFormFile arquivo, ArquivoRecusado recusa)
        {
            var request = httpContext.Request;
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            var forwardedFor = request.Headers["X-Forwarded-For"].ToString();

            _logger.LogWarning(
                Registro.Deteccao(_validador.Modo, "Upload de arquivo recusado") + " IP: {Ip} X-Forwarded-For: {ForwardedFor} {Metodo} {Path} Campo: {Campo} Arquivo: {Arquivo} Motivo: {Motivo}",
                ip, forwardedFor, request.Method, request.Path.Value,
                arquivo?.Name, ValidadorArquivos.ParaLog(arquivo?.FileName), recusa.Motivo);
        }
    }
}
