using System.Collections.ObjectModel;
using System.ComponentModel;
using GravadorMulti.Models;

namespace GravadorMulti.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private double _nivelMicrofone;
        public double NivelMicrofone
        {
            get => _nivelMicrofone;
            set { _nivelMicrofone = value; OnPropertyChanged(nameof(NivelMicrofone)); }
        }

        public ObservableCollection<Projeto> ProjetosAbertos { get; set; } = new();

        private Projeto? _projetoSelecionado;
        public Projeto? ProjetoSelecionado
        {
            get => _projetoSelecionado;
            set
            {
                _projetoSelecionado = value;
                OnPropertyChanged(nameof(ProjetoSelecionado));
                OnPropertyChanged(nameof(TemProjetosAbertos));
            }
        }

        public bool TemProjetosAbertos => ProjetosAbertos.Count > 0;

        public ObservableCollection<string> DispositivosEntrada { get; set; } = new();

        private int _indiceDispositivoSelecionado;
        public int IndiceDispositivoSelecionado
        {
            get => _indiceDispositivoSelecionado;
            set
            {
                _indiceDispositivoSelecionado = value;
                OnPropertyChanged(nameof(IndiceDispositivoSelecionado));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
