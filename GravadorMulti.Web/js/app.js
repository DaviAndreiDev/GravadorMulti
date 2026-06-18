/**
 * GravadorMulti Web - App Principal
 * Entry point da aplicação
 */

// Instância global da aplicação
let app = null;

class GravadorMultiApp {
    constructor() {
        this.audioService = new AudioService();
        this.projectService = new ProjectService();
        this.uiManager = new UIManager();
        
        this.isInitialized = false;
        this.isRecording = false;
        this.itemGravando = null;
    }

    /**
     * Inicializa a aplicação
     */
    async init() {
        try {
            console.log('Inicializando GravadorMulti Web...');
            
            // Inicializa UI
            this.uiManager.init();
            
            // Inicializa serviços
            await this.audioService.init();
            await this.projectService.init();
            
            // Configura event listeners
            this.setupEventListeners();
            
            // Carrega dispositivos de áudio
            await this.loadAudioDevices();
            
            // Configura callbacks do audio service
            this.setupAudioCallbacks();
            
            this.isInitialized = true;
            console.log('GravadorMulti Web inicializado com sucesso!');
            
            this.uiManager.showToast('Aplicação inicializada!', 'success');
        } catch (error) {
            console.error('Erro ao inicializar:', error);
            this.uiManager.showToast('Erro ao inicializar: ' + error.message, 'error');
        }
    }

    /**
     * Configura event listeners da UI
     */
    setupEventListeners() {
        // Botão novo projeto
        document.getElementById('btnNewProject').addEventListener('click', () => this.newProject());
        
        // Botões de gravação/playback
        document.getElementById('btnRecord').addEventListener('click', () => this.recordCurrentItem());
        document.getElementById('btnPlay').addEventListener('click', () => this.playCurrentItem());
        document.getElementById('btnStop').addEventListener('click', () => this.stopPlayback());
        
        // Seleção de dispositivo
        document.getElementById('deviceSelect').addEventListener('change', (e) => {
            this.changeAudioDevice(e.target.value);
        });
        
        // Texto do roteiro
        document.getElementById('roteiroText').addEventListener('input', (e) => {
            this.onRoteiroTextChanged(e.target.value);
        });
        
        // Botão fatiar texto
        document.getElementById('btnSplitText').addEventListener('click', () => this.splitTexto());
        
        // Botão adicionar item
        document.getElementById('btnAddItem').addEventListener('click', () => this.addItem());
        
        // Undo/Redo
        document.getElementById('btnUndo').addEventListener('click', () => this.uiManager.undo());
        document.getElementById('btnRedo').addEventListener('click', () => this.uiManager.redo());
        
        // Exportar
        document.getElementById('btnExport').addEventListener('click', () => this.exportProject());
        
        // Atalhos de teclado
        document.addEventListener('keydown', (e) => this.handleKeyDown(e));
        
        // Salvar projeto antes de fechar
        window.addEventListener('beforeunload', (e) => {
            if (this.uiManager.projetoSelecionado?.temAlteracoesNaoSalvas) {
                e.preventDefault();
                e.returnValue = '';
            }
        });
    }

    /**
     * Configura callbacks do serviço de áudio
     */
    setupAudioCallbacks() {
        // Callback de volume (VU meter)
        this.audioService.onVolumeReceived = (level) => {
            this.uiManager.updateVUMeter(level);
        };

        // Callback de gravação parada
        this.audioService.onRecordingStopped = async (blob) => {
            await this.handleRecordingStopped(blob);
        };

        // Callback de playback parado
        this.audioService.onPlaybackStopped = () => {
            this.uiManager.updateControls();
        };
    }

    /**
     * Carrega lista de dispositivos de áudio
     */
    async loadAudioDevices() {
        try {
            // Solicita permissão primeiro
            await navigator.mediaDevices.getUserMedia({ audio: true });
            
            const devices = await this.audioService.getMediaDevices();
            this.uiManager.updateDeviceList(devices);
            
            if (devices.length > 0) {
                document.getElementById('deviceSelect').value = devices[0].deviceId;
                await this.audioService.startMonitoring(devices[0].deviceId);
            }
        } catch (error) {
            console.error('Erro ao carregar dispositivos:', error);
            this.uiManager.showToast('Permissão de microfone negada', 'warning');
        }
    }

    /**
     * Troca dispositivo de áudio
     * @param {string} deviceId - ID do dispositivo
     */
    async changeAudioDevice(deviceId) {
        if (deviceId) {
            await this.audioService.startMonitoring(deviceId);
            
            if (this.uiManager.projetoSelecionado) {
                this.uiManager.projetoSelecionado.dispositivoEntrada = deviceId;
                this.markProjectChanged();
            }
        }
    }

    /**
     * Cria novo projeto
     */
    async newProject() {
        const nome = prompt('Nome do projeto:', 'Novo Projeto');
        if (!nome) return;

        const projeto = new Projeto({ nome });
        this.uiManager.projetosAbertos.push(projeto);
        this.uiManager.selectProject(projeto);
        
        this.uiManager.showToast(`Projeto "${nome}" criado!`, 'success');
    }

