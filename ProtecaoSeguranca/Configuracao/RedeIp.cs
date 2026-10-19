using System.Net;
using System.Net.Sockets;

namespace ProtecaoSeguranca.Configuracao
{
    /// <summary>
    /// Rede no formato CIDR (ex.: "10.0.0.0/9").
    /// </summary>
    public class RedeIp
    {
        private readonly byte[] _prefixo;
        private readonly int _tamanhoPrefixo;
        private readonly AddressFamily _familia;

        private RedeIp(IPAddress endereco, int tamanhoPrefixo)
        {
            _prefixo = endereco.GetAddressBytes();
            _tamanhoPrefixo = tamanhoPrefixo;
            _familia = endereco.AddressFamily;
        }

        public static bool TryParse(string cidr, out RedeIp rede)
        {
            rede = null;
            if (string.IsNullOrWhiteSpace(cidr))
                return false;

            var partes = cidr.Trim().Split('/');
            if (!IPAddress.TryParse(partes[0], out var endereco))
                return false;

            var bits = endereco.GetAddressBytes().Length * 8;
            var tamanhoPrefixo = bits;

            if (partes.Length > 2 || (partes.Length == 2 &&
                (!int.TryParse(partes[1], out tamanhoPrefixo) || tamanhoPrefixo < 0 || tamanhoPrefixo > bits)))
                return false;

            rede = new RedeIp(endereco, tamanhoPrefixo);
            return true;
        }

        public bool Contem(IPAddress ip)
        {
            if (ip == null)
                return false;

            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            if (ip.AddressFamily != _familia)
                return false;

            var bytes = ip.GetAddressBytes();
            var bitsRestantes = _tamanhoPrefixo;

            for (var i = 0; i < bytes.Length && bitsRestantes > 0; i++, bitsRestantes -= 8)
            {
                var mascara = bitsRestantes >= 8 ? (byte)0xFF : (byte)(0xFF << (8 - bitsRestantes));
                if ((bytes[i] & mascara) != (_prefixo[i] & mascara))
                    return false;
            }

            return true;
        }
    }
}
