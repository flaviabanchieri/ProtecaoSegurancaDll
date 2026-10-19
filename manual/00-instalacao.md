# Instalação e configuração da DLL

Este manual mostra como colocar a DLL `ProtecaoSeguranca` num projeto, ligar as proteções e configurar os logs.
Os detalhes de cada proteção ficam nos manuais próprios (ver o [índice](README.md)).

## 1. Qual versão da DLL o projeto vai usar

É um único pacote NuGet para projetos novos e antigos. O NuGet escolhe sozinho a versão certa da DLL conforme o framework do projeto:

| Projeto | Versão da DLL usada | Como registrar |
|---|---|---|
| ASP.NET (.NET Framework 4.5.2 a 4.8.1): MVC, Web API, Web Forms, ASMX | `net452` | Módulos no `web.config` |
| ASP.NET Core 2.2 | `netcoreapp2.2` | Middleware no `Startup` |
| ASP.NET Core 3.1 e 5 | `netcoreapp3.1` | Middleware no `Startup` |
| ASP.NET Core 6, 7, 8 e 9 | `net6.0` | Middleware no `Program.cs` ou `Startup` |
| ASP.NET Core 10 em diante | `net10.0` | Middleware no `Program.cs` |

Dependências que o pacote traz junto:
- .NET Framework: `Microsoft.AspNet.WebApi.Core` 5.2.3 (e, com ele, o `Newtonsoft.Json`).
- ASP.NET Core 3.1 em diante: `Newtonsoft.Json` 13.0.3.

## 2. Instalar o pacote

O pacote está no feed **RepoSTI** (Nexus). Se o feed ainda não estiver configurado na máquina, peça a URL à STI e cadastre-o uma vez:

- **Visual Studio:** *Ferramentas → Opções → Gerenciador de Pacotes do NuGet → Origens de Pacotes* → adicionar a URL do RepoSTI.
- **Linha de comando:**
  ```bash
  dotnet nuget add source <URL do RepoSTI> -n RepoSTI
  ```

Para manter o feed junto com o projeto (útil para o build no servidor), use um `nuget.config` na pasta da solução:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="RepoSTI" value="<URL do RepoSTI>" />
  </packageSources>
</configuration>
```

Depois, instale o pacote `ProtecaoSeguranca`:

- **Visual Studio:** botão direito no projeto → *Gerenciar Pacotes do NuGet* → origem *RepoSTI* → procurar `ProtecaoSeguranca` → *Instalar*. Funciona com `packages.config` e com `PackageReference`.
- **Linha de comando (ASP.NET Core):**
  ```bash
  dotnet add package ProtecaoSeguranca
  ```
- **Direto no `.csproj`:**
  ```xml
  <PackageReference Include="ProtecaoSeguranca" Version="1.1.0" />
  ```

> O pacote, a **DLL** e os namespaces se chamam `ProtecaoSeguranca`. É esse nome que vai nos `using` e no `web.config`.

## 3. Registrar no ASP.NET Core

### 3.1 ASP.NET Core 6 em diante (`Program.cs` com `WebApplication`)

```csharp
using ProtecaoSeguranca.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Remove o header "Server: Kestrel" (ver Headers de segurança)
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UsePortalSeguranca();     // depois de CORS/autenticação e antes dos controllers

app.MapControllers();
app.Run();
```

### 3.2 ASP.NET Core 2.2 a 5 (`Startup.cs`)

```csharp
using ProtecaoSeguranca.Extensions;

public void Configure(IApplicationBuilder app)
{
    app.UseCors();
    app.UseAuthentication();

    app.UsePortalSeguranca();     // depois de CORS/autenticação e antes do UseMvc/UseEndpoints

    app.UseMvc();                 // ou app.UseRouting(); ... app.UseEndpoints(...)
}
```

E, no `Program.cs`, para remover o header `Server`:
```csharp
WebHost.CreateDefaultBuilder(args)
    .UseKestrel(o => o.AddServerHeader = false)
    .UseStartup<Startup>();
