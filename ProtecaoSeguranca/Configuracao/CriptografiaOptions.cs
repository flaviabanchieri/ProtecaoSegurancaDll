using System.Collections.Generic;

namespace ProtecaoSeguranca.Configuracao
{
    /// <summary>
    /// Chaves da criptografia de dados, lidas da seção "Seguranca:Criptografia" do appsettings
    /// (ou das chaves "Seguranca:Criptografia:*" do appSettings no .NET Framework).
    /// Todas as instâncias e APIs que trocam dados criptografados entre si precisam da mesma chave.
    /// </summary>
    public class CriptografiaOptions
    {
        public const string Secao = "Seguranca:Criptografia";

        /// <summary>Chave atual: 32 bytes em base64. Gerar com <see cref="Criptografia.GerarChave"/>.</summary>
        public string Chave { get; set; }

        /// <summary>
        /// Chaves antigas, aceitas só para descriptografar. Ao trocar a chave, mover a anterior para cá
        /// para que os valores já entregues ao front continuem válidos.
        /// </summary>
        public List<string> ChavesAnteriores { get; set; }
    }
}
