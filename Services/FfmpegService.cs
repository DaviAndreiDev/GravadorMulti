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

        public FfmpegService()
        {
            _isAvailable = TryFindFfmpeg();
        }

        /// <summary>
        /// Verifica se o FFmpeg está disponível no sistema
        /// </summary>
        public bool IsAvailable => _isAvailable;

        /// <summary>
        /// Tenta encontrar o executável do FFmpeg no sistema
        /// </summary>
        private bool TryFindFfmpeg()
        {
            // Tenta encontrar no PATH
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

            // Tenta caminhos comuns no Windows
            string[] commonPaths = new[]
            {
                @"C:fmpeginfmpeg.exe",
                @"C:fmpegfmpeg.exe",
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
