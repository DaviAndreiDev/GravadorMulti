/**
 * WaveformUtils
 * Utilitários para geração e renderização de waveforms de áudio
 */

const WaveformUtils = {
    /**
     * Configurações padrão para waveform
     */
    config: {
        width: 800,
        height: 80,
        barWidth: 2,
        gap: 1,
        padding: 4
    },

    /**
     * Extrai pontos de waveform de um AudioBuffer
     * @param {AudioBuffer} audioBuffer - Buffer de áudio da Web Audio API
     * @param {number} width - Largura desejada do waveform em pixels
     * @returns {Array<number>} Array de amplitudes normalizadas (0-1)
     */
    extractWaveformPoints(audioBuffer, width = 800) {
        const channelData = audioBuffer.getChannelData(0); // Canal esquerdo (mono)
        const samples = channelData.length;
        const blockSize = Math.floor(samples / width);
        const points = [];

        for (let i = 0; i < width; i++) {
            let max = 0;
            const start = i * blockSize;
            const end = start + blockSize;

            // Encontra o pico máximo no bloco
            for (let j = start; j < end && j < samples; j++) {
                const amplitude = Math.abs(channelData[j]);
                if (amplitude > max) {
                    max = amplitude;
                }
            }

            points.push(max);
        }

        return points;
    },

    /**
     * Desenha waveform em um canvas
     * @param {HTMLCanvasElement} canvas - Elemento canvas
     * @param {Array<number>} points - Pontos de amplitude do waveform
     * @param {string} color - Cor do waveform
     * @param {object} options - Opções adicionais
     */
    drawWaveform(canvas, points, color = '#1E3A5F', options = {}) {
        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;
        const padding = options.padding || this.config.padding;
        const barWidth = options.barWidth || this.config.barWidth;
        const gap = options.gap || this.config.gap;

        // Limpa o canvas
        ctx.clearRect(0, 0, width, height);

        // Preenche background
        ctx.fillStyle = '#1E1E1E';
        ctx.fillRect(0, 0, width, height);

        // Desenha waveform
        ctx.fillStyle = color;
        
        const availableHeight = height - (padding * 2);
        const centerY = height / 2;

        for (let i = 0; i < points.length; i++) {
            const x = i * (barWidth + gap);
            const amplitude = points[i] * availableHeight;
            const y = centerY - amplitude / 2;

            ctx.fillRect(x, y, barWidth, amplitude);
        }

        // Desenha linha central
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(0, centerY);
        ctx.lineTo(width, centerY);
        ctx.stroke();
    },

    /**
     * Desenha waveform com posição de playback
     * @param {HTMLCanvasElement} canvas - Elemento canvas
     * @param {Array<number>} points - Pontos de amplitude
     * @param {number} position - Posição atual de playback (0-1)
     * @param {string} color - Cor do waveform
     */
    drawWaveformWithPosition(canvas, points, position, color = '#1E3A5F') {
        this.drawWaveform(canvas, points, color);

        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;

        // Desenha indicador de posição
        const xPos = position * width;
        
        ctx.strokeStyle = '#FFFFFF';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(xPos, 0);
        ctx.lineTo(xPos, height);
        ctx.stroke();

        // Desenha triângulo no topo
        ctx.fillStyle = '#FFFFFF';
        ctx.beginPath();
        ctx.moveTo(xPos - 5, 0);
        ctx.lineTo(xPos + 5, 0);
        ctx.lineTo(xPos, 8);
        ctx.closePath();
        ctx.fill();
    },

    /**
     * Gera pontos de waveform a partir de um Blob de áudio
     * @param {Blob} audioBlob - Blob de áudio
     * @param {AudioContext} audioContext - Contexto de áudio
     * @returns {Promise<Array<number>>} Promise com pontos de waveform
     */
    async generateWaveformFromBlob(audioBlob, audioContext) {
        try {
            const arrayBuffer = await audioBlob.arrayBuffer();
            const audioBuffer = await audioContext.decodeAudioData(arrayBuffer);
            return this.extractWaveformPoints(audioBuffer);
        } catch (error) {
            console.error('Erro ao gerar waveform:', error);
            return [];
        }
    },

    /**
     * Converte volumes em tempo real para pontos de desenho
     * @param {Array<number>} volumes - Array de volumes (0-1)
     * @param {number} width - Largura do canvas
     * @returns {Array<{x: number, y: number, height: number}>}
     */
    converterVolumesParaPontos(volumes, width) {
        const points = [];
        const step = width / volumes.length;

        volumes.forEach((volume, index) => {
            const x = index * step;
            const height = volume * 80; // Altura máxima de 80px
            const y = (80 - height) / 2;

            points.push({ x, y, height });
        });

        return points;
    },

    /**
     * Renderiza waveform em tempo real durante gravação
     * @param {HTMLCanvasElement} canvas - Canvas
     * @param {AnalyserNode} analyser - AnalyserNode da Web Audio API
     * @param {string} color - Cor do waveform
     */
    renderRealtimeWaveform(canvas, analyser, color = '#2D63C8') {
        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;
        const bufferLength = analyser.frequencyBinCount;
        const dataArray = new Uint8Array(bufferLength);

        const draw = () => {
            requestAnimationFrame(draw);

            analyser.getByteTimeDomainData(dataArray);

            ctx.fillStyle = '#1E1E1E';
            ctx.fillRect(0, 0, width, height);

            ctx.lineWidth = 2;
            ctx.strokeStyle = color;
            ctx.beginPath();

            const sliceWidth = width / bufferLength;
            let x = 0;

            for (let i = 0; i < bufferLength; i++) {
                const v = dataArray[i] / 128.0;
                const y = (v * height) / 2;

                if (i === 0) {
                    ctx.moveTo(x, y);
                } else {
                    ctx.lineTo(x, y);
                }

                x += sliceWidth;
            }

            ctx.lineTo(width, height / 2);
            ctx.stroke();
        };

        draw();
    },

    /**
     * Calcula RMS (Root Mean Square) de um array de samples
     * @param {Float32Array} samples - Array de samples de áudio
     * @returns {number} Valor RMS normalizado (0-1)
     */
    calculateRMS(samples) {
        let sum = 0;
        for (let i = 0; i < samples.length; i++) {
            sum += samples[i] * samples[i];
        }
        return Math.sqrt(sum / samples.length);
    },

    /**
     * Converte valor linear para dB
     * @param {number} value - Valor linear (0-1)
     * @returns {string} Valor em dB formatado
     */
    linearToDb(value) {
        if (value <= 0.00001) {
            return '-∞';
        }
        const db = 20 * Math.log10(value);
        return db.toFixed(1);
    }
};

// Export para ambientes module
if (typeof module !== 'undefined' && module.exports) {
    module.exports = WaveformUtils;
}
