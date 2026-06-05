# GravadorMulti Web - Versão 2.0

> **Última Atualização**: Junho 2025  
> **Versão**: 2.0 Web  
> **Propósito**: Guia de desenvolvimento da versão web do GravadorMulti

---

## 1. Visão Geral do Projeto

### 1.1 Descrição
**GravadorMulti Web** é uma aplicação web de gravação multi-track para produção de voiceover/dublagem. Permite:
- Gravar áudio sincronizado com roteiro de texto
- Visualizar waveforms de áudio em tempo real
- Gerenciar múltiplos projetos
- Editar e organizar frases de roteiro
- Exportar mixagens de áudio
- Funcionar totalmente no navegador (PWA)

### 1.2 Stack Tecnológico
| Componente | Versão | Propósito |
|------------|--------|-----------|
| HTML5 | - | Estrutura da aplicação |
| CSS3 | - | Estilização e tema escuro |
| JavaScript (ES6+) | - | Lógica da aplicação |
| Web Audio API | - | Engine de áudio nativa do navegador |
| IndexedDB | - | Armazenamento local de projetos |
| MediaRecorder API | - | Captura de áudio do microfone |
| Canvas API | - | Renderização de waveforms |

### 1.3 Estrutura de Diretórios
```
GravadorMulti.Web/
├── index.html              # Página principal
├── css/
│   ├── styles.css          # Estilos globais
│   └── theme.css           # Tema escuro
├── js/
│   ├── app.js              # Entry point da aplicação
│   ├── audio-service.js    # Engine de áudio (Web Audio API)
│   ├── project-service.js  # CRUD de projetos (IndexedDB)
│   ├── waveform-utils.js   # Geração de waveforms
│   └── ui-manager.js       # Gerenciamento de UI
├── models/
│   ├── Projeto.js          # Modelo de projeto
│   └── ItemRoteiro.js      # Modelo de item de roteiro
├── manifest.json           # PWA Manifest
└── README.md               # Este arquivo
```

---

## 2. Arquitetura do Sistema

### 2.1 Padrão de Arquitetura
O projeto utiliza um padrão **MVC simplificado** com módulos ES6:
- **Model**: Classes em `/models`
- **View**: HTML com bindings manuais via JavaScript
- **Controller**: Módulos em `/js`

### 2.2 Fluxo de Dados
```
[HTML/UI] <-> [UI Manager] <-> [Audio Service]
                 ↓                    ↓
         [Project Service]     [Web Audio API]
                 ↓                    ↓
          [IndexedDB]          [Hardware de Áudio]
```

### 2.3 Componentes Principais

#### AudioService (Baseado em Web Audio API)
```javascript
// Responsabilidades:
- Inicialização do AudioContext
- Captura de áudio (getUserMedia + MediaRecorder)
- Reprodução de áudio (AudioBufferSourceNode)
- Seeking exato em milissegundos
- Monitoramento de volume (AnalyserNode para VU Meter)
- Processamento de áudio em tempo real
```

#### ProjectService (Baseado em IndexedDB)
```javascript
// Responsabilidades:
- Criar/Ler/Atualizar/Deletar projetos
- Armazenar áudios como Blob
- Serializar metadados como JSON
- Sincronização com localStorage para cache
```

---

## 3. Funcionalidades Detalhadas

### 3.1 Gerenciamento de Projetos

#### Estrutura de Dados do Projeto
```javascript
{
  id: "uuid",
  nome: "Meu Projeto",
  dataCriacao: "2025-06-05T14:39:00Z",
  dataModificacao: "2025-06-05T14:39:00Z",
  itens: [
    {
      id: 1,
      texto: "Linha 1 do roteiro",
      audioBlob: null, // Blob do áudio gravado
      temAudio: false,
      aprovado: false,
      duracao: 0, // em segundos
      corteInicio: 0, // trimming não-destrutivo
      corteFim: 0
    }
  ]
}
```

### 3.2 Sistema de Gravação

