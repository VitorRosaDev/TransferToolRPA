# TransferTool RPA

O **TransferTool RPA** é uma aplicação desktop desenvolvida em C# / .NET 8 com WPF (Windows Presentation Foundation) para a Automação Robótica de Processos (RPA) de transferências de mercadorias. O robô interage diretamente com o DOM do portal web **Atende.Net** (ERP da IPM Sistemas) utilizando o Microsoft Playwright por meio de conexões CDP (Chrome DevTools Protocol).

Esta ferramenta atua de forma integrada com o **TransferTool Mobile**, importando payloads JSON de cargas geradas offline e automatizando a digitação manual de operador humano no ERP da prefeitura, mitigando erros logísticos e agilizando a distribuição.

> 🔒 **Privacidade:** a ferramenta opera de forma local (conexão CDP com o navegador) e **não expõe dados sensíveis dentro do ERP** a terceiros.

---

## 🏗️ Arquitetura de Software e Padrões

O projeto foi reestruturado sob os princípios da **Clean Architecture** e adota o padrão **Clean MVVM (Model-View-ViewModel)** aliado ao **Command Pattern puro**, garantindo testabilidade isolada e desacoplamento de responsabilidades:

```mermaid
graph TD
    View[MainWindow.xaml] --> ViewModel[MainViewModel]
    ViewModel --> Commands[Commands / ICommand]
    Commands --> Queue[ICargaQueueService]
    Commands --> Automation[IAutomationService]
    Automation --> Engine[AutomationEngine]
    Engine --> Flow[IAtendeNetFlow / AtendeNetFlow]
    Flow --> Selectors[AtendeNetSelectors]
    Flow --> Lote[LoteSelector]
    Engine --> PathResolver[PlaywrightPathResolver]
    Engine --> Nav[NavegadorHelper]
```

### Principais Componentes

**Views**

* [`MainWindow.xaml`](MainWindow.xaml): UI Dark Fluent (Windows 11) com importação por drag-and-drop de JSON, fila de cargas, log em tempo real e `GridSplitter` para redimensionar os painéis.

**ViewModels**

* [`ViewModels/MainViewModel.cs`](ViewModels/MainViewModel.cs): estado reativo da UI (`INotifyPropertyChanged`) e exposição dos comandos de forma desacoplada.

**Commands** (pasta `Commands/`, cada um com responsabilidade única, implementando `ICommand`)

* `AbrirNavegadorCommand`, `ImportarCargaCommand`, `IniciarAutomacaoCommand`, `CancelarAutomacaoCommand`, `ExcluirCargaCommand`.

**Services**

* `ICargaQueueService` / `CargaQueueService`: fila de payloads thread-safe na thread da UI.
* `IAutomationService` / `PlaywrightAutomationService`: dispara o motor Playwright.
* `ILoggerService` / `ObservableLoggerService`: logs concorrentes para o console visual do WPF.
* `IPayloadService` / `PayloadService`: leitura e parse dos JSONs de carga.

**Models (dados, seletores e algoritmo)**

* [`Models/TransferenciaPayload.cs`](Models/TransferenciaPayload.cs): contratos JSON fortemente tipados + `PayloadValidator` (sanitização de inputs).
* [`Models/AutomationEngine.cs`](Models/AutomationEngine.cs): engine Playwright que orquestra o navegador (CDP).
* [`Models/AtendeNetFlow.cs`](Models/AtendeNetFlow.cs) + [`Models/IAtendeNetFlow.cs`](Models/IAtendeNetFlow.cs): fluxo de alto nível (navegação, origem/destino, filtro, lote, carrinho e confirmação); a interface permite testes com mocks.
* [`Models/AtendeNetSelectors.cs`](Models/AtendeNetSelectors.cs): **único ponto de manutenção dos seletores** do ERP.
* [`Models/LoteSelector.cs`](Models/LoteSelector.cs): algoritmo puro de desempate logístico de lotes (1º validade mais curta, 2º menor quantidade).
* [`Models/NavegadorHelper.cs`](Models/NavegadorHelper.cs): abertura/conexão do Chrome/Edge via CDP.
* [`Models/PlaywrightPathResolver.cs`](Models/PlaywrightPathResolver.cs): resolve os caminhos do driver e dos browsers do Playwright em runtime.