```

### 3.3 O que o `UsePortalSeguranca()` liga

Sem parâmetros, ele registra nesta ordem:

| Proteção | Padrão | Opção | Registro individual |
|---|---|---|---|
| [Headers de segurança](04-headers-seguranca.md) | ligado | `UsarHeadersSeguranca` | `app.UseHeadersSeguranca()` |
| [Ocultar erros 5xx](02-ocultar-erros.md) | sempre ligado | — | `app.UseOcultarErros()` |
| [Rate limit](03-rate-limit.md) | **desligado** | `UsarRateLimit` | `app.UseRateLimit()` |
| [Validação de upload](05-validacao-upload.md) | **desligado** | `UsarValidacaoUpload` | `app.UseValidacaoUpload()` |
| [Criptografia de dados](06-criptografia-dados.md) | ligado | `UsarCriptografia` | `app.UseCriptografia()` |
| [SQL Injection](01-sql-injection.md) | sempre ligado | — | `app.UseProtecaoSqlInjection()` |

Para mudar as escolhas:
```csharp
app.UsePortalSeguranca(o =>
{
    o.UsarRateLimit = true;
    o.UsarValidacaoUpload = true;
    o.UsarHeadersSeguranca = false;
    o.UsarCriptografia = false;
});
```

Se precisar de uma combinação que o `UsePortalSeguranca` não oferece (ex.: sem SQL Injection), registre as proteções uma a uma, **na mesma ordem da tabela**:
```csharp
app.UseHeadersSeguranca();
app.UseOcultarErros();
app.UseRateLimit();
app.UseValidacaoUpload();
app.UseCriptografia();
app.UseProtecaoSqlInjection();
```

### 3.4 Configuração no `appsettings.json`

Todas as proteções funcionam sem configuração nenhuma. Os ajustes ficam na seção `Seguranca`, uma subseção por proteção:
```json
{
  "Seguranca": {
    "SqlInjection":     { "Modo": "Bloquear" },
    "RateLimit":        { "Modo": "Bloquear", "LimitePorMinuto": 100 },
    "HeadersSeguranca": { "XFrameOptions": "DENY" },
    "UploadArquivos":   { "TamanhoMaximoMb": 10 },
    "Criptografia":     { "Chave": "<32 bytes em base64>" }
  }
}
```
Como qualquer configuração do ASP.NET Core, os valores também podem vir de variáveis de ambiente, trocando `:` por `__` (ex.: `Seguranca__Criptografia__Chave`).

### 3.5 Logs

No ASP.NET Core, a DLL usa o `ILogger` da própria aplicação. As mensagens aparecem com as categorias dos middlewares (`ProtecaoSeguranca.Middleware.*`); nenhuma configuração extra é necessária.

## 4. Registrar no ASP.NET (.NET Framework)

### 4.1 Módulo único (recomendado)

No `web.config` (pipeline integrado do IIS):
```xml
<system.webServer>
  <modules>
    <add name="PortalSeguranca" type="ProtecaoSeguranca.AspNet.PortalSegurancaModule, ProtecaoSeguranca" />
  </modules>
</system.webServer>
```

O `PortalSegurancaModule` liga as mesmas proteções e na mesma ordem do `UsePortalSeguranca`. As escolhas vão no `appSettings`:
```xml
<appSettings>
  <add key="Seguranca:UsarHeadersSeguranca" value="true" />   <!-- padrão: true -->
  <add key="Seguranca:UsarRateLimit" value="true" />          <!-- padrão: false -->
  <add key="Seguranca:UsarValidacaoUpload" value="true" />    <!-- padrão: false -->
</appSettings>
```

> A **criptografia de dados** não é ligada pelo módulo: no .NET Framework ela funciona só no Web API 2 e é ligada no `WebApiConfig` (ver [Criptografia de dados](06-criptografia-dados.md)).

### 4.2 Módulos individuais

Para escolher proteção por proteção, registre só os módulos desejados **no lugar** do `PortalSeguranca`, nesta ordem:
```xml
<system.webServer>
  <modules>
    <add name="HeadersSeguranca" type="ProtecaoSeguranca.AspNet.HeadersSegurancaModule, ProtecaoSeguranca" />
    <add name="OcultarErros" type="ProtecaoSeguranca.AspNet.OcultarErrosModule, ProtecaoSeguranca" />
    <add name="RateLimit" type="ProtecaoSeguranca.AspNet.RateLimitModule, ProtecaoSeguranca" />
    <add name="UploadArquivos" type="ProtecaoSeguranca.AspNet.UploadArquivosModule, ProtecaoSeguranca" />
    <add name="SqlInjection" type="ProtecaoSeguranca.AspNet.SqlInjectionModule, ProtecaoSeguranca" />
  </modules>
