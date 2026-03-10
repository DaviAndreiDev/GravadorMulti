using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json; 

namespace GravadorMulti.Models
{
    public class Projeto : INotifyPropertyChanged
    {
        public string Nome { get; set; } = "Novo Projeto";
        public string CaminhoArquivoProjeto { get; set; } = "";
        public string PastaRaiz { get; set; } = "";
        public string PastaAudios { get; set; } = "";
        public string PastaExports { get; set; } = "";

        // Flag para saber se precisa salvar (Dirty)
        private bool _temAlteracoesNaoSalvas;
        [JsonIgnore]
        public bool TemAlteracoesNaoSalvas 
        { 
            get => _temAlteracoesNaoSalvas; 
            set { _temAlteracoesNaoSalvas = value; OnPropertyChanged(nameof(TemAlteracoesNaoSalvas)); } 
        }

        private bool _cortarSilencioAutomaticamente;
        public bool CortarSilencioAutomaticamente
        {
            get => _cortarSilencioAutomaticamente;
            set
            {
                _cortarSilencioAutomaticamente = value;
                TemAlteracoesNaoSalvas = true;
                OnPropertyChanged(nameof(CortarSilencioAutomaticamente));
            }
        }

        private string _textoRoteiro = "";
        public string TextoRoteiro 
        { 
            get => _textoRoteiro; 
            set 
            { 
                _textoRoteiro = value; 
                TemAlteracoesNaoSalvas = true; // Mudou texto = precisa salvar
                OnPropertyChanged(nameof(TextoRoteiro)); 
            } 
        }

        public ObservableCollection<ItemRoteiro> Itens { get; set; } = new ObservableCollection<ItemRoteiro>();

        private string _statusTexto = "Pronto";
        [JsonIgnore]
        public string StatusTexto 
        { 
            get => _statusTexto; 
            set { _statusTexto = value; OnPropertyChanged(nameof(StatusTexto)); } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Projeto() { }
    }
}