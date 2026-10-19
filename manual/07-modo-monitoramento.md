# Modo somente monitorar (`SomenteLog`)

Permite ligar uma proteção em produção **sem risco de barrar usuários legítimos**: a DLL registra no log o que teria bloqueado, mas deixa a requisição seguir.

## Proteções que têm o modo

| Proteção | Chave (ASP.NET Core) | Chave (.NET Framework) |
|---|---|---|
| [SQL Injection](01-sql-injection.md) | `Seguranca:SqlInjection:Modo` | `Seguranca:SqlInjection:Modo` |
| [Rate limit](03-rate-limit.md) | `Seguranca:RateLimit:Modo` | `Seguranca:RateLimit:Modo` |
| [Validação de upload](05-validacao-upload.md) | `Seguranca:UploadArquivos:Modo` | `Seguranca:UploadArquivos:Modo` |

Valores:
- **`Bloquear`** (padrão): registra no log e bloqueia a requisição.
- **`SomenteLog`**: só registra no log e deixa a requisição seguir.

As demais proteções (headers, ocultar erros, criptografia) não bloqueiam requisições e não têm esse modo.

## Configuração

**ASP.NET Core** (`appsettings.json`):
```json
"Seguranca": {
  "SqlInjection":   { "Modo": "SomenteLog" },
  "RateLimit":      { "Modo": "SomenteLog" },
  "UploadArquivos": { "Modo": "SomenteLog" }
}
```

**.NET Framework** (`web.config`):
```xml
<appSettings>
  <add key="Seguranca:SqlInjection:Modo" value="SomenteLog" />
  <add key="Seguranca:RateLimit:Modo" value="SomenteLog" />
  <add key="Seguranca:UploadArquivos:Modo" value="SomenteLog" />
</appSettings>
```

O valor não diferencia maiúsculas no .NET Framework. Um valor inválido é ignorado (vale `Bloquear`).

> Lembre-se de que o rate limit e a validação de upload também precisam estar **ligados** (`UsarRateLimit`, `UsarValidacaoUpload`) para registrar algo.

## Como identificar no log

No modo `SomenteLog`, a mensagem começa com `[MONITORAMENTO]` e termina o evento com `(não bloqueado)`:
```
[MONITORAMENTO] Possível SQL Injection (não bloqueado). IP: 200.x.x.x X-Forwarded-For:  GET /api/servidor Campo: nome Valor: D'Ávila
[MONITORAMENTO] Rate limit excedido (não bloqueado). IP: 200.x.x.x GET /api/servidor Limite: 100/min
```
No modo `Bloquear`, o mesmo evento aparece como `Possível SQL Injection, requisição bloqueada. ...`.

Basta filtrar o log por `[MONITORAMENTO]` para ver tudo o que seria bloqueado.

## Roteiro recomendado

1. **Publicar em `SomenteLog`** as proteções que bloqueiam.
2. **Deixar rodando uma ou duas semanas**, cobrindo os fluxos mais usados (fechamento de mês, períodos de inscrição etc.).
3. **Analisar os registros `[MONITORAMENTO]`** e separar:
   - ataques ou varreduras reais → confirmam que a proteção é útil;
   - usos legítimos → precisam de ajuste (ex.: campo de nome com apóstrofo, endpoint de relatório que passa de 100/min, upload de `.xml` que o sistema realmente precisa receber).
4. **Ajustar**: regras de rate limit, extensões permitidas e regras por path do upload, ou o próprio front.
5. **Trocar para `Bloquear`** e acompanhar o log nos primeiros dias.
