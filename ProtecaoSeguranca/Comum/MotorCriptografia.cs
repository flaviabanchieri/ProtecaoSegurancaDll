using ProtecaoSeguranca.Configuracao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ProtecaoSeguranca.Comum
{
    /// <summary>Tipo do valor original, para devolvê-lo com o tipo certo no JSON.</summary>
    internal enum TipoValor : byte
    {
        Texto = (byte)'S',
        Inteiro = (byte)'I',
        Longo = (byte)'L',
        Guid = (byte)'G',
        Json = (byte)'J'
    }

    /// <summary>
    /// AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC). O mesmo formato em todos os alvos, para que um valor
    /// criptografado numa API ASP.NET Core possa ser lido numa API .NET Framework com a mesma chave.
    /// Formato: "ProtecaoSeguranca." + base64url(versão | idChave | tipo | IV | cifra | HMAC).
    /// </summary>
    internal sealed class MotorCriptografia
    {
        public const string Prefixo = "ProtecaoSeguranca.";

        private const byte Versao = 1;
        private const int TamanhoCabecalho = 3;
        private const int TamanhoIv = 16;
        private const int TamanhoBloco = 16;
        private const int TamanhoMac = 32;
        private const int TamanhoChave = 32;

        private readonly Chave _atual;
        private readonly List<Chave> _chaves;

        public MotorCriptografia(CriptografiaOptions opcoes)
        {
            if (opcoes == null || string.IsNullOrWhiteSpace(opcoes.Chave))
                throw new InvalidOperationException(
                    $"Criptografia sem chave configurada. Defina \"{CriptografiaOptions.Secao}:Chave\" com 32 bytes em base64 " +
                    "(gerar com Criptografia.GerarChave()).");

            _atual = Chave.Derivar(opcoes.Chave, "Chave");
            _chaves = new List<Chave> { _atual };
            _chaves.AddRange((opcoes.ChavesAnteriores ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => Chave.Derivar(c, "ChavesAnteriores")));
        }

        public static bool PareceCriptografado(string texto) =>
            texto != null && texto.StartsWith(Prefixo, StringComparison.Ordinal);

        public string Cifrar(TipoValor tipo, string valor)
        {
            var dados = Encoding.UTF8.GetBytes(valor);

            using (var aes = CriarAes(_atual.Cifra))
            {
                aes.GenerateIV();

                byte[] cifra;
                using (var cifrador = aes.CreateEncryptor())
                {
                    cifra = cifrador.TransformFinalBlock(dados, 0, dados.Length);
                }

                var corpo = new byte[TamanhoCabecalho + TamanhoIv + cifra.Length];
                corpo[0] = Versao;
                corpo[1] = _atual.Id;
                corpo[2] = (byte)tipo;
                Buffer.BlockCopy(aes.IV, 0, corpo, TamanhoCabecalho, TamanhoIv);
                Buffer.BlockCopy(cifra, 0, corpo, TamanhoCabecalho + TamanhoIv, cifra.Length);

                var mac = CalcularMac(_atual.Mac, corpo, corpo.Length);

                var resultado = new byte[corpo.Length + TamanhoMac];
                Buffer.BlockCopy(corpo, 0, resultado, 0, corpo.Length);
                Buffer.BlockCopy(mac, 0, resultado, corpo.Length, TamanhoMac);

                return Prefixo + Base64Url.Codificar(resultado);
            }
        }

        public bool TentarDecifrar(string texto, out TipoValor tipo, out string valor)
        {
            tipo = default(TipoValor);
            valor = null;

            if (!PareceCriptografado(texto))
                return false;

            var bytes = Base64Url.Decodificar(texto.Substring(Prefixo.Length));
            if (bytes == null || bytes.Length < TamanhoCabecalho + TamanhoIv + TamanhoBloco + TamanhoMac || bytes[0] != Versao)
                return false;

            var tamanhoCorpo = bytes.Length - TamanhoMac;

            foreach (var chave in _chaves.Where(c => c.Id == bytes[1]))
            {
                if (!MacValido(chave.Mac, bytes, tamanhoCorpo))
                    continue;

                if (!Enum.IsDefined(typeof(TipoValor), bytes[2]))
                    return false;

                try
                {
                    using (var aes = CriarAes(chave.Cifra))
                    {
                        var iv = new byte[TamanhoIv];
                        Buffer.BlockCopy(bytes, TamanhoCabecalho, iv, 0, TamanhoIv);
                        aes.IV = iv;

                        var inicioCifra = TamanhoCabecalho + TamanhoIv;
                        using (var decifrador = aes.CreateDecryptor())
                        {
                            var dados = decifrador.TransformFinalBlock(bytes, inicioCifra, tamanhoCorpo - inicioCifra);
                            tipo = (TipoValor)bytes[2];
                            valor = Encoding.UTF8.GetString(dados);
                            return true;
                        }
                    }
                }
                catch (CryptographicException)
                {
                    return false;
                }
            }

            return false;
        }

        private static Aes CriarAes(byte[] chave)
        {
            var aes = Aes.Create();
            aes.KeySize = TamanhoChave * 8;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = chave;
            return aes;
        }

        private static byte[] CalcularMac(byte[] chave, byte[] dados, int tamanho)
        {
            using (var hmac = new HMACSHA256(chave))
            {
                return hmac.ComputeHash(dados, 0, tamanho);
            }
        }

        private static bool MacValido(byte[] chave, byte[] bytes, int tamanhoCorpo)
        {
            var esperado = CalcularMac(chave, bytes, tamanhoCorpo);
            var diferenca = 0;
            for (var i = 0; i < TamanhoMac; i++)
                diferenca |= esperado[i] ^ bytes[tamanhoCorpo + i];

            return diferenca == 0;
        }

        private sealed class Chave
        {
            public byte Id;
            public byte[] Cifra;
            public byte[] Mac;

            public static Chave Derivar(string base64, string nomeOpcao)
            {
                byte[] mestra;
                try
                {
                    mestra = Convert.FromBase64String(base64.Trim());
                }
                catch (FormatException)
                {
                    mestra = null;
                }

                if (mestra == null || mestra.Length != TamanhoChave)
                    throw new InvalidOperationException(
                        $"\"{CriptografiaOptions.Secao}:{nomeOpcao}\" inválida: precisa ter {TamanhoChave} bytes em base64 " +
                        "(gerar com Criptografia.GerarChave()).");

                using (var hmac = new HMACSHA256(mestra))
                {
                    return new Chave
                    {
                        Id = hmac.ComputeHash(Encoding.ASCII.GetBytes("ProtecaoSeguranca-id"))[0],
                        Cifra = hmac.ComputeHash(Encoding.ASCII.GetBytes("ProtecaoSeguranca-cifra")),
                        Mac = hmac.ComputeHash(Encoding.ASCII.GetBytes("ProtecaoSeguranca-mac"))
                    };
                }
            }
        }
    }

    internal static class Base64Url
    {
        public static string Codificar(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] Decodificar(string texto)
        {
            var base64 = texto.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
                case 1: return null;
            }

            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
