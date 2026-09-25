using System;
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
        private readonly ILoggerService _loggerService;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _loggerService = viewModel.GetType().GetProperty("LoggerService")?.GetValue(viewModel) as ILoggerService
                ?? viewModel.GetType().GetField("_loggerService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(viewModel) as ILoggerService;
            DataContext = _viewModel;

            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
            _loggerService?.Log($"[DEBUG] Rodando como Admin: {isAdmin}");
            if (isAdmin)
            {
                _loggerService?.Log("[AVISO] Drag & drop pode não funcionar quando executado como Administrador.");
            }
        }

        // Realiza o Scroll Automático do Console de Logs ao adicionar novas mensagens
        private void TxtLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
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
