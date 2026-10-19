# Headers de segurança

Adiciona a todas as respostas os headers HTTP que instruem o navegador a se proteger (clickjacking, MIME sniffing, HTTPS obrigatório...) e remove os headers que revelam a tecnologia do servidor.

## Disponibilidade

| Plataforma | Como ligar | Padrão |
|---|---|---|
| ASP.NET Core | `app.UsePortalSeguranca()` ou `app.UseHeadersSeguranca()` | ligado (`o.UsarHeadersSeguranca = false` desliga) |
| .NET Framework | `PortalSegurancaModule` ou `HeadersSegurancaModule` no `web.config` | ligado (`Seguranca:UsarHeadersSeguranca = false` desliga) |

Instalação: ver [Instalação e configuração](00-instalacao.md).

## Headers adicionados

| Header | Valor padrão | Para que serve |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | Impede o navegador de “adivinhar” o tipo do conteúdo. |
| `X-Frame-Options` | `DENY` | Impede exibir a resposta dentro de `<iframe>` (clickjacking). |
| `Referrer-Policy` | `no-referrer` | Não envia a URL de origem para outros sites. |
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'` | Restrição máxima; adequada para APIs que só devolvem JSON. |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=()` | Desliga recursos do navegador que a API não usa. |
| `Strict-Transport-Security` | `max-age=31536000` | Obriga HTTPS por 1 ano. Sem `includeSubDomains`, para não afetar subdomínios que ainda usam HTTP. |

Regras:
- Headers que a **aplicação já definiu não são sobrescritos**. Uma action pode definir seu próprio `Content-Security-Policy`, por exemplo.
- Os headers vão também nas respostas de erro e de bloqueio das outras proteções.
- O **HSTS não é enviado em desenvolvimento**: no ASP.NET Core, quando o ambiente é `Development`; no .NET Framework, em requisições locais (`localhost`). Isso evita que o navegador passe a exigir HTTPS em `localhost` para todos os projetos.

## Headers removidos

Com `RemoverHeadersServidor = true` (padrão): `Server`, `X-Powered-By`, `X-AspNet-Version` e `X-AspNetMvc-Version`.

Alguns são escritos pelo servidor **depois** da DLL e precisam ser desligados na própria aplicação:

**ASP.NET Core — header `Server: Kestrel`:**
```csharp
// ASP.NET Core 6+ (Program.cs com WebApplication)
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// ASP.NET Core 2.2 a 5 (Program.cs com WebHost/Host builder)
webBuilder.UseKestrel(o => o.AddServerHeader = false);
```

**.NET Framework / IIS:**
```xml
<system.web>
  <httpRuntime enableVersionHeader="false" />            <!-- X-AspNet-Version -->
</system.web>
<system.webServer>
  <httpProtocol>
    <customHeaders>
      <remove name="X-Powered-By" />
    </customHeaders>
  </httpProtocol>
</system.webServer>
```
No MVC, o `X-AspNetMvc-Version` sai com, no `Application_Start`:
```csharp
MvcHandler.DisableMvcResponseHeader = true;
```

## Configuração

Sem configuração, valem os padrões. Para mudar um header, informe o novo valor; **valor `""` desliga o header**.

### ASP.NET Core (`appsettings.json`)

```json
"Seguranca": {
  "HeadersSeguranca": {
    "XContentTypeOptions": "nosniff",
    "XFrameOptions": "DENY",
    "ReferrerPolicy": "no-referrer",
    "ContentSecurityPolicy": "default-src 'none'; frame-ancestors 'none'",
    "StrictTransportSecurity": "max-age=31536000",
    "PermissionsPolicy": "camera=(), microphone=(), geolocation=(), payment=()",
    "RemoverHeadersServidor": true
  }
}
```

### .NET Framework (`web.config`)

```xml
<appSettings>
  <add key="Seguranca:HeadersSeguranca:XFrameOptions" value="SAMEORIGIN" />
  <add key="Seguranca:HeadersSeguranca:ContentSecurityPolicy" value="" />
  <add key="Seguranca:HeadersSeguranca:RemoverHeadersServidor" value="true" />
</appSettings>
```

## Swagger e páginas HTML

O `Content-Security-Policy` padrão bloqueia scripts, estilos e imagens, então qualquer página HTML (Swagger UI, telas MVC, Web Forms) aparece em branco.

Opções:
- **API que expõe o Swagger só em desenvolvimento:** nada a fazer, desde que o Swagger UI não seja publicado em produção.
- **Swagger em produção ou site com páginas:** usar uma política mais permissiva, por exemplo:
  ```json
  "ContentSecurityPolicy": "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; frame-ancestors 'none'"
  ```
- **Sites legados com recursos de vários domínios:** começar com `""` (desligado) e montar a política aos poucos.

Se o sistema é exibido dentro de um `<iframe>` de outro sistema do mesmo domínio, troque `XFrameOptions` para `SAMEORIGIN` e o `frame-ancestors` do CSP para `'self'`.

## Particularidades do .NET Framework

- No pipeline **Integrado** do IIS, headers já existentes não são sobrescritos e os headers do servidor são removidos.
- No pipeline **Clássico**, os headers são acrescentados sem verificar os existentes e a remoção não é feita. Prefira o Integrado.
