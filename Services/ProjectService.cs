using System;
using System.IO;
using GravadorMulti.Models;
using Newtonsoft.Json;

namespace GravadorMulti.Services
{
    public static class ProjectService
    {
        // Cria a estrutura de pastas e retorna um Projeto novinho
        public static Projeto CriarNovoProjeto(string nome, string pastaDestino)
        {
            // 1. Define caminhos
            string pastaProjeto = Path.Combine(pastaDestino, nome);
            string pastaAudios = Path.Combine(pastaProjeto, "Audios");
            string pastaExports = Path.Combine(pastaProjeto, "Exports");
            string arquivoJson = Path.Combine(pastaProjeto, "projeto.json");

            // 2. Cria diretórios físicos
            if (!Directory.Exists(pastaProjeto)) Directory.CreateDirectory(pastaProjeto);
            if (!Directory.Exists(pastaAudios)) Directory.CreateDirectory(pastaAudios);
            if (!Directory.Exists(pastaExports)) Directory.CreateDirectory(pastaExports);

            // 3. Cria objeto Projeto
            var proj = new Projeto
            {
                Nome = nome,
                PastaRaiz = pastaProjeto,
                PastaAudios = pastaAudios,
                PastaExports = pastaExports,
                CaminhoArquivoProjeto = arquivoJson,
                TextoRoteiro = ""
            };

            // 4. Salva o JSON inicial
            SalvarProjeto(proj);

            return proj;
        }

        public static void SalvarProjeto(Projeto proj)
        {
            if (string.IsNullOrEmpty(proj.CaminhoArquivoProjeto)) return;

            string json = JsonConvert.SerializeObject(proj, Formatting.Indented);
            File.WriteAllText(proj.CaminhoArquivoProjeto, json);
        }

        public static Projeto CarregarProjeto(string caminhoJson)
        {
            if (!File.Exists(caminhoJson)) throw new FileNotFoundException("Arquivo de projeto não encontrado.");

            string json = File.ReadAllText(caminhoJson);
            var proj = JsonConvert.DeserializeObject<Projeto>(json);

            if (proj == null) throw new Exception("Falha ao ler arquivo de projeto.");

            // Recalcula caminhos absolutos (caso a pasta tenha mudado de lugar)
            proj.CaminhoArquivoProjeto = caminhoJson;
            proj.PastaRaiz = Path.GetDirectoryName(caminhoJson)!;
            proj.PastaAudios = Path.Combine(proj.PastaRaiz, "Audios");
            proj.PastaExports = Path.Combine(proj.PastaRaiz, "Exports");

            // Garante que as pastas existam
            if (!Directory.Exists(proj.PastaAudios)) Directory.CreateDirectory(proj.PastaAudios);
            if (!Directory.Exists(proj.PastaExports)) Directory.CreateDirectory(proj.PastaExports);

            return proj;
        }
    }
}