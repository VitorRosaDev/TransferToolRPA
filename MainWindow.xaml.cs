using System;
using System.Collections.Specialized;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TransferToolRPA.ViewModels;
using TransferToolRPA.Services;

namespace TransferToolRPA
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        // Injetado sempre no construtor (DI manual em App.xaml.cs); o "null!" silencia
        // a análise de nulidade do WPF para campos readonly de interface.
        private readonly ILoggerService _loggerService = null!;

        public MainWindow(MainViewModel viewModel, ILoggerService loggerService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _loggerService = loggerService;
            DataContext = _viewModel;

            PosicionarNaLateralDireita();

            // Realiza o Scroll Automático do Console de Logs ao adicionar novas mensagens
            _viewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;

            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
            _loggerService?.Log($"[DEBUG] Rodando como Admin: {isAdmin}");
            if (isAdmin)
            {
                _loggerService?.Log("[AVISO] Drag & drop pode não funcionar quando executado como Administrador.");
            }
        }

        /// <summary>
        /// Abre a janela colada na borda direita da tela, com altura total e largura de
        /// 50% da area de trabalho. Calculado em runtime (SystemParameters.WorkArea) para
        /// se adaptar a qualquer resolucao/escala de monitor.
        /// </summary>
        private void PosicionarNaLateralDireita()
        {
            try
            {
                var area = SystemParameters.WorkArea;

                Width = area.Width * 0.5;
                Height = area.Height;
                Left = area.Right - Width;
                Top = area.Top;
                // Logs colapsados no inicio: ~10% da altura da tela (cabecalho + ~1 linha).
                MainGrid.RowDefinitions[3].Height = new GridLength(area.Height * 0.11);
            }
            catch
            {
                // Se nao for possivel calcular, mantem as dimensoes padrao do XAML.
            }
        }

        // Realiza o Scroll Automático do Console de Logs ao adicionar novas mensagens
        private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add)
                return;

            Dispatcher.BeginInvoke(new Action(RolarLogParaOFim),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void RolarLogParaOFim()
        {
            try
            {
                if (LstLog.Items.Count > 0)
                {
                    LstLog.ScrollIntoView(LstLog.Items[LstLog.Items.Count - 1]);
                }
            }
            catch
            {
                // Scroll automático é cosmético; nunca deve derrubar a aplicação.
            }
        }

        // Copia todo o conteúdo do console de logs para a área de transferência
        private void CopiarLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string log = _loggerService?.FullLog ?? string.Empty;
                Clipboard.SetText(log);
                _loggerService?.LogSuccess("Log copiado para a área de transferência.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Falha ao copiar o log: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Gerenciamento de Drag & Drop de arquivos JSON
        private void Border_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (sender is Border border)
                {
                    // Destaca a borda visualmente ao arrastar o arquivo por cima
                    border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202329"));
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Border_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16181C"));
            }
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16181C"));
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    if (Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        _viewModel.ImportarJsonCommand.Execute(filePath);
                    }
                    else
                    {
                        MessageBox.Show("Por favor, selecione apenas arquivos com a extensão .json", 
                                        "Formato Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        // Abre diálogo de seleção manual caso o operador clique na área
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Arquivos JSON (*.json)|*.json",
                    Title = "Selecionar Carga do TransferTool Mobile"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    _viewModel.ImportarJsonCommand.Execute(openFileDialog.FileName);
                }
            }
        }
    }
}
