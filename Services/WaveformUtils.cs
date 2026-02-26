using Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GravadorMulti.Services
{
    /// <summary>
    /// Utilitários para geração de waveforms visuais a partir de dados de áudio.
    /// Thread-safe e otimizado para streaming de arquivos grandes.
    /// </summary>
    public static class WaveformUtils
    {
        // Tamanho máximo de arquivo para carregar em memória (10MB)
        private const long MAX_MEMORY_FILE_SIZE = 10 * 1024 * 1024;
        // Tamanho do buffer para streaming
        private const int STREAM_BUFFER_SIZE = 8192;
        // Mínimo de pontos para uma waveform válida
        private const int MIN_POINTS = 2;

        /// <summary>
        /// Gera pontos de waveform a partir de um arquivo WAV.
        /// Usa streaming para arquivos grandes para evitar OutOfMemoryException.
        /// </summary>
        /// <param name="path">Caminho do arquivo WAV</param>
        /// <param name="width">Largura do controle visual em pixels</param>
        /// <param name="height">Altura do controle visual em pixels</param>
        /// <returns>Lista de pontos para desenho do polígono</returns>
        /// <exception cref="ArgumentException">Parâmetros inválidos</exception>
        /// <exception cref="FileNotFoundException">Arquivo não existe</exception>
        /// <exception cref="InvalidDataException">Formato de arquivo inválido</exception>
        public static List<Point> GerarPontosDoArquivo(string path, double width, double height)
        {
            ValidarParametros(path, width, height);

            // Para arquivos pequenos, usa método em memória (mais rápido)
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length <= MAX_MEMORY_FILE_SIZE)
            {
                return GerarPontosEmMemoria(path, width, height);
            }

            // Para arquivos grandes, usa streaming
            return GerarPontosStreaming(path, width, height);
        }

        private static void ValidarParametros(string path, double width, double height)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Caminho do arquivo não pode ser vazio.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("Arquivo de áudio não encontrado.", path);

            if (width <= 0)
                throw new ArgumentException("Largura deve ser maior que zero.", nameof(width));

            if (height <= 0)
                throw new ArgumentException("Altura deve ser maior que zero.", nameof(height));

            if (width > 10000 || height > 10000)
                throw new ArgumentException("Dimensões excessivamente grandes.");
        }

        /// <summary>
        /// Versão otimizada para arquivos pequenos (carrega tudo em memória).
        /// </summary>
        private static List<Point> GerarPontosEmMemoria(string path, double width, double height)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException("Sem permissão para ler o arquivo.", ex);
            }
            catch (IOException ex)
            {
                throw new IOException("Erro ao ler arquivo de áudio.", ex);
            }

            // Valida cabeçalho WAV mínimo (44 bytes)
            if (bytes.Length < 44)
                throw new InvalidDataException("Arquivo WAV muito pequeno ou corrompido.");

            // Valida assinatura RIFF/WAVE
            if (!ValidarCabecalhoWav(bytes))
                throw new InvalidDataException("Arquivo não é um WAV válido.");

            int numSamples = (bytes.Length - 44) / 2;
            if (numSamples == 0)
                return CriarWaveformVazia(width, height);

            // Extrai amostras (16-bit mono)
            short[] samples = new short[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = BitConverter.ToInt16(bytes, 44 + i * 2);
            }

            return CalcularPontosWaveform(samples, width, height);
        }

        /// <summary>
        /// Versão com streaming para arquivos grandes.
        /// </summary>
        private static List<Point> GerarPontosStreaming(string path, double width, double height)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, STREAM_BUFFER_SIZE, FileOptions.SequentialScan);
            using var reader = new BinaryReader(fs);

            // Lê e valida cabeçalho
            var header = reader.ReadBytes(44);
            if (header.Length < 44)
                throw new InvalidDataException("Arquivo WAV muito pequeno.");

            if (!ValidarCabecalhoWav(header))
                throw new InvalidDataException("Arquivo não é um WAV válido.");

            // Calcula número total de amostras
            long fileLength = fs.Length;
            long numSamples = (fileLength - 44) / 2;

            if (numSamples == 0)
                return CriarWaveformVazia(width, height);

            // Calcula quantas amostras por pixel
            int samplesPerPixel = (int)Math.Max(1, numSamples / (long)width);
            int numPixels = (int)Math.Min(width, numSamples);

            var points = new List<Point>((numPixels + 2) * 2);
            double centroY = height / 2;

            // Adiciona ponto inicial
            points.Add(new Point(0, centroY));

            // Buffer para leitura
            byte[] sampleBuffer = new byte[STREAM_BUFFER_SIZE];
            int bufferOffset = 0;
            long currentSample = 0;
            float maxValInPixel = 0;
            int samplesInCurrentPixel = 0;
            int currentPixel = 0;

            // Processa arquivo em chunks
            int bytesRead;
            while ((bytesRead = reader.Read(sampleBuffer, bufferOffset, sampleBuffer.Length - bufferOffset)) > 0)
            {
                bytesRead += bufferOffset;
                int samplesInBuffer = bytesRead / 2;

                for (int i = 0; i < samplesInBuffer; i++)
                {
                    short sample = BitConverter.ToInt16(sampleBuffer, i * 2);
                    float normalized = Math.Abs(sample) / 32768f;
                    maxValInPixel = Math.Max(maxValInPixel, normalized);
                    samplesInCurrentPixel++;
                    currentSample++;

                    // Quando acumulou amostras suficientes para um pixel
                    if (samplesInCurrentPixel >= samplesPerPixel && currentPixel < numPixels)
                    {
                        double x = currentPixel;
                        double y = centroY - (maxValInPixel * centroY * 0.9);
                        points.Add(new Point(x, y));

                        currentPixel++;
                        samplesInCurrentPixel = 0;
                        maxValInPixel = 0;
                    }
                }

                // Copia bytes restantes para início do buffer
                int remainingBytes = bytesRead % 2;
                if (remainingBytes > 0)
                {
                    sampleBuffer[0] = sampleBuffer[bytesRead - 1];
                    bufferOffset = 1;
                }
                else
                {
                    bufferOffset = 0;
                }
            }

            // Adiciona último ponto se necessário
            if (samplesInCurrentPixel > 0 && currentPixel < numPixels)
            {
                double x = currentPixel;
                double y = centroY - (maxValInPixel * centroY * 0.9);
                points.Add(new Point(x, y));
            }

            // Parte inferior (espelho)
            int upperCount = points.Count;
            for (int i = upperCount - 1; i >= 0; i--)
            {
                var p = points[i];
                double y = centroY + ((centroY - p.Y)); // Espelha em relação ao centro
                points.Add(new Point(p.X, y));
            }

            // Fecha o polígono
            points.Add(new Point(0, centroY));

            return points;
        }

        private static bool ValidarCabecalhoWav(byte[] header)
        {
            if (header.Length < 12) return false;

            // Verifica "RIFF"
            if (header[0] != 'R' || header[1] != 'I' || header[2] != 'F' || header[3] != 'F')
                return false;

            // Verifica "WAVE"
            if (header[8] != 'W' || header[9] != 'A' || header[10] != 'V' || header[11] != 'E')
                return false;

            return true;
        }

        private static List<Point> CalcularPontosWaveform(short[] samples, double width, double height)
        {
            var points = new List<Point>();
            int numSamples = samples.Length;

            if (numSamples < MIN_POINTS)
                return CriarWaveformVazia(width, height);

            // Calcula step para downsampling
            int numPixels = (int)Math.Min(width, numSamples);
            int samplesPerPixel = (int)Math.Ceiling((double)numSamples / numPixels);

            double centroY = height / 2;

            // Parte superior
            points.Add(new Point(0, centroY));

            for (int pixel = 0; pixel < numPixels; pixel++)
            {
                int startSample = pixel * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, numSamples);

                // Encontra pico absoluto neste range
                short peak = 0;
                for (int i = startSample; i < endSample; i++)
                {
                    if (Math.Abs(samples[i]) > Math.Abs(peak))
                        peak = samples[i];
                }

                double normalized = Math.Abs(peak) / 32768f;
                double y = centroY - (normalized * centroY * 0.9);
                double x = (pixel / (double)(numPixels - 1)) * width;

                points.Add(new Point(x, Math.Max(0, Math.Min(height, y))));
            }

            // Parte inferior (espelho)
            for (int i = points.Count - 1; i >= 0; i--)
            {
                var p = points[i];
                double y = centroY + ((centroY - p.Y));
                points.Add(new Point(p.X, Math.Max(0, Math.Min(height, y))));
            }

            // Fecha o polígono
            points.Add(new Point(0, centroY));

            return points;
        }

        private static List<Point> CriarWaveformVazia(double width, double height)
        {
            double centroY = height / 2;
            return new List<Point>
            {
                new Point(0, centroY),
                new Point(width, centroY),
                new Point(width, centroY),
                new Point(0, centroY)
            };
        }

        /// <summary>
        /// Converte lista de volumes (0.0 a 1.0) em pontos de waveform.
        /// Otimizado para visualização em tempo real durante gravação.
        /// </summary>
        public static List<Point> ConverterVolumesParaPontos(List<float> volumes, double width, double height)
        {
            if (volumes == null)
                throw new ArgumentNullException(nameof(volumes));

            if (width <= 0)
                throw new ArgumentException("Largura deve ser maior que zero.", nameof(width));

            if (height <= 0)
                throw new ArgumentException("Altura deve ser maior que zero.", nameof(height));

            if (volumes.Count == 0)
                return CriarWaveformVazia(width, height);

            var points = new List<Point>(volumes.Count + 2);
            double centroY = height / 2;

            // Determina quantos pontos usar
            int numPoints = Math.Min(volumes.Count, (int)width);
            double step = volumes.Count > width ? (double)volumes.Count / width : 1.0;

            // Parte superior
            points.Add(new Point(0, centroY));

            for (double i = 0; i < volumes.Count; i += step)
            {
                int idx = (int)i;
                if (idx >= volumes.Count) break;

                double x;
                if (volumes.Count <= width)
                {
                    // Espaçamento uniforme quando há poucos pontos
                    x = i * (width / Math.Max(volumes.Count - 1, 1));
                }
                else
                {
                    // Compressão quando há muitos pontos
                    x = (i / volumes.Count) * width;
                }

                double val = Math.Max(0, Math.Min(1, volumes[idx])); // Clamp 0-1
                double y = centroY - (val * centroY * 0.9);

                points.Add(new Point(x, Math.Max(0, Math.Min(height, y))));
            }

            // Parte inferior (espelho)
            int upperCount = points.Count;
            for (int i = upperCount - 1; i >= 0; i--)
            {
                var p = points[i];
                double y = centroY + ((centroY - p.Y));
                points.Add(new Point(p.X, Math.Max(0, Math.Min(height, y))));
            }

            return points;
        }

        /// <summary>
        /// Calcula a duração aproximada de um arquivo WAV em segundos.
        /// </summary>
        public static TimeSpan CalcularDuracaoWav(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Arquivo não encontrado.", path);

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(fs);

                // Lê cabeçalho
                var header = reader.ReadBytes(44);
                if (header.Length < 44)
                    return TimeSpan.Zero;

                // Extrai sample rate (bytes 24-27)
                int sampleRate = BitConverter.ToInt32(header, 24);
                if (sampleRate <= 0)
                    return TimeSpan.Zero;

                // Calcula duração
                long dataSize = fs.Length - 44;
                double seconds = dataSize / (double)(sampleRate * 2); // 16-bit mono = 2 bytes/sample

                return TimeSpan.FromSeconds(seconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao calcular duração: {ex.Message}");
                return TimeSpan.Zero;
            }
        }
    }
}
