# TransferToolRPA - Relatório de Estado do Projeto

**Última atualização:** 2026-09-24  
**Branch:** main  
**Status:** 🎉 Primeira transferência E2E concluída. Próximo trabalho: **gargalos operacionais** (produto com muitos lotes/paginação etc.) — ver "🚨 Próximo Gargalo Operacional".

---

## ⚠️ **REGRA OBRIGATÓRIA - ANTES DE QUALQUER TAREFA**

> **Antes de executar qualquer tarefa neste projeto, DEVE-SE percorrer a lista completa de skills (seção "📚 Skills Catalog") em busca de uma skill adequada à tarefa.**
> 
> - É possível que não haja uma skill adequada para a tarefa específica
> - **Mas sempre que houver, ela DEVE ser usada como recurso para garantir a qualidade final da aplicação**
> - Use a ferramenta `skill` para invocar a skill antes de iniciar o trabalho

---

## ✅ Blocker Resolvido — Seletor CSS inválido (`#janela_*`)

**Descoberto em:** 2026-09-23 (teste E2E real, log do app) · **Corrigido em:** 2026-09-23 (mapeamento E2E real via Chrome DevTools MCP)

- **Erro:** `SyntaxError: Failed to execute 'matches' on 'Element': '#janela_*' is not a valid selector.`
- **Causa raiz:** dois seletores em `Models/AtendeNetSelectors.cs` usavam `#janela_*` como "wildcard" para os IDs dinâmicos de janela. `*` **não é válido** dentro de um seletor de ID CSS (os IDs reais são `janela_28027_102_1`, `janela_28027_101_1`, `janela_1790032478819` etc.).
- **Correção aplicada:** trocado `#janela_*` por um seletor **escopado à janela ativa** `[id^="janela_"].janela_ipm_ativa`:
  - A janela em foco recebe a classe `janela_ipm_ativa` (as de fundo recebem `janela_ipm_desativa`).
  - **Importante:** `[id^="janela_"]` puro NÃO bastava — casa **várias** janelas abertas (consulta + inclusão), o que violaria o strict-mode do Playwright (múltiplos elementos). O escopo `.janela_ipm_ativa` desambigua para exatamente 1 elemento.
- **Seletores corrigidos:**
  - `ConfiguracaoColunas.BotaoConfigurar` → `[id^="janela_"].janela_ipm_ativa > div.area_total_janela > div span:nth-of-type(5) > input`
  - `FiltroProduto.InputFiltro` → `[id^="janela_"].janela_ipm_ativa aside td:nth-of-type(3) > input`
- **Código morto removido:** `AtendeNetSelectors.GetJanelaDinamicaPrefixo()` (não tinha chamadores).

### Bugs adicionais descobertos no mapeamento (e corrigidos)
- **`CheckboxValidade`** clicava o `<a>` do nó jsTree (não alterna o checkbox). Corrigido para `... > a > ins.jstree-checkbox` (o `<ins>` do checkbox real).
- **`ObterLotesDisponiveisAsync`** usava índice de coluna **8** para "Quantidade" (na verdade a coluna Quantidade é **td[11] = índice 10**; Validade é td[9] = índice 8). Sem isso, a quantidade era lida da coluna de validade (parsing → 0) e **nenhum lote** era selecionado.
- **`GradeLotes.Cabecalhos`** busca `<th>`, mas a grade usa `<td class="estrutura_titulo_colunas_consulta">` — a detecção dinâmica de colunas não dispara; os índices hardcoded (corrigidos) assumem.

---

## ✅ SUCESSO — Primeira transferência E2E concluída

**Em:** 2026-09-24 (~22:41)

- Carga destino **99** com 3 itens (2203 Qtd 20, 7513 Qtd 10, 2227 Qtd 20) — **todos incluídos e confirmados** (`Processo concluído com sucesso!`).
- Confirma o fluxo E2E completo: navegação → origem/destino → colunas → filtro → lote (validade) → quantidade → incluir → carrinho → confirmar.

## 🚨 Próximo Gargalo Operacional — Produto com MUITOS lotes (paginação da grade)

**Descoberto em:** 2026-09-24 (~22:40, carga destino 80)

