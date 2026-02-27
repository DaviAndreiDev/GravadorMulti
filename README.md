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
- ✂️ **Modo de Corte** — Trim/crop de áudio não-destrutivo com handles arrastáveis
- 🔀 **Drag & Drop Visual** — Reordene frases arrastando com feedback visual em tempo real (ghost flutuante + indicador de posição)
- ↩️ **Undo/Redo Global** — `Ctrl+Z` / `Ctrl+Y` para todas as ações (gravação, corte, edição de lista)
- 📂 **Multi-Projeto** — Trabalhe com vários projetos em abas simultâneas
- 💾 **Auto-Save Inteligente** — Indicador visual de alterações não salvas
- 🎨 **Dark Studio Theme** — Interface escura profissional com ícones vetoriais SVG
- 📦 **Exportação Multi-formato** — Mixagem e exportação em WAV, MP3, AAC/M4A, FLAC e OGG via FFmpeg

---

## 🖥️ Screenshots

> _Em breve — contribua com screenshots!_

---

## ⚡ Pré-requisitos

| Dependência | Versão |
|-------------|--------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0+ |
| Windows | 10/11 (x64) |
| [FFmpeg](https://ffmpeg.org/) | Opcional (para exportação MP3/AAC/FLAC/OGG) |

> **Nota:** O `bass.dll` nativo (x64) já está incluído no repositório em `runtimes/`.

---

## 🚀 Instalação e Build

```bash
# Clone o repositório
git clone https://github.com/seu-usuario/GravadorMulti.git
cd GravadorMulti

# Restaure dependências e compile
dotnet restore
dotnet build

# Execute
dotnet run
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

**Menu de contexto (botão direito):**
- ↑ Mover para Cima / ↓ Mover para Baixo
- ✂ Modo de Corte
- ✕ Excluir Frase

---

## 🏗️ Arquitetura

```
GravadorMulti/
├── MainWindow.axaml(.cs)   # UI principal + code-behind
├── Models/                  # Projeto, ItemRoteiro
├── ViewModels/              # MainWindowViewModel
├── Services/                # AudioService, ProjectService, FfmpegService, WaveformUtils
├── Converters/              # Binding converters (Progress, Inverse, BoolToBrush)
└── runtimes/                # bass.dll nativo (x64)
```

**Padrão:** MVVM simplificado com code-behind.  
**Áudio:** ManagedBass (wrapper .NET para BASS) — gravação PCM 16-bit Mono @ 44100 Hz.  
**Exportação:** FFmpeg via Xabe.FFmpeg para codificação em formatos comprimidos.  
**Persistência:** JSON via Newtonsoft.Json.

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
- [ ] Tema claro/escuro configurável
- [ ] Plugin system para efeitos de áudio
- [ ] Suporte a Linux e macOS
- [ ] Configurações de projeto (sample rate, canais)
- [ ] Atalhos configuráveis
- [ ] Testes automatizados

---

## 📥 Download

Baixe a versão mais recente na página de [Releases](https://github.com/DaviAndreiDev/GravadorMulti/releases).

O executável é **self-contained** — não precisa ter o .NET instalado para rodar.

---

## 📌 Changelog

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
