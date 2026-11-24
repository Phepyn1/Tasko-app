# Análise e integração — `luizprompt/Tasko-app` × TASKO

Análise original: 2026-11-22. Integração concluída: 2026-11-23. O protótipo foi usado como
referência visual e depois removido da árvore de trabalho. A aplicação final é servida pela API
.NET na mesma origem, sem servidor Node.js. Documento de referência: `Tasko_Final.docx`
(22 RFs, 12 RNFs, 12 RNs, 7 telas T01–T07, identidade Emerald Trust).

## Resultado da integração

A recomendação A desta análise foi executada. A nova SPA fica em `src/TASKO.Api/wwwroot` e usa
diretamente os 40 endpoints `/api/v1`:

- T04 possui oferta e demanda, com orçamento/prazo e regras por tipo;
- T03/T05 mostram propostas com valor, aceite atômico, contato condicional e estados;
- T06 permite editar dados e habilidades sem expor contato no perfil público;
- T07 inclui avaliações verificadas, denúncias e fila de moderação;
- recuperação de senha, notificações e histórico de atividades foram integrados;
- a barra fictícia “Servidor Node.js Ativo” foi substituída pelo `/health` real;
- não restaram `server.js`, `db.json`, `package.json`, proxy ou chamadas ao servidor antigo.

O restante do documento preserva a avaliação do protótipo anterior como registro histórico.

## 1. O que o projeto é tecnicamente

| Item | Situação |
| --- | --- |
| Stack | Node.js 22 + Express 5 + SPA em HTML/CSS/JS puro (sem build, sem framework) |
| Arquivos | `server.js` (335 linhas), `public/index.html` (800 linhas, CSS inline), `public/app.js` (553 linhas) |
| Persistência | Arquivo `db.json` criado em runtime (sem banco relacional) |
| Senhas | `bcryptjs` custo 10 (adequado), seed com senha `123456` |
| Sessão | **Nenhuma**: `/api/login` devolve o objeto do usuário e o front guarda em `localStorage` |
| Autorização | **Inexistente**: endpoints recebem `usuario_id`/`cliente_id`/`prestador_id` no corpo ou na URL |
| Testes / CI | Nenhum (`npm test` é placeholder) |
| Documentação | Sem README; apenas comentários de seção no código |

Execução histórica: o protótipo exigia npm e porta 3000; esses passos não se aplicam à aplicação final.
Conta semeada: `carlos@tasko.com` / `123456` (perfil PRESTADOR). Há 5 prestadores e 5 anúncios.

## 2. Modelo de domínio implementado (diverge do documento)

O app implementa **apenas um lado** do marketplace:

```
usuarios (tipo_perfil: CLIENTE | PRESTADOR | AMBOS)
servicos (prestador_id, categoria, titulo, descricao, preco_base, regiao, servicos_inclusos)
contratacoes (servico_id, cliente_id, prestador_id, mensagem, data_desejada,
              status: Em Negociação | Combinado | Recusado | Concluído, avaliacao)
```

No documento, o fluxo central é: **solicitante publica uma demanda → prestadores enviam propostas
→ solicitante aceita/recusa**. No app, o fluxo é **invertido**: o prestador publica um anúncio de
serviço e o cliente manda um pedido de contratação, que o **prestador** aceita ou recusa.

Consequências diretas:
- Não existe entidade "publicação de solicitação" (RF06, prioridade P1).
- Não existe "proposta com preço" enviada pelo prestador (RF10) — o pedido não tem valor.
- RN04 ("apenas o solicitante criador aceita/recusa") é violada por desenho.
- Não há status na publicação (serviço), logo RF14 fica só na contratação.

## 3. Cobertura dos Requisitos Funcionais

Legenda: ✅ atende · 🟡 parcial/adaptado · ❌ ausente

