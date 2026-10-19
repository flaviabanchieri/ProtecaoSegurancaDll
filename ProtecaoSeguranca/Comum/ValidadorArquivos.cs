using ProtecaoSeguranca.Configuracao;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProtecaoSeguranca.Comum
{
    /// <summary>
    /// Validação de arquivos enviados, comum ao ASP.NET Core e ao ASP.NET:
    /// nome, extensão (lista branca e lista negra), tamanho, assinatura do formato e scripts embutidos.
    /// </summary>
    public class ValidadorArquivos
    {
        private const int TamanhoBuffer = 64 * 1024;

        private readonly UploadArquivosOptions _opcoes;
        private readonly HashSet<string> _permitidas;
        private readonly bool _qualquerExtensao;
        private readonly List<string> _bloqueadas;
        private readonly List<RegraCompilada> _regras;

        public ValidadorArquivos(UploadArquivosOptions opcoes)
        {
            _opcoes = opcoes;
            _qualquerExtensao = ContemQualquer(opcoes.ExtensoesPermitidas);
            _permitidas = Normalizar(opcoes.ExtensoesPermitidas ?? UploadArquivosOptions.ExtensoesPermitidasPadrao);
            _bloqueadas = Normalizar(opcoes.ExtensoesBloqueadas ?? UploadArquivosOptions.ExtensoesBloqueadasPadrao).ToList();
            _regras = (opcoes.Regras ?? new List<RegraUpload>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Path))
                .Select(r => new RegraCompilada(r))
                .ToList();
        }

        public ModoProtecao Modo => _opcoes.Modo;

        /// <summary>Retorna null se a quantidade de arquivos da requisição está dentro do limite.</summary>
        public ArquivoRecusado ValidarQuantidade(int quantidade) =>
            _opcoes.QuantidadeMaximaArquivos > 0 && quantidade > _opcoes.QuantidadeMaximaArquivos
                ? ArquivoRecusado.Quantidade(quantidade, _opcoes.QuantidadeMaximaArquivos)
                : null;

        /// <summary>
        /// Valida um arquivo. Retorna null se está liberado; caso contrário, o motivo da recusa.
        /// O stream é lido a partir da posição atual e não é fechado.
        /// </summary>
        public ArquivoRecusado Validar(string path, string nomeArquivo, long tamanho, Stream conteudo)
        {
            var regra = _regras.FirstOrDefault(r => path != null && path.IndexOf(r.Path, StringComparison.OrdinalIgnoreCase) >= 0);

            var nome = ExtrairNome(nomeArquivo);
            if (nome == null)
                return ArquivoRecusado.Tipo("nome de arquivo inválido");

            var extensao = ObterExtensao(nome);
            if (extensao == null)
                return ArquivoRecusado.Tipo("arquivo sem extensão");

            var bloqueada = _bloqueadas.FirstOrDefault(b => nome.EndsWith(b, StringComparison.OrdinalIgnoreCase) ||
                                                           nome.IndexOf(b + ".", StringComparison.OrdinalIgnoreCase) >= 0);
            if (bloqueada != null)
                return ArquivoRecusado.Tipo($"extensão bloqueada ({bloqueada})");

            var aceitaQualquer = regra != null ? regra.QualquerExtensao : _qualquerExtensao;
            var permitidas = regra?.Permitidas ?? _permitidas;
            if (!aceitaQualquer && !permitidas.Contains(extensao))
                return ArquivoRecusado.Tipo($"extensão não permitida ({extensao})");

            var tamanhoMaximoMb = regra?.TamanhoMaximoMb ?? _opcoes.TamanhoMaximoMb;
            if (tamanhoMaximoMb > 0 && tamanho > tamanhoMaximoMb * 1024L * 1024L)
                return ArquivoRecusado.Tamanho($"{tamanho} bytes, máximo {tamanhoMaximoMb} MB");

            if (conteudo == null || (!_opcoes.ValidarAssinatura && !_opcoes.ProcurarScripts))
                return null;

            return ValidarConteudo(extensao, conteudo);
        }

        /// <summary>Nome do arquivo para o log: sem quebras de linha (que forjariam linhas no log) e truncado.</summary>
        public static string ParaLog(string nomeArquivo) =>
            nomeArquivo == null ? null : RegrasSqlInjection.ParaLog(new string(nomeArquivo.Select(c => char.IsControl(c) ? '?' : c).ToArray()));

        private ArquivoRecusado ValidarConteudo(string extensao, Stream conteudo)
        {
            var inicio = new byte[AssinaturasArquivo.TamanhoCabecalho];
            var lidos = LerInicio(conteudo, inicio);
            var cabecalho = new ArraySegment<byte>(inicio, 0, lidos);

            var formatoBinario = AssinaturasArquivo.Conhecida(extensao);

            if (_opcoes.ValidarAssinatura)
            {
                if (formatoBinario && !AssinaturasArquivo.Confere(extensao, cabecalho))
                    return ArquivoRecusado.Tipo($"conteúdo não corresponde à extensão {extensao}");

                var executavel = AssinaturasArquivo.Executavel(cabecalho);
                if (executavel != null)
                    return ArquivoRecusado.Tipo($"conteúdo de {executavel}");
            }

            if (!_opcoes.ProcurarScripts)
                return null;

            // Em binários, assinaturas curtas ("<?=") aparecem por acaso; ali só procura as longas
            var assinaturas = formatoBinario ? AssinaturasArquivo.ScriptsLongos : AssinaturasArquivo.Scripts;
            var script = ProcurarScript(conteudo, inicio, lidos, assinaturas);

            return script != null ? ArquivoRecusado.Tipo($"código embutido ({script})") : null;
        }

        private static int LerInicio(Stream conteudo, byte[] buffer)
        {
            var total = 0;
            int lidos;
            while (total < buffer.Length && (lidos = conteudo.Read(buffer, total, buffer.Length - total)) > 0)
                total += lidos;
            return total;
        }

        /// <summary>
        /// Percorre o arquivo em blocos, mantendo uma sobra do bloco anterior para achar trechos que caem na divisa.
        /// </summary>
        private static string ProcurarScript(Stream conteudo, byte[] inicio, int lidosInicio, byte[][] assinaturas)
        {
            var sobra = assinaturas.Max(a => a.Length) - 1;
            var buffer = new byte[TamanhoBuffer + sobra];

            Buffer.BlockCopy(inicio, 0, buffer, 0, lidosInicio);
            var preenchido = lidosInicio;

            while (true)
            {
                int lidos;
                while (preenchido < buffer.Length && (lidos = conteudo.Read(buffer, preenchido, buffer.Length - preenchido)) > 0)
                    preenchido += lidos;

                var encontrada = assinaturas.FirstOrDefault(a => Contem(buffer, preenchido, a));
                if (encontrada != null)
                    return Encoding.ASCII.GetString(encontrada);

                if (preenchido < buffer.Length)
                    return null;

                Buffer.BlockCopy(buffer, preenchido - sobra, buffer, 0, sobra);
                preenchido = sobra;
            }
        }

        /// <summary>Busca sem diferenciar maiúsculas (a assinatura está em minúsculas).</summary>
        private static bool Contem(byte[] buffer, int tamanho, byte[] assinatura)
        {
            var primeiro = assinatura[0];
            for (var i = 0; i <= tamanho - assinatura.Length; i++)
            {
                if (Minuscula(buffer[i]) != primeiro)
                    continue;

                var j = 1;
                while (j < assinatura.Length && Minuscula(buffer[i + j]) == assinatura[j])
                    j++;

                if (j == assinatura.Length)
                    return true;
            }
            return false;
        }

        private static byte Minuscula(byte b) => b >= (byte)'A' && b <= (byte)'Z' ? (byte)(b + 32) : b;

        /// <summary>
        /// Nome sem o caminho que alguns navegadores enviam. Recusa caracteres de controle e ":"
        /// (fluxos alternativos do NTFS, como "shell.php::$DATA").
        /// </summary>
        private static string ExtrairNome(string nomeArquivo)
        {
            if (string.IsNullOrWhiteSpace(nomeArquivo))
                return null;

            var nome = nomeArquivo.Trim().Trim('"');
            nome = nome.Substring(Math.Max(nome.LastIndexOf('/'), nome.LastIndexOf('\\')) + 1);

            // O Windows ignora pontos e espaços no fim: "shell.php." vira "shell.php"
            nome = nome.TrimEnd('.', ' ');

            if (nome.Length == 0 || nome.Any(c => char.IsControl(c) || c == ':'))
                return null;

            return nome;
        }

        private static string ObterExtensao(string nome)
        {
            var ponto = nome.LastIndexOf('.');
            return ponto >= 0 && ponto < nome.Length - 1 ? nome.Substring(ponto).ToLowerInvariant() : null;
        }

        private static bool ContemQualquer(IEnumerable<string> extensoes) =>
            extensoes != null && extensoes.Any(e => e != null && e.Trim() == UploadArquivosOptions.QualquerExtensao);

        private static HashSet<string> Normalizar(IEnumerable<string> extensoes) =>
            new HashSet<string>(
                extensoes
                    .Where(e => !string.IsNullOrWhiteSpace(e) && e.Trim() != UploadArquivosOptions.QualquerExtensao)
                    .Select(e => e.Trim().ToLowerInvariant())
                    .Select(e => e.StartsWith(".") ? e : "." + e),
                StringComparer.OrdinalIgnoreCase);

        private class RegraCompilada
        {
            public RegraCompilada(RegraUpload regra)
            {
                Path = regra.Path.Trim();
                TamanhoMaximoMb = regra.TamanhoMaximoMb;

                if (regra.ExtensoesPermitidas != null && regra.ExtensoesPermitidas.Count > 0)
                {
                    QualquerExtensao = ContemQualquer(regra.ExtensoesPermitidas);
                    Permitidas = Normalizar(regra.ExtensoesPermitidas);
                }
            }

            public string Path { get; }
            public HashSet<string> Permitidas { get; }
            public bool QualquerExtensao { get; }
            public int? TamanhoMaximoMb { get; }
        }
    }

    public class ArquivoRecusado
    {
        private ArquivoRecusado(string motivo, bool tamanhoExcedido)
        {
            Motivo = motivo;
            TamanhoExcedido = tamanhoExcedido;
        }

        /// <summary>Motivo detalhado, só para o log.</summary>
        public string Motivo { get; }

        public bool TamanhoExcedido { get; }

        /// <summary>Corpo JSON da resposta ao cliente, sem detalhes do motivo.</summary>
        public string Resposta => TamanhoExcedido ? Respostas.ArquivoMuitoGrande : Respostas.ArquivoNaoPermitido;

        internal static ArquivoRecusado Tipo(string motivo) => new ArquivoRecusado(motivo, false);

        internal static ArquivoRecusado Tamanho(string motivo) => new ArquivoRecusado("tamanho excedido: " + motivo, true);

        internal static ArquivoRecusado Quantidade(int quantidade, int maximo) =>
            new ArquivoRecusado($"{quantidade} arquivos, máximo {maximo}", false);
    }
}