    /**
     * Salva projeto atual
     */
    async saveProject() {
        if (!this.uiManager.projetoSelecionado) return;
        
        try {
            await this.projectService.salvarProjeto(this.uiManager.projetoSelecionado);
            this.uiManager.showToast('Projeto salvo!', 'success');
        } catch (error) {
            console.error('Erro ao salvar:', error);
            this.uiManager.showToast('Erro ao salvar projeto', 'error');
        }
    }

    /**
     * Marca projeto como alterado
     */
    markProjectChanged() {
        if (this.uiManager.projetoSelecionado) {
            this.uiManager.projetoSelecionado.temAlteracoesNaoSalvas = true;
            this.uiManager.projetoSelecionado.dataModificacao = new Date().toISOString();
            this.uiManager.updateControls();
        }
    }

    /**
     * Grava item atual
     * @param {ItemRoteiro} item - Item para gravar (opcional)
     */
    async recordItem(item = null) {
        const targetItem = item || this.uiManager.itemSelecionado;
        
        if (!targetItem || !this.uiManager.projetoSelecionado) {
            this.uiManager.showToast('Selecione um item para gravar', 'warning');
            return;
        }

        this.itemGravando = targetItem;
        this.isRecording = true;
        
        // Salva estado anterior para undo
        const previousBlob = targetItem.audioBlob;
        const previousDuration = targetItem.duracao;
        
        this.uiManager.pushUndo(
            () => {
                // Ação (já foi feita)
            },
            () => {
                // Undo
                targetItem.audioBlob = previousBlob;
                targetItem.duracao = previousDuration;
                targetItem.temAudio = !!previousBlob;
                this.uiManager.renderTracks();
            }
        );

        try {
            await this.audioService.startRecording();
            this.uiManager.updateControls();
            this.uiManager.showToast('Gravando...', 'info');
        } catch (error) {
            console.error('Erro ao gravar:', error);
            this.uiManager.showToast('Erro ao iniciar gravação', 'error');
            this.isRecording = false;
        }
    }

    /**
     * Handler quando gravação é parada
     * @param {Blob} blob - Blob do áudio gravado
     */
    async handleRecordingStopped(blob) {
        if (!this.itemGravando) return;

        try {
            // Decodifica áudio para obter duração e waveform
            const audioBuffer = await this.audioService.decodeAudio(blob);
            const waveformPoints = WaveformUtils.extractWaveformPoints(audioBuffer);
            
            // Atualiza item
            this.itemGravando.audioBlob = blob;
            this.itemGravando.audioBuffer = audioBuffer;
            this.itemGravando.waveformPoints = waveformPoints;
            this.itemGravando.duracao = audioBuffer.duration;
            this.itemGravando.temAudio = true;
            
            // Salva áudio no IndexedDB
            await this.projectService.salvarAudio(
                this.itemGravando.id,
                this.uiManager.projetoSelecionado.id,
                blob
            );
            
            this.markProjectChanged();
            this.uiManager.renderTracks();
            this.uiManager.updateControls();
            
            this.uiManager.showToast('Gravação concluída!', 'success');
        } catch (error) {
            console.error('Erro ao processar gravação:', error);
            this.uiManager.showToast('Erro ao processar áudio', 'error');
        } finally {
            this.isRecording = false;
            this.itemGravando = null;
        }
    }

    /**
     * Toca item atual
     * @param {ItemRoteiro} item - Item para tocar (opcional)
     */
    async playItem(item = null) {
        const targetItem = item || this.uiManager.itemSelecionado;
        
        if (!targetItem || !targetItem.audioBuffer) {
            this.uiManager.showToast('Item sem áudio para reproduzir', 'warning');
            return;
        }

        try {
            this.audioService.play(targetItem.audioBuffer);
            this.uiManager.updateControls();
        } catch (error) {
            console.error('Erro ao reproduzir:', error);
            this.uiManager.showToast('Erro ao reproduzir áudio', 'error');
        }
    }

    /**
     * Para playback
     */
    stopPlayback() {
        this.audioService.stop();
        this.uiManager.updateControls();
    }

    /**
     * Seek em um item
     * @param {number} position - Posição (0-1)
     */
    seekItem(position) {
        if (!this.uiManager.itemSelecionado?.audioBuffer) return;
        
        const time = position * this.uiManager.itemSelecionado.audioBuffer.duration;
        this.audioService.seek(time);
    }