- **Erro:** `Timeout 30000ms exceeded. waiting for Locator("[data-subcontexto-id=\"subcontexto_dados_tela_consulta_estoque\"] tbody tr").Nth(7).Locator("td[nomecoluna=\"estdatavalidade\"]")`.
- **Contexto:** item `2201` (Qtd 50). O HTML de diagnóstico (`diag_20260924_224004.html`) mostra a grade com **`aria-rowcount="4"`** (4 lotes, todos `2201`), mas o código tentou clicar na **8ª linha** (`.Nth(7)`).
- **Causa (hipótese):** o produto 2201 tem **mais lotes do que a grade exibe por página** — o diagnóstico confirma `mostraRegistrosPagina: true` / `mostraPaginador: true` (a grade tem **paginação**). A leitura capturou 8+ linhas em um estado transitório/paginado, escolheu o lote na posição 7 (validade mais antiga), mas a grade assentou em 4 linhas (página 1) → a 8ª linha não existe/está fora da página → timeout.
- **Resposta a hipótese de causa:** No feedback visual não observo a presença de paginação. Vejo apenas uma lista com 4 opções. 
|Descrição|Validade|Quantidade|
|ARROZ PARBOILIZADO|14/09/2027|1.860,00000|
|ARROZ PARBOILIZADO|10/08/2027|156,00000|
|ARROZ PARBOILIZADO|10/08/2027|4.620,00000|
|ARROZ PARBOILIZADO|10/08/2027|1.830,00000|
PÁGINA 1 DE 1

Para este caso, o correto seria iterar sobre os lotes com vencimento em 10/08/2027 e decidir começar pelo lote que tem 156,00000 unidades.
- **Próximo passo:** identificar se há linhas ocultas na UI, mapear esta e outras possíveis situações operacionais recorrentes no fluxo de operação.

### 🎯 Situações operacionais comuns a tratar (próxima sessão)
Estes casos obedecem lógicas simples e devem ter tratamento previsto no fluxo:
1. **Produto com muitos lotes** (mais que uma página da grade) → paginação/navegação.
2. **Produto sem lote/estoque** (grade vazia após filtrar) → erro claro + pular item.
3. **Quantidade maior que o estoque de um lote** → completamento automático com múltiplos lotes (lógica já parcialmente presente em `SelecionarMelhorLoteComCompletamento`).
4. **Código de produto inexistente** → grade vazia → mensagem de erro.
5. **Modais/diálogos inesperados** (ex.: confirmação "Estorno", alertas) → tratamento genérico.
6. **Produto repetido na mesma carga** → consolidar ou tratar duplicidade.

### ✅ Bloqueadores anteriores RESOLVIDOS (2026-09-24, tarde)
1. **`Index was out of range` / `.Nth(4)` na seleção de lote** — corrida do filtro (grade não filtrada). Corrigido: `AguardarGradeResultadosAsync(codigo)` espera a 1ª linha refletir o `prdcodigo`; `SelecionarLotePorValidadeAsync` usa `melhorLote.Linha`.
2. **Falso-positivo de "loading"** (`[class*='aguarde']` casa com o overlay persistente "Aguarde") → removida a verificação `IndicaCarregamentoAsync` e a classe `Carregamento`; `AguardarGradeResultadosAsync` depende só do `prdcodigo`.
3. **Seletores posicionais frágeis** → trocados por **`nomecoluna`** (`prdcodigo`, `estdatavalidade`, `estquantidade`).
4. **Quantidade não preenchida** → `input[name="quantidade_transferir"]`; `button[name="botao_incluir"]`.
5. **Quantidade limpa por corrida** → `AguardarSelecaoLoteAsync()` (espera `quantidade_disponivel` > 0).
6. **Detecção do carrinho** → conta `tr.linha_dados` (o `aria-rowcount` do carrinho não atualiza).

### Aprendizado-chave desta sessão (seletores)
- A grade e o carrinho usam **`data-subcontexto-id="..."`** (NÃO `id="..."`):
  - Grade de lotes: `[data-subcontexto-id="subcontexto_dados_tela_consulta_estoque"] tbody tr`
  - Carrinho: `[data-subcontexto-id="subcontexto_dados_grid_itens_transferencia"]` + checar `aria-rowcount > 0`
- **Pegadinha:** grep `id="subcontexto` casa com `data-subcontexto-id="subcontexto` (sufixo `-id`) e induz a erro — conferir o nome completo do atributo.
- Botão "Consultar" do filtro: `<input type=button>` coberto por `<span>` (rótulo); clicar no wrapper `span[name="consultar"][title="Consultar"]`.
- Quantidade: usar cultura pt-BR (`NumberStyles.Number` + `CultureInfo("pt-BR")`) — `"1.100,00000".Replace(",", ".")` vira 110000000.

## ▶️ Início da Sessão de Mapeamento — Passo a Passo (Chrome porta 9222)

> Sequência de comandos para abrir o Chrome com depuração remota e dar início ao mapeamento do fluxo junto ao agente (Cline + `chrome-devtools-mcp`).

### 1. Fechar instâncias existentes do Chrome
Feche **todas** as janelas do Chrome. Se já houver um Chrome rodando, a flag `--remote-debugging-port` é **ignorada** e a nova instância delega para a existente (a porta 9222 **não** abre).

