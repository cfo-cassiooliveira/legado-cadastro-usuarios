# ADR-0002: Validação de endereço via ViaCEP + IBGE

- **Status**: Aceito
- **Data**: 2026-11-08
- **Decisores**: Cassio Oliveira (responsável técnico)
- **Artigos da Constituição relacionados**: Art. IV.5, Art. VII.1, Art. III.1

## Contexto

O cadastro exige validar o **CEP nos Correios** e a **cidade**. A API oficial dos Correios exige contrato
e credenciais, o que não cabe numa base de demonstração.

## Decisão

- **CEP**: consultado no **ViaCEP** (`viacep.com.br`), que publica a base de CEPs dos Correios e devolve
  o código IBGE do município.
- **Cidades**: lista oficial por UF na **API de Localidades do IBGE** (`servicodados.ibge.gov.br`),
  com cache em memória de 24 h (decorator `CachedCityDirectory`).
- As chamadas são feitas **pelo backend** (adapters em `Legado.Infrastructure/Addresses`, typed `HttpClient` com
  `AddStandardResilienceHandler`, timeout de 5 s por tentativa e 15 s no total). O frontend só fala com a própria API.
- No cadastro, o backend **revalida**: o CEP existe, a cidade pertence à UF e o CEP pertence à cidade (pelo código IBGE).
- Se o serviço estiver indisponível, a API responde `503` com `code: address_service.unavailable`.
  No health check `/health`, os serviços aparecem como `Degraded`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Motivo da rejeição |
|---|---|---|---|
| API contratada dos Correios | Fonte primária | Exige contrato/token | Inviável para demo |
| Chamar ViaCEP direto do Angular | Menos código no backend | Validação burlável, CSP mais aberto | Validação tem que ser no servidor (Art. IV.5) |
| Base de CEPs local | Sem dependência externa | Desatualiza rápido, volume grande | Custo de manutenção |

## Consequências

- **Positivas**: sem credenciais, dados oficiais e validação no servidor.
- **Negativas / riscos**: dependência de serviços públicos sem SLA, mitigada por timeout, retry, circuit breaker e cache.
- **Ações decorrentes**: trocar o adapter `IPostalCodeLookup` se houver contrato com os Correios (sem mudar o domínio).