    /**
     * Handler de teclas de atalho
     * @param {KeyboardEvent} e - Evento de teclado
     */
    handleKeyDown(e) {
        const isCtrl = e.ctrlKey || e.metaKey;
        
        // Ctrl+S - Salvar
        if (isCtrl && e.key === 's') {
            e.preventDefault();
            this.saveProject();
            return;
        }
        
        // Ctrl+Z - Undo
        if (isCtrl && e.key === 'z' && !e.shiftKey) {
            e.preventDefault();
            this.uiManager.undo();
            return;
        }
        
        // Ctrl+Y ou Ctrl+Shift+Z - Redo
        if ((isCtrl && e.key === 'y') || (isCtrl && e.shiftKey && e.key === 'z')) {
            e.preventDefault();
            this.uiManager.redo();
            return;
        }
        
        // Space - Play/Pause (quando item selecionado)
        if (e.key === ' ' && this.uiManager.itemSelecionado) {
            e.preventDefault();
            if (this.audioService.isPlaying) {
                this.stopPlayback();
            } else {
                this.playCurrentItem();
            }
            return;
        }
        
        // R - Gravar
        if (e.key === 'r' && !isCtrl && this.uiManager.itemSelecionado) {
            e.preventDefault();
            if (this.isRecording) {
                this.audioService.stopRecording();
            } else {
                this.recordCurrentItem();
            }
            return;
        }
        
        // Delete - Limpar áudio
        if (e.key === 'Delete' && this.uiManager.itemSelecionado) {
            e.preventDefault();
            this.clearItemAudio(this.uiManager.itemSelecionado);
            return;
        }
    }

    /**
     * Grava item selecionado atualmente
     */
    recordCurrentItem() {
        if (this.isRecording) {
            this.audioService.stopRecording();
        } else {
            this.recordItem();
        }
    }

    /**
     * Toca item selecionado atualmente
     */
    playCurrentItem() {
        this.playItem();
    }

    /**
     * Limpa áudio de um item
     * @param {ItemRoteiro} item - Item
     */
    clearItemAudio(item) {
        this.uiManager.pushUndo(() => {
            item.audioBlob = null;
            item.audioBuffer = null;
            item.waveformPoints = [];
            item.duracao = 0;
            item.temAudio = false;
            this.uiManager.renderTracks();
            this.markProjectChanged();
        }, () => {
            // Restaurar seria complexo, então apenas notificamos
            this.uiManager.showToast('Ação não pode ser desfeita', 'warning');
        });
        
        this.uiManager.showToast('Áudio limpo', 'info');
    }

    /**
     * Fatiar texto do roteiro em itens
     */
    splitTexto() {
        const texto = document.getElementById('roteiroText').value;
        
        if (!texto.trim()) {
            this.uiManager.showToast('Digite um roteiro primeiro', 'warning');
            return;
        }

        if (!this.uiManager.projetoSelecionado) {
            this.uiManager.showToast('Crie ou abra um projeto primeiro', 'warning');
            return;
        }

        const count = this.uiManager.projetoSelecionado.fatiarTexto(texto);
        this.uiManager.renderTracks();
        this.markProjectChanged();
        
        this.uiManager.showToast(`${count} linhas criadas!`, 'success');
    }

    /**
     * Adiciona item manual
     */
    addItem() {
        if (!this.uiManager.projetoSelecionado) {
            this.uiManager.showToast('Crie ou abra um projeto primeiro', 'warning');
            return;
        }

        const texto = prompt('Texto da linha:');
        if (texto) {
            this.uiManager.projetoSelecionado.adicionarItem(texto);
            this.uiManager.renderTracks();
            this.markProjectChanged();
        }
    }

    /**
     * Handler de mudança no texto do roteiro
     * @param {string} value - Novo valor
     */
    onRoteiroTextChanged(value) {
        if (this.uiManager.projetoSelecionado) {
            this.uiManager.projetoSelecionado.textoRoteiro = value;
            this.markProjectChanged();
        }
    }

    /**
     * Exporta projeto
     */
    async exportProject() {
        if (!this.uiManager.projetoSelecionado) {
            this.uiManager.showToast('Nenhum projeto selecionado', 'warning');
            return;
        }

        try {
            const data = await this.projectService.exportarProjeto(this.uiManager.projetoSelecionado);
            
            // Cria JSON para download
            const jsonStr = JSON.stringify(data.projeto, null, 2);
            const blob = new Blob([jsonStr], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            
            const a = document.createElement('a');
            a.href = url;
            a.download = `${this.uiManager.projetoSelecionado.nome.replace(/[^a-z0-9]/gi, '_')}_export.json`;
            a.click();
            
            URL.revokeObjectURL(url);
            
            this.uiManager.showToast('Projeto exportado!', 'success');
        } catch (error) {
            console.error('Erro ao exportar:', error);
            this.uiManager.showToast('Erro ao exportar projeto', 'error');
        }
    }

    /**
     * Limpa recursos ao fechar
     */
    dispose() {
        this.audioService.dispose();
        this.projectService.close();
    }
}

// Inicializa aplicação quando DOM estiver pronto
document.addEventListener('DOMContentLoaded', () => {
    app = new GravadorMultiApp();
    app.init();
});

// Export para ambientes module
if (typeof module !== 'undefined' && module.exports) {
    module.exports = GravadorMultiApp;
}
