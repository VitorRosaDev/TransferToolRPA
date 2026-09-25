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
        private static readonly SolidColorBrush Info = BrushFactory.Criar("#A3E4D7");
        private static readonly SolidColorBrush Sucesso = BrushFactory.Criar("#00BA7C");
        private static readonly SolidColorBrush Aviso = BrushFactory.Criar("#FFB020");
        private static readonly SolidColorBrush Erro = BrushFactory.Criar("#F4212E");

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