| RF | Nome | Status | Evidência / observação |
| --- | --- | --- | --- |
| RF01 | Cadastro de conta | ✅ | `POST /api/register`, bcrypt, e-mail único (409) |
| RF02 | Autenticação | 🟡 | Login/logout existem, mas não há sessão/token; logout só limpa `localStorage` |
| RF03 | Definição de atuação | 🟡 | `tipo_perfil` no cadastro (CLIENTE/PRESTADOR/AMBOS); não é editável depois |
| RF04 | Gerenciamento de perfil | ❌ | Tela de perfil é somente leitura; sem apresentação, habilidades ou edição |
| RF05 | Publicação de serviço | ✅ | `POST /api/servicos`, exige perfil prestador/ambos |
| RF06 | Publicação de solicitação | ❌ | Não existe; nenhum campo de prazo/orçamento de demanda |
| RF07 | Busca de oportunidades | 🟡 | Busca textual em serviços; não há solicitações |
| RF08 | Filtros de pesquisa | 🟡 | Chips por categoria; região só via texto livre, sem filtro dedicado |
| RF09 | Visualização de detalhes | ✅ | Tela `screen-detail` com descrição, preço, inclusos, avaliação |
| RF10 | Envio de proposta | 🟡 | Existe "contratação" (mensagem + data), sem preço e no sentido inverso |
| RF11 | Consulta de propostas | 🟡 | Painel mostra pedidos recebidos; sem perfil do proponente nem valor |
| RF12 | Aceite ou recusa | 🟡 | Prestador aceita/recusa; qualquer participante pode mudar status livremente |
| RF13 | Canal de contato | ✅ | Telefone liberado em `Combinado`/`Concluído` (mas API expõe sem autenticar) |
| RF14 | Controle de status | 🟡 | Status só na contratação; publicação não tem estado; transições sem regra |
| RF15 | Avaliação de serviço | 🟡 | Só o cliente avalia, só após `Concluído`; comentário não é público |
| RF16 | Denúncia de conteúdo | ❌ | Sem rota/tela (`/api/denuncias` → 404) |
| RF17 | Painel de moderação | ❌ | Inexistente |
| RF18 | Notificações | 🟡 | Apenas badge com contagem de pedidos pendentes; sem eventos/registro |
| RF19 | Edição de publicações | ❌ | Só exclusão; permite excluir mesmo com contratação ativa (RN09) |
| RF20 | Histórico de atividades | 🟡 | Lista de contratações; sem histórico de publicações/propostas |
| RF21 | Recuperação de senha | ❌ | Inexistente |
| RF22 | Tema visual Emerald Trust | ✅ | Paleta e componentes aderentes (ver §6) |

**Placar:** 5 atendidos · 11 parciais · 6 ausentes (de 22).
Dos P1 do documento (RF01–RF07, RF09–RF12, RF14), faltam **RF04 e RF06** e há **5 parciais**.

## 4. Cobertura dos Requisitos Não Funcionais

| RNF | Status | Observação |
| --- | --- | --- |
| RNF01 Usabilidade | ✅ | Fluxo curto, feedback por toast, linguagem clara |
| RNF02 Responsividade | 🟡 | Moldura 410×800 px com media query; em telas baixas o frame fixo corta |
| RNF03 Desempenho | 🟡 | App leve, mas imagens vêm do Unsplash (dependência externa) e não há medição |
| RNF04 Segurança | ❌ | Sem sessão/token, sem autorização, IDOR em todos os endpoints, senha mínima de 4 caracteres |
| RNF05 Privacidade (LGPD) | ❌ | `GET /api/servicos` devolve `prestador_telefone` para **anônimos** (`server.js:144`) |
| RNF06 Integridade | 🟡 | Valida campos básicos; não valida transições de estado nem limites de texto |
| RNF07 Acessibilidade | 🟡 | Contraste da paleta é bom; textos de 10–11 px, ícones em emoji, sem ARIA/foco visível |
| RNF08 Compatibilidade | ✅ | HTML/CSS/JS padrão, testado em Chromium |
| RNF09 Manutenibilidade | ❌ | 3 arquivos monolíticos, CSS inline, lógica de negócio no handler HTTP, zero testes |
| RNF10 Confiabilidade | 🟡 | Erros retornam `{ erro }`, mas várias chamadas do front falham em silêncio |
| RNF11 Rastreabilidade | ❌ | Nenhum log de ação crítica (só `console.log` de boot) |
| RNF12 Escalabilidade | ❌ | `db.json` + escrita síncrona do arquivo inteiro a cada request; sem paginação |