### 2. Abrir o Chrome com depuração remota (PowerShell)
```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222 --user-data-dir="$env:LOCALAPPDATA\TransferToolRPA\ChromeProfile" "https://alvorada.atende.net/atende.php?rot=1&aca=1`#!/sistema/28"
```
- `--remote-debugging-port=9222` → CDP na porta 9222 (usado pelo TransferToolRPA **e** pelo MCP do Cline).
- `--user-data-dir=...\ChromeProfile` → perfil dedicado (não expõe o perfil pessoal; exigência de segurança do Chrome para CDP).

### 3. Login manual no Atende.Net
Na janela aberta, faça o login manual. **O agente não usa/armazena suas credenciais.**

### 4. Avisar o agente
No Cline, diga **"estou logado"**. O agente confirma com `list_pages` + `take_snapshot` e inicia o mapeamento passo a passo.

### 5. Executar o fluxo (1–2x)
Execute a transferência **passo a passo**, pausando em cada tela para o agente capturar (snapshot). Transferências reais podem ser criadas e **estornadas** depois — sem problema.

> ⚠️ **Segurança:** com a porta 9222 aberta, qualquer processo local pode controlar o browser. Ao terminar, feche esse Chrome e não navegue em sites sensíveis com ele aberto.

---

## 📋 Visão Geral

**TransferToolRPA** é uma aplicação WPF (.NET 8) para automação de transferências de estoque no sistema **Atende.Net** (IPM Sistemas). O aplicativo carrega payloads JSON gerados pelo app mobile de conferência TransferToolMobile ("C:\dev\TransferToolMobile\TransferTool") e executa a automação via Playwright conectando a uma instância Chrome/Edge já aberta com depuração remota (porta 9222).

### Objetivo Principal
Automatizar o preenchimento de formulários de transferência entre depósitos no Atende.Net, eliminando entrada manual repetitiva e propensa a erros.

### Contexto Técnico
- **ERP Alvo:** Atende.Net (IPM Sistemas) - URL: `https://alvorada.atende.net/atendenet#!/sistema/28`
- **Método:** CDP (Chrome DevTools Protocol) via Playwright
- **Entrada:** JSON com array de transferências (origem, destino, itens com código + quantidade)
- **Saída:** Transferências criadas no ERP via UI automation

---

## 🏗️ Arquitetura

```
TransferToolRPA/
├── Models/
│   ├── TransferenciaPayload.cs      # Modelos + Validação + Parsing JSON
│   ├── AutomationEngine.cs          # Orquestrador principal (CDP + Playwright)
│   ├── AtendeNetSelectors.cs        # Seletores centralizados (extraídos de gravações humanas)
│   ├── AtendeNetFlow.cs             # Fluxo de alto nível (navegação, preenchimento, lotes)
│   ├── LoteSelector.cs              # Lógica de seleção de lote (validade + quantidade)
│   ├── NavegadorHelper.cs           # Inicialização Chrome/Edge com CDP
│   ├── PlaywrightPathResolver.cs    # Resolve PLAYWRIGHT_DRIVER_SEARCH_PATH + PLAYWRIGHT_BROWSERS_PATH
│   └── IAtendeNetFlow.cs            # Interface para testabilidade
├── Services/
│   ├── PlaywrightAutomationService.cs
│   ├── PayloadService.cs
│   ├── CargaQueueService.cs
│   └── ObservableLoggerService.cs
├── ViewModels/
│   └── MainViewModel.cs             # MVVM + Commands
├── Commands/                        # ICommand implementations
└── TransferToolRPA.Tests/           # 78 testes unitários
```

### Padrões Utilizados
- **MVVM** com CommunityToolkit.Mvvm (Commands, ObservableProperty)
- **Dependency Injection** manual (construtores)
- **Strategy Pattern** para `IAtendeNetFlow` (testabilidade)
- **Retry com Backoff** para operações flaky
- **Diagnóstico Rico** (screenshot + HTML + frames no erro)

---

## ✅ Funcionalidades Implementadas

### Fase 1 - Modelo de Dados (Concluída)
- [x] Parsing de JSON com array de transferências
- [x] Validação robusta (códigos, quantidades, caracteres suspeitos)
- [x] Suporte a múltiplas transferências por arquivo
- [x] Fila de processamento sequencial
- [x] 21 testes de validação

### Fase 2 - Automação Playwright (Concluída)
- [x] Seletores centralizados baseados em 2 gravações humanas reais
- [x] Fluxo desacoplado: `AtendeNetFlow` (navegação, origem/destino, filtros, lotes, confirmação)
- [x] Espera robusta por janela/frame dinâmico (`page.Frames` + seletores contextuais)
- [x] Seleção de lote por **3 critérios**:
  1. Validade mais antiga
  2. Menor quantidade (empate)
  3. Completamento automático (usa qtd do 1º, se insuficiente completa com próximo)