</system.webServer>
```

### 4.3 Configuração no `appSettings`

As chaves são as mesmas do `appsettings.json` do ASP.NET Core, escritas por extenso com `:`:
```xml
<appSettings>
  <add key="Seguranca:SqlInjection:Modo" value="Bloquear" />
  <add key="Seguranca:RateLimit:LimitePorMinuto" value="100" />
  <add key="Seguranca:HeadersSeguranca:XFrameOptions" value="SAMEORIGIN" />
</appSettings>
```
Regras de formato:
- **Listas** são separadas por vírgula (ou ponto e vírgula): `value="10.1.2.3,10.1.2.4"`.
- **Regras do rate limit** usam `path=limite`: `value="/autenticacao=5,/login=10"`.
- **Regras do upload** usam `path=extensões|tamanho`: `value="/documentos=.pdf|5MB"`.
- Valores inválidos são ignorados (vale o padrão) e geram um aviso no log.

### 4.4 Logs

O .NET Framework não tem `ILogger`, então os módulos escrevem no `System.Diagnostics.Trace` com o prefixo `[ProtecaoSeguranca]`.
Para mandar para o log da aplicação, troque o destino no `Application_Start` do `Global.asax`:
```csharp
using ProtecaoSeguranca.AspNet;

protected void Application_Start()
{
    RegistroSeguranca.Destino = (nivel, mensagem, ex) =>
    {
        if (nivel == NivelRegistro.Erro)
            log.Error(mensagem, ex);
        else
            log.Warn(mensagem, ex);
    };

    // ... restante da inicialização
}
```
Funciona com log4net, NLog, Serilog ou qualquer outro. Exceções lançadas pelo destino são ignoradas, para não derrubar a requisição.

## 5. Ligando com segurança em produção

O SQL Injection, o rate limit e a validação de upload têm o modo **`SomenteLog`**, que registra no log sem bloquear nada.
Ao ligar a DLL num sistema que já está em produção, o caminho recomendado é:

1. Instalar com essas proteções em `SomenteLog`.
2. Deixar rodando uma ou duas semanas e procurar no log por `[MONITORAMENTO]`.
3. Ajustar o que for preciso (regras, limites, extensões, telas que mandam apóstrofo...).
4. Trocar para `Bloquear`.

Ver [Modo somente monitorar](07-modo-monitoramento.md).

## 6. Conferir se está funcionando

Com a aplicação rodando, faça uma requisição suspeita e confira a resposta e o log:
```bash
curl -i "https://localhost:5001/api/qualquer?nome=1%27%20or%201=1"
```
- Resposta esperada: `400` com `{"mensagem":"Requisição contém caracteres ou termos não permitidos."}`.
- Log esperado: `Possível SQL Injection, requisição bloqueada. ...`.
- Os headers da resposta devem trazer `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy` etc.

## 7. Atualizar a versão

Atualize o pacote pelo NuGet do Visual Studio ou mude a `Version` do `PackageReference`. As notas de cada versão estão no campo *Release Notes* do pacote.

## Problemas comuns

| Sintoma | Causa provável |
|---|---|
| `Could not load type 'ProtecaoSeguranca.AspNet...'` | O `type` do `web.config` está com o nome da classe ou da DLL errado, ou a DLL não foi copiada para o `bin`. |
| Os módulos não rodam no IIS | O pool está em modo **Clássico**: os módulos ficam em `<system.webServer><modules>`, que exige pipeline **Integrado**. |
| Swagger UI em branco | O `Content-Security-Policy` padrão bloqueia HTML. Ver [Headers de segurança](04-headers-seguranca.md#swagger-e-páginas-html). |
| Nomes com apóstrofo (D'Ávila) recusados com 400 | Comportamento do SQL Injection. Ver [SQL Injection](01-sql-injection.md#falsos-positivos). |
| Todos os usuários caem no mesmo limite do rate limit | A API está atrás de um proxy cuja rede não está em `RedesConfiaveis`. Ver [Rate limit](03-rate-limit.md#atrás-de-proxy-reverso-traefik-nginx-iis-arr). |
| `InvalidOperationException: Criptografia sem chave configurada` | Há `[Criptografado]` nos DTOs mas falta `Seguranca:Criptografia:Chave`. |
