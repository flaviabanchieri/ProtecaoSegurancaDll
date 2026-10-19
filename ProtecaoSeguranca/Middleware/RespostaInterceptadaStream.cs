using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtecaoSeguranca.Middleware
{
    /// <summary>
    /// Stream que, na primeira escrita, decide pelo status da resposta:
    /// abaixo de 500 repassa direto ao stream original (sem buffer);
    /// a partir de 500 captura o corpo em memória para ser substituído.
    /// </summary>
    public class RespostaInterceptadaStream : Stream
    {
        private readonly Stream _original;
        private readonly HttpResponse _response;
        private readonly int _limiteCaptura;
        private readonly MemoryStream _capturado = new MemoryStream();
        private bool? _capturando;

        public RespostaInterceptadaStream(Stream original, HttpResponse response, int limiteCaptura)
        {
            _original = original;
            _response = response;
            _limiteCaptura = limiteCaptura;
        }

        public bool Capturou => _capturando == true;

        public byte[] ObterCapturado() => _capturado.ToArray();

        private bool Capturando
        {
            get
            {
                if (_capturando == null)
                    _capturando = _response.StatusCode >= StatusCodes.Status500InternalServerError;

                return _capturando.Value;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Capturando)
                Capturar(buffer, offset, count);
            else
                _original.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!Capturando)
                return _original.WriteAsync(buffer, offset, count, cancellationToken);

            Capturar(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override void Flush()
        {
            if (!Capturando)
                _original.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Capturando ? Task.CompletedTask : _original.FlushAsync(cancellationToken);
        }

        private void Capturar(byte[] buffer, int offset, int count)
        {
            var restante = _limiteCaptura - (int)_capturado.Length;
            if (restante > 0)
                _capturado.Write(buffer, offset, Math.Min(count, restante));
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _capturado.Dispose();

            base.Dispose(disposing);
        }
    }
}
