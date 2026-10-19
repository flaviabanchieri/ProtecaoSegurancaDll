# Manuais — ProtecaoSeguranca

DLL de cibersegurança para APIs e sites .NET (ASP.NET Core 2.2 a 10 e ASP.NET .NET Framework 4.5.2+), distribuída como pacote NuGet `ProtecaoSeguranca` no feed RepoSTI.

## Comece por aqui

- [Instalação e configuração da DLL](00-instalacao.md) — instalar o pacote, registrar no `Program.cs`/`Startup` ou no `web.config`, configurar e direcionar os logs.

## Manuais por feature

| Manual | O que faz | Padrão |
|---|---|---|
| [SQL Injection](01-sql-injection.md) | Bloqueia (400) padrões de SQL Injection no path, query string e corpo. | ligado |
| [Ocultar erros 5xx](02-ocultar-erros.md) | Troca a mensagem de erros internos por uma genérica com código de correlação. | ligado |
| [Rate limit](03-rate-limit.md) | Limita requisições por IP por minuto (429), com regras por path. | desligado |
| [Headers de segurança](04-headers-seguranca.md) | Adiciona CSP, HSTS, X-Frame-Options... e remove headers que revelam o servidor. | ligado |
| [Validação de upload](05-validacao-upload.md) | Recusa arquivos maliciosos: extensão, tamanho, assinatura e código embutido. | desligado |
| [Criptografia de dados](06-criptografia-dados.md) | Propriedades `[Criptografado]` vão criptografadas ao front e voltam descriptografadas. | ligado (ASP.NET Core) / Web API 2 |
| [Modo somente monitorar](07-modo-monitoramento.md) | Registra sem bloquear, para ligar proteções em produção com segurança. | — |

## Resumo rápido

**ASP.NET Core:**
```csharp
using ProtecaoSeguranca.Extensions;

app.UsePortalSeguranca(o =>
{
    o.UsarRateLimit = true;
    o.UsarValidacaoUpload = true;
});
```

**.NET Framework (`web.config`):**
```xml
<system.webServer>
  <modules>
    <add name="PortalSeguranca" type="ProtecaoSeguranca.AspNet.PortalSegurancaModule, ProtecaoSeguranca" />
  </modules>
</system.webServer>
```
