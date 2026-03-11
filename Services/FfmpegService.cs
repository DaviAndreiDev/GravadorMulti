using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace GravadorMulti.Services
{
    /// <summary>
    /// Serviço para conversão de áudio usando FFmpeg.
    /// Suporta múltiplos formatos de exportação além do WAV nativo.
    /// </summary>
    public class FfmpegService
    {
        private string? _ffmpegPath;
        private bool _isAvailable;
        private static bool _downloadAttempted;

        /// <summary>
        /// Pasta padrão onde o FFmpeg será baixado/armazenado para este app.
        /// </summary>
        private static string PastaFfmpegLocal =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GravadorMulti", "ffmpeg");

        public FfmpegService()
        {
            _isAvailable = TryFindFfmpeg();
        }

        /// <summary>
        /// Verifica se o FFmpeg está disponível no sistema
        /// </summary>
        public bool IsAvailable => _isAvailable;

        /// <summary>
        /// Garante que o FFmpeg esteja disponível, baixando-o se necessário.
        /// Deve ser chamado antes de usar qualquer funcionalidade que dependa do FFmpeg.
        /// </summary>
        public async Task GarantirDisponibilidadeAsync()
        {
            if (_isAvailable) return;

            if (_downloadAttempted) return; // Evita tentar baixar várias vezes na mesma sessão
            _downloadAttempted = true;

            try
            {
                var destino = PastaFfmpegLocal;
                Directory.CreateDirectory(destino);

                Console.WriteLine($"[FfmpegService] Baixando FFmpeg para: {destino}");
                await Xabe.FFmpeg.Downloader.FFmpegDownloader.GetLatestVersion(
                    Xabe.FFmpeg.Downloader.FFmpegVersion.Official, destino);
                Console.WriteLine("[FfmpegService] Download do FFmpeg concluído.");

                // Tenta encontrar novamente após o download
                _isAvailable = TryFindFfmpeg();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FfmpegService] Falha ao baixar FFmpeg: {ex.Message}");
            }
        }

        /// <summary>
        /// Tenta encontrar o executável do FFmpeg no sistema
        /// </summary>
        private bool TryFindFfmpeg()
        {
            // 1. Tenta encontrar no PATH
            try
            {
                var process = new Process();
                process.StartInfo.FileName = "ffmpeg";
                process.StartInfo.Arguments = "-version";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                
                if (process.Start())
                {
                    process.WaitForExit(2000);
                    if (process.ExitCode == 0)
                    {
                        _ffmpegPath = "ffmpeg";
                        return true;
                    }
                }
            }
            catch { }

            // 2. Tenta caminhos locais do app e caminhos comuns no Windows
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] commonPaths = new[]
            {
                // Pasta local do app (onde o Xabe.FFmpeg.Downloader salva)
                Path.Combine(PastaFfmpegLocal, "ffmpeg.exe"),
                Path.Combine(PastaFfmpegLocal, "ffmpeg"),
                // Pasta do executável
                Path.Combine(appDir, "ffmpeg.exe"),
                Path.Combine(appDir, "ffmpeg"),
                // Caminhos comuns do Windows
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\ffmpeg\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFmpeg", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FFmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FFmpeg", "bin", "ffmpeg.exe"),
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    _ffmpegPath = path;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Define o caminho manual para o FFmpeg
        /// </summary>
        public void SetFfmpegPath(string path)
        {
            if (File.Exists(path))
            {
                _ffmpegPath = path;
                _isAvailable = true;
            }
            else
            {
                throw new FileNotFoundException("FFmpeg não encontrado no caminho especificado.", path);
            }
        }

        /// <summary>
        /// Exporta uma lista de arquivos WAV concatenados para um formato específico
        /// </summary>
        public async Task ExportarAsync(List<string> arquivosWav, string saida, ExportFormat formato, int bitrate = 192)
        {
            if (!_isAvailable)
                throw new InvalidOperationException("FFmpeg não está disponível. Por favor, instale o FFmpeg ou especifique o caminho manualmente.");

            if (arquivosWav == null || arquivosWav.Count == 0)
                throw new ArgumentException("Lista de arquivos não pode ser vazia.");

            // Cria arquivo temporário com lista de arquivos para concatenação
            var listaTemp = Path.GetTempFileName() + ".txt";
            try
            {
                // Escreve a lista de arquivos no formato esperado pelo FFmpeg
                var linhas = new List<string>();
                foreach (var arquivo in arquivosWav)
                {
                    if (File.Exists(arquivo))
                    {
                        // Escape para caminhos com espaços e caracteres especiais
                        linhas.Add($"file '{arquivo.Replace("'", "'\\''")}'");
                    }
                }
                await File.WriteAllLinesAsync(listaTemp, linhas);

                // Prepara os argumentos do FFmpeg baseados no formato
                string args = PrepararArgumentos(listaTemp, saida, formato, bitrate);

                // Executa o FFmpeg
                await ExecutarFfmpegAsync(args);
            }
            finally
            {
                // Limpa arquivo temporário
                try { File.Delete(listaTemp); } catch { }
            }
        }

        /// <summary>
        /// Converte um único arquivo para outro formato
        /// </summary>
        public async Task ConverterAsync(string entrada, string saida, ExportFormat formato, int bitrate = 192)
        {
            if (!_isAvailable)
                throw new InvalidOperationException("FFmpeg não está disponível.");

            string args = formato switch
            {
                ExportFormat.Mp3 => $"-i \"{entrada}\" -codec:a libmp3lame -b:a {bitrate}k -y \"{saida}\"",
                ExportFormat.Aac => $"-i \"{entrada}\" -codec:a aac -b:a {bitrate}k -y \"{saida}\"",
                ExportFormat.Flac => $"-i \"{entrada}\" -codec:a flac -y \"{saida}\"",
                ExportFormat.Ogg => $"-i \"{entrada}\" -codec:a libvorbis -q:a 4 -y \"{saida}\"",
                ExportFormat.Wav => $"-i \"{entrada}\" -codec:a pcm_s16le -y \"{saida}\"",
                _ => throw new NotSupportedException($"Formato não suportado: {formato}")
            };

            await ExecutarFfmpegAsync(args);
        }

        /// <summary>
        /// Prepara os argumentos do FFmpeg para concatenação e conversão
        /// </summary>
        private string PrepararArgumentos(string listaArquivos, string saida, ExportFormat formato, int bitrate)
        {
            string codecArgs = formato switch
            {
                ExportFormat.Mp3 => $"-codec:a libmp3lame -b:a {bitrate}k",
                ExportFormat.Aac => $"-codec:a aac -b:a {bitrate}k",
                ExportFormat.Flac => "-codec:a flac",
                ExportFormat.Ogg => "-codec:a libvorbis -q:a 4",
                ExportFormat.Wav => "-codec:a pcm_s16le",
                _ => throw new NotSupportedException($"Formato não suportado: {formato}")
            };

            // Usa o demuxer concat para concatenar arquivos
            return $"-f concat -safe 0 -i \"{listaArquivos}\" {codecArgs} -y \"{saida}\"";
        }

        /// <summary>
        /// Executa o FFmpeg com os argumentos especificados
        /// </summary>
        private async Task ExecutarFfmpegAsync(string args)
        {
            var process = new Process();
            process.StartInfo.FileName = _ffmpegPath;
            process.StartInfo.Arguments = args;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            var tcs = new TaskCompletionSource<bool>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) => tcs.TrySetResult(process.ExitCode == 0);

            process.Start();
            
            // Lê saída de erro (onde o FFmpeg geralmente escreve)
            string error = await process.StandardError.ReadToEndAsync();
            
            await tcs.Task;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg falhou com código {process.ExitCode}: {error}");
            }
        }

        /// <summary>
        /// Remove silêncios de um arquivo de áudio usando o FFmpeg e parâmetros dinâmicos.
        /// </summary>
        public async Task<string> RemoverSilencioAsync(string filePath, string tempFileDest, double durationRequerida, int limiteThresholdDb)
        {
            if (!_isAvailable)
                throw new InvalidOperationException("FFmpeg não está disponível.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Arquivo de áudio não encontrado.", filePath);

            try
            {
                string durStr = durationRequerida.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string args = $"-i \"{filePath}\" -af \"silenceremove=stop_periods=-1:stop_duration={durStr}:stop_threshold={limiteThresholdDb}dB\" -y \"{tempFileDest}\"";

                await ExecutarFfmpegAsync(args);

                return tempFileDest;
            }
            catch
            {
                if (File.Exists(tempFileDest))
                {
                    try { File.Delete(tempFileDest); } catch { }
                }
                throw;
            }
        }

        /// <summary>
        /// Retorna a extensão de arquivo para o formato especificado
        /// </summary>
        public static string GetExtension(ExportFormat formato)
        {
            return formato switch
            {
                ExportFormat.Mp3 => ".mp3",
                ExportFormat.Aac => ".aac",
                ExportFormat.Flac => ".flac",
                ExportFormat.Ogg => ".ogg",
                ExportFormat.Wav => ".wav",
                _ => ".wav"
            };
        }
    }

    /// <summary>
    /// Formatos de exportação suportados
    /// </summary>
    public enum ExportFormat
    {
        Wav,
        Mp3,
        Aac,
        Flac,
        Ogg
    }
}