---

## 🤖 Fluxo de Automação do Robô (Passo a Passo)

O robô assume a execução a partir do **Passo 4** da rotina operacional de almoxarifado:

1. **Passo 3 (Consulta Aberta):** o operador faz login humano e abre a tela de consulta de transferências (`Almoxarifado » Movimento » Transferência`).
2. **Passo 4 (Acesso à Inclusão):** o robô abre "Outras opções" (`aside.area_acoes span.drop_down`) e aciona "Incluir transferência" (`#context_menu tr:nth-of-type(1) span > span`).
3. **Passo 5 (Depósitos):** preenche Origem e Destino (`aside input.campo-numerico`), dispara `Tab` e aguarda a validação AJAX do ERP.
4. **Passo 6 (Produto):** digita o código no campo de filtro da janela ativa (`[id^="janela_"].janela_ipm_ativa aside td:nth-of-type(3) > input`) e pressiona `Enter`.
5. **Passo 7 (Configuração de Colunas):** garante a coluna "Validade" visível (engrenagem `div.area_total_janela > div span:nth-of-type(5) > input` → marca `ins.jstree-checkbox` → aplicar/fechar).
6. **Passo 8 (Seleção de Lote & Inclusão):** identifica as células da grade por **`nomecoluna`** (`prdcodigo`, `estdatavalidade`, `estquantidade`). Para itens com múltiplos códigos, consulta cada código e **agrega os lotes numa lista única**, ordenando por **validade × quantidade** (lotes sem validade por último) com **completamento entre lotes/códigos**. Seleciona o lote por **matching de validade + quantidade**, preenche `input[name="quantidade_transferir"]` e clica em `button[name="botao_incluir"]`. Itens sem lotes são pulados (log vermelho); estoque insuficiente gera log âmbar (parcial).
7. **Passo 9 (Iteração):** repete os passos 6–8 para todos os itens da carga.
8. **Passo 10 (Confirmação):** clica em "Confirmar" (`button.estrutura_botao_colorido`) e aceita o modal opcional de simulação.

> **Seletores-chave (aprendizados do mapeamento real):**
>
> - A grade e o carrinho usam **`data-subcontexto-id="..."`** (NÃO `id="..."`):
>   - Lotes: `[data-subcontexto-id="subcontexto_dados_tela_consulta_estoque"] tbody tr`
>   - Carrinho: `[data-subcontexto-id="subcontexto_dados_grid_itens_transferencia"] tbody tr.linha_dados`
> - Janela ativa: `[id^="janela_"].janela_ipm_ativa` (desambigua consulta × inclusão).
> - Quantidade lida em cultura **pt-BR** (`NumberStyles.Number` + `CultureInfo("pt-BR")`).

---

## 🚀 Como Executar

### 💿 Instalação rápida (versão release)

A forma mais simples é baixar o instalável da **versão release** direto pelo GitHub, sem precisar compilar o código-fonte:

