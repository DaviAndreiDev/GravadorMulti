/**
 * AudioService
 * Serviço de áudio baseado na Web Audio API para gravação e reprodução
 */

class AudioService {
    constructor() {
        this.audioContext = null;
        this.analyser = null;
        this.mediaStream = null;
        this.mediaRecorder = null;
        this.sourceNode = null;
        this.gainNode = null;
        
        this.isRecording = false;
        this.isMonitoring = false;
        this.isPlaying = false;
        this.currentSource = null;
        
        this.startTime = 0;
        this.pauseTime = 0;
        this.recordedChunks = [];
        
        // Callbacks
        this.onVolumeReceived = null;
        this.onRecordingStopped = null;
        this.onPlaybackStopped = null;
        
        // Configurações
        this.sampleRate = 44100;
        this.bufferSize = 4096;
        this.channels = 1; // Mono
        
        // Monitoramento VU meter
        this.monitorInterval = null;
    }

    /**
     * Inicializa o contexto de áudio
     */
    async init() {
        try {
            // Cria AudioContext (compatibilidade cross-browser)
            const AudioContextClass = window.AudioContext || window.webkitAudioContext;
            this.audioContext = new AudioContextClass();
            
            // Cria analyser para visualização
            this.analyser = this.audioContext.createAnalyser();
            this.analyser.fftSize = 256;
            
            console.log('AudioService inicializado com sucesso');
            return true;
        } catch (error) {
            console.error('Erro ao inicializar AudioService:', error);
            throw error;
        }
    }

    /**
     * Solicita permissão e obtém dispositivos de entrada
     */
    async getMediaDevices() {
        try {
            const devices = await navigator.mediaDevices.enumerateDevices();
            return devices.filter(device => device.kind === 'audioinput');
        } catch (error) {
            console.error('Erro ao listar dispositivos:', error);
            return [];
        }
    }

    /**
     * Inicia captura do microfone
     * @param {string} deviceId - ID do dispositivo de entrada (opcional)
     */
    async startMonitoring(deviceId = null) {
        try {
            // Para monitoramento anterior se existir
            await this.stopMonitoring();
            
            const constraints = {
                audio: {
                    deviceId: deviceId ? { exact: deviceId } : undefined,
                    echoCancellation: true,
                    noiseSuppression: true,
                    autoGainControl: false
                }
            };

            this.mediaStream = await navigator.mediaDevices.getUserMedia(constraints);
            
            // Conecta ao analyser para VU meter
            const source = this.audioContext.createMediaStreamSource(this.mediaStream);
            source.connect(this.analyser);
            
            this.isMonitoring = true;
            this.startVUMonitor();
            
            return true;
        } catch (error) {
            console.error('Erro ao iniciar monitoramento:', error);
            throw error;
        }
    }

    /**
     * Para o monitoramento do microfone
     */
    async stopMonitoring() {
        if (this.mediaStream) {
            this.mediaStream.getTracks().forEach(track => track.stop());
            this.mediaStream = null;
        }
        
        this.isMonitoring = false;
        this.stopVUMonitor();
    }

    /**
     * Inicia gravação de áudio
     * @param {string} mimeType - MIME type para gravação (padrão: audio/webm)
     */
    async startRecording(mimeType = 'audio/webm') {
        if (!this.mediaStream) {
            await this.startMonitoring();
        }

        try {
            this.recordedChunks = [];
            
            // Verifica suporte a MIME types
            if (!MediaRecorder.isTypeSupported(mimeType)) {
                mimeType = 'audio/webm'; // Fallback
            }

            this.mediaRecorder = new MediaRecorder(this.mediaStream, {
                mimeType: mimeType,
                audioBitsPerSecond: 128000
            });

            this.mediaRecorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    this.recordedChunks.push(event.data);
                }
            };

            this.mediaRecorder.onstop = async () => {
                const blob = new Blob(this.recordedChunks, { type: mimeType });
                this.isRecording = false;
                
                if (this.onRecordingStopped) {
                    this.onRecordingStopped(blob);
                }
            };

