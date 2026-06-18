/**
 * UIManager
 * Gerenciador de interface do usuário e interações
 */

class UIManager {
    constructor() {
        this.projetosAbertos = [];
        this.projetoSelecionado = null;
        this.itemSelecionado = null;
        this.undoStack = [];
        this.redoStack = [];
        this.maxUndoHistory = 50;
        
        // Elementos DOM cacheados
        this.elements = {};
    }

    /**
     * Inicializa o UIManager e faz cache dos elementos DOM
     */
    init() {
        // Cache de elementos
        this.elements = {
            projectTabs: document.getElementById('projectTabs'),
            btnNewProject: document.getElementById('btnNewProject'),
            btnSettings: document.getElementById('btnSettings'),
            deviceSelect: document.getElementById('deviceSelect'),
            vuLevel: document.getElementById('vuLevel'),
            vuText: document.getElementById('vuText'),
            btnRecord: document.getElementById('btnRecord'),
            btnPlay: document.getElementById('btnPlay'),
            btnStop: document.getElementById('btnStop'),
            roteiroText: document.getElementById('roteiroText'),
            btnSplitText: document.getElementById('btnSplitText'),
            btnAddItem: document.getElementById('btnAddItem'),
            btnUndo: document.getElementById('btnUndo'),
            btnRedo: document.getElementById('btnRedo'),
            btnExport: document.getElementById('btnExport'),
            tracksList: document.getElementById('tracksList'),
            modalOverlay: document.getElementById('modalOverlay'),
            modalTitle: document.getElementById('modalTitle'),
            modalMessage: document.getElementById('modalMessage'),
            modalCancel: document.getElementById('modalCancel'),
            modalConfirm: document.getElementById('modalConfirm'),
            toastContainer: document.getElementById('toastContainer')
        };

        console.log('UIManager inicializado');
    }

    /**
     * Atualiza o VU meter
     * @param {number} level - Nível (0-1)
     */
    updateVUMeter(level) {
        const percentage = Math.min(level * 100, 100);
        this.elements.vuLevel.style.width = `${percentage}%`;
        
        const db = WaveformUtils.linearToDb(level);
        this.elements.vuText.textContent = `${db} dB`;
    }

    /**
     * Renderiza a lista de tabs de projetos
     */
    renderProjectTabs() {
        this.elements.projectTabs.innerHTML = '';
        
        this.projetosAbertos.forEach((projeto, index) => {
            const tab = document.createElement('div');
            tab.className = `tab ${this.projetoSelecionado === projeto ? 'active' : ''}`;
            tab.textContent = projeto.nome;
            tab.onclick = () => this.selectProject(projeto);
            
            // Botão de fechar
            const closeBtn = document.createElement('span');
            closeBtn.innerHTML = '&times;';
            closeBtn.style.cssText = 'margin-left: 8px; cursor: pointer; opacity: 0.7;';
            closeBtn.onclick = (e) => {
                e.stopPropagation();
                this.closeProject(projeto);
            };
            
            tab.appendChild(closeBtn);
            this.elements.projectTabs.appendChild(tab);
        });
    }

    /**
     * Seleciona um projeto para edição
     * @param {Projeto} projeto - Projeto a selecionar
     */
    selectProject(projeto) {
        // Verifica alterações não salvas no projeto atual
        if (this.projetoSelecionado?.temAlteracoesNaoSalvas) {
            this.showModal(
                'Salvar alterações?',
                'O projeto atual tem alterações não salvas. Deseja salvar antes de mudar?',
                async () => {
                    await app.saveProject();
                    this.doSelectProject(projeto);
                },
                () => this.doSelectProject(projeto)
            );
        } else {
            this.doSelectProject(projeto);
        }
    }

    /**
     * Executa a seleção do projeto
     */
    doSelectProject(projeto) {
        this.projetoSelecionado = projeto;
        this.renderProjectTabs();
        this.renderTracks();
        this.updateControls();
        
        // Atualiza textarea do roteiro
        if (this.elements.roteiroText) {
            this.elements.roteiroText.value = projeto.textoRoteiro || '';
        }
    }

    /**
     * Fecha um projeto
     * @param {Projeto} projeto - Projeto a fechar
     */
    async closeProject(projeto) {
        if (projeto.temAlteracoesNaoSalvas) {
            const confirmed = await this.showConfirm(
                'Salvar alterações?',
                'Deseja salvar as alterações antes de fechar?'
            );
            
            if (confirmed) {
                await app.saveProject();
            }
        }
        
        const index = this.projetosAbertos.indexOf(projeto);
        if (index !== -1) {
            this.projetosAbertos.splice(index, 1);
            
            if (this.projetoSelecionado === projeto) {
                this.projetoSelecionado = this.projetosAbertos[0] || null;
                if (this.projetoSelecionado) {
                    this.doSelectProject(this.projetoSelecionado);
                } else {
                    this.renderTracks();
                    this.updateControls();
                }
            }
            
            this.renderProjectTabs();
        }
    }

    /**
     * Renderiza a lista de tracks
     */
    renderTracks() {
        this.elements.tracksList.innerHTML = '';
        
        if (!this.projetoSelecionado) {
            this.elements.tracksList.innerHTML = '<p style="text-align: center; color: var(--text-secondary); padding: 40px;">Nenhum projeto selecionado. Crie ou abra um projeto para começar.</p>';
            return;
        }

        this.projetoSelecionado.itens.forEach((item, index) => {
            const trackEl = this.createTrackElement(item, index);
            this.elements.tracksList.appendChild(trackEl);
        });
    }

    /**
     * Cria elemento de track
     * @param {ItemRoteiro} item - Item do roteiro
     * @param {number} index - Índice do item
     * @returns {HTMLElement}
     */
    createTrackElement(item, index) {
        const track = document.createElement('div');
        track.className = `track-item ${this.itemSelecionado === item ? 'selected' : ''}`;
        track.dataset.itemId = item.id;

        // Header da track
        const header = document.createElement('div');
        header.className = 'track-header';
        
        header.innerHTML = `
            <span class="track-number">#${index + 1}</span>
            <span class="track-text">${this.escapeHtml(item.texto)}</span>
            <div class="track-status">
                <span class="status-badge ${item.temAudio ? (item.aprovado ? 'approved' : 'has-audio') : 'no-audio'}">
                    ${item.temAudio ? (item.aprovado ? '✓ Aprovado' : '● Com áudio') : '○ Sem áudio'}
                </span>
            </div>
        `;

        // Body da track
        const body = document.createElement('div');
        body.className = 'track-body';

        // Canvas do waveform
        const waveformContainer = document.createElement('div');
        waveformContainer.className = 'waveform-container';
        
        const canvas = document.createElement('canvas');
        canvas.className = 'waveform-canvas';
        canvas.width = 800;
        canvas.height = 80;
        
        waveformContainer.appendChild(canvas);
        body.appendChild(waveformContainer);

        // Controles da track
        const controls = document.createElement('div');
        controls.className = 'track-controls';
        
        controls.innerHTML = `
            <span class="track-time">${item.getFormattedDuration()}</span>
            <div class="track-actions">
                <button class="track-btn record" data-action="record" title="Gravar">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                        <circle cx="12" cy="12" r="10"/>
                    </svg>
                </button>
                <button class="track-btn play" data-action="play" title="Reproduzir">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M8 5v14l11-7z"/>
                    </svg>
                </button>
                <button class="track-btn" data-action="approve" title="${item.aprovado ? 'Desaprovar' : 'Aprovar'}">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>
                    </svg>
                </button>
                <button class="track-btn" data-action="delete" title="Excluir">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/>
                    </svg>
                </button>
            </div>
        `;

        body.appendChild(controls);
        track.appendChild(header);
        track.appendChild(body);

        // Event listeners
        track.onclick = (e) => {
            if (!e.target.closest('.track-btn')) {
                this.selectItem(item);
            }
        };

        // Botões de ação
        body.querySelectorAll('.track-btn').forEach(btn => {
            btn.onclick = (e) => {
                e.stopPropagation();
                const action = btn.dataset.action;
                this.handleTrackAction(action, item, canvas);
            };
        });

        // Clique no waveform para seek
        waveformContainer.onclick = (e) => {
            if (item.temAudio && item.audioBuffer) {
                const rect = canvas.getBoundingClientRect();
                const x = e.clientX - rect.left;
                const position = x / rect.width;
                app.seekItem(position);
            }
        };

        // Desenha waveform se houver áudio
        if (item.temAudio && item.waveformPoints.length > 0) {
            setTimeout(() => {
                WaveformUtils.drawWaveform(canvas, item.waveformPoints, item.getWaveformColor());
            }, 0);
        }

        return track;
    }

    /**
     * Seleciona um item
     * @param {ItemRoteiro} item - Item a selecionar
     */
    selectItem(item) {
        this.itemSelecionado = item;
        this.renderTracks();
        this.updateControls();
    }

    /**
     * Atualiza estado dos controles
     */
    updateControls() {
        const hasProject = !!this.projetoSelecionado;
        const hasItem = !!this.itemSelecionado;
        const hasAudio = hasItem && this.itemSelecionado.temAudio;

        this.elements.btnRecord.disabled = !hasProject;
        this.elements.btnPlay.disabled = !hasAudio;
        this.elements.btnStop.disabled = !app.audioService.isPlaying;
        this.elements.btnUndo.disabled = this.undoStack.length === 0;
        this.elements.btnRedo.disabled = this.redoStack.length === 0;
    }