1. Acesse a página de **[Releases](https://github.com/VitorRosaDev/TransferToolRPA/releases)** do repositório.
2. Baixe o instalador `TransferToolRPA-Setup-<versão>.exe` (arquivo único, self-contained).
3. Execute o instalador (Inno Setup) — ele cria atalhos na **Área de Trabalho** e no **Menu Iniciar** e já embute o runtime .NET, o driver `node.exe` e os browsers do Playwright.
4. Abra o **TransferTool RPA** pelo atalho criado e siga para a seção [Conexão CDP](#3-conexão-cdp-porta-9222).

> O instalador é grande (~250–560 MB) por embutir o runtime .NET e os browsers do Playwright — o mesmo trade-off de instaladores como GIMP/PowerPoint.

### Pré-requisitos (build a partir do código-fonte)

* **.NET SDK 8.0** (Windows).
* **Google Chrome** ou **Microsoft Edge** no caminho padrão.

### 1. Build e testes

```bash
dotnet build -c Release
dotnet test
```

### 2. Publish (self-contained, SEM single-file)

```bash
dotnet publish TransferToolRPA.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

> O publish é **self-contained sem single-file** (o single-file faz `Assembly.Location` retornar vazio e quebra a resolução do driver do Playwright). O target MSBuild `CopyPlaywrightBrowsers` copia a pasta `.playwright/` (driver `node.exe` + browsers `chromium-*`/`ffmpeg-*`) para o publish.
> **Distribuição:** envie a pasta inteira como ZIP e rode `TransferToolRPA.exe` **de dentro** da pasta (não copie apenas o `.exe`).

### 3. Conexão CDP (porta 9222)

1. Abra o Chrome com depuração remota **ou** use o botão **🌐 Abrir Chrome** do próprio app:
   ```powershell
   & "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222 --user-data-dir="$env:LOCALAPPDATA\TransferToolRPA\ChromeProfile" "https://SEU-SERVIDOR.atende.net/atende.php?rot=1&aca=1#!/sistema/28"
   ```

   > Substitua `SEU-SERVIDOR` pela URL do Atende.Net da sua instalação.
   >
2. Faça o **login humano** e navegue até a tela de consulta de transferências.
3. Abra o `TransferToolRPA.exe`, arraste o JSON exportado pelo **TransferTool Mobile** para a área de drag-and-drop e clique em **Iniciar RPA** — as cargas da fila são processadas sequencialmente.

> ⚠️ **Segurança:** com a porta 9222 aberta, qualquer processo local pode controlar o browser. Não navegue em sites sensíveis com esse Chrome aberto.
> **Diagnóstico de erro:** screenshot + HTML + frames são gravados em `%LOCALAPPDATA%\TransferToolRPA\Diagnostico`.
> **Log persistido:** cada execução é gravada em `%LOCALAPPDATA%\TransferToolRPA\Logs\TransferToolRPA-YYYY-MM-DD.log` (rastreabilidade; inclui os diagnósticos do Playwright que não aparecem no console).

---

## 🧪 Suíte de Testes Automatizados (xUnit)

A aplicação conta com **95 casos de teste** (xUnit), distribuídos em 11 arquivos:

| Arquivo                                 |        Casos | Foco                                                                                             |
| --------------------------------------- | -----------: | ------------------------------------------------------------------------------------------------ |
| `PayloadServiceTests.cs`              |           31 | Parsing, validação e normalização multi-código                                              |
| `PayloadSecurityTests.cs`             |            8 | Injeção, Path Traversal e limites                                                              |
| `LoteSelectorTests.cs`                |           17 | Ordenação/planejamento de lotes (validade × quantidade, multi-código, sem validade, parcial) |
| `AtendeNetFlowLogicTests.cs`          |            7 | Leitura de lotes e seleção de lote (grade mockada)                                             |
| `AtendeNetFlowErrorScenariosTests.cs` |            5 | Grade vazia e lote inexistente                                                                   |
| `AutomationEngineTests.cs`            |            7 | Orquestração multi-código, skip, parcial, progresso, cancelamento                             |
| `AutomationIntegrationTests.cs`       |            7 | Hostname do WebSocket CDP e seletores                                                            |
| `PlaywrightPathResolverTests.cs`      |            5 | Resolução de driver/browsers (env vars, idempotência)                                         |
| `ObservableLoggerServiceTests.cs`     |            4 | Persistência de log em arquivo, sanitização de log injection e limite do histórico           |
| `CargaQueueServiceTests.cs`           |            3 | Fila: wrappers, status e mover para o fim                                                        |
| `DiagnosticoHelperTests.cs`           |            1 | Rotação de arquivos de diagnóstico (retenção N dias)                                        |
| **Total**                         | **95** |                                                                                                  |

> Os testes do `AutomationEngine` chamam `ExecutarFluxoAsync()` (orquestração) com o fluxo mockado — **não** abrem navegador.

Para executar a partir da pasta raiz:

```bash
dotnet test
```

---

## 📚 Documentação e Referências

* [`CHANGELOG.md`](CHANGELOG.md): histórico de mudanças por versão.

---

## 📄 Licença

Distribuído sob a licença [GNU Lesser General Public License v3.0](LICENSE) (LGPLv3).