#### Estados de Gravação
| Estado | Descrição | Transição |
|--------|-----------|-----------|
| Idle | Aguardando | iniciarGravacao() |
| Recording | Gravando ativamente | pararGravacao() |
| Monitoring | Monitorando VU apenas | iniciarMonitoramento() |

#### Formato de Áudio
- **Codec**: Opus ou PCM (depende do navegador)
- **Sample Rate**: 44100 Hz ou 48000 Hz
- **Container**: WebM ou WAV
- **Streaming**: Gravação via MediaRecorder com chunks

### 3.3 Visualização de Waveform

#### Tipos de Renderização
1. **Em tempo real** (durante gravação):
   - Usa AnalyserNode.getByteTimeDomainData()
   - Atualização via requestAnimationFrame
   
2. **Pós-gravação** (de arquivo):
   - Decodifica AudioBuffer
   - Extrai picos de amplitude
   - Desenha no Canvas

#### Cores de Estado do Item
| Estado | Cor | Hex |
|--------|-----|-----|
| Sem áudio | Cinza escuro | `#2B2B2B` |
| Com áudio | Azul profundo | `#1E3A5F` |
| Aprovado | Verde escuro | `#1E441E` |

### 3.4 Sistema de Undo/Redo

#### Implementação
```javascript
class UndoManager {
  constructor() {
    this.undoStack = [];
    this.redoStack = [];
    this.maxHistory = 50;
  }

  push(action, undoAction) {
    this.undoStack.push({ action, undoAction });
    if (this.undoStack.length > this.maxHistory) {
      this.undoStack.shift();
    }
    this.redoStack = [];
  }

  undo() {
    const command = this.undoStack.pop();
    if (command) {
      command.undoAction();
      this.redoStack.push(command);
    }
  }

  redo() {
    const command = this.redoStack.pop();
    if (command) {
      command.action();
      this.undoStack.push(command);
    }
  }
}
```

### 3.5 Atalhos de Teclado

| Atalho | Ação | Contexto |
|--------|------|----------|
| `Ctrl+S` | Salvar projeto | Global |
| `Ctrl+Z` | Undo | Global |
| `Ctrl+Y` | Redo | Global |
| `Space` | Play/Pause | Item selecionado |
| `R` | Gravar/Parar | Item selecionado |
| `Delete` | Limpar áudio | Item selecionado |

---

## 4. Convenções de Código

### 4.1 Nomenclatura
| Elemento | Convenção | Exemplo |
|----------|-----------|---------|
| Classes | PascalCase | `AudioService` |
| Funções | camelCase | `iniciarGravacao()` |
| Variáveis | camelCase | `estaGravando` |
| Constantes | UPPER_SNAKE_CASE | `SAMPLE_RATE` |
| Eventos | on + PascalCase | `onVolumeReceived` |

### 4.2 Organização de Arquivos
```javascript
// Ordem em classes/módulos:
1. Constantes
2. Variáveis privadas
3. Constructor/Init
4. Métodos públicos
5. Métodos privados
6. Event handlers
```

### 4.3 Thread Safety (JavaScript é single-threaded)
```javascript
// Use async/await para operações longas:
async function processarAudio() {
  await audioContext.decodeAudioData(arrayBuffer);
  // Atualiza UI após processamento
  atualizarUI();
}
```

---

## 5. Guia de Modificações

### 5.1 Adicionando Nova Funcionalidade de Áudio

1. **Adicione métodos em `audio-service.js`**:
```javascript
async function novaFuncao() {
  if (estaGravando) return;
  // Implementação...
}
```

2. **Exponha callbacks se necessário**:
```javascript
setOnNovoEvento(callback) {
  this.onNovoEvento = callback;
}
```

3. **Atualize `ui-manager.js`**:
- Adicione listener de evento
- Crie handler de UI

### 5.2 Modificando a UI

1. **HTML**: Edite `index.html`
   - Use classes CSS existentes
   - Mantenha o tema escuro

