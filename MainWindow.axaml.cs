using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using GravadorMulti.Models;
using GravadorMulti.Services;
using GravadorMulti.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GravadorMulti
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _vm = new();
        private AudioService? _audioService;

        // Undo / Redo
        public class UndoCommand
        {
            public Action DoAction { get; set; } = null!;
            public Action UndoAction { get; set; } = null!;
        }
        private readonly Stack<UndoCommand> _undoStack = new();
        private readonly Stack<UndoCommand> _redoStack = new();

        // State
        private bool _tentandoFecharJanela;
        private ItemRoteiro? _itemGravando;
        private ItemRoteiro? _itemTocando;
        private ItemRoteiro? _itemSelecionadoNaLista;
        private ItemRoteiro? _itemScrubbing;
        private Projeto? _projetoParaFechar;

        private readonly List<float> _volumesGravacaoAtual = new();
        private DispatcherTimer _timerHardware = null!;
        private DispatcherTimer _timerReproducao = null!;
        private DispatcherTimer _scrubIdleTimer = null!;
        private bool _isScrubbing;
        private bool _estaTocando;
        private bool _wasPlayingBeforeScrub;
        private DateTime _lastScrubTime = DateTime.MinValue;
        private const double LARGURA_ONDA_DESENHO = 800.0;

        // Undo Recording State
        private string? _caminhoArquivoAntesGravacao;
        private List<Avalonia.Point>? _waveformAntesGravacao;
        private bool _tinhaAudioAntesGravacao;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;


            // B1 FIX: Single AudioService instantiation inside try/catch
            try
            {
                _audioService = new AudioService();

                _vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MainWindowViewModel.IndiceDispositivoSelecionado))
                    {
                        _audioService.IniciarMonitoramento(_vm.IndiceDispositivoSelecionado);
                    }
                };

                CarregarDispositivosAudio();
                SetupAudioEvents();
                // B10: Force start monitoring after loading devices, in case default index 0 doesn't trigger PropertyChanged
                _audioService.IniciarMonitoramento(_vm.IndiceDispositivoSelecionado);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERRO FATAL AUDIO: {ex.Message}");
            }

            // B11: Changed to Normal priority and 30ms (approx 30fps) so Avalonia doesn't drop the frame rendering
            _timerReproducao = new DispatcherTimer(
                TimeSpan.FromMilliseconds(30),
                DispatcherPriority.Normal,
                TimerReproducao_Tick);

            _scrubIdleTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(150),
                DispatcherPriority.Normal,
                ScrubIdleTimer_Tick);

            _timerHardware = new DispatcherTimer(
                TimeSpan.FromSeconds(1),
                DispatcherPriority.Background,
                (_, _) => VerificarNovosDispositivos());
            _timerHardware.Start();
        }

        private void ScrubIdleTimer_Tick(object? sender, EventArgs e)
        {
            _scrubIdleTimer.Stop();
            if (_isScrubbing && _audioService != null)
            {
                _audioService.PausarReproducao();
                _estaTocando = false;
                _timerReproducao.Stop();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  KEYBOARD SHORTCUTS
        // ═══════════════════════════════════════════════════════════

        private void OnWindow_KeyDown(object sender, KeyEventArgs e)
        {
            bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool isAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

            // Global shortcuts
            if (isCtrl && e.Key == Key.S) { BtnSalvar_Click(null, null); e.Handled = true; return; }
            if (isCtrl && !isAlt && e.Key == Key.Z) { ExecutarUndo(); e.Handled = true; return; }
            if ((isCtrl && isAlt && e.Key == Key.Z) || (isCtrl && e.Key == Key.Y)) { ExecutarRedo(); e.Handled = true; return; }

            // Don't intercept typing in text boxes
            if (e.Source is TextBox) return;

            // Item-scoped shortcuts
            if (_itemSelecionadoNaLista != null && _vm.ProjetoSelecionado != null)
            {
                switch (e.Key)
                {
                    case Key.Space:
                        AlternarReproducao(_itemSelecionadoNaLista);
                        e.Handled = true;
                        break;
                    case Key.R:
                        AlternarGravacao(_itemSelecionadoNaLista);
                        e.Handled = true;
                        break;
                    case Key.Delete:
                        LimparAudioDoItem(_itemSelecionadoNaLista);
                        e.Handled = true;
                        break;
                }
            }
        }

        private void OnListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
                _itemSelecionadoNaLista = e.AddedItems[0] as ItemRoteiro;
        }

        // ═══════════════════════════════════════════════════════════
        //  PLAYBACK
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// B3 FIX: Unified play/pause toggle — works from both button clicks and keyboard.
        /// </summary>
        private void AlternarReproducao(ItemRoteiro item, Button? btnVisual = null)
        {
            if (_audioService == null || !item.TemAudio || !File.Exists(item.CaminhoArquivo))
                return;

            // If already playing THIS item → pause
            if (_estaTocando && _itemTocando == item)
            {
                _audioService.PausarReproducao();
                _estaTocando = false;
                _timerReproducao.Stop();
                if (btnVisual != null) btnVisual.Content = "▶️";
                return;
            }

            // If playing a DIFFERENT item → stop it first
            if (_itemTocando != null && _itemTocando != item)
                PararReproducaoTotal();

            try
            {
                _audioService.TocarAudio(item.CaminhoArquivo);

                // Sync position if scrubbed previously
                double totalSegundos = _audioService.GetTotalTime().TotalSeconds;
                if (totalSegundos > 0 && item.PlaybackProgress > 0)
                    _audioService.DefinirTempo(item.PlaybackProgress * totalSegundos);

                _timerReproducao.Start();
                _estaTocando = true;
                _itemTocando = item;

                if (btnVisual != null) btnVisual.Content = "⏸️";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao iniciar reprodução: {ex.Message}");
            }
        }

        private void PararReproducaoTotal()
        {
            _audioService?.PararReproducao();
            _timerReproducao.Stop();
            _estaTocando = false;

            if (_itemTocando != null)
            {
                _itemTocando.PlaybackProgress = 0;
                _itemTocando = null;
            }
        }

        private void BtnPlay_Click(object? sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn?.DataContext as ItemRoteiro;
            if (item != null)
                AlternarReproducao(item, btn);
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            PararReproducaoTotal();
        }

        private void TimerReproducao_Tick(object? sender, EventArgs e)
        {
            if (_isScrubbing || _itemTocando == null || _audioService == null) return;
            if (!_estaTocando) return;

            double atual = _audioService.GetPosition().TotalSeconds;
            double total = _audioService.GetTotalTime().TotalSeconds;
            _itemTocando.PlaybackProgress = total > 0 ? Math.Clamp(atual / total, 0, 1) : 0;
        }

        // ═══════════════════════════════════════════════════════════
        //  RECORDING
        // ═══════════════════════════════════════════════════════════

        private void AlternarGravacao(ItemRoteiro item)
        {
            if (_audioService == null) return;
            var proj = _vm.ProjetoSelecionado;
            if (proj == null) return;

            if (_itemGravando == item)
            {
                // Stop current recording
                _audioService.PararGravacao();
                item.IsRecording = false;
                _itemGravando = null;
                return;
            }

            // Capture state before overwriting for Undo purposes
            if (_itemGravando == null)
            {
                _caminhoArquivoAntesGravacao = item.CaminhoArquivo;
                _waveformAntesGravacao = item.WaveformPoints?.ToList() ?? new List<Point>();
                _tinhaAudioAntesGravacao = item.TemAudio;
            }

            // Stop any previous recording
            if (_itemGravando != null)
            {
                _audioService.PararGravacao();
                _itemGravando.IsRecording = false;
            }

            // Collapse all items, expand this one
            foreach (var i in proj.Itens)
            {
                i.IsExpanded = false;
                i.IsRecording = false;
            }
            item.IsExpanded = true;
            item.IsRecording = true;

            // Stop any playback
            _audioService.PararTudo();
            _timerReproducao.Stop();
            _estaTocando = false;

            // Prepare recording path
            string arq = Path.Combine(proj.PastaAudios, $"Audio_{item.Id}_{DateTime.Now.Ticks}.wav");
            item.CaminhoArquivo = arq;
            item.PlaybackProgress = 0;
            item.TemAudio = true; // B5: This calls AtualizarCorFundo() automatically
            item.WaveformPoints = new List<Point>();

            _itemGravando = item;
            _volumesGravacaoAtual.Clear();

            try
            {
                _audioService.IniciarGravacao(arq, _vm.IndiceDispositivoSelecionado);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro crítico ao iniciar gravação: {ex.Message}");
                _itemGravando.IsRecording = false;
                _itemGravando = null;
            }
        }

        private void BtnGravar_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var item = (sender as Button)?.DataContext as ItemRoteiro;
            if (item != null) AlternarGravacao(item);
        }

        // ═══════════════════════════════════════════════════════════
        //  CONTEXT MENU ACTIONS
        // ═══════════════════════════════════════════════════════════

        private void BtnExcluirItem_Click(object? sender, RoutedEventArgs e)
        {
            var item = (sender as MenuItem)?.CommandParameter as ItemRoteiro ?? (sender as MenuItem)?.DataContext as ItemRoteiro;
            var proj = _vm.ProjetoSelecionado;

            if (item == null || proj == null) return;

            // Stop any active playback/recording on this item
            if (_itemTocando == item || _itemGravando == item)
            {
                _audioService?.PararTudo();
                _itemTocando = null;
                _itemGravando = null;
                _estaTocando = false;
                _timerReproducao.Stop();
            }

            int index = proj.Itens.IndexOf(item);
            PushUndo(
                doAction: () => {
                    proj.Itens.Remove(item);
                    proj.TemAlteracoesNaoSalvas = true;
                },
                undoAction: () => {
                    // Safe insert in case index changed, though it shouldn't normally
                    if (index >= 0 && index <= proj.Itens.Count)
                        proj.Itens.Insert(index, item);
                    else
                        proj.Itens.Add(item);
                    proj.TemAlteracoesNaoSalvas = true;
                }
            );

            proj.Itens.Remove(item);
            proj.TemAlteracoesNaoSalvas = true;
            proj.StatusTexto = "Frase removida.";
        }

        // ═══════════════════════════════════════════════════════════
        //  PROJECT MANAGEMENT
        // ═══════════════════════════════════════════════════════════

        private void CarregarDispositivosAudio()
        {
            if (_audioService == null) return;

            var dispositivos = _audioService.ObterDispositivosEntrada();
            foreach (var d in dispositivos)
                _vm.DispositivosEntrada.Add(d);

            if (_vm.DispositivosEntrada.Count > 0)
            {
                _vm.IndiceDispositivoSelecionado = 0;
                _audioService.IniciarMonitoramento(0);
            }
        }

        private async void BtnNovoProjeto_Click(object sender, RoutedEventArgs e)
        {
            var pastas = await this.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "Pasta do Projeto", AllowMultiple = false });

            if (pastas.Count > 0)
            {
                var novoProj = ProjectService.CriarNovoProjeto(
                    "Projeto " + (_vm.ProjetosAbertos.Count + 1),
                    pastas[0].Path.LocalPath);
                _vm.ProjetosAbertos.Add(novoProj);
                _vm.ProjetoSelecionado = novoProj;
            }
        }

        private async void BtnCarregar_Click(object sender, RoutedEventArgs e)
        {
            var arquivos = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Abrir Projeto",
                FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }
            });

            if (arquivos.Count == 0) return;

            // B7 FIX: Show error instead of silently swallowing
            try
            {
                var proj = ProjectService.CarregarProjeto(arquivos[0].Path.LocalPath);
                foreach (var item in proj.Itens)
                {
                    if (item.TemAudio && File.Exists(item.CaminhoArquivo))
                    {
                        item.WaveformPoints = WaveformUtils.GerarPontosDoArquivo(
                            item.CaminhoArquivo, LARGURA_ONDA_DESENHO, 80);
                    }
                }
                _vm.ProjetosAbertos.Add(proj);
                _vm.ProjetoSelecionado = proj;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar projeto: {ex.Message}");
                if (_vm.ProjetoSelecionado != null)
                    _vm.ProjetoSelecionado.StatusTexto = $"Erro ao carregar: {ex.Message}";
            }
        }

        private void BtnSalvar_Click(object? sender, RoutedEventArgs? e)
        {
            var proj = _vm.ProjetoSelecionado;
            if (proj == null) return;

            ProjectService.SalvarProjeto(proj);
            proj.TemAlteracoesNaoSalvas = false;
            proj.StatusTexto = "Projeto Salvo!";
        }

        private async void BtnExportarWav_Click(object sender, RoutedEventArgs e) => await ExportarFormatoAsync(ExportFormat.Wav);
        private async void BtnExportarMp3_Click(object sender, RoutedEventArgs e) => await ExportarFormatoAsync(ExportFormat.Mp3);
        private async void BtnExportarAac_Click(object sender, RoutedEventArgs e) => await ExportarFormatoAsync(ExportFormat.Aac);
        private async void BtnExportarFlac_Click(object sender, RoutedEventArgs e) => await ExportarFormatoAsync(ExportFormat.Flac);
        private async void BtnExportarOgg_Click(object sender, RoutedEventArgs e) => await ExportarFormatoAsync(ExportFormat.Ogg);

        private async Task ExportarFormatoAsync(ExportFormat formato)
        {
            var proj = _vm.ProjetoSelecionado;
            if (proj == null || proj.Itens.Count == 0 || _audioService == null) return;

            var lista = proj.Itens
                .Where(item => item.TemAudio && File.Exists(item.CaminhoArquivo))
                .Select(item => item.CaminhoArquivo)
                .ToList();

            if (lista.Count == 0) return;

            string ext = FfmpegService.GetExtension(formato);
            var arquivo = await this.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Exportar como {formato.ToString().ToUpper()}",
                DefaultExtension = ext.TrimStart('.'),
                SuggestedFileName = $"Mix_{DateTime.Now:HHmm}{ext}"
            });

            if (arquivo == null) return;

            var caminho = arquivo.Path.LocalPath;
            proj.StatusTexto = $"Exportando como {formato}...";
            
            try
            {
                if (formato == ExportFormat.Wav)
                {
                    // Usa direto o AudioService local (rápido e sem perda)
                    await Task.Run(() => _audioService.ExportarMixagem(lista, caminho));
                }
                else
                {
                    // Usa FFmpeg
                    var ffmpegService = new FfmpegService();
                    if (!ffmpegService.IsAvailable)
                    {
                        proj.StatusTexto = "Erro: FFmpeg não foi encontrado no sistema.";
                        return;
                    }
                    await ffmpegService.ExportarAsync(lista, caminho, formato);
                }
                
                proj.StatusTexto = $"Mixagem exportada com sucesso!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro na exportação: {ex}");
                proj.StatusTexto = "Erro ao exportar mixagem!";
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  TAB / CLOSE MANAGEMENT
        // ═══════════════════════════════════════════════════════════

        private void BtnFecharAba_Click(object sender, RoutedEventArgs e)
        {
            var proj = (sender as Button)?.DataContext as Projeto;
            if (proj == null) return;

            if (proj.TemAlteracoesNaoSalvas)
            {
                _projetoParaFechar = proj;
                OverlayFechar.IsVisible = true;
            }
            else
            {
                FecharProjetoDefinitivo(proj);
            }
        }

        /// <summary>
        /// B2 FIX: Capture comparison BEFORE removing from collection.
        /// </summary>
        private void FecharProjetoDefinitivo(Projeto proj)
        {
            bool eraSelecionado = _vm.ProjetoSelecionado == proj;

            if (eraSelecionado)
                _audioService?.PararTudo();

            _vm.ProjetosAbertos.Remove(proj);

            if (eraSelecionado)
                _vm.ProjetoSelecionado = _vm.ProjetosAbertos.LastOrDefault();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            bool existeProjetoSujo = _vm.ProjetosAbertos.Any(p => p.TemAlteracoesNaoSalvas);

            if (existeProjetoSujo && !_tentandoFecharJanela)
            {
                e.Cancel = true;
                _tentandoFecharJanela = true;
                OverlayFechar.IsVisible = true;
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// B9 FIX: Dispose timers and audio service on close.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _timerHardware.Stop();
            _timerReproducao.Stop();
            _audioService?.Dispose();
            base.OnClosed(e);
        }

        private void BtnSalvarFechar_Click(object sender, RoutedEventArgs e)
        {
            if (_tentandoFecharJanela)
            {
                foreach (var proj in _vm.ProjetosAbertos.Where(p => p.TemAlteracoesNaoSalvas))
                {
                    ProjectService.SalvarProjeto(proj);
                    proj.TemAlteracoesNaoSalvas = false;
                }
                _tentandoFecharJanela = false;
                OverlayFechar.IsVisible = false;
                Close();
            }
            else if (_projetoParaFechar != null)
            {
                ProjectService.SalvarProjeto(_projetoParaFechar);
                _projetoParaFechar.TemAlteracoesNaoSalvas = false;
                FecharProjetoDefinitivo(_projetoParaFechar);
                OverlayFechar.IsVisible = false;
                _projetoParaFechar = null;
            }
        }

        private void BtnNaoSalvarFechar_Click(object sender, RoutedEventArgs e)
        {
            if (_tentandoFecharJanela)
            {
                foreach (var proj in _vm.ProjetosAbertos)
                    proj.TemAlteracoesNaoSalvas = false;

                _tentandoFecharJanela = false;
                OverlayFechar.IsVisible = false;
                Close();
            }
            else if (_projetoParaFechar != null)
            {
                FecharProjetoDefinitivo(_projetoParaFechar);
                OverlayFechar.IsVisible = false;
                _projetoParaFechar = null;
            }
        }

        private void BtnCancelarFechar_Click(object sender, RoutedEventArgs e)
        {
            OverlayFechar.IsVisible = false;
            _projetoParaFechar = null;
            _tentandoFecharJanela = false;
        }

        // ═══════════════════════════════════════════════════════════
        //  REORDERING (DRAG & DROP / CONTEXT MENU)
        // ═══════════════════════════════════════════════════════════

        private void BtnMoverCima_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuItem)?.CommandParameter as ItemRoteiro ?? (sender as MenuItem)?.DataContext as ItemRoteiro;
            MoverItem(item, -1);
        }

        private void BtnMoverBaixo_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuItem)?.CommandParameter as ItemRoteiro ?? (sender as MenuItem)?.DataContext as ItemRoteiro;
            MoverItem(item, 1);
        }

        private void MoverItem(ItemRoteiro? item, int offset)
        {
            var proj = _vm.ProjetoSelecionado;
            if (proj == null || item == null) return;

            int oldIndex = proj.Itens.IndexOf(item);
            int newIndex = oldIndex + offset;

            if (oldIndex < 0 || newIndex < 0 || newIndex >= proj.Itens.Count) return;

            // Capture indices for undo closure
            int capturedOld = oldIndex;
            int capturedNew = newIndex;

            PushUndo(
                doAction: () =>
                {
                    proj.Itens.Move(capturedOld, capturedNew);
                    proj.TemAlteracoesNaoSalvas = true;
                },
                undoAction: () =>
                {
                    proj.Itens.Move(capturedNew, capturedOld);
                    proj.TemAlteracoesNaoSalvas = true;
                }
            );

            // Actually perform the move NOW
            proj.Itens.Move(oldIndex, newIndex);
            proj.TemAlteracoesNaoSalvas = true;
            proj.StatusTexto = "Item reordenado.";
        }

        private Point _dragStartPoint;
        private bool _isDragging;
        private bool _isActiveDrag;
        private ItemRoteiro? _draggedItemDnD;
        private Control? _draggedContainer;
        private int _dropTargetIndex = -1;

        private void OnDragHandlePointerPressed(object sender, PointerPressedEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            var props = e.GetCurrentPoint(border).Properties;
            if (props.IsLeftButtonPressed)
            {
                _dragStartPoint = e.GetPosition(this);
                _isDragging = true;
                _isActiveDrag = false;
                _draggedItemDnD = border.DataContext as ItemRoteiro;
                e.Handled = true;
            }
        }

        private void OnDragHandlePointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isDragging || _draggedItemDnD == null) return;

            var border = sender as Border;
            if (border == null) return;

            var currentPoint = e.GetPosition(this);

            // Safety: if button released unexpectedly
            var props = e.GetCurrentPoint(border).Properties;
            if (!props.IsLeftButtonPressed)
            {
                CleanupDrag(e.Pointer);
                return;
            }

            if (!_isActiveDrag)
            {
                // Check threshold before starting drag
                if (Math.Abs(currentPoint.Y - _dragStartPoint.Y) > 6 ||
                    Math.Abs(currentPoint.X - _dragStartPoint.X) > 6)
                {
                    _isActiveDrag = true;
                    e.Pointer.Capture(border);

                    // Show ghost with item text
                    var txt = _draggedItemDnD.Texto ?? "";
                    DragGhostText.Text = txt.Length > 60 ? txt.Substring(0, 60) + "…" : txt;
                    DragOverlayCanvas.IsVisible = true;

                    // Dim the original item while dragging
                    var listBox = this.FindControl<ListBox>("ItensListBox");
                    var proj = _vm.ProjetoSelecionado;
                    if (listBox != null && proj != null)
                    {
                        int idx = proj.Itens.IndexOf(_draggedItemDnD);
                        if (idx >= 0)
                        {
                            _draggedContainer = listBox.ContainerFromIndex(idx);
                            if (_draggedContainer != null)
                                _draggedContainer.Opacity = 0.3;
                        }
                    }
                }
                else return;
            }

            // Move ghost to follow cursor
            Canvas.SetLeft(DragGhost, currentPoint.X + 20);
            Canvas.SetTop(DragGhost, currentPoint.Y - 15);

            // Update drop indicator position
            UpdateDropIndicator(currentPoint);

            e.Handled = true;
        }

        private void OnDragHandlePointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isActiveDrag && _draggedItemDnD != null && _dropTargetIndex >= 0)
            {
                var proj = _vm.ProjetoSelecionado;
                if (proj != null)
                {
                    int oldIndex = proj.Itens.IndexOf(_draggedItemDnD);
                    if (oldIndex >= 0 && _dropTargetIndex != oldIndex)
                    {
                        int capturedOld = oldIndex;
                        int capturedNew = _dropTargetIndex;
                        PushUndo(
                            doAction: () =>
                            {
                                proj.Itens.Move(capturedOld, capturedNew);
                                proj.TemAlteracoesNaoSalvas = true;
                            },
                            undoAction: () =>
                            {
                                proj.Itens.Move(capturedNew, capturedOld);
                                proj.TemAlteracoesNaoSalvas = true;
                            }
                        );
                        proj.Itens.Move(oldIndex, _dropTargetIndex);
                        proj.TemAlteracoesNaoSalvas = true;
                        proj.StatusTexto = "Item reordenado.";
                    }
                }
            }

            CleanupDrag(e.Pointer);
        }

        private void CleanupDrag(IPointer? pointer)
        {
            if (_draggedContainer != null)
            {
                _draggedContainer.Opacity = 1.0;
                _draggedContainer = null;
            }

            _isDragging = false;
            _isActiveDrag = false;
            _draggedItemDnD = null;
            _dropTargetIndex = -1;
            DragOverlayCanvas.IsVisible = false;
            DropIndicatorLine.IsVisible = false;
            pointer?.Capture(null);
        }

        private void UpdateDropIndicator(Point windowPoint)
        {
            var proj = _vm.ProjetoSelecionado;
            if (proj == null || _draggedItemDnD == null) return;

            var listBox = this.FindControl<ListBox>("ItensListBox");
            if (listBox == null) return;

            _dropTargetIndex = -1;
            DropIndicatorLine.IsVisible = false;

            int draggedIndex = proj.Itens.IndexOf(_draggedItemDnD);

            // Collect visible containers (excluding dragged item)
            double lastBottom = 0, lastX = 0, lastW = 0;
            int slot = 0;
            bool found = false;

            for (int i = 0; i < proj.Itens.Count; i++)
            {
                if (i == draggedIndex) continue;

                var container = listBox.ContainerFromIndex(i);
                if (container == null) { slot++; continue; }

                var topLeft = container.TranslatePoint(new Point(0, 0), this);
                if (topLeft == null) { slot++; continue; }

                double top = topLeft.Value.Y;
                double bottom = top + container.Bounds.Height;
                double mid = (top + bottom) / 2;
                double x = topLeft.Value.X;
                double w = container.Bounds.Width;

                // Cursor is above midpoint → insert BEFORE this item
                if (windowPoint.Y < mid)
                {
                    _dropTargetIndex = slot;
                    Canvas.SetLeft(DropIndicatorLine, x);
                    Canvas.SetTop(DropIndicatorLine, top - 2);
                    DropIndicatorLine.Width = w;
                    DropIndicatorLine.IsVisible = true;
                    found = true;
                    break;
                }

                lastBottom = bottom;
                lastX = x;
                lastW = w;
                slot++;
            }

            // Cursor is below all items → insert at end
            if (!found && slot > 0)
            {
                _dropTargetIndex = slot;
                Canvas.SetLeft(DropIndicatorLine, lastX);
                Canvas.SetTop(DropIndicatorLine, lastBottom - 1);
                DropIndicatorLine.Width = lastW;
                DropIndicatorLine.IsVisible = true;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  SCRIPT SLICING
        // ═══════════════════════════════════════════════════════════

        private void BtnFatiar_Click(object sender, RoutedEventArgs e)
        {
            var proj = _vm.ProjetoSelecionado;
            if (proj == null || string.IsNullOrEmpty(proj.TextoRoteiro)) return;

            var novas = proj.TextoRoteiro
                .Split('\n')
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .ToList();

            int perdidas = proj.Itens.Count(v => v.TemAudio && !novas.Contains(v.Texto));

            if (perdidas > 0)
            {
                TxtMsgFatiar.Text = $"{perdidas} gravações serão perdidas.";
                OverlayFatiar.IsVisible = true;
            }
            else
            {
                ExecutarFatiamento();
            }
        }

        private void BtnConfirmarFatia_Click(object sender, RoutedEventArgs e)
        {
            ExecutarFatiamento();
            OverlayFatiar.IsVisible = false;
        }

        private void BtnCancelarFatia_Click(object sender, RoutedEventArgs e)
        {
            OverlayFatiar.IsVisible = false;
        }

        private void ExecutarFatiamento()
        {
            var proj = _vm.ProjetoSelecionado;
            if (proj == null) return;

            var velhos = proj.Itens.ToList();
            var textoAtual = proj.TextoRoteiro;

            Action makeSliced = () =>
            {
                var localVelhos = velhos.ToList();
                proj.Itens.Clear();
                int id = 1;

                foreach (var linha in textoAtual.Split('\n'))
                {
                    string txt = linha.Trim();
                    if (string.IsNullOrWhiteSpace(txt)) continue;

                    var existe = localVelhos.FirstOrDefault(x => x.Texto == txt && x.TemAudio);
                    if (existe != null)
                    {
                        existe.Id = id++;
                        existe.IsExpanded = false;
                        proj.Itens.Add(existe);
                        localVelhos.Remove(existe);
                    }
                    else
                    {
                        proj.Itens.Add(new ItemRoteiro { Id = id++, Texto = txt });
                    }
                }
                proj.TemAlteracoesNaoSalvas = true;
            };

            PushUndo(
                doAction: makeSliced,
                undoAction: () =>
                {
                    proj.Itens.Clear();
                    foreach (var originalItem in velhos) proj.Itens.Add(originalItem);
                    proj.TemAlteracoesNaoSalvas = true;
                }
            );

            makeSliced();
        }
        // ═══════════════════════════════════════════════════════════
        //  MODO CORTE E EDICAO AUDIO
        // ═══════════════════════════════════════════════════════════

        private void BtnModoRecorte_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.DataContext is ItemRoteiro item)
            {
                PararReproducaoTotal();
                item.IsCroppingMode = true;
                item.SelectionStartProgress = 0.0;
                item.SelectionEndProgress = 1.0;
            }
        }

        private void BtnCancelarCorte_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is Button btn && btn.DataContext is ItemRoteiro item)
            {
                item.IsCroppingMode = false;
            }
        }

        private async void BtnConfirmarCorte_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is Button btn && btn.DataContext is ItemRoteiro item)
            {
                if (!item.IsCroppingMode || _audioService == null) return;
                
                PararReproducaoTotal();

                double start = Math.Min(item.SelectionStartProgress, item.SelectionEndProgress);
                double end = Math.Max(item.SelectionStartProgress, item.SelectionEndProgress);
                
                // Retira seleção na hora pra snappiness (UI)
                item.IsCroppingMode = false;

                // Gera novo caminho
                string fileName = $"audio_{item.Id}_{DateTime.Now.Ticks}.wav";
                string newPath = Path.Combine(_vm.ProjetoSelecionado.PastaAudios, fileName);

                // Executa corte no sistema de arquivos
                bool sucess = _audioService.CortarAudio(item.CaminhoArquivo, newPath, start, end);
                
                if (sucess)
                {
                    var proj = _vm.ProjetoSelecionado;
                    string caminhoAntigo = item.CaminhoArquivo;
                    var pontosAntigos = item.WaveformPoints?.ToList() ?? new List<Point>();
                    double larguraOnda = (sender as Visual)?.FindAncestorOfType<Grid>()?.Bounds.Width ?? 800; // fallback pra redesenhar
                    
                    PushUndo(
                        doAction: () =>
                        {
                            item.CaminhoArquivo = newPath;
                            item.WaveformPoints = WaveformUtils.GerarPontosDoArquivo(newPath, larguraOnda, 80);
                            item.PlaybackProgress = 0;
                            if (proj != null) proj.TemAlteracoesNaoSalvas = true;
                        },
                        undoAction: () =>
                        {
                            item.CaminhoArquivo = caminhoAntigo;
                            item.WaveformPoints = pontosAntigos;
                            item.PlaybackProgress = 0;
                            if (proj != null) proj.TemAlteracoesNaoSalvas = true;
                        }
                    );

                    item.CaminhoArquivo = newPath;
                    item.WaveformPoints = WaveformUtils.GerarPontosDoArquivo(item.CaminhoArquivo, larguraOnda, 80);
                    item.PlaybackProgress = 0;
                    if (proj != null) proj.TemAlteracoesNaoSalvas = true;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  ITEM INTERACTION
        // ═══════════════════════════════════════════════════════════

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (e.Source is Visual v && v.FindAncestorOfType<Button>() != null) return;
            var item = (sender as Grid)?.DataContext as ItemRoteiro;
            if (item != null) item.IsExpanded = !item.IsExpanded;
        }

        // ═══════════════════════════════════════════════════════════
        //  WAVEFORM SCRUBBING
        // ═══════════════════════════════════════════════════════════

        private enum DragHandle { None, Left, Right }
        private DragHandle _currentDragHandle = DragHandle.None;

        private void Waveform_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            var item = (sender as Grid)?.DataContext as ItemRoteiro;

            if (item != null && item.TemAudio && _itemGravando == null)
            {
                var pt = e.GetPosition(sender as Grid);
                double width = (sender as Grid)?.Bounds.Width ?? 1;
                double progresso = Math.Clamp(pt.X / width, 0, 1);

                if (item.IsCroppingMode)
                {
                    double distL = Math.Abs(progresso - item.SelectionStartProgress);
                    double distR = Math.Abs(progresso - item.SelectionEndProgress);
                    
                    // Tolerance for hitting the handles
                    double tolerance = 0.08;

                    if (distL < tolerance && distL <= distR)
                    {
                        _currentDragHandle = DragHandle.Left;
                        _itemScrubbing = item;
                    }
                    else if (distR < tolerance && distR < distL)
                    {
                        _currentDragHandle = DragHandle.Right;
                        _itemScrubbing = item;
                    }
                    else
                    {
                        _currentDragHandle = DragHandle.None;
                    }
                    return;
                }

                // SCRUB NORMAL
                _isScrubbing = true;
                _currentDragHandle = DragHandle.None;
                _itemScrubbing = item;
                _wasPlayingBeforeScrub = (_estaTocando && _itemTocando == item);
                AtualizarPosicaoScrub(sender as Grid, e);

                if (_audioService != null)
                {
                    if (_itemTocando != null && _itemTocando != item)
                        PararReproducaoTotal();

                    _audioService.TocarAudio(item.CaminhoArquivo);
                    _estaTocando = true;
                    _itemTocando = item;
                    _timerReproducao.Start();

                    double total = _audioService.GetTotalTime().TotalSeconds;
                    if (total > 0)
                        _audioService.DefinirTempo(_itemScrubbing.PlaybackProgress * total, true);

                    _lastScrubTime = DateTime.Now;
                    _scrubIdleTimer.Stop();
                    _scrubIdleTimer.Start();
                }
            }
        }

        private void Waveform_PointerMoved(object sender, PointerEventArgs e)
        {
            e.Handled = true;
            var item = (sender as Grid)?.DataContext as ItemRoteiro;
            
            // Cursor update when hovering over handles
            if (!_isScrubbing && _currentDragHandle == DragHandle.None && item != null && item.IsCroppingMode)
            {
                var pt = e.GetPosition(sender as Grid);
                double width = (sender as Grid)?.Bounds.Width ?? 1;
                double progresso = pt.X / width;
                double distL = Math.Abs(progresso - item.SelectionStartProgress);
                double distR = Math.Abs(progresso - item.SelectionEndProgress);
                
                double hoverTolerance = 0.05;
                if (distL < hoverTolerance || distR < hoverTolerance)
                    (sender as Grid).Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeWestEast);
                else
                    (sender as Grid).Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
            }

            if (_itemScrubbing != null)
            {
                if (_itemScrubbing.IsCroppingMode && _currentDragHandle != DragHandle.None)
                {
                    var pt = e.GetPosition(sender as Grid);
                    double width = (sender as Grid)?.Bounds.Width ?? 1;
                    double progresso = Math.Clamp(pt.X / width, 0, 1);
                    
                    if (_currentDragHandle == DragHandle.Left)
                    {
                        _itemScrubbing.SelectionStartProgress = Math.Clamp(progresso, 0, _itemScrubbing.SelectionEndProgress - 0.01);
                    }
                    else if (_currentDragHandle == DragHandle.Right)
                    {
                        _itemScrubbing.SelectionEndProgress = Math.Clamp(progresso, _itemScrubbing.SelectionStartProgress + 0.01, 1);
                    }
                    (sender as Grid).Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeWestEast);
                }
                else if (_isScrubbing)
                {
                    AtualizarPosicaoScrub(sender as Grid, e);

                    if (_audioService != null)
                    {
                        var now = DateTime.Now;
                        if ((now - _lastScrubTime).TotalMilliseconds > 60)
                        {
                            _lastScrubTime = now;
                            double total = _audioService.GetTotalTime().TotalSeconds;
                            if (total > 0)
                                _audioService.DefinirTempo(_itemScrubbing.PlaybackProgress * total, true);
                            
                            _estaTocando = true;
                            _timerReproducao.Start();
                        }

                        _scrubIdleTimer.Stop();
                        _scrubIdleTimer.Start();
                    }
                }
            }
        }

        private void Waveform_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            e.Handled = true;
            _scrubIdleTimer.Stop();
            (sender as Grid).Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);

            if (_currentDragHandle != DragHandle.None)
            {
                _currentDragHandle = DragHandle.None;
                _itemScrubbing = null;
                return;
            }

            if (!_isScrubbing || _itemScrubbing == null || _audioService == null)
            {
                _isScrubbing = false;
                _itemScrubbing = null;
                return;
            }

            _isScrubbing = false;
            double total = _audioService.GetTotalTime().TotalSeconds;
            double novaPosicao = total > 0 ? _itemScrubbing.PlaybackProgress * total : 0;
            _audioService.DefinirTempo(novaPosicao);

            if (_wasPlayingBeforeScrub)
            {
                _audioService.DefinirTempo(novaPosicao, true);
                _estaTocando = true;
                _timerReproducao.Start();
            }
            else
            {
                _audioService.PausarReproducao();
                _estaTocando = false;
                _timerReproducao.Stop();
            }

            _itemScrubbing = null;
        }

        private void AtualizarPosicaoScrub(Grid? grid, PointerEventArgs e)
        {
            if (grid == null || _itemScrubbing == null) return;

            double w = grid.Bounds.Width <= 0 ? 1 : grid.Bounds.Width;
            double pct = Math.Clamp(e.GetCurrentPoint(grid).Position.X / w, 0, 1);
            _itemScrubbing.PlaybackProgress = pct;
        }

        // ═══════════════════════════════════════════════════════════
        //  UNDO / REDO
        // ═══════════════════════════════════════════════════════════

        private void PushUndo(Action doAction, Action undoAction)
        {
            _undoStack.Push(new UndoCommand { DoAction = doAction, UndoAction = undoAction });
            _redoStack.Clear();
            var proj = _vm.ProjetoSelecionado;
            if (proj != null) proj.TemAlteracoesNaoSalvas = true;
        }

        private void ExecutarUndo()
        {
            if (_undoStack.Count == 0) return;
            var cmd = _undoStack.Pop();
            cmd.UndoAction?.Invoke();
            _redoStack.Push(cmd);
            if (_vm.ProjetoSelecionado != null)
            {
                _vm.ProjetoSelecionado.StatusTexto = "Ação desfeita.";
                _vm.ProjetoSelecionado.TemAlteracoesNaoSalvas = true;
            }
        }

        private void ExecutarRedo()
        {
            if (_redoStack.Count == 0) return;
            var cmd = _redoStack.Pop();
            cmd.DoAction?.Invoke();
            _undoStack.Push(cmd);
            if (_vm.ProjetoSelecionado != null)
            {
                _vm.ProjetoSelecionado.StatusTexto = "Ação refeita.";
                _vm.ProjetoSelecionado.TemAlteracoesNaoSalvas = true;
            }
        }

        private void LimparAudioDoItem(ItemRoteiro item)
        {
            if (!item.TemAudio) return;

            // Stop any active playback/recording on this item
            if (_itemTocando == item || _itemGravando == item)
            {
                _audioService?.PararTudo();
                _timerReproducao.Stop();
                _estaTocando = false;
            }

            // Save state for undo
            string caminhoVelho = item.CaminhoArquivo;
            var ondaVelha = item.WaveformPoints;

            PushUndo(
                doAction: () =>
                {
                    item.TemAudio = false; // Triggers AtualizarCorFundo()
                    item.WaveformPoints = new List<Point>();
                },
                undoAction: () =>
                {
                    item.CaminhoArquivo = caminhoVelho;
                    item.WaveformPoints = ondaVelha;
                    item.TemAudio = true; // Triggers AtualizarCorFundo()
                }
            );

            // Execute the clear
            item.TemAudio = false; // Triggers AtualizarCorFundo()
            item.WaveformPoints = new List<Point>();

            if (_vm.ProjetoSelecionado != null)
                _vm.ProjetoSelecionado.StatusTexto = "Áudio apagado.";
        }

        private void BtnLimparAudio_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var item = (sender as Button)?.DataContext as ItemRoteiro;
            if (item != null) LimparAudioDoItem(item);
        }

        private void RegistrarGravacaoNoHistorico(ItemRoteiro item, string caminhoNovo)
        {
            string caminhoAntigo = _caminhoArquivoAntesGravacao ?? "";
            var ondaAntiga = _waveformAntesGravacao ?? new List<Point>();
            bool tinhaAudio = _tinhaAudioAntesGravacao;

            PushUndo(
                doAction: () =>
                {
                    item.CaminhoArquivo = caminhoNovo;
                    item.WaveformPoints = WaveformUtils.GerarPontosDoArquivo(caminhoNovo, LARGURA_ONDA_DESENHO, 80);
                    item.TemAudio = true; // Triggers AtualizarCorFundo()
                },
                undoAction: () =>
                {
                    item.CaminhoArquivo = caminhoAntigo;
                    item.WaveformPoints = ondaAntiga;
                    item.TemAudio = tinhaAudio; // Triggers AtualizarCorFundo()
                }
            );
        }

        // ═══════════════════════════════════════════════════════════
        //  AUDIO EVENTS
        // ═══════════════════════════════════════════════════════════

        private void SetupAudioEvents()
        {
            if (_audioService == null) return;

            _audioService.OnVolumeReceived += (vol) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _vm.NivelMicrofone = vol;

                    if (_itemGravando != null)
                    {
                        _volumesGravacaoAtual.Add(vol);
                        _itemGravando.WaveformPoints = WaveformUtils.ConverterVolumesParaPontos(
                            _volumesGravacaoAtual, LARGURA_ONDA_DESENHO, 80);
                    }
                });
            };

            _audioService.OnRecordingStopped += () =>
            {
                if (_itemGravando == null) return;

                var item = _itemGravando;
                _itemGravando = null;

                // B8 FIX: Capture project reference before Task.Run
                var projetoAtual = _vm.ProjetoSelecionado;

                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        if (projetoAtual != null && projetoAtual.CortarSilencioAutomaticamente)
                        {
                            var ffmpeg = new FfmpegService();
                            if (ffmpeg.IsAvailable)
                            {
                                await ffmpeg.RemoverSilencioAsync(item.CaminhoArquivo);
                            }
                        }

                        var waveformPoints = WaveformUtils.GerarPontosDoArquivo(item.CaminhoArquivo, 800, 80);

                        Dispatcher.UIThread.Invoke(() =>
                        {
                            item.TemAudio = true; // Triggers AtualizarCorFundo()
                            item.WaveformPoints = waveformPoints;
                            RegistrarGravacaoNoHistorico(item, item.CaminhoArquivo);
                            _volumesGravacaoAtual.Clear();

                            if (projetoAtual != null)
                            {
                                projetoAtual.TemAlteracoesNaoSalvas = true;
                                var projRef = projetoAtual;
                                System.Threading.Tasks.Task.Run(() => ProjectService.SalvarProjeto(projRef));
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar gravação: {ex.Message}");
                    }
                });

                // Re-enable mic monitoring after recording
                System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        _audioService?.ReiniciarMonitoramentoSeParado(_vm.IndiceDispositivoSelecionado);
                    });
                });
            };

            _audioService.OnPlaybackStopped += () => Dispatcher.UIThread.Invoke(() =>
            {
                if (_isScrubbing) return;

                _timerReproducao.Stop();
                _estaTocando = false;

                if (_itemTocando != null)
                {
                    _itemTocando.PlaybackProgress = 0;
                    _itemTocando = null;
                }
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  DEVICE HOTPLUG
        // ═══════════════════════════════════════════════════════════

        private void VerificarNovosDispositivos()
        {
            if (_audioService == null || !_audioService.HouveAlteracaoDeDispositivos())
                return;

            var dispositivosAtuais = _audioService.ObterDispositivosEntrada();
            var selecionadoAntes = _vm.DispositivosEntrada.ElementAtOrDefault(_vm.IndiceDispositivoSelecionado);

            _vm.DispositivosEntrada.Clear();
            foreach (var d in dispositivosAtuais)
                _vm.DispositivosEntrada.Add(d);

            int novoIndex = selecionadoAntes != null
                ? _vm.DispositivosEntrada.IndexOf(selecionadoAntes)
                : -1;

            _vm.IndiceDispositivoSelecionado = novoIndex >= 0 ? novoIndex : 0;
            _audioService.ReiniciarSistemaDeSaida();
        }
    }
}