- [x] Retry com backoff exponencial (3 tentativas)
- [x] Diagnóstico rico: screenshot + HTML + frames no erro
- [x] 25 testes de integração (mocks NSubstitute)

### Fase 3 - Qualidade + CI/CD (Concluída)
- [x] Async/await puro (sem `.Wait()`/`.Result`)
- [x] Interface `IAtendeNetFlow` para DI e mocks
- [x] 78 testes totais (42 originais + 36 novos) - **100% passing**
- [x] GitHub Actions CI: build + test + code quality
- [x] Publish self-contained (ver Fase 4 - single-file foi removido)

### Fase 4 - Deploy & Runtime (Concluída)
- [x] Publish **self-contained SEM single-file** (corrige a resolução do driver do Playwright)
- [x] **Driver (node.exe) + browsers embutidos no publish** (`.playwright/` com `node/win32_x64/node.exe`, `package/`, `chromium-1117`, `ffmpeg-1009`)
- [x] **Resolução de caminhos centralizada** em `PlaywrightPathResolver` (aplicada em `App.OnStartup` + `AutomationEngine.ExecutarAsync`)
- [x] **Drag & Drop funcional** (`AllowDrop="True"` em Border + Grid + StackPanel)
- [x] **Detecção de execução como Admin** (aviso proativo no log: `[AVISO] Drag & drop pode não funcionar quando executado como Administrador.`)
- [x] **UX melhorada no botão "Abrir Chrome"** (mensagens orientativas: "Faça login manualmente e, após acessar o sistema, clique em 'Iniciar RPA'")
- [ ] **Teste end-to-end real** (pendente - requer login manual no Atende.Net com Chrome CDP)

---

## 🔧 Configuração Atual

### Publicação (`.csproj`)
```xml
<!-- self-contained SEM single-file -->
<UseAppHost>true</UseAppHost>
<ApplicationIcon>icon.ico</ApplicationIcon>
```

> **Nota:** `EnableCompressionInSingleFile` e `IncludeNativeLibrariesForSelfExtract` foram **removidos**.
> O single-file fazia `Assembly.Location` retornar vazio, quebrando a resolução do driver do
> Playwright e impedindo a cópia do `node.exe` para o publish.

### Ícone
- ✅ Multi-resolução (256, 128, 64, 48, 32, 24, 16) - 304 KB
- ✅ Embedado no `.exe` via `<ApplicationIcon>`

### Playwright
- **Driver + browsers embutidos no publish** (`.playwright/` inclui `node/win32_x64/node.exe`, `package/`, `chromium-1117`, `ffmpeg-1009`)
- Conecta via CDP na porta 9222
- Fallback: inicia Chrome/Edge com perfil dedicado se não encontrar instância
- **Auto-instalação REMOVIDA** (era: `dotnet tool install Microsoft.Playwright.CLI` + `playwright install chromium`)
- **Resolução de caminhos** centralizada em `Models/PlaywrightPathResolver.cs` (chamada em `App.OnStartup` **antes** de qualquer uso do Playwright, e defensivamente em `AutomationEngine.ExecutarAsync`):
  - `PLAYWRIGHT_DRIVER_SEARCH_PATH` → diretório **base** (que CONTÉM `.playwright`); controla o **driver** (`node.exe`).
  - `PLAYWRIGHT_BROWSERS_PATH` → pasta `.playwright`; controla os **browsers** (`chromium-*`, `ffmpeg-*`).

> ⚠️ **Lição crítica:** são DUAS variáveis distintas. Apontar `PLAYWRIGHT_DRIVER_SEARCH_PATH`
> para `.playwright` (em vez do diretório base) faz o Playwright lançar
> `Couldn't find driver in "PLAYWRIGHT_DRIVER_SEARCH_PATH"` (verificado por reflexão na DLL 1.44.0).

---

## 🧪 Testes

| Suite | Testes | Cobertura |
|-------|--------|-----------|
| PayloadServiceTests | 21 | Validação, parsing, segurança |
| PayloadSecurityTests | 6 | Injection, path traversal, limites |
| AtendeNetFlowLogicTests | 6 | Lógica seleção de lote (3 critérios) |
| AtendeNetFlowErrorScenariosTests | 13 | Cenários de erro (grade vazia, timeouts, etc) |
| AutomationEngineTests | 7 | Orquestração, progresso, cancelamento, códigos |
| AutomationIntegrationTests | 6 | WebSocket, seletores, LoteSelector |
| PlaywrightPathResolverTests | 5 | Resolução driver/browsers (env vars, idempotência) |
| **Total** | **78** | **100% passing** |