2. **CSS**: Adicione estilos em `styles.css`
```css
/* Cores do tema */
:root {
  --bg-primary: #121212;
  --bg-secondary: #1E1E1E;
  --action-primary: #2D63C8;
  --action-hover: #3A75DF;
}
```

---

## 6. Pontos de Atenção Críticos

### 6.1 Permissões do Navegador
- Solicitar permissão de microfone no primeiro uso
- Lidar com negação de permissão gracefulmente
- HTTPS obrigatório para produção (exceto localhost)

### 6.2 Compatibilidade entre Navegadores
- Testar em Chrome, Firefox, Edge, Safari
- Web Audio API tem diferenças sutis entre browsers
- MediaRecorder suporta codecs diferentes por browser

### 6.3 Gerenciamento de Memória
- Liberar AudioBuffers quando não usados
- Revogar Object URLs de blobs após uso
- Limitar histórico de undo para evitar memory bloat

### 6.4 Persistência de Dados
- IndexedDB tem quotas por domínio
- Implementar limpeza de projetos antigos
- Considerar exportação/importação de backup

---

## 7. Checklist de Testes

Antes de commitar mudanças, verifique:

- [ ] Gravação inicia/para corretamente
- [ ] Playback funciona com pause/resume
- [ ] Waveform desenha em tempo real e de arquivo
- [ ] Salvar/Carregar projeto preserva dados
- [ ] Undo/Redo funcionam para áudio
- [ ] Atalhos de teclado respondem
- [ ] Permissão de microfone é solicitada
- [ ] Fechar com alterações não salvas mostra aviso
- [ ] Scrubbing (clique na waveform) funciona
- [ ] Funciona offline (PWA)

---

## 8. Referências Rápidas

### 8.1 Web Audio API Constants
```javascript
const SAMPLE_RATE = 44100; // Hz
const BUFFER_SIZE = 4096;  // Samples
const CHANNELS = 1;        // Mono
```

### 8.2 IndexedDB Schema
```javascript
const DB_NAME = 'GravadorMultiDB';
const DB_VERSION = 1;
const STORE_PROJETOS = 'projetos';
const STORE_AUDIOS = 'audios';
```

### 8.3 Events do AudioService
| Evento | Gatilho | Handler Típico |
|--------|---------|----------------|
| onVolumeReceived | Volume capturado | Atualizar VU meter |
| onRecordingStopped | Gravação finalizada | Gerar waveform, salvar |
| onPlaybackStopped | Áudio terminou/parou | Resetar ícones |

---

## 9. Solução de Problemas Comuns

### 9.1 "Microfone não acessível"
- Verificar permissões do navegador
- Garantir que está em HTTPS (ou localhost)
- Testar em outro navegador

### 9.2 "Áudio não toca"
- Verificar se AudioContext está resumed (navegadores bloqueiam autoplay)
- Checar se o buffer foi decodificado corretamente

### 9.3 "Projeto não salva"
- Verificar quota do IndexedDB
- Limpar dados antigos do navegador
- Verificar console por erros

### 9.4 "Waveform não renderiza"
- Verificar se Canvas context foi obtido
- Checar se há dados de áudio válidos
- Confirmar que requestAnimationFrame está sendo chamado

---

## 10. Roadmap Sugerido

### Prioridade Alta
- [x] Estrutura básica do projeto
- [ ] Implementar gravação de áudio
- [ ] Implementar playback de áudio
- [ ] Renderização de waveform
- [ ] CRUD de projetos no IndexedDB

### Prioridade Média
- [ ] Sistema de undo/redo
- [ ] Trim/crop de áudio não-destrutivo
- [ ] Exportação de mixagem
- [ ] PWA (offline support)

### Prioridade Baixa
- [ ] Múltiplas takes por frase
- [ ] Efeitos de áudio (EQ, compressão)
- [ ] Compartilhamento de projetos
- [ ] Temas claro/escuro

---

**Fim do Documento**
