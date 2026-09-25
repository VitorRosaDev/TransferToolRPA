# Changelog

Todas as mudanças relevantes deste projeto são documentadas neste arquivo.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota o [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [0.2.0] - 2026-09-25

### Adicionado
- Tratamento de eventualidades operacionais na automação: múltiplos códigos por item, item não localizado, lote sem validade e quantidade parcial.
- Log em tempo real com cor por severidade (Info, Sucesso, Aviso, Erro).
- Status de conclusão por carga na fila (pendente, concluída, parcial, não encontrado).
- Persistência diária de logs em `%LOCALAPPDATA%\TransferToolRPA\Logs`.
- Limite do histórico de log em memória para evitar crescimento ilimitado em sessões longas.
- Rotação automática dos arquivos de diagnóstico (retenção de 7 dias em `%LOCALAPPDATA%\TransferToolRPA\Diagnostico`).

### Alterado
- Centralização de timeouts/delays/retentativas ("magic numbers") em `ConfiguracaoAutomacao`.
- Grade vazia: mensagem residual "Registro não encontrado" tratada como transitória (janela de estabilidade).

### Segurança
- Mitigação de log injection: quebras de linha nas mensagens são sanitizadas antes de persistir no log.
- Correção de dependências transitivas vulneráveis (System.Text.Json, System.Net.Http, System.Text.RegularExpressions).
- Varredura de CVEs: nenhum pacote vulnerável identificado.

## [0.1.0] - 2026-09-24

### Adicionado
- Primeira transferência E2E concluída no Atende.Net via Playwright (conexão CDP).
- Arquitetura Clean Architecture + MVVM + Command Pattern.
- Suíte de testes automatizados (xUnit).
