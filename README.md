# 🎙️ GravadorMulti

**Gravador de áudio multi-track profissional para produção de voiceover e dublagem.**

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.3-7B2BFC?logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0id2hpdGUiIGQ9Ik0xMiAyTDEgMjFoMjJMMTIgMnoiLz48L3N2Zz4=)](https://avaloniaui.net/)
[![ManagedBass](https://img.shields.io/badge/Audio-ManagedBass-FF6B00)](https://github.com/ManagedBass/ManagedBass)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/DaviAndreiDev/GravadorMulti?label=Release&color=blue)](https://github.com/DaviAndreiDev/GravadorMulti/releases)

---

## ✨ Funcionalidades

- 📝 **Editor de Roteiro** — Cole seu texto e fatie-o automaticamente em frases individuais
- 🎙️ **Gravação por Frase** — Grave cada frase individualmente com waveform em tempo real
- 🎵 **Waveform Interativo** — Scrubbing, playback e visualização de onda vetorial
- ✂️ **Modo de Corte** — Trim/crop de áudio não-destrutivo com handles arrastáveis e **zoom via Alt+Scroll**
- 🔇 **Remoção de Silêncios** — Detecção e remoção automática de silêncios com preview em tempo real (via FFmpeg)
- 🔀 **Drag & Drop Visual** — Reordene frases arrastando com feedback visual em tempo real (ghost flutuante + indicador de posição)
- ↩️ **Undo/Redo Global** — `Ctrl+Z` / `Ctrl+Y` para todas as ações (gravação, corte, edição de lista, remoção de silêncios)
- 📂 **Multi-Projeto** — Trabalhe com vários projetos em abas simultâneas
- 💾 **Auto-Save Inteligente** — Indicador visual de alterações não salvas
- 🎨 **Temas Claro e Escuro** — Alternância completa entre tema Dark Studio e tema Light, com recursos temáticos dedicados
- 📦 **Exportação Multi-formato** — Mixagem e exportação em WAV, MP3, AAC/M4A, FLAC e OGG via FFmpeg
- 📋 **Menu Padrão** — Barra de menu com Arquivo, Editar, Visualizar, Ajuda e acesso rápido a Preferências e Sobre

---

## 🖥️ Screenshots

> _Em breve — contribua com screenshots!_

---

## ⚡ Pré-requisitos

| Dependência | Versão |
|-------------|--------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0+ |
| Windows | 10/11 (x64) |
| [FFmpeg](https://ffmpeg.org/) | Opcional — baixado automaticamente quando necessário |

> **Nota:** O `bass.dll` nativo (x64) já está incluído no repositório em `runtimes/`.
> O FFmpeg é baixado automaticamente na primeira utilização de funcionalidades que o exijam (exportação em formatos comprimidos, remoção de silêncios).

---

## 🚀 Instalação e Build

```bash
# Clone o repositório
git clone https://github.com/DaviAndreiDev/GravadorMulti.git
cd GravadorMulti

# Restaure dependências e compile
dotnet restore
dotnet build

# Execute
dotnet run
```

### Build para Distribuição (Self-Contained)

```bash
# Gera executável standalone (não precisa do .NET no destino)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## ⌨️ Atalhos de Teclado

| Atalho | Ação |
|--------|------|
| `Ctrl+S` | Salvar projeto |
| `Ctrl+Z` | Desfazer (Undo) |
| `Ctrl+Y` | Refazer (Redo) |
| `Ctrl+Alt+Z` | Refazer (Redo) alternativo |
| `Space` | Play/Pause (item selecionado) |
| `R` | Gravar/Parar (item selecionado) |
| `Delete` | Limpar áudio (item selecionado) |
| `Alt+Scroll` | Zoom no waveform (durante modo de corte) |

**Menu de contexto (botão direito):**
- ↑ Mover para Cima / ↓ Mover para Baixo
- ✂ Modo de Corte
- 🔇 Remover Silêncios
- ✕ Excluir Frase

---

## 🏗️ Arquitetura

```
GravadorMulti/
├── App.axaml(.cs)           # Entry point da aplicação
├── Program.cs               # Entry point do programa
├── MainWindow.axaml(.cs)    # UI principal + code-behind (~1777 linhas)
├── ThemeResources.axaml     # Recursos temáticos (Dark + Light)
├── Models/                  # Modelos de dados
│   ├── Projeto.cs           # Entidade principal do projeto
│   └── ItemRoteiro.cs       # Item individual do roteiro
├── ViewModels/              # ViewModel principal
│   └── MainWindowViewModel.cs
├── Services/                # Camada de serviços
│   ├── AudioService.cs      # Engine de áudio (ManagedBass) — thread-safe
│   ├── ProjectService.cs    # CRUD de projetos JSON
│   ├── FfmpegService.cs     # Exportação e processamento via FFmpeg
│   └── WaveformUtils.cs     # Geração de waveforms (memória + streaming)
├── Converters/              # Binding converters (Avalonia)
│   ├── ProgressToPixelConverter.cs
│   ├── InverseProgressToPixelConverter.cs
│   ├── BoolToBrushConverter.cs
│   ├── MathMultiplyConverter.cs
│   └── NullToEmptyPointsConverter.cs
└── runtimes/                # bass.dll nativo (x64)
```

**Padrão:** MVVM simplificado com code-behind.  
**Áudio:** ManagedBass (wrapper .NET para BASS) — gravação PCM 16-bit Mono @ 44100 Hz.  
**Processamento:** FFmpeg via Xabe.FFmpeg para codificação e processamento avançado de áudio.  
**Persistência:** JSON via Newtonsoft.Json.  
**Temas:** Suporte completo a Dark e Light via `ThemeResources.axaml` com `ThemeDictionaries`.

---

## 🤝 Contribuindo

Contribuições são bem-vindas! Siga estes passos:

1. **Fork** o repositório
2. Crie uma branch para sua feature: `git checkout -b feature/minha-feature`
3. Faça commit das suas mudanças: `git commit -m 'feat: adiciona minha feature'`
4. Push para a branch: `git push origin feature/minha-feature`
5. Abra um **Pull Request**

### Convenções

- **Commits:** Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `refactor:`)
- **Idioma do código:** Português (nomes de variáveis, métodos, comentários)
- **Estilo:** Siga as convenções documentadas no `project bible.md`

### Ideias para Contribuir

- [ ] Suporte a múltiplas takes por frase
- [ ] Plugin system para efeitos de áudio
- [ ] Suporte a Linux e macOS
- [ ] Configurações de projeto (sample rate, canais)
- [ ] Atalhos configuráveis
- [ ] Testes automatizados
- [ ] Sincronização cloud

---

## 📥 Download

Baixe a versão mais recente na página de [Releases](https://github.com/DaviAndreiDev/GravadorMulti/releases).

O executável é **self-contained** — não precisa ter o .NET instalado para rodar.

---

## 📌 Changelog

### v2.0.0 — 2026-03-26

**Nova versão principal** com melhorias significativas de funcionalidade e UX:

#### ✨ Novas Funcionalidades
- 🔇 **Remoção interativa de silêncios** — Overlay dedicado com sliders de duração mínima e threshold, preview comparativo (original vs. processado), e aplicação com suporte a Undo/Redo
- 🎨 **Temas Claro e Escuro** — Sistema de temas completo com alternância via menu, usando `ThemeDictionaries` do Avalonia com paletas dedicadas para cada modo
- 📋 **Menu superior padrão** — Barra de menu com itens de Arquivo, Editar, Visualizar e Ajuda
- ℹ️ **Tela Sobre** — Informações sobre a aplicação
- ⚙️ **Tela Preferências** — Configurações do usuário
- 🔍 **Zoom no Waveform** — Zoom com `Alt+Scroll` durante o modo de corte para ajuste fino da seleção (1x–10x)
- ⬇️ **Download automático do FFmpeg** — O FFmpeg é baixado automaticamente via `Xabe.FFmpeg.Downloader` quando não encontrado no sistema

#### 🐛 Correções
- Fix de offset e sobreposição no Drag & Drop de itens
- Melhoria de contraste no hover de itens de menu
- Correções no Auto Trim de silêncio
- Múltiplas correções de estabilidade e bugs gerais

#### 🏗️ Melhorias Técnicas
- `ThemeResources.axaml` — Mais de 40 recursos temáticos (backgrounds, foregrounds, buttons, borders, accents) com variantes Dark e Light
- `FfmpegService.cs` — Serviço completo com busca automática, download sob demanda e processamento de silêncios
- `WaveformUtils.cs` — Suporte a streaming para arquivos grandes (>10MB), validação de cabeçalho WAV e cálculo de duração
- 5 converters de binding para suporte robusto a UI dinâmica

---

### v1.0.0 — 2025-02-27

**Lançamento inicial** com todas as funcionalidades principais:

- 📝 Editor de roteiro com fatiamento automático de texto
- 🎙️ Gravação por frase com waveform em tempo real
- 🎵 Waveform interativo com scrubbing e playback
- ✂️ Modo de corte não-destrutivo com handles arrastáveis
- 🔀 Drag & drop visual com ghost flutuante e indicador de posição
- ↩️ Undo/Redo global para todas as ações
- 📂 Multi-projeto com abas simultâneas
- 💾 Auto-save inteligente com indicador de alterações
- 📦 Exportação multi-formato (WAV, MP3, AAC/M4A, FLAC, OGG) via FFmpeg
- 🎨 Dark studio theme com ícones vetoriais SVG

---

## 📋 Licença

Este projeto está licenciado sob a [MIT License](LICENSE).

---

<p align="center">
  Feito com ❤️ usando <strong>Avalonia UI</strong> e <strong>ManagedBass</strong>
</p>
