# Changelog

Todas as mudanças relevantes deste projeto são documentadas neste arquivo.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota o [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Não publicado]

### Adicionado
- Tratamento de eventualidades operacionais na automação: múltiplos códigos por item, item não localizado, lote sem validade e quantidade parcial.
- Log em tempo real com cor por severidade (Info, Sucesso, Aviso, Erro).
- Status de conclusão por carga na fila (pendente, concluída, parcial, não encontrado).
- Persistência diária de logs em `%LOCALAPPDATA%\TransferToolRPA\Logs`.

## [0.1.0] - 2026-09-24

### Adicionado
- Primeira transferência E2E concluída no Atende.Net via Playwright (conexão CDP).
- Arquitetura Clean Architecture + MVVM + Command Pattern.
- Suíte de testes automatizados (xUnit).
