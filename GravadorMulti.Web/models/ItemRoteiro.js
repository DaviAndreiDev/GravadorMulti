/**
 * Modelo ItemRoteiro
 * Representa uma linha individual do roteiro com seu áudio associado
 */

class ItemRoteiro {
    constructor(data = {}) {
        this.id = data.id || this.generateId();
        this.texto = data.texto || '';
        this.audioBlob = data.audioBlob || null;
        this.temAudio = data.temAudio || false;
        this.aprovado = data.aprovado || false;
        this.duracao = data.duracao || 0;
        this.corteInicio = data.corteInicio || 0;
        this.corteFim = data.corteFim || 0;
        this.audioBuffer = data.audioBuffer || null;
        this.waveformPoints = data.waveformPoints || [];
    }

    generateId() {
        return 'item_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }

    /**
     * Verifica se o item tem áudio gravado
     */
    hasAudio() {
        return this.temAudio && (this.audioBlob !== null || this.audioBuffer !== null);
    }

    /**
     * Obtém a cor do waveform baseada no estado
     */
    getWaveformColor() {
        if (this.aprovado) {
            return '#1E441E';
        } else if (this.temAudio) {
            return '#1E3A5F';
        } else {
            return '#2B2B2B';
        }
    }

    /**
     * Formata a duração para exibição (MM:SS.mmm)
     */
    getFormattedDuration() {
        const minutes = Math.floor(this.duracao / 60);
        const seconds = Math.floor(this.duracao % 60);
        const milliseconds = Math.floor((this.duracao % 1) * 1000);
        
        return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}.${milliseconds.toString().padStart(3, '0')}`;
    }

    /**
     * Serializa o item para armazenamento (exclui audioBuffer que é regenerável)
     */
    toJSON() {
        return {
            id: this.id,
            texto: this.texto,
            temAudio: this.temAudio,
            aprovado: this.aprovado,
            duracao: this.duracao,
            corteInicio: this.corteInicio,
            corteFim: this.corteFim,
            waveformPoints: this.waveformPoints,
            // audioBlob é serializado separadamente como Blob
            audioBlobId: this.audioBlob ? this.id + '_blob' : null
        };
    }

    /**
     * Cria um item a partir de JSON
     */
    static fromJSON(json) {
        return new ItemRoteiro({
            id: json.id,
            texto: json.texto,
            temAudio: json.temAudio,
            aprovado: json.aprovado,
            duracao: json.duracao,
            corteInicio: json.corteInicio,
            corteFim: json.corteFim,
            waveformPoints: json.waveformPoints || []
        });
    }

    /**
     * Clona o item
     */
    clone() {
        return new ItemRoteiro({
            id: this.id,
            texto: this.texto,
            audioBlob: this.audioBlob,
            temAudio: this.temAudio,
            aprovado: this.aprovado,
            duracao: this.duracao,
            corteInicio: this.corteInicio,
            corteFim: this.corteFim,
            audioBuffer: this.audioBuffer,
            waveformPoints: [...this.waveformPoints]
        });
    }
}

// Export para ambientes module
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ItemRoteiro;
}