## 5. Cobertura das Regras de Negócio

| RN | Status | Evidência |
| --- | --- | --- |
| RN01 múltiplos papéis | ✅ | `tipo_perfil` aceita `AMBOS` |
| RN02 campos obrigatórios | 🟡 | Exige categoria/título/descrição; região cai em default silencioso |
| RN03 proposta vinculada a solicitação aberta | 🟡 | Vínculo com serviço existe, mas serviço não tem status "aberto" |
| RN04 só o solicitante aceita/recusa | ❌ | O prestador decide; `PATCH /status` aceita qualquer participante (`server.js:295`) |
| RN05 aceite → Em Negociação/Combinado | 🟡 | Vai direto para `Combinado`; publicação não muda de estado |
| RN06 contatos só para participantes | ❌ | UI restringe, API não: qualquer um lê `contratacoes/:usuario_id` |
| RN07 avaliar só após Concluído | ✅ | Validação no servidor (`server.js:311`) |
| RN08 sem autoavaliação | ✅ | Bloqueia contratar o próprio serviço; avaliação é do cliente sobre o prestador |
| RN09 editar sem contratação ativa | ❌ | Não há edição; exclusão não checa contratação ativa |
| RN10 denúncia sem exclusão automática | ❌ | Não há denúncia |
| RN11 advertência sobre dados sensíveis | ❌ | Não há aviso no cadastro/publicação |
| RN12 intermediação sem pagamento | ✅ | Nenhum gateway/escrow, coerente com o MVP |

## 6. Identidade visual (Emerald Trust)

Aderência alta e verificável no CSS (`public/index.html:8-22`):

| Token do documento | Valor | No app |
| --- | --- | --- |
| Emerald Main | `#064E3B` | ✅ `--emerald-main` |
| Deep Pine | `#022C22` | ✅ `--deep-pine` |
| Pure Emerald | `#059669` | ✅ `--pure-emerald` |
| Champagne Gold | `#D97706` | ✅ `--gold` |
| Slate Neutral | `#1E293B` / `#334155` / `#F8FAFC` | ✅ `--text` / `#334155` / `--bg` |
| Tipografia | Arial/Inter 12–28 px | ✅ `Inter, Arial, sans-serif` |
| Botões | Pure Emerald, radius 8–12 px, hover | ✅ |
| Cards | Branco, borda sutil, sombra suave | ✅ |
| Badges de status | Verde aberto/concluído, amarelo negociação | ✅ `badge`, `badge-gold`, `badge-red` |

Extras de apresentação: logo SVG próprio, moldura de celular, barra "Servidor Node.js Ativo",
grade de categorias com imagens. É o ponto mais forte do projeto para a banca.

## 7. Riscos técnicos e de segurança (priorizados)

1. **IDOR / ausência de autenticação**: qualquer requisição pode agir como qualquer usuário
   (`cliente_id`, `prestador_id`, `usuario_id` vêm do cliente). Ex.: `PATCH /api/contratacoes/:id/status`
   só confere se o id enviado é participante — e o id é escolhido por quem chama.
2. **Telefone público**: `GET /api/servicos` retorna `prestador_telefone` sem login (viola RN06/RNF05).
   O front não exibe, mas o dado sai na resposta.
3. **Máquina de estados inexistente**: `PATCH /status` aceita `Combinado`, `Recusado` ou `Concluído`
   a partir de qualquer estado, por qualquer participante. Permite `Recusado → Concluído`.
4. **Média de avaliação fabricada**: `nota_media`/`total_avaliacoes` nascem com números inventados
   (4.9/12, 5.0/19…) e a média é recalculada sobre essa base (`server.js:322-324`).
