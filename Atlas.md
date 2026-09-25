# Atlas.md — Ledger do Projeto (TransferToolRPA)

> Registro de lições aprendidas (drift) para guiar contratos de objetivo em sessões futuras.
> Mantido pelo skill `atlas-ledger`. Chaves de máquina (`WHEN`/`DON'T`/`INSTEAD`, IDs, `seen`,
> `severity`, `Source`) ficam em inglês; o conteúdo das cláusulas é escrito em português.
> Cláusulas confirmadas viram guard-rails para o `atlas-contract`; observações provisórias são apenas consultivas.

---

## Confirmed Clauses

### L1 — Playwright: driver e browsers são variáveis distintas; single-file quebra o driver

- **WHEN:** publicando (`dotnet publish`) uma aplicação .NET que usa `Microsoft.Playwright`, ou configurando os caminhos do Playwright em runtime.
- **DON'T:** usar `PublishSingleFile` self-contained, nem setar `PLAYWRIGHT_DRIVER_SEARCH_PATH` para a própria pasta `.playwright` (ou para `node\win32_x64`).
- **INSTEAD:** publicar **self-contained SEM single-file** e apontar `PLAYWRIGHT_DRIVER_SEARCH_PATH` para o diretório **base** (aquele que CONTÉM a pasta `.playwright`). Lembrar que:
  - `PLAYWRIGHT_DRIVER_SEARCH_PATH` resolve o **driver** (`node.exe`, em `.playwright\node\win32_x64\node.exe`);
  - `PLAYWRIGHT_BROWSERS_PATH` resolve os **browsers** (`chromium-*`, `ffmpeg-*`);
  - o single-file faz `Assembly.Location` retornar vazio, impedindo a resolução automática do driver.
- **seen:** 1
- **severity:** high
- **Source:** 2026-09-23 — blocker `Driver not found: C:\dev\TT2\.playwright\node\win32_x64\node.exe` durante o deploy do TransferToolRPA.

---

## Provisional Observations

_(nenhuma no momento)_
