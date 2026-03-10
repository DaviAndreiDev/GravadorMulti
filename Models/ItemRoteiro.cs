using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Newtonsoft.Json;

namespace GravadorMulti.Models
{
    public class ItemRoteiro : INotifyPropertyChanged
    {
        // Color constants used throughout the app
        public const string CorSemAudio = "#2B2B2B";
        public const string CorComAudio = "#1E3A5F";
        public const string CorAprovado = "#1E441E";
        public const string CorTransparente = "Transparent";

        public int Id { get; set; }
        public string Texto { get; set; } = "";
        public string CaminhoArquivo { get; set; } = "";

        private bool _temAudio;
        public bool TemAudio
        {
            get => _temAudio;
            set { _temAudio = value; OnPropertyChanged(nameof(TemAudio)); AtualizarCorFundo(); }
        }

        private bool _aprovado;
        public bool Aprovado
        {
            get => _aprovado;
            set { _aprovado = value; OnPropertyChanged(nameof(Aprovado)); AtualizarCorFundo(); }
        }

        private string _corFundo = CorSemAudio;
        public string CorFundo
        {
            get => _corFundo;
            set { _corFundo = value; OnPropertyChanged(nameof(CorFundo)); }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        private List<Point> _waveformPoints = new();
        [JsonIgnore]
        public List<Point> WaveformPoints
        {
            get => _waveformPoints ?? new List<Point> { new Point(0, 0), new Point(1, 0) };
            set 
            { 
                _waveformPoints = value ?? new List<Point> { new Point(0, 0), new Point(1, 0) }; 
                OnPropertyChanged(nameof(WaveformPoints)); 
            }
        }

        private double _playbackProgress;
        [JsonIgnore]
        public double PlaybackProgress
        {
            get => _playbackProgress;
            set { _playbackProgress = value; OnPropertyChanged(nameof(PlaybackProgress)); }
        }

        private double _selectionStartProgress;
        [JsonIgnore]
        public double SelectionStartProgress
        {
            get => _selectionStartProgress;
            set { _selectionStartProgress = value; OnPropertyChanged(nameof(SelectionStartProgress)); }
        }

        private double _selectionEndProgress;
        [JsonIgnore]
        public double SelectionEndProgress
        {
            get => _selectionEndProgress;
            set { _selectionEndProgress = value; OnPropertyChanged(nameof(SelectionEndProgress)); }
        }

        private bool _isCroppingMode;
        [JsonIgnore]
        public bool IsCroppingMode
        {
            get => _isCroppingMode;
            set { _isCroppingMode = value; OnPropertyChanged(nameof(IsCroppingMode)); }
        }

        private bool _isPlaying;
        [JsonIgnore]
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(TextoPlayPause));
            }
        }

        private bool _isRecording;
        [JsonIgnore]
        public bool IsRecording
        {
            get => _isRecording;
            set { _isRecording = value; OnPropertyChanged(nameof(IsRecording)); }
        }

        [JsonIgnore]
        public string TextoPlayPause => IsPlaying ? "⏸️" : "▶️";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Derives the background color from the current audio/approval state.
        /// </summary>
        private void AtualizarCorFundo()
        {
            if (Aprovado) CorFundo = CorAprovado;
            else if (TemAudio) CorFundo = CorComAudio;
            else CorFundo = CorSemAudio;
        }
    }
}