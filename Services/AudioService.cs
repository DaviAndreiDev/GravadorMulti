using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;

namespace GravadorMulti.Services
{
    /// <summary>
    /// Serviço de áudio thread-safe para gravação e reprodução usando BASS.
    /// </summary>
    public class AudioService : IDisposable
    {
        // --- Estado ---
        private volatile bool _isDisposed;
        private volatile bool _isMonitoring;
        private volatile bool _isRecording;
        private volatile bool _isPlaying;
        private bool _bassInitialized;

        // --- Threads ---
        private Thread? _monitorThread;
        private int _selectedDeviceIndex;

        // --- Gravação ---
        private int _recordHandle;
        private string? _currentRecordingPath;
        private FileStream? _recordFileStream;
        private BinaryWriter? _recordWriter;
        private long _totalBytesGravados;
        private DateTime _lastVolumeUpdate = DateTime.MinValue;

        // --- Reprodução ---
        private int _playbackHandle;
        private string? _currentPlaybackPath;

        // --- Constantes ---
        private const int SAMPLE_RATE = 44100;

        // --- Eventos ---
        public event Action<float>? OnVolumeReceived;
        public event Action? OnRecordingStopped;
        public event Action? OnPlaybackStopped;

        // --- Hotplug ---
        private string _ultimoHashDispositivos = "";

        // --- Delegate Pinning (prevent GC collection of P/Invoke callbacks) ---
        private RecordProcedure? _recordProcedure;
        private SyncProcedure? _syncProcedure;