> **Nota:** 30 warnings `CS8625` (nullable) nos testes - não afetam execução.

---

## 📦 Build & Execução

### Build Local
```bash
dotnet build -c Release
dotnet test
```

### Publish (self-contained, SEM single-file)
```bash
dotnet publish TransferToolRPA.csproj -c Release -r win-x64 --self-contained true -o ./publish
# Output: pasta ./publish/ com TransferToolRPA.exe (~0,4 MB) + DLLs do .NET + .playwright/
#   - driver:  .playwright/node/win32_x64/node.exe  (~66 MB)
#   - browsers: .playwright/chromium-1117, .playwright/ffmpeg-1009
# Total da pasta: ~560 MB
```

> **Distribuição:** envie a pasta inteira como ZIP. O usuário descompacta e roda
> `TransferToolRPA.exe` **de dentro da pasta** (não copie apenas o `.exe`).

### Execução
```powershell
# 1. Iniciar Chrome com CDP (ANTES do app)
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222 --user-data-dir="$env:LOCALAPPDATA\TransferToolRPA\ChromeProfile" "https://alvorada.atende.net/atende.php?rot=1&aca=1`#!/sistema/28"

# 2. Login manual no Atende.Net no Chrome aberto

# 3. Executar app
.\TransferToolRPA.exe
```

### Payload de Teste
```bash
# Arquivos disponíveis em C:\Users\vitor\Downloads\
# - "Payload gerado pelo aplicativo de conferência.json" (1 transferência, 9 itens)
# - "Payload gerado pelo aplicativo de conferência 2.json" (2 transferências, 13 itens)
```

---

## 🛠️ Ferramenta de Desenvolvimento — Chrome DevTools MCP

Para depurar/validar seletores contra o DOM real do Atende.Net **sem** depender de erros do app, foi configurado o **`chrome-devtools-mcp`** no Cline. É ferramenta de desenvolvimento apenas — **NÃO** faz parte do `TransferToolRPA.exe` publicado.

**Arquivo de config do Cline:** `C:\Users\vitor\.cline\data\settings\cline_mcp_settings.json`

```json
{
  "mcpServers": {
    "chrome-devtools": {
      "command": "npx",
      "args": ["-y", "chrome-devtools-mcp@latest", "--browser-url=http://127.0.0.1:9222", "--no-usage-statistics"]
    }
  }
}
```

- **Pré-requisitos (OK nesta máquina):** Node.js LTS (`v26.7.0`) + npm/npx (`12.0.2`).
- **Conexão:** `--browser-url=http://127.0.0.1:9222` conecta à instância Chrome **já aberta** com `--remote-debugging-port=9222` (o mesmo fluxo já usado pelo app). O MCP **não** abre um navegador novo.
- **Flags:** `--no-usage-statistics` desativa a telemetria do Google (recomendado para dados corporativos/ERP).
- **Após editar:** reiniciar o Cline/VS Code para o MCP carregar as tools.
- **Referência oficial:** <https://github.com/ChromeDevTools/chrome-devtools-mcp>

---

## ⚠️ Pontos de Atenção / Riscos Conhecidos

| Item | Status | Mitigação |
|------|--------|-----------|
| **Seletor CSS inválido `#janela_*`** | `SyntaxError ... '#janela_*' is not a valid selector` | **PENDENTE** — trocar por `[id^="janela_"]` em `AtendeNetSelectors.cs` |
| **Seletores Atende.Net** | Baseados em 2 gravações | Podem variar por versão/usuário; ajustar em `AtendeNetSelectors.cs` |
| **Frames dinâmicos** | IDs gerados (`conteudo_28027_102_1`) | Busca recursiva em `page.Frames` + seletores contextuais |
| **Validação AJAX depósito** | Race condition | `Task.Delay(500)` após Tab |
| **Playwright browsers** | ~150 MB embutidos | Embutidos no publish; sem download em runtime |
| **Chrome CDP obrigatório** | App falha sem porta 9222 | Fallback inicia Chrome, mas usuário deve logar |
| **Single-file WPF** | `DllNotFoundException` + driver não publicado | **RESOLVIDO** — publish self-contained **sem** single-file; `Assembly.Location` válido |
| **Driver (node.exe) não publicado** | `Driver not found: ...\node\win32_x64\node.exe` | **RESOLVIDO** — `.playwright/node/win32_x64/node.exe` copiado no publish |
| **Drag & Drop falha como Admin** | Integridade alta vs Explorer média | Log de aviso proativo; orientar executar sem elevação |
| **Tamanho do publish** | ~560 MB (pasta completa) | Trade-off: standalone total (runtime + browsers + driver) vs download runtime; distribuir como ZIP |

---

## 📂 Estrutura de Arquivos Principais

