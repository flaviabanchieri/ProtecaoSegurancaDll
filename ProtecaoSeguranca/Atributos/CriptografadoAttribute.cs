using System;

namespace ProtecaoSeguranca.Atributos
{
    /// <summary>
    /// Marca uma propriedade de DTO (ou um parâmetro de rota/query) como criptografada, em qualquer endpoint:
    /// na saída o valor vai cifrado para o front; na entrada precisa chegar cifrado (senão a resposta é 400,
    /// exceto nulo, vazio ou zero) e a API recebe o valor original.
    /// Ligado pelo UsePortalSeguranca (ASP.NET Core) ou por config.UsarCriptografia() (Web API 2).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public sealed class CriptografadoAttribute : Attribute
    {
    }
}
