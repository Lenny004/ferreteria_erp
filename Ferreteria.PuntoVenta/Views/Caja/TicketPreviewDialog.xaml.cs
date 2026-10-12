using System.Windows;

namespace Ferreteria.PuntoVenta.Views.Caja;

/// <summary>Muestra el texto plano del ticket con fuente monoespaciada.</summary>
/// <remarks>El foco inicia en Cerrar; Enter o Escape cierran la vista previa.</remarks>
public partial class TicketPreviewDialog : Window
{
    /// <summary>Inicializa la ventana con el contenido generado.</summary>
    /// <param name="text">Texto plano del ticket.</param>
    public TicketPreviewDialog(string text)
    {
        InitializeComponent();
        PreviewTextBox.Text = text;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
