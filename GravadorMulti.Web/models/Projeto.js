/**
 * Modelo Projeto
 * Representa um projeto completo de gravação com múltiplos itens de roteiro
 */

class Projeto {
    constructor(data = {}) {
        this.id = data.id || this.generateId();
        this.nome = data.nome || 'Novo Projeto';
        this.dataCriacao = data.dataCriacao || new Date().toISOString();
        this.dataModificacao = data.dataModificacao || new Date().toISOString();
        this.itens = data.itens || [];
        this.textoRoteiro = data.textoRoteiro || '';
        this.dispositivoEntrada = data.dispositivoEntrada || '';
        this.temAlteracoesNaoSalvas = false;
    }

    generateId() {
        return 'proj_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }

    /**
     * Adiciona um novo item ao projeto
     */
    adicionarItem(texto = '') {
        const item = new ItemRoteiro({ texto });
        this.itens.push(item);
        this.dataModificacao = new Date().toISOString();
        this.temAlteracoesNaoSalvas = true;
        return item;
    }

    /**
     * Remove um item do projeto
     */
    removerItem(itemId) {
        const index = this.itens.findIndex(i => i.id === itemId);
        if (index !== -1) {
            this.itens.splice(index, 1);
            this.dataModificacao = new Date().toISOString();
            this.temAlteracoesNaoSalvas = true;
            return true;
        }
        return false;
    }

    /**
     * Obtém um item pelo ID
     */
    getItem(itemId) {
        return this.itens.find(i => i.id === itemId);
    }

    /**
     * Fatiar texto em itens individuais
     */
    fatiarTexto(texto) {
        this.textoRoteiro = texto;
        this.itens = [];
        
        // Divide por quebras de linha e filtra linhas vazias
        const linhas = texto.split('\n').filter(linha => linha.trim() !== '');
        
        linhas.forEach(linha => {
            this.adicionarItem(linha.trim());
        });
        
        this.dataModificacao = new Date().toISOString();
        this.temAlteracoesNaoSalvas = true;
        return this.itens.length;
    }

    /**
     * Conta quantos itens têm áudio
     */
    contarItensComAudio() {
        return this.itens.filter(item => item.temAudio).length;
    }

    /**
     * Conta quantos itens estão aprovados
     */
    contarItensAprovados() {
        return this.itens.filter(item => item.aprovado).length;
    }

    /**
     * Obtém a duração total do projeto
     */
    getDuracaoTotal() {
        return this.itens.reduce((total, item) => total + (item.duracao || 0), 0);
    }

    /**
     * Formata a duração total para exibição
     */
    getDuracaoTotalFormatada() {
        const totalSegundos = this.getDuracaoTotal();
        const minutes = Math.floor(totalSegundos / 60);
        const seconds = Math.floor(totalSegundos % 60);
        return `${minutes}m ${seconds}s`;
    }

    /**
     * Serializa o projeto para armazenamento
     */
    toJSON() {
        return {
            id: this.id,
            nome: this.nome,
            dataCriacao: this.dataCriacao,
            dataModificacao: this.dataModificacao,
            textoRoteiro: this.textoRoteiro,
            dispositivoEntrada: this.dispositivoEntrada,
            itens: this.itens.map(item => item.toJSON())
        };
    }

    /**
     * Cria um projeto a partir de JSON
     */
    static fromJSON(json) {
        const projeto = new Projeto({
            id: json.id,
            nome: json.nome,
            dataCriacao: json.dataCriacao,
            dataModificacao: json.dataModificacao,
            textoRoteiro: json.textoRoteiro,
            dispositivoEntrada: json.dispositivoEntrada
        });
        
        // Reconstrói os itens
        if (json.itens && Array.isArray(json.itens)) {
            projeto.itens = json.itens.map(itemJson => ItemRoteiro.fromJSON(itemJson));
        }
        
        return projeto;
    }

    /**
     * Clona o projeto
     */
    clone() {
        const clone = new Projeto({
            id: this.id,
            nome: this.nome,
            dataCriacao: this.dataCriacao,
            dataModificacao: this.dataModificacao,
            textoRoteiro: this.textoRoteiro,
            dispositivoEntrada: this.dispositivoEntrada
        });
        
        clone.itens = this.itens.map(item => item.clone());
        return clone;
    }

    /**
     * Limpa todos os áudios do projeto
     */
    limparTodosAudios() {
        this.itens.forEach(item => {
            item.temAudio = false;
            item.audioBlob = null;
            item.audioBuffer = null;
            item.waveformPoints = [];
            item.duracao = 0;
        });
        this.dataModificacao = new Date().toISOString();
        this.temAlteracoesNaoSalvas = true;
    }
}

// Export para ambientes module
if (typeof module !== 'undefined' && module.exports) {
    module.exports = Projeto;
}