```
C:\dev\TT2\RPA\
├── TransferToolRPA.csproj
├── icon.ico                          # Multi-res (7 tamanhos)
├── PROJECT_REPORT.md                 # Este arquivo
├── .github/workflows/ci.yml          # CI/CD
├── Models/
│   ├── TransferenciaPayload.cs
│   ├── AutomationEngine.cs
│   ├── AtendeNetSelectors.cs
│   ├── AtendeNetFlow.cs
│   ├── LoteSelector.cs
│   ├── NavegadorHelper.cs
│   ├── PlaywrightPathResolver.cs
│   └── IAtendeNetFlow.cs
├── Services/
├── ViewModels/
├── Commands/
├── TransferToolRPA.Tests/
│   ├── PayloadServiceTests.cs
│   ├── PayloadSecurityTests.cs
│   ├── AtendeNetFlowLogicTests.cs
│   ├── AtendeNetFlowErrorScenariosTests.cs
│   ├── AutomationEngineTests.cs
│   ├── AutomationIntegrationTests.cs
│   └── PlaywrightPathResolverTests.cs
└── publish/                          # Output do publish (self-contained, sem single-file)
    ├── TransferToolRPA.exe           # ~0,4 MB
    ├── *.dll                         # runtime .NET + app
    └── .playwright/                  # driver (node.exe) + browsers (copiados no publish)
```

---

## 🎯 Próximos Passos Imediatos

### 0. ✅ Corrigir seletor CSS inválido `#janela_*` (CONCLUÍDO)
- [x] Trocado `#janela_*` → `[id^="janela_"].janela_ipm_ativa` em `BotaoConfigurar` e `InputFiltro`.
- [x] Removido `GetJanelaDinamicaPrefixo()` (código morto).
- [x] Corrigido `CheckboxValidade` (clicar `ins.jstree-checkbox`) e índice da coluna Quantidade (8 → 10).
- [x] Validado com `dotnet build -c Release` (0 erros) + `dotnet test` (78 testes 100% passing).

### 1. Teste End-to-End Real (pendente)
```powershell
# Terminal 1: Iniciar Chrome CDP
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222 --user-data-dir="$env:LOCALAPPDATA\TransferToolRPA\ChromeProfile" "https://alvorada.atende.net/atende.php?rot=1&aca=1`#!/sistema/28"

# Terminal 2: Testar app
cd C:\dev\TT2\RPA\publish
.\TransferToolRPA.exe
# UI: Importar JSON (drag & drop ou clique) → Iniciar Automação
# Esperado: log "Driver do Playwright localizado: ...node.exe" e NÃO ocorrer "Driver not found"
```

### 2. Validação de Seletores
Se falhar: logs mostram qual seletor falhou → ajustar `AtendeNetSelectors.cs` baseado no HTML real.

### 3. Ajustes Finais
- Timeouts em `AguardarJanelaInclusaoAsync` / `AguardarGradeResultadosAsync`
- Logs de progresso mais granulares
- Tratamento de modais inesperados

---

## 📝 Contexto para Próxima Sessão

> **Este projeto está ~97% completo.** Automação implementada e testada (78 testes 100% passing).
> O blocker de deploy foi **RESOLVIDO**: o driver `node.exe` agora é publicado corretamente.
>
> **Sessão 2026-09-24 (tarde/noite):** 🎉 primeira transferência E2E concluída (destino 99). Resolvidos os blockers de seleção de lote (corrida do filtro), falso-positivo de "loading", seletores posicionais (→ `nomecoluna`), quantidade não preenchida/limpa por corrida e detecção do carrinho (`tr.linha_dados`). Próximo trabalho: **gargalos operacionais** — produto com muitos lotes (paginação), produto sem estoque, quantidade insuficiente, código inexistente, modais, duplicidade. Ver "🚨 Próximo Gargalo Operacional".
> Mapeamento E2E real feito via Chrome DevTools MCP (`--browser-url=http://127.0.0.1:9222`) validou todos os seletores contra o DOM real.
> Também corrigidos: `CheckboxValidade` (clicar `ins.jstree-checkbox`) e índice da coluna Quantidade na grade (8 → 10).

