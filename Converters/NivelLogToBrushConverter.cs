using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TransferToolRPA.Models;

namespace TransferToolRPA.Converters
{
    /// <summary>
    /// Mapeia o nível de uma linha de log para a cor de exibição no console da UI:
    /// Info (verde-água), Sucesso (verde), Aviso (âmbar) e Erro (vermelho).
    /// </summary>
    public class NivelLogToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Info = Criar("#A3E4D7");
        private static readonly SolidColorBrush Sucesso = Criar("#00BA7C");
        private static readonly SolidColorBrush Aviso = Criar("#FFB020");
        private static readonly SolidColorBrush Erro = Criar("#F4212E");

        private static SolidColorBrush Criar(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                NivelLog.Sucesso => Sucesso,
                NivelLog.Aviso => Aviso,
                NivelLog.Erro => Erro,
                _ => Info
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}