    /**
     * Manipula ações da track
     * @param {string} action - Ação a executar
     * @param {ItemRoteiro} item - Item alvo
     * @param {HTMLCanvasElement} canvas - Canvas do waveform
     */
    handleTrackAction(action, item, canvas) {
        switch (action) {
            case 'record':
                app.recordItem(item);
                break;
            case 'play':
                app.playItem(item);
                break;
            case 'approve':
                this.toggleApproveItem(item);
                break;
            case 'delete':
                this.deleteItem(item);
                break;
        }
    }

    /**
     * Alterna status de aprovação do item
     * @param {ItemRoteiro} item - Item
     */
    toggleApproveItem(item) {
        this.pushUndo(() => {
            item.aprovado = !item.aprovado;
            this.renderTracks();
            app.markProjectChanged();
        }, () => {
            item.aprovado = !item.aprovado;
            this.renderTracks();
            app.markProjectChanged();
        });
    }

    /**
     * Exclui um item
     * @param {ItemRoteiro} item - Item a excluir
     */
    deleteItem(item) {
        this.showModal(
            'Excluir item',
            `Tem certeza que deseja excluir "${item.texto.substring(0, 50)}${item.texto.length > 50 ? '...' : ''}"?`,
            () => {
                this.pushUndo(() => {
                    const index = this.projetoSelecionado.itens.indexOf(item);
                    if (index !== -1) {
                        this.projetoSelecionado.itens.splice(index, 1);
                        this.renderTracks();
                        app.markProjectChanged();
                    }
                }, () => {
                    this.projetoSelecionado.itens.push(item);
                    this.renderTracks();
                    app.markProjectChanged();
                });
            }
        );
    }

    /**
     * Adiciona ação ao undo stack
     * @param {Function} action - Ação para fazer
     * @param {Function} undoAction - Ação para desfazer
     */
    pushUndo(action, undoAction) {
        this.undoStack.push({ action, undoAction });
        if (this.undoStack.length > this.maxUndoHistory) {
            this.undoStack.shift();
        }
        this.redoStack = [];
        this.updateControls();
    }

    /**
     * Desfaz última ação
     */
    undo() {
        if (this.undoStack.length > 0) {
            const command = this.undoStack.pop();
            command.undoAction();
            this.redoStack.push(command);
            this.updateControls();
        }
    }

    /**
     * Refaz ação desfeita
     */
    redo() {
        if (this.redoStack.length > 0) {
            const command = this.redoStack.pop();
            command.action();
            this.undoStack.push(command);
            this.updateControls();
        }
    }

    /**
     * Mostra modal de confirmação
     * @param {string} title - Título
     * @param {string} message - Mensagem
     * @param {Function} onConfirm - Callback de confirmação
     * @param {Function} onCancel - Callback de cancelamento
     */
    showModal(title, message, onConfirm, onCancel = null) {
        return new Promise((resolve) => {
            this.elements.modalTitle.textContent = title;
            this.elements.modalMessage.textContent = message;
            this.elements.modalOverlay.classList.add('active');

            const confirmHandler = () => {
                this.elements.modalOverlay.classList.remove('active');
                this.elements.modalConfirm.removeEventListener('click', confirmHandler);
                this.elements.modalCancel.removeEventListener('click', cancelHandler);
                if (onConfirm) onConfirm();
                resolve(true);
            };

            const cancelHandler = () => {
                this.elements.modalOverlay.classList.remove('active');
                this.elements.modalConfirm.removeEventListener('click', confirmHandler);
                this.elements.modalCancel.removeEventListener('click', cancelHandler);
                if (onCancel) onCancel();
                resolve(false);
            };

            this.elements.modalConfirm.addEventListener('click', confirmHandler);
            this.elements.modalCancel.addEventListener('click', cancelHandler);
        });
    }

    /**
     * Mostra confirmação rápida
     * @param {string} title - Título
     * @param {string} message - Mensagem
     * @returns {Promise<boolean>}
     */
    async showConfirm(title, message) {
        return this.showModal(title, message, () => {}, () => {});
    }

    /**
     * Mostra notificação toast
     * @param {string} message - Mensagem
     * @param {string} type - Tipo (success, error, warning, info)
     */
    showToast(message, type = 'info') {
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.textContent = message;
        
        this.elements.toastContainer.appendChild(toast);
        
        setTimeout(() => {
            toast.style.animation = 'slideIn 0.3s ease reverse';
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    /**
     * Escapa HTML para prevenir XSS
     * @param {string} text - Texto a escapar
     * @returns {string}
     */
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Atualiza lista de dispositivos de áudio
     * @param {Array} devices - Lista de dispositivos
     */
    updateDeviceList(devices) {
        this.elements.deviceSelect.innerHTML = '<option value="">Selecione o microfone...</option>';
        
        devices.forEach(device => {
            const option = document.createElement('option');
            option.value = device.deviceId;
            option.textContent = device.label || `Microfone ${device.index + 1}`;
            this.elements.deviceSelect.appendChild(option);
        });
    }
}

// Export para ambientes module
if (typeof module !== 'undefined' && module.exports) {
    module.exports = UIManager;
}