**RESOLUÇÃO DO BLOCKER (2026-09-23):**
- **Erro original:** `Driver not found: C:\dev\TT2\.playwright\node\win32_x64\node.exe`
- **Causa raiz (duas camadas):**
  1. O `node.exe` (driver) **nunca era copiado** para o publish — a pasta `publish\.playwright\node\win32_x64\` estava vazia (só havia chromium/ffmpeg).
  2. O single-file self-contained fazia `Assembly.Location` retornar vazio, levando o Playwright a resolver um caminho relativo contra o diretório de trabalho.
- **Correção aplicada:**
  1. Removido o single-file (`EnableCompressionInSingleFile`, `IncludeNativeLibrariesForSelfExtract`).
  2. Criado `Models/PlaywrightPathResolver.cs` — seta `PLAYWRIGHT_DRIVER_SEARCH_PATH` (driver) e `PLAYWRIGHT_BROWSERS_PATH` (browsers) em `App.OnStartup` **antes** de qualquer uso do Playwright.
  3. Removido o antigo `ConfigurarPlaywrightBrowsersPath()` (duplicado/verboso) do `AutomationEngine`.
  4. Corrigido o `dumpDir` hardcoded no `AtendeNetFlow` → `%LOCALAPPDATA%\TransferToolRPA\Diagnostico`.

**Para continuar (ordem):**
1. **Teste end-to-end real** com Chrome CDP ativo (porta 9222) + login manual no Atende.Net.
2. Se o teste falhar em um seletor, ajustar `AtendeNetSelectors.cs`.

**Arquivos-chave para manutenção:**
- `Models/AtendeNetSelectors.cs` - Seletores (único ponto de manutenção)
- `Models/AtendeNetFlow.cs` - Lógica de fluxo
- `Models/AutomationEngine.cs` - Orquestração
- `Models/PlaywrightPathResolver.cs` - **NOVO:** resolução driver/browsers
- `App.xaml.cs` - Config Playwright early
- `TransferToolRPA.Tests/` - 78 testes (73 originais + 5 do resolver)

---

## 🔗 Referências Externas

- **Gravações humanas (referência seletores):**
  - `C:\dev\TT2\RPA\Artefatos\Gravação de fluxo executado no ERP WEB via chrome dev tools.json`
  - `C:\dev\TT2\RPA\Artefatos\Gravação de fluxo executado no ERP WEB via chrome dev tools 2.json`
- **Payloads de exemplo:**
  - `C:\dev\TT2\RPA\Artefatos\Payload gerado pelo aplicativo de conferência.json`
  - `C:\dev\TT2\RPA\Artefatos\Payload gerado pelo aplicativo de conferência 2.json`

---

*Relatório gerado automaticamente para continuidade de desenvolvimento entre sessões agenticas.*

---

## 📚 Skills Catalog - Catálogo Completo de Skills

### ✅ **Skills Usadas Nesta Sessão (Invocadas Explicitamente)**

| # | Skill | Descrição | Quando Foi Usada |
|---|-------|-----------|------------------|
| 1 | **browser-automation** | Expert em automação de browser (Playwright, Puppeteer, Selenium). Seletores, waits, detecção. | Planejamento Fase 2 - seletores e fluxo Playwright |
| 2 | **playwright-skill** | Automação Playwright customizada. Detecção de dev servers, scripts em /tmp, execução visível. | Testes de integração com mocks NSubstitute |
| 3 | **plan-writing** | Framework para quebrar trabalho em tarefas claras, verificáveis, ordenadas logicamente. | Criação do plano detalhado Fase 1 |
| 4 | **debugging-strategies** | Diagnóstico de `Assembly.Location` vazio em single-file, `Environment.ProcessPath` vs `AppContext.BaseDirectory`. | Resolução do `PLAYWRIGHT_BROWSERS_PATH` blocker |
| 5 | **deployment-procedures** | `AfterPublish` target para copiar `.playwright/`, single-file com assets nativos embutidos. | Browsers embutidos no publish |
| 6 | **error-handling-patterns** | Fallback cascade para resolução de caminho (ProcessPath → CommandLineArgs → BaseDirectory). | Configuração robusta do browsers path |

### ✅ **Skills Aplicadas Implicitamente (Relevantes ao Trabalho Realizado)**

| # | Skill | Como Se Aplicou |
|---|-------|-----------------|
| 4 | **clean-code** | Refatoração do `AutomationEngine`, separação em `AtendeNetFlow`/`AtendeNetSelectors` |
| 5 | **code-review-and-quality** | Revisão dos 78 testes, correção de warnings nullable |
| 6 | **testing-patterns** / **pytest-skill** | Estrutura dos testes unitários (AAA, mocks NSubstitute) |
| 7 | **debugging-strategies** | Diagnóstico do `DllNotFoundException` no single-file WPF, `Assembly.Location` vazio, `Environment.ProcessPath` |
| 8 | **error-handling-patterns** | Retry com backoff no `AtendeNetFlow`, auto-instalação Playwright, fallback cascade para resolução de caminho |
| 9 | **documentation** | Geração do `PROJECT_REPORT.md` |
| 10 | **git-workflow-and-versioning** | Commits implícitos nas modificações |
| 11 | **dependency-management-deps-audit** | Verificação indireta via `dotnet build/test` |
| 12 | **deployment-procedures** | Publish single-file self-contained com configurações corretas, `AfterPublish` target para copiar `.playwright/` |
| 13 | **debugging-strategies** | Diagnóstico de `Assembly.Location` vazio em single-file, `Environment.ProcessPath` vs `AppContext.BaseDirectory` |
| 14 | **deployment-procedures** | `AfterPublish` target para copiar `.playwright/`, single-file com assets nativos embutidos |
| 15 | **error-handling-patterns** | Fallback cascade para resolução de caminho (ProcessPath → CommandLineArgs → BaseDirectory → user profile) |

### 🔴 **Skills Críticas para Finalização (Devem Ser Usadas Próximos Passos)**

| # | Skill | Por Que Aplicar |
|---|-------|-----------------|
| 13 | **acceptance-orchestrator** | Orquestra validação end-to-end: issue → implementação → review → deploy → verificação runtime com mínimo re-intervenção. Ideal para o teste final no Atende.Net. |
| 14 | **android_ui_verification** / **appium-skill** | Embora seja desktop, o padrão de verificação UI automatizada (screenshots, assertions, relatórios) se aplica. Pode adaptar para validação visual do WPF/Playwright. |
| 15 | **agenttrace-session-audit** | Audita sessões de coding agent: custo, falhas de tools, latência, anomalias. Útil para auditar a sessão atual antes de considerar "done". |
| 16 | **atlas-contract** | Goal-integrity: emite contratos de objetivo, detecta drift, registra lições aprendidas. Garante que o teste final cumpra o objetivo original. |
| 17 | **atlas-ledger** | Companheiro do atlas-contract. Registra erros/drifts como lições aprendidas (WHEN/DON'T/INSTEAD). |

### 🟡 **Skills Valiosas para Qualidade/Produção**

| # | Skill | Aplicação |
|---|-------|-----------|
| 18 | **aegisops-ai** | DevSecOps/FinOps autônomo: audita patches, custos, compliance K8s. Se for deployar em containers/K8s no futuro. |
| 19 | **aws-sst-development** / **azd-deployment** | Deploy infra-as-code para ambientes de homologação/produção. |
| 20 | **observability-and-instrumentation** | Adiciona logging, métricas, tracing, alerting ao app. Essencial para monitorar automações em produção. |
| 21 | **slo-implementation** | Define SLIs/SLOs com error budgets. Garante confiabilidade da automação (ex: "99% das transferências completam em <5min"). |
| 22 | **security-audit** / **007** | Auditoria de segurança completa (OWASP, STRIDE, secrets, deps). Importante se app rodar em ambiente corporativo. |
| 22 | **agent-memory** / **tree-ring-memory** | Persiste decisões arquiteturais, contexto de sessão, lições aprendidas entre execuções. |

### 🟢 **Skills Nice-to-Have (Futuro)**

| # | Skill | Contexto |
|---|-------|----------|
| 23 | **app-store-changelog** | Gera release notes automáticas do git para versão. |
| 24 | **changelog-automation** | Automatiza changelog seguindo Keep a Changelog. |
| 25 | **api-onboarding** | Se expor API no futuro (ex: webhook de status da transferência). |
| 26 | **analyze-project** | Forense retrospectiva: classifica scope deltas, rework, root causes. |
| 27 | **agent-orchestrator** | Meta-skill que orquestra todos os agentes do ecossistema. Scan automático de skills, match por capacidades. |
| 28 | **antigravity-skill-orchestrator** | Meta-skill para seleção dinâmica de skills por requisitos da tarefa. |
| 28 | **anti-sycophancy** | Elimina concordância sicofântica. Garante revisão crítica honesta. |
| 29 | **anti-deception** | Detecta pressão para validar/agrees sem análise. Garante revisão honesta. |
| 30 | **ask-questions-if-underspecified** | Clarifica requisitos antes de implementar. Usar quando houver dúvidas sérias. |
| 31 | **skill-router** | Roteador: entrevista usuário e recomenda melhor skill. Usar quando não souber qual skill aplicar. |
| 32 | **ask-matt** | Router sobre skills do usuário. Pergunta qual skill/flow cabe na situação. |

---

## 🎯 Prioridade de Uso para Próximos Passos

1. **acceptance-orchestrator** → Estruturar teste end-to-end final
2. **atlas-contract** → Garantir que teste valide objetivo real
3. **observability-and-instrumentation** → Instrumentar app antes de produção
4. **agenttrace-session-audit** → Auditar sessão antes de fechar
5. **atlas-ledger** → Registrar lições aprendidas do desenvolvimento
6. **security-audit / 007** → Auditoria segurança antes de produção
7. **slo-implementation** → Definir SLIs/SLOs para automação

---

*Catálogo mantido para continuidade entre sessões. Antes de qualquer tarefa, consulte esta lista.*