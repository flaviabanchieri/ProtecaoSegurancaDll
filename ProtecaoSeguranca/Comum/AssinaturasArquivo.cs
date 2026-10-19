using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProtecaoSeguranca.Comum
{
    /// <summary>
    /// Assinaturas ("magic numbers") dos formatos aceitos e trechos de código de servidor procurados no conteúdo.
    /// </summary>
    internal static class AssinaturasArquivo
    {
        /// <summary>Bytes do início do arquivo necessários para conferir qualquer assinatura da tabela.</summary>
        public const int TamanhoCabecalho = 16;

        private static readonly Assinatura[] Zip = { new Assinatura(0, 0x50, 0x4B, 0x03, 0x04), new Assinatura(0, 0x50, 0x4B, 0x05, 0x06) };
        private static readonly Assinatura[] Ole = { new Assinatura(0, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1) };
        private static readonly Assinatura[] Jpeg = { new Assinatura(0, 0xFF, 0xD8, 0xFF) };
        private static readonly Assinatura[] Tiff = { new Assinatura(0, 0x49, 0x49, 0x2A, 0x00), new Assinatura(0, 0x4D, 0x4D, 0x00, 0x2A) };
        private static readonly Assinatura[] Mp4 = { new Assinatura(4, "ftyp") };

        /// <summary>
        /// Formatos binários conhecidos. Extensões fora da tabela (ex.: .txt, .csv) não têm assinatura a conferir.
        /// Os formatos do Office atuais (.docx, .xlsx...) e do LibreOffice são arquivos zip.
        /// </summary>
        private static readonly Dictionary<string, Assinatura[]> Formatos = new Dictionary<string, Assinatura[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = new[] { new Assinatura(0, "%PDF") },
            [".png"] = new[] { new Assinatura(0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) },
            [".jpg"] = Jpeg,
            [".jpeg"] = Jpeg,
            [".jfif"] = Jpeg,
            [".gif"] = new[] { new Assinatura(0, "GIF87a"), new Assinatura(0, "GIF89a") },
            [".bmp"] = new[] { new Assinatura(0, "BM") },
            [".tif"] = Tiff,
            [".tiff"] = Tiff,
            [".webp"] = new[] { new Assinatura(0, "RIFF").E(8, "WEBP") },
            [".zip"] = Zip,
            [".docx"] = Zip,
            [".xlsx"] = Zip,
            [".pptx"] = Zip,
            [".odt"] = Zip,
            [".ods"] = Zip,
            [".odp"] = Zip,
            [".doc"] = Ole,
            [".xls"] = Ole,
            [".ppt"] = Ole,
            [".msg"] = Ole,
            [".rtf"] = new[] { new Assinatura(0, "{\\rtf") },
            [".rar"] = new[] { new Assinatura(0, "Rar!\x1A\x07") },
            [".7z"] = new[] { new Assinatura(0, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C) },
            [".gz"] = new[] { new Assinatura(0, 0x1F, 0x8B) },
            [".mp3"] = new[] { new Assinatura(0, "ID3"), new Assinatura(0, 0xFF, 0xFB), new Assinatura(0, 0xFF, 0xF3), new Assinatura(0, 0xFF, 0xF2) },
            [".mp4"] = Mp4,
            [".m4a"] = Mp4,
            [".mov"] = Mp4,
            [".wav"] = new[] { new Assinatura(0, "RIFF").E(8, "WAVE") },
            [".ogg"] = new[] { new Assinatura(0, "OggS") }
        };

        private static readonly KeyValuePair<string, Assinatura>[] Executaveis =
        {
            new KeyValuePair<string, Assinatura>("executável Windows", new Assinatura(0, "MZ")),
            new KeyValuePair<string, Assinatura>("executável Linux", new Assinatura(0, 0x7F, 0x45, 0x4C, 0x46)),
            new KeyValuePair<string, Assinatura>("script (#!)", new Assinatura(0, "#!"))
        };

        /// <summary>Trechos que só aparecem em código (minúsculas; a busca não diferencia maiúsculas).</summary>
        public static readonly byte[][] ScriptsLongos = Bytes(
            "<?php", "<script", "<jsp:", "<asp:", "runat=\"server\"", "<!--#exec", "<!--#include");

        /// <summary>Inclui trechos curtos, que em arquivos binários aparecem por acaso. Só para arquivos de texto.</summary>
        public static readonly byte[][] Scripts = ScriptsLongos.Concat(Bytes("<?=", "<%@", "<%=", "<%#", "<% ")).ToArray();

        public static bool Conhecida(string extensao) => Formatos.ContainsKey(extensao);

        public static bool Confere(string extensao, ArraySegment<byte> cabecalho) =>
            Formatos[extensao].Any(a => a.Confere(cabecalho));

        /// <summary>Retorna a descrição do executável se o cabeçalho for de um; caso contrário, null.</summary>
        public static string Executavel(ArraySegment<byte> cabecalho) =>
            Executaveis.Where(e => e.Value.Confere(cabecalho)).Select(e => e.Key).FirstOrDefault();

        private static byte[][] Bytes(params string[] textos) => textos.Select(t => Encoding.ASCII.GetBytes(t)).ToArray();

        private class Assinatura
        {
            private readonly List<KeyValuePair<int, byte[]>> _partes = new List<KeyValuePair<int, byte[]>>();

            public Assinatura(int posicao, params byte[] bytes)
            {
                _partes.Add(new KeyValuePair<int, byte[]>(posicao, bytes));
            }

            public Assinatura(int posicao, string texto) : this(posicao, Latin1(texto))
            {
            }

            /// <summary>Exige também um segundo trecho (ex.: "RIFF" no início e "WEBP" na posição 8).</summary>
            public Assinatura E(int posicao, string texto)
            {
                _partes.Add(new KeyValuePair<int, byte[]>(posicao, Latin1(texto)));
                return this;
            }

            public bool Confere(ArraySegment<byte> cabecalho) =>
                _partes.All(p => p.Key + p.Value.Length <= cabecalho.Count &&
                                 p.Value.Select((b, i) => cabecalho.Array[cabecalho.Offset + p.Key + i] == b).All(igual => igual));

            private static byte[] Latin1(string texto) => texto.Select(c => (byte)c).ToArray();
        }
    }
}
