# Rate limit (limite de requisições por IP)

Limita quantas requisições cada IP pode fazer por minuto. Protege contra força bruta em login, varredura de dados e abuso de endpoints caros.

## Disponibilidade

| Plataforma | Como ligar | Padrão |
|---|---|---|
| ASP.NET Core | `app.UsePortalSeguranca(o => o.UsarRateLimit = true)` ou `app.UseRateLimit()` | **desligado** |
| .NET Framework | `Seguranca:UsarRateLimit = true` com o `PortalSegurancaModule`, ou `RateLimitModule` no `web.config` | **desligado** |

Instalação: ver [Instalação e configuração](00-instalacao.md).

## Como funciona

- Janela **fixa de 1 minuto** por IP: a contagem começa na primeira requisição e zera um minuto depois.
- Cada **regra por path** tem seu próprio contador. Requisições que não casam com nenhuma regra usam o contador geral.
- A regra é escolhida pelo **trecho do path**, sem diferenciar maiúsculas; a primeira que casar vale.
- Requisições `OPTIONS` (preflight de CORS) não contam.
- Os contadores ficam **em memória**, por instância da aplicação. Com várias réplicas atrás de um balanceador, cada uma conta separadamente.

Padrões sem configuração:
- **100 requisições/minuto** por IP no geral.
- **5 requisições/minuto** em paths que contenham `/autenticacao`.

## Resposta ao exceder

```
HTTP/1.1 429 Too Many Requests
Retry-After: 37
Content-Type: application/json; charset=utf-8

{"mensagem":"Muitas requisições. Tente novamente em instantes."}
```
`Retry-After` traz os segundos que faltam para a janela zerar.

Log (uma única vez por IP e janela, para não inundar o log):
```
Rate limit excedido, requisição bloqueada. IP: 200.x.x.x POST /api/autenticacao Limite: 5/min
```

## Configuração

### ASP.NET Core (`appsettings.json`)

```json
"Seguranca": {
  "RateLimit": {
    "Modo": "Bloquear",
    "LimitePorMinuto": 100,
    "Regras": [
      { "Path": "/autenticacao", "LimitePorMinuto": 5 },
      { "Path": "/relatorio", "LimitePorMinuto": 10 }
    ],
    "IpsLiberados": [ "10.1.2.3" ],
    "RedesConfiaveis": [ "10.0.0.0/9", "321.8.9.0.33", "::1/654" ]
  }
}
```

### .NET Framework (`web.config`)

```xml
<appSettings>
  <add key="Seguranca:UsarRateLimit" value="true" />
  <add key="Seguranca:RateLimit:Modo" value="Bloquear" />
  <add key="Seguranca:RateLimit:LimitePorMinuto" value="100" />
  <add key="Seguranca:RateLimit:Regras" value="/autenticacao=5,/relatorio=10" />
  <add key="Seguranca:RateLimit:IpsLiberados" value="10.1.2.3" />
  <add key="Seguranca:RateLimit:RedesConfiaveis" value=""10.0.0.0/9,321.8.9.0.33,::1/654" />
</appSettings>
```

### Opções

| Opção | Padrão | Descrição |
|---|---|---|
| `Modo` | `Bloquear` | `SomenteLog` só registra, sem responder 429. Ver [Modo somente monitorar](07-modo-monitoramento.md). |
| `LimitePorMinuto` | `100` | Limite geral por IP para requisições fora das regras. |
| `Regras` | `/autenticacao` = 5 | Limites por trecho de path. **Ao informar, substitui o padrão**: inclua `/autenticacao` se quiser mantê-lo. |
| `IpsLiberados` | nenhum | IPs que nunca passam pelo limite (ex.: servidores internos que chamam a API em lote). Comparação exata, sem CIDR. |
| `RedesConfiaveis` | redes privadas | Redes (CIDR) dos proxies reversos. Ver abaixo. CIDR inválido é ignorado com aviso no log. |

## Atrás de proxy reverso (Traefik, Nginx, IIS ARR)

Atrás de um proxy, toda requisição chega com o IP do proxy. Para não colocar todos os usuários no mesmo contador, o IP do cliente é lido do header `X-Forwarded-For` — **mas só quando a conexão vem de uma rede em `RedesConfiaveis`**. De qualquer outra origem o header é ignorado, porque o próprio cliente poderia forjá-lo para escapar do limite.

O header é lido da direita para a esquerda, pulando os IPs das redes confiáveis; o primeiro IP fora delas é o cliente.

Exemplo: conexão vinda de `10.0.0.5` (Traefik) com `X-Forwarded-For: 1.2.3.4, 200.1.1.1`:
- `10.0.0.5` está em `10.0.0.0/9` → confiável, então o header é lido;
- `200.1.1.1` não está em nenhuma rede confiável → é o cliente. O `1.2.3.4` foi enviado pelo próprio cliente e é ignorado.

Ajuste `RedesConfiaveis` quando:
- o proxy está numa rede pública ou fora das faixas privadas → **inclua** a rede dele;
- clientes legítimos chegam pela rede interna sem proxy e você quer contá-los separadamente → **restrinja** às redes dos proxies (ex.: só `10.20.0.0/18`).

## Dicas

- Comece com o padrão e `Modo: SomenteLog` por alguns dias, depois ajuste os limites conforme o log.
- Telas que carregam muitos dados em paralelo podem passar de 100/min por usuário; aumente o geral em vez de liberar IPs.
- Usuários atrás do mesmo NAT (ex.: uma unidade inteira com um IP público) dividem o contador. Considere isso ao definir o limite geral.
