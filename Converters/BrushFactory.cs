using System.Windows.Media;

namespace TransferToolRPA.Converters
{
    /// <summary>
    /// Cria pincéis de cor congelados (thread-safe) a partir de um valor hexadecimal,
    /// centralizando a construção de <see cref="SolidColorBrush"/> compartilhada pela UI.
    /// </summary>
    public static class BrushFactory
    {
        public static SolidColorBrush Criar(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
            brush.Freeze();
            return brush;
        }
    }
}
