/**
 * ProjectService
 * Serviço para CRUD de projetos usando IndexedDB
 */

class ProjectService {
    constructor() {
        this.dbName = 'GravadorMultiDB';
        this.dbVersion = 1;
        this.storeProjetos = 'projetos';
        this.storeAudios = 'audios';
        this.db = null;
    }

    /**
     * Inicializa o banco de dados IndexedDB
     */
    async init() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, this.dbVersion);

            request.onerror = () => {
                console.error('Erro ao abrir IndexedDB:', request.error);
                reject(request.error);
            };

            request.onsuccess = () => {
                this.db = request.result;
                console.log('IndexedDB inicializado com sucesso');
                resolve(this.db);
            };

            request.onupgradeneeded = (event) => {
                const db = event.target.result;

                // Cria store de projetos
                if (!db.objectStoreNames.contains(this.storeProjetos)) {
                    const projetoStore = db.createObjectStore(this.storeProjetos, { keyPath: 'id' });
                    projetoStore.createIndex('nome', 'nome', { unique: false });
                    projetoStore.createIndex('dataModificacao', 'dataModificacao', { unique: false });
                }

                // Cria store de áudios
                if (!db.objectStoreNames.contains(this.storeAudios)) {
                    const audioStore = db.createObjectStore(this.storeAudios, { keyPath: 'id' });
                    audioStore.createIndex('itemId', 'itemId', { unique: false });
                    audioStore.createIndex('projetoId', 'projetoId', { unique: false });
                }
            };
        });
    }

    /**
     * Salva um projeto no IndexedDB
     * @param {Projeto} projeto - Projeto a ser salvo
     */
    async salvarProjeto(projeto) {
        if (!this.db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeProjetos], 'readwrite');
            const store = transaction.objectStore(this.storeProjetos);
            
            const data = projeto.toJSON();
            const request = store.put(data);

            request.onsuccess = () => {
                projeto.temAlteracoesNaoSalvas = false;
                console.log('Projeto salvo:', projeto.nome);
                resolve(data.id);
            };

            request.onerror = () => {
                console.error('Erro ao salvar projeto:', request.error);
                reject(request.error);
            };
        });
    }

    /**
     * Carrega um projeto do IndexedDB
     * @param {string} projetoId - ID do projeto
     * @returns {Promise<Projeto>}
     */
    async carregarProjeto(projetoId) {
        if (!this.db) await this.init();

        return new Promise(async (resolve, reject) => {
            const transaction = this.db.transaction([this.storeProjetos], 'readonly');
            const store = transaction.objectStore(this.storeProjetos);
            const request = store.get(projetoId);

            request.onsuccess = async () => {
                if (request.result) {
                    const projeto = Projeto.fromJSON(request.result);
                    
                    // Carrega blobs de áudio para cada item
                    for (const item of projeto.itens) {
                        if (item.temAudio) {
                            try {
                                const blob = await this.carregarAudio(item.id);
                                if (blob) {
                                    item.audioBlob = blob;
                                }
                            } catch (error) {
                                console.error(`Erro ao carregar áudio do item ${item.id}:`, error);
                            }
                        }
                    }
                    
                    resolve(projeto);
                } else {
                    resolve(null);
                }
            };

            request.onerror = () => {
                console.error('Erro ao carregar projeto:', request.error);
                reject(request.error);
            };
        });
    }

    /**
     * Lista todos os projetos salvos
     * @returns {Promise<Array<{id, nome, dataModificacao}>>}
     */
    async listarProjetos() {
        if (!this.db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeProjetos], 'readonly');
            const store = transaction.objectStore(this.storeProjetos);
            const request = store.getAll();

            request.onsuccess = () => {
                const projetos = request.result.map(p => ({
                    id: p.id,
                    nome: p.nome,
                    dataModificacao: p.dataModificacao
                }));
                resolve(projetos);
            };

            request.onerror = () => {
                console.error('Erro ao listar projetos:', request.error);
                reject(request.error);
            };
        });
    }

    /**
     * Exclui um projeto do IndexedDB
     * @param {string} projetoId - ID do projeto
     */
    async excluirProjeto(projetoId) {
        if (!this.db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeProjetos, this.storeAudios], 'readwrite');
            const projetoStore = transaction.objectStore(this.storeProjetos);
            const audioStore = transaction.objectStore(this.storeAudios);

            // Exclui projeto
            const deleteRequest = projetoStore.delete(projetoId);

            deleteRequest.onsuccess = async () => {
                // Exclui áudios associados
                await this.excluirAudiosDoProjeto(projetoId);
                console.log('Projeto excluído:', projetoId);
                resolve();
            };

            deleteRequest.onerror = () => {
                console.error('Erro ao excluir projeto:', deleteRequest.error);
                reject(deleteRequest.error);
            };
        });
    }

    /**
     * Salva um áudio associado a um item
     * @param {string} itemId - ID do item
     * @param {string} projetoId - ID do projeto
     * @param {Blob} blob - Blob do áudio
     */
    async salvarAudio(itemId, projetoId, blob) {
        if (!this.db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeAudios], 'readwrite');
            const store = transaction.objectStore(this.storeAudios);

            const data = {
                id: itemId,
                itemId: itemId,
                projetoId: projetoId,
                blob: blob,
                dataSalvo: new Date().toISOString()
            };

            const request = store.put(data);

            request.onsuccess = () => {
                console.log('Áudio salvo:', itemId);
                resolve(itemId);
            };

            request.onerror = () => {
                console.error('Erro ao salvar áudio:', request.error);
                reject(request.error);
            };
        });
    }

    /**
     * Carrega um áudio de um item
     * @param {string} itemId - ID do item
     * @returns {Promise<Blob|null>}
     */
    async carregarAudio(itemId) {
        if (!this.db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeAudios], 'readonly');
            const store = transaction.objectStore(this.storeAudios);
            const request = store.get(itemId);

            request.onsuccess = () => {
                if (request.result) {
                    resolve(request.result.blob);
                } else {
                    resolve(null);
                }
            };

            request.onerror = () => {
                console.error('Erro ao carregar áudio:', request.error);
                reject(request.error);
            };
        });
    }

    /**
     * Exclui todos os áudios de um projeto
     * @param {string} projetoId - ID do projeto
     */
    async excluirAudiosDoProjeto(projetoId) {
        if (!this.db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([this.storeAudios], 'readwrite');
            const store = transaction.objectStore(this.storeAudios);
            const index = store.index('projetoId');
            const request = index.getAllKeys(projetoId);

            request.onsuccess = () => {
                const ids = request.result;
                ids.forEach(id => {
                    store.delete(id);
                });
                console.log(`Áudios do projeto ${projetoId} excluídos`);
                resolve();
            };

            request.onerror = () => {
                console.error('Erro ao excluir áudios:', request.error);
                reject(request.error);
            };
        });
    }

    /**
     * Exporta projeto como JSON + Blobs em um arquivo ZIP (simulado)
     * @param {Projeto} projeto - Projeto a exportar
     * @returns {Promise<Object>} Dados para download
     */
    async exportarProjeto(projeto) {
        const exportData = {
            projeto: projeto.toJSON(),
            audios: {}
        };

        // Coleta todos os blobs
        for (const item of projeto.itens) {
            if (item.temAudio && item.audioBlob) {
                exportData.audios[item.id] = item.audioBlob;
            }
        }

        return exportData;
    }

    /**
     * Importa projeto de dados exportados
     * @param {Object} importData - Dados importados
     * @returns {Promise<Projeto>}
     */
    async importarProjeto(importData) {
        const projeto = Projeto.fromJSON(importData.projeto);
        
        // Restaura blobs
        for (const [itemId, blob] of Object.entries(importData.audios || {})) {
            const item = projeto.getItem(itemId);
            if (item) {
                item.audioBlob = blob;
                await this.salvarAudio(itemId, projeto.id, blob);
            }
        }

        await this.salvarProjeto(projeto);
        return projeto;
    }

    /**
     * Limpa todo o banco de dados
     */
    async limparTudo() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.deleteDatabase(this.dbName);

            request.onsuccess = () => {
                this.db = null;
                console.log('Banco de dados limpo');
                resolve();
            };

            request.onerror = () => {
                console.error('Erro ao limpar banco:', request.error);
                reject(request.error);
            };
        });
    }

    /**
     * Fecha conexão com o banco
     */
    close() {
        if (this.db) {
            this.db.close();
            this.db = null;
        }
    }
}

// Export para ambientes module
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ProjectService;
}