        public AudioService()
        {
            try
            {
                // Inicializa BASS para playback (device -1 = default output)
                if (!Bass.Init(-1, SAMPLE_RATE, DeviceInitFlags.Default))
                {
                    var error = Bass.LastError;
                    Console.WriteLine($"Aviso: Bass.Init falhou com erro {error}, tentando device 0...");
                    if (!Bass.Init(0, SAMPLE_RATE, DeviceInitFlags.Default))
                    {
                        Console.WriteLine($"ERRO: Bass.Init falhou completamente: {Bass.LastError}");
                        return; // Don't throw - allow app to run without audio
                    }
                }
                _bassInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERRO FATAL AUDIO: {ex.Message}");
                // Don't throw - let the app run but audio won't work
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  DISPOSITIVOS DE ENTRADA
        // ═══════════════════════════════════════════════════════════

        public List<string> ObterDispositivosEntrada()
        {
            var lista = new List<string>();

            try
            {
                for (int i = 0; Bass.RecordGetDeviceInfo(i, out var info); i++)
                {
                    if (info.IsEnabled)
                        lista.Add(info.Name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao listar dispositivos: {ex.Message}");
            }

            if (lista.Count == 0)
                lista.Add("Microfone Padrão");

            return lista;
        }

        // ═══════════════════════════════════════════════════════════
        //  MONITORAMENTO DE VOLUME (VU METER)
        // ═══════════════════════════════════════════════════════════

        private int _monitorHandle = 0;
        private RecordProcedure? _monitorProcedure;

        public void IniciarMonitoramento(int deviceIndex)
        {
            if (_isDisposed || !_bassInitialized) return;

            if (_isRecording || _isMonitoring)
                PararMonitoramento();

            _selectedDeviceIndex = deviceIndex;
            
            try
            {
                if (!Bass.RecordInit(_selectedDeviceIndex))
                {
                    Console.WriteLine($"Erro ao iniciar dispositivo de monitoramento: {Bass.LastError}");
                    return;
                }

                _monitorProcedure = MonitorProc;
                _monitorHandle = Bass.RecordStart(SAMPLE_RATE, 1, BassFlags.Default, 50, _monitorProcedure);
                
                if (_monitorHandle == 0)
                {
                    Console.WriteLine($"Erro ao iniciar monitoramento: {Bass.LastError}");
                    Bass.RecordFree();
                    return;
                }

                _isMonitoring = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro fatal no monitoramento: {ex.Message}");
            }
        }

        private bool MonitorProc(int Handle, IntPtr Buffer, int Length, IntPtr User)
        {
            if (!_isMonitoring || _isRecording || _isDisposed) return false;

            try
            {
                byte[] data = new byte[Length];
                System.Runtime.InteropServices.Marshal.Copy(Buffer, data, 0, Length);

                float maxVol = 0;
                for (int i = 0; i < Length - 1; i += 2)
                {
                    short val = (short)((data[i + 1] << 8) | data[i]);
                    float norm = Math.Abs(val) / 32768f;
                    if (norm > maxVol) maxVol = norm;
                }
                
                SafeInvokeVolume(maxVol);
            }
            catch
            {
                // Ignorar erros pequenos de marshal/array
            }

            return true;
        }



        private void SafeInvokeVolume(float volume)
        {
            try
            {
                OnVolumeReceived?.Invoke(volume);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no callback de volume: {ex.Message}");
            }
        }

        public void PararMonitoramento()
        {
            _isMonitoring = false;

            if (_monitorHandle != 0)
            {
                try { Bass.ChannelStop(_monitorHandle); } catch { }
                _monitorHandle = 0;
            }
            
            try { Bass.RecordFree(); } catch { }
        }

        public void ReiniciarMonitoramentoSeParado(int deviceIndex)
        {
            if (!_isMonitoring && !_isRecording && !_isDisposed)
                IniciarMonitoramento(deviceIndex);
        }

        // ═══════════════════════════════════════════════════════════
        //  GRAVAÇÃO
        // ═══════════════════════════════════════════════════════════

        public void IniciarGravacao(string caminhoArquivo, int deviceIndex)
        {
            if (_isDisposed || !_bassInitialized) return;
            if (_isRecording) throw new InvalidOperationException("Já existe uma gravação em andamento.");

            PararMonitoramento();

            // Valida caminho do arquivo
            var diretorio = Path.GetDirectoryName(caminhoArquivo);
            if (!string.IsNullOrEmpty(diretorio) && !Directory.Exists(diretorio))
                Directory.CreateDirectory(diretorio);

            _currentRecordingPath = caminhoArquivo;
            _totalBytesGravados = 0;

            try
            {
                _recordFileStream = new FileStream(caminhoArquivo, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.SequentialScan);
                _recordWriter = new BinaryWriter(_recordFileStream);
                EscreverCabecalhoWavVazio(_recordWriter);
            }
            catch (Exception ex)
            {
                _recordFileStream?.Dispose();
                _recordWriter = null;
                _recordFileStream = null;
                throw new IOException("Falha ao criar arquivo de gravação.", ex);
            }

            if (!Bass.RecordInit(deviceIndex))
                throw new InvalidOperationException($"Falha ao inicializar gravação: {Bass.LastError}");

            _recordProcedure = RecordProc; // Pin delegate to prevent GC
            _recordHandle = Bass.RecordStart(SAMPLE_RATE, 1, BassFlags.Default, 50, _recordProcedure);
            if (_recordHandle == 0)
            {
                Bass.RecordFree();
                _recordWriter?.Dispose();
                _recordFileStream?.Dispose();
                throw new InvalidOperationException($"Falha ao iniciar gravação: {Bass.LastError}");
            }

            _isRecording = true;
        }

        private bool RecordProc(int Handle, IntPtr Buffer, int Length, IntPtr User)
        {
            if (!_isRecording || _recordWriter == null) return false;

            try
            {
                byte[] data = new byte[Length];
                System.Runtime.InteropServices.Marshal.Copy(Buffer, data, 0, Length);

                _recordWriter.Write(data, 0, Length);
                _totalBytesGravados += Length;

                // VU meter a cada 50ms
                if (DateTime.Now - _lastVolumeUpdate > TimeSpan.FromMilliseconds(50))
                {
                    float maxVol = 0;
                    for (int i = 0; i < Length - 1; i += 2)
                    {
                        short val = (short)((data[i + 1] << 8) | data[i]);
                        float norm = Math.Abs(val) / 32768f;
                        if (norm > maxVol) maxVol = norm;
                    }
                    SafeInvokeVolume(maxVol);
                    _lastVolumeUpdate = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro durante gravação: {ex.Message}");
            }

            return true; // Continue recording
        }

        public void PararGravacao()
        {
            if (!_isRecording) return;

            _isRecording = false;

            try
            {
                if (_recordHandle != 0)
                {
                    Bass.ChannelStop(_recordHandle);
                    _recordHandle = 0;
                }
                Bass.RecordFree();

                // Finaliza WAV
                if (_recordWriter != null)
                {
                    _recordWriter.Flush();
                    AtualizarTamanhoCabecalhoWav(_recordWriter, _totalBytesGravados);
                    _recordWriter.Dispose();
                    _recordWriter = null;
                }
                _recordFileStream?.Dispose();
                _recordFileStream = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao parar gravação: {ex.Message}");
            }

            SafeInvokeRecordingStopped();
        }

        private void SafeInvokeRecordingStopped()
        {
            try { OnRecordingStopped?.Invoke(); }
            catch (Exception ex) { Console.WriteLine($"Erro no callback de gravação: {ex.Message}"); }
        }

        // ═══════════════════════════════════════════════════════════
        //  REPRODUÇÃO
        // ═══════════════════════════════════════════════════════════

        public void TocarAudio(string caminhoArquivo)
        {
            if (_isDisposed || !_bassInitialized) return;
            if (!File.Exists(caminhoArquivo)) return;

            // If already have a stream for this file, just resume/restart
            if (_playbackHandle != 0 && _currentPlaybackPath == caminhoArquivo)
            {
                var state = Bass.ChannelIsActive(_playbackHandle);
                if (state == PlaybackState.Paused)
                {
                    Bass.ChannelPlay(_playbackHandle, false);
                    _isPlaying = true;
                    return;
                }
                if (state == PlaybackState.Stopped || state == PlaybackState.Stalled)
                {
                    Bass.ChannelSetPosition(_playbackHandle, 0L);
                    Bass.ChannelPlay(_playbackHandle, false);
                    _isPlaying = true;
                    return;
                }
                if (state == PlaybackState.Playing)
                    return; // already playing
            }

            // Different file or no handle — create new stream
            FreePlaybackStream();

            _playbackHandle = Bass.CreateStream(caminhoArquivo, 0, 0, BassFlags.Default);
            if (_playbackHandle == 0)
            {
                Console.WriteLine($"Falha ao criar stream: {Bass.LastError}");
                return;
            }

            _currentPlaybackPath = caminhoArquivo;

            // Pin the sync callback to prevent GC collection
            _syncProcedure = OnPlaybackEndSync;
            Bass.ChannelSetSync(_playbackHandle, SyncFlags.End, 0, _syncProcedure);

            Bass.ChannelPlay(_playbackHandle);
            _isPlaying = true;
        }

        /// <summary>
        /// Carrega o áudio e deixa pausado (para scrubbing silencioso).
        /// </summary>
        public void PrepararAudioParaScrub(string caminhoArquivo)
        {
            if (_isDisposed || !_bassInitialized) return;
            if (!File.Exists(caminhoArquivo)) return;

            // If we already have this file loaded, just pause it
            if (_playbackHandle != 0 && _currentPlaybackPath == caminhoArquivo)
            {
                var state = Bass.ChannelIsActive(_playbackHandle);
                if (state == PlaybackState.Playing)
                    Bass.ChannelPause(_playbackHandle);
                return;
            }

            // Different file — create new stream (don't play)
            FreePlaybackStream();

            _playbackHandle = Bass.CreateStream(caminhoArquivo, 0, 0, BassFlags.Default);
            if (_playbackHandle == 0)
            {
                Console.WriteLine($"Falha ao criar stream: {Bass.LastError}");
                return;
            }

            _currentPlaybackPath = caminhoArquivo;

            _syncProcedure = OnPlaybackEndSync;
            Bass.ChannelSetSync(_playbackHandle, SyncFlags.End, 0, _syncProcedure);
            // Stream created but NOT playing — ready for seek
        }

        private void OnPlaybackEndSync(int Handle, int Channel, int Data, IntPtr User)
        {
            _isPlaying = false;
            SafeInvokePlaybackStopped();
        }

        public void PausarReproducao()
        {
            if (_isDisposed || _playbackHandle == 0) return;
            Bass.ChannelPause(_playbackHandle);
            _isPlaying = false;
        }

        public void PararReproducao()
        {
            if (_isDisposed) return;
            FreePlaybackStream();
            _isPlaying = false;
            SafeInvokePlaybackStopped();
        }

        private void FreePlaybackStream()
        {
            if (_playbackHandle != 0)
            {
                try
                {
                    Bass.ChannelStop(_playbackHandle);
                    Bass.StreamFree(_playbackHandle);
                }
                catch { }
                _playbackHandle = 0;
                _currentPlaybackPath = null;
            }
        }

        private void SafeInvokePlaybackStopped()
        {
            try { OnPlaybackStopped?.Invoke(); }
            catch (Exception ex) { Console.WriteLine($"Erro no callback de playback: {ex.Message}"); }
        }

        // ═══════════════════════════════════════════════════════════
        //  SEEKING / SCRUBBING
        // ═══════════════════════════════════════════════════════════

        public void DefinirTempo(double segundos, bool forcePlay = false)
        {
            if (_isDisposed || _playbackHandle == 0) return;

            long bytePos = Bass.ChannelSeconds2Bytes(_playbackHandle, segundos);
            Bass.ChannelSetPosition(_playbackHandle, bytePos);

            if (forcePlay)
            {
                var state = Bass.ChannelIsActive(_playbackHandle);
                if (state != PlaybackState.Playing)
                {
                    Bass.ChannelPlay(_playbackHandle, false);
                    _isPlaying = true;
                }
            }
        }

        /// <summary>
        /// Scrub mode flag — kept for API compat, but ManagedBass doesn't need it.
        /// </summary>
        public void SetScrubMode(bool mode) { /* no-op for BASS */ }

        public TimeSpan GetPosition()
        {
            if (_isDisposed || _playbackHandle == 0) return TimeSpan.Zero;

            try
            {
                long bytePos = Bass.ChannelGetPosition(_playbackHandle);
                double seconds = Bass.ChannelBytes2Seconds(_playbackHandle, bytePos);
                return TimeSpan.FromSeconds(seconds);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        public TimeSpan GetTotalTime()
        {
            if (_isDisposed || _playbackHandle == 0) return TimeSpan.Zero;

            try
            {
                long byteLen = Bass.ChannelGetLength(_playbackHandle);
                double seconds = Bass.ChannelBytes2Seconds(_playbackHandle, byteLen);
                return TimeSpan.FromSeconds(seconds);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  MIXAGEM / EXPORTAÇÃO
        // ═══════════════════════════════════════════════════════════

        public void ExportarMixagem(List<string> arquivos, string saida)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AudioService));
            if (arquivos == null || arquivos.Count == 0)
                throw new ArgumentException("Lista de arquivos não pode ser vazia.");

            try
            {
                var diretorio = Path.GetDirectoryName(saida);
                if (!string.IsNullOrEmpty(diretorio) && !Directory.Exists(diretorio))
                    Directory.CreateDirectory(diretorio);

                ConcatenarWavFiles(arquivos, saida);
            }
            catch (Exception ex)
            {
                throw new IOException("Falha ao exportar mixagem.", ex);
            }
        }

        private void ConcatenarWavFiles(List<string> arquivos, string saida)
        {
            byte[] header = new byte[44];
            using (var fs = new FileStream(arquivos[0], FileMode.Open, FileAccess.Read))
                fs.ReadExactly(header, 0, 44);

            short numChannels = BitConverter.ToInt16(header, 22);
            int sampleRate = BitConverter.ToInt32(header, 24);
            short bitsPerSample = BitConverter.ToInt16(header, 34);

            long totalDataSize = 0;
            foreach (var arquivo in arquivos)
            {
                var fi = new FileInfo(arquivo);
                if (fi.Exists)
                    totalDataSize += fi.Length - 44;
            }

            using var outFs = new FileStream(saida, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(outFs);

            EscreverCabecalhoWavConcatenado(writer, totalDataSize, numChannels, sampleRate, bitsPerSample);

            byte[] buffer = new byte[4096];
            foreach (var arquivo in arquivos)
            {
                using var inFs = new FileStream(arquivo, FileMode.Open, FileAccess.Read);
                inFs.Seek(44, SeekOrigin.Begin);
                int bytesRead;
                while ((bytesRead = inFs.Read(buffer, 0, buffer.Length)) > 0)
                    writer.Write(buffer, 0, bytesRead);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  CROCKING / TRIMMING
        // ═══════════════════════════════════════════════════════════

        public bool CortarAudio(string caminhoArquivo, string outputPath, double startProgress, double endProgress)
        {
            if (string.IsNullOrEmpty(caminhoArquivo) || !File.Exists(caminhoArquivo))
                return false;

            try
            {
                // Ensure audio is stopped and free handles before modifying
                if (_currentPlaybackPath == caminhoArquivo)
                {
                    FreePlaybackStream();
                }

                byte[] fileBytes = File.ReadAllBytes(caminhoArquivo);
                
                // Very basic WAV validation
                if (fileBytes.Length < 44) return false;

                int sampleRate = BitConverter.ToInt32(fileBytes, 24);
                int byteRate = BitConverter.ToInt32(fileBytes, 28);
                short blockAlign = BitConverter.ToInt16(fileBytes, 32);

                int dataSize = BitConverter.ToInt32(fileBytes, 40);
                
                // Real data starts at 44
                int startByte = (int)(dataSize * startProgress);
                int endByte = (int)(dataSize * endProgress);

                // Align to sample blocks (16-bit mono = 2 bytes per block)
                startByte -= startByte % blockAlign;
                endByte -= endByte % blockAlign;

                if (startByte >= endByte || startByte >= dataSize) return false;

                int newDataSize = endByte - startByte;
                byte[] newAudioData = new byte[newDataSize];
                
                Buffer.BlockCopy(fileBytes, 44 + startByte, newAudioData, 0, newDataSize);

                using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs))
                {
                    // Escreve header vazio
                    EscreverCabecalhoWavVazio(writer);
                    
                    // Ajusta pra valores reais do frame capturado
                    writer.Seek(24, SeekOrigin.Begin);
                    writer.Write(sampleRate);
                    writer.Write(byteRate);
                    
                    // Vai pro final do cabeçalho
                    writer.Seek(44, SeekOrigin.Begin);
                    writer.Write(newAudioData);

                    // Atualiza o RIFF e DATA chunks
                    AtualizarTamanhoCabecalhoWav(writer, newDataSize);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao cortar áudio: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  WAV HELPERS
        // ═══════════════════════════════════════════════════════════

        private void EscreverCabecalhoWavVazio(BinaryWriter bw)
        {
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(0);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(SAMPLE_RATE);
            bw.Write(SAMPLE_RATE * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(0);
        }

        private void AtualizarTamanhoCabecalhoWav(BinaryWriter bw, long totalBytesAudio)
        {
            bw.Seek(4, SeekOrigin.Begin);
            bw.Write((int)(36 + totalBytesAudio));
            bw.Seek(40, SeekOrigin.Begin);
            bw.Write((int)totalBytesAudio);
        }

        private void EscreverCabecalhoWavConcatenado(BinaryWriter writer, long dataSize, short numChannels, int sampleRate, short bitsPerSample)
        {
            int byteRate = sampleRate * numChannels * (bitsPerSample / 8);
            short blockAlign = (short)(numChannels * (bitsPerSample / 8));

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write((int)(36 + dataSize));
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(numChannels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write((int)dataSize);
        }

        // ═══════════════════════════════════════════════════════════
        //  HOTPLUG
        // ═══════════════════════════════════════════════════════════

        public bool HouveAlteracaoDeDispositivos()
        {
            try
            {
                var atuais = ObterDispositivosEntrada();
                string assinaturaAtual = string.Join("|", atuais);

                if (assinaturaAtual != _ultimoHashDispositivos)
                {
                    _ultimoHashDispositivos = assinaturaAtual;
                    return true;
                }
            }
            catch { }
            return false;
        }

        public void ReiniciarSistemaDeSaida()
        {
            if (_isRecording || _isDisposed) return;

            try
            {
                FreePlaybackStream();
                Bass.Free();
                if (!Bass.Init(-1, SAMPLE_RATE, DeviceInitFlags.Default))
                    Console.WriteLine($"Erro ao reiniciar BASS: {Bass.LastError}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao reiniciar sistema de saída: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  UTILIDADES
        // ═══════════════════════════════════════════════════════════

        public void PararTudo()
        {
            PararGravacao();
            PararReproducao();
            PararMonitoramento();
        }

        // ═══════════════════════════════════════════════════════════
        //  DISPOSE
        // ═══════════════════════════════════════════════════════════

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try { PararTudo(); } catch { }

            if (_bassInitialized)
            {
                try { Bass.Free(); } catch { }
                _bassInitialized = false;
            }

            GC.SuppressFinalize(this);
        }

        ~AudioService()
        {
            try { Dispose(); } catch { }
        }
    }
}