5. **Senha fraca**: `minlength="4"` no formulário e nenhuma política no servidor; seed `123456`.
6. **Sem logs/auditoria**: nenhuma ação crítica registrada (RNF11).
7. **Exclusão destrutiva**: `DELETE /api/servicos/:id` remove o anúncio mesmo com contratação ativa.
8. **Persistência frágil**: escrita síncrona do `db.json` inteiro; corrupção em concorrência.
9. **Sem paginação** e sem limites de tamanho de texto.
10. **Métricas fixas na UI**: "Verificado", "~20 min de resposta" são hardcoded no detalhe do serviço.

## 8. Telas T01–T07

| Tela | Status | Observação |
| --- | --- | --- |
| T01 Inicial/Explorar | 🟡 | Banner, busca e categorias OK; faltam "cards recentes" na home |
| T02 Listagem | ✅ | Cards + filtros por chips (adaptado de "filtros laterais") |
| T03 Detalhes | ✅ | Completo, com CTA de contratação |
| T04 Formulário de publicação | 🟡 | Só oferta de serviço; não há formulário de solicitação |
| T05 Painel de atividades | ✅ | Pedidos enviados/recebidos, status, contato e avaliação |
| T06 Perfil | 🟡 | Só leitura; sem edição de dados/habilidades |
| T07 Avaliação e moderação | 🟡 | Avaliação OK; denúncia e moderação ausentes |

## 9. Comparação com o backend `TASKO_TCC` (este repositório)

| Aspecto | App do grupo | Backend .NET deste repo |
| --- | --- | --- |
| Modelo | anúncio de serviço + pedido do cliente | publicação `offer`/`request` + proposta do prestador |
| Aceite | prestador decide, sem transação | autor decide, transação única com lock (`FOR UPDATE`) |
| Status | 4 estados livres na contratação | máquina de estados + histórico de transições |
| Contato | telefone na API pública | DTO dedicado só para participantes após aceite |
| Avaliação | 1 lado, média fabricada | 2 lados, só após conclusão, unicidade por publicação |
| Moderação | ausente | denúncia + painel + ocultação com justificativa |
| Persistência | `db.json` | PostgreSQL + migrations |
| Erros | `{ erro }` ad hoc | RFC 7807 com `code` estável |
| Testes | nenhum | 45 testes |

## 10. Recomendação (executada)

O app serve muito bem como **protótipo visual** (identidade Emerald Trust, 7 telas, navegação
mobile), mas **não cobre o fluxo central do documento**. Para o TCC, há dois caminhos:

**A. Reaproveitar a UI e ligar no backend .NET (recomendado e concluído).**
Manter `index.html`/`app.js` como camada visual e trocar o `server.js` por chamadas ao
`TASKO.Api` (`/api/v1/...`), ajustando o modelo:
- T04 passa a ter duas abas: *oferecer serviço* e *publicar solicitação* (RF05/RF06);
- T03 mostra propostas recebidas com valor e perfil do proponente (RF10/RF11);
- T05 vira painel de propostas/contratações com aceite do **autor** e histórico de status (RF12/RF14);
- T06 ganha edição de perfil/habilidades (RF04) e T07 ganha denúncia (RF16) e fila de moderação (RF17).

**B. Manter o app isolado como demonstração** e documentar explicitamente as lacunas (RF04, RF06,
RF16, RF17, RF19, RF21) e os riscos de segurança — arriscado numa banca que comparar com o documento.

Em qualquer caminho, antes de apresentar: corrigir a exposição de telefone, adicionar sessão
(mesmo JWT simples) e implementar a máquina de estados.

## 11. Checklist mínimo para fechar as lacunas P1/P2

- [x] RF04: edição de perfil (apresentação, habilidades, região).
- [x] RF06: publicação de solicitação com prazo e orçamento + listagem própria.
- [x] RF10/RF11: proposta com preço e visualização no painel do solicitante.
- [x] RF12/RN04: aceite exclusivo do autor da publicação, em transação.
- [x] RF14: status da publicação + histórico de transições.
- [x] RF16/RF17: denúncia e painel de moderação.
- [x] RF19: edição/encerramento bloqueados com contratação ativa.
- [x] RF21: recuperação de senha por token.
- [x] RNF04/RN06: sessão autenticada + autorização por participante; contato fora da listagem.
- [x] RNF11: log de ações críticas.