            this.mediaRecorder.start(100); // Coleta chunks a cada 100ms
            this.isRecording = true;
            this.startTime = this.audioContext.currentTime;
            
            console.log('Gravação iniciada');
            return true;
        } catch (error) {
            console.error('Erro ao iniciar gravação:', error);
            throw error;
        }
    }

    /**
     * Para a gravação atual
     */
    stopRecording() {
        if (this.mediaRecorder && this.isRecording) {
            this.mediaRecorder.stop();
            this.isRecording = false;
        }
    }

    /**
     * Toca um AudioBuffer
     * @param {AudioBuffer} audioBuffer - Buffer de áudio para tocar
     * @param {number} offset - Posição inicial em segundos
     * @param {number} startTime - Tempo de início no AudioContext
     */
    play(audioBuffer, offset = 0, startTime = 0) {
        if (!audioBuffer) {
            console.error('AudioBuffer não fornecido');
            return;
        }

        // Para playback anterior se existir
        this.stop();

        try {
            // Resume context se estiver suspenso (política de autoplay)
            if (this.audioContext.state === 'suspended') {
                this.audioContext.resume();
            }

            // Cria source node
            this.currentSource = this.audioContext.createBufferSource();
            this.currentSource.buffer = audioBuffer;
            
            // Cria gain node para controle de volume
            this.gainNode = this.audioContext.createGain();
            this.gainNode.gain.value = 1.0;
            
            // Conecta nodes: source -> gain -> destination
            this.currentSource.connect(this.gainNode);
            this.gainNode.connect(this.audioContext.destination);
            
            // Inicia playback
            const when = startTime || this.audioContext.currentTime;
            this.currentSource.start(when, offset);
            
            this.isPlaying = true;
            this.startTime = when;
            this.pauseTime = offset;
            
            // Callback quando terminar
            this.currentSource.onended = () => {
                if (this.isPlaying) {
                    this.isPlaying = false;
                    if (this.onPlaybackStopped) {
                        this.onPlaybackStopped();
                    }
                }
            };
            
            console.log('Playback iniciado');
        } catch (error) {
            console.error('Erro ao iniciar playback:', error);
            throw error;
        }
    }

    /**
     * Pausa o playback atual
     */
    pause() {
        if (this.currentSource && this.isPlaying) {
            this.currentSource.stop();
            this.pauseTime = this.getCurrentTime();
            this.isPlaying = false;
        }
    }

    /**
     * Retoma playback do ponto de pausa
     */
    resume() {
        if (this.currentSource && !this.isPlaying && this.pauseTime > 0) {
            this.play(this.currentSource.buffer, this.pauseTime);
        }
    }

    /**
     * Para o playback atual
     */
    stop() {
        if (this.currentSource) {
            try {
                this.currentSource.stop();
            } catch (e) {
                // Ignora erro se já estiver parado
            }
            this.currentSource = null;
        }
        this.isPlaying = false;
        this.pauseTime = 0;
    }

    /**
     * Seek para uma posição específica
     * @param {number} time - Tempo em segundos
     */
    seek(time) {
        if (this.isPlaying) {
            this.pause();
            setTimeout(() => {
                this.play(this.currentSource.buffer, time);
            }, 50);
        } else {
            this.pauseTime = time;
        }
    }

    /**
     * Obtém tempo atual de playback
     * @returns {number} Tempo em segundos
     */
    getCurrentTime() {
        if (!this.isPlaying) {
            return this.pauseTime;
        }
        return this.audioContext.currentTime - this.startTime + this.pauseTime;
    }

    /**
     * Obtém duração do buffer atual
     * @returns {number} Duração em segundos
     */
    getDuration() {
        if (this.currentSource && this.currentSource.buffer) {
            return this.currentSource.buffer.duration;
        }
        return 0;
    }

    /**
     * Inicia monitoramento do VU meter
     */
    startVUMonitor() {
        if (!this.analyser) return;

        const dataArray = new Uint8Array(this.analyser.frequencyBinCount);

        this.monitorInterval = setInterval(() => {
            if (this.isMonitoring || this.isRecording) {
                this.analyser.getByteTimeDomainData(dataArray);
                
                // Calcula RMS
                let sum = 0;
                for (let i = 0; i < dataArray.length; i++) {
                    const normalized = (dataArray[i] - 128) / 128;
                    sum += normalized * normalized;
                }
                const rms = Math.sqrt(sum / dataArray.length);
                
                // Normaliza para 0-1
                const normalizedRMS = Math.min(rms * 4, 1);
                
                if (this.onVolumeReceived) {
                    this.onVolumeReceived(normalizedRMS);
                }
            }
        }, 50);
    }

    /**
     * Para monitoramento do VU meter
     */
    stopVUMonitor() {
        if (this.monitorInterval) {
            clearInterval(this.monitorInterval);
            this.monitorInterval = null;
        }
    }

    /**
     * Decodifica um Blob de áudio para AudioBuffer
     * @param {Blob} blob - Blob de áudio
     * @returns {Promise<AudioBuffer>}
     */
    async decodeAudio(blob) {
        try {
            const arrayBuffer = await blob.arrayBuffer();
            return await this.audioContext.decodeAudioData(arrayBuffer);
        } catch (error) {
            console.error('Erro ao decodificar áudio:', error);
            throw error;
        }
    }

    /**
     * Converte AudioBuffer para Blob WAV
     * @param {AudioBuffer} buffer - Buffer de áudio
     * @returns {Blob} Blob WAV
     */
    bufferToWav(buffer) {
        const numChannels = buffer.numberOfChannels;
        const sampleRate = buffer.sampleRate;
        const format = 1; // PCM
        const bitDepth = 16;
        
        const bytesPerSample = bitDepth / 8;
        const blockAlign = numChannels * bytesPerSample;
        
        const data = [];
        for (let i = 0; i < buffer.length; i++) {
            for (let channel = 0; channel < numChannels; channel++) {
                const sample = buffer.getChannelData(channel)[i];
                const intSample = Math.max(-1, Math.min(1, sample));
                data.push(intSample < 0 ? intSample * 0x8000 : intSample * 0x7FFF);
            }
        }
        
        const dataLength = data.length * bytesPerSample;
        const buffer_size = 44 + dataLength;
        const arrayBuffer = new ArrayBuffer(buffer_size);
        const view = new DataView(arrayBuffer);
        
        // RIFF chunk descriptor
        this.writeString(view, 0, 'RIFF');
        view.setUint32(4, 36 + dataLength, true);
        this.writeString(view, 8, 'WAVE');
        
        // fmt sub-chunk
        this.writeString(view, 12, 'fmt ');
        view.setUint32(16, 16, true);
        view.setUint16(20, format, true);
        view.setUint16(22, numChannels, true);
        view.setUint32(24, sampleRate, true);
        view.setUint32(28, sampleRate * blockAlign, true);
        view.setUint16(32, blockAlign, true);
        view.setUint16(34, bitDepth, true);
        
        // data sub-chunk
        this.writeString(view, 36, 'data');
        view.setUint32(40, dataLength, true);
        
        // Escreve samples
        let offset = 44;
        for (let i = 0; i < data.length; i++) {
            view.setInt16(offset, data[i], true);
            offset += 2;
        }
        
        return new Blob([arrayBuffer], { type: 'audio/wav' });
    }

    writeString(view, offset, string) {
        for (let i = 0; i < string.length; i++) {
            view.setUint8(offset + i, string.charCodeAt(i));
        }
    }

    /**
     * Limpa recursos e libera memória
     */
    dispose() {
        this.stop();
        this.stopMonitoring();
        this.stopVUMonitor();
        
        if (this.audioContext) {
            this.audioContext.close();
            this.audioContext = null;
        }
        
        console.log('AudioService disposed');
    }
}

// Export para ambientes module
if (typeof module !== 'undefined' && module.exports) {
    module.exports = AudioService;
}
