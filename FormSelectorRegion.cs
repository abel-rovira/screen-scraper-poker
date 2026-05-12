namespace PokerScreenScraper;

public sealed class FormSelectorRegion : Form
{
    private Point puntoInicial;
    private Point puntoActual;
    private bool estaArrastrando;

    public Rectangle RegionSeleccionada { get; private set; }

    public FormSelectorRegion()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.White;
        Opacity = 0.55;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        TopMost = true;
        KeyPreview = true;
        ShowInTaskbar = false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        estaArrastrando = true;
        puntoInicial = PointToScreen(e.Location);
        puntoActual = puntoInicial;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!estaArrastrando)
        {
            return;
        }

        puntoActual = PointToScreen(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!estaArrastrando || e.Button != MouseButtons.Left)
        {
            return;
        }

        estaArrastrando = false;
        puntoActual = PointToScreen(e.Location);
        RegionSeleccionada = NormalizarRectangulo(puntoInicial, puntoActual);

        if (RegionSeleccionada.Width < 2 || RegionSeleccionada.Height < 2)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (!estaArrastrando)
        {
            DibujarAyuda(e.Graphics);
            return;
        }

        var rectanguloPantalla = NormalizarRectangulo(puntoInicial, puntoActual);
        var rectanguloCliente = RectangleToClient(rectanguloPantalla);
        using var relleno = new SolidBrush(Color.FromArgb(85, Color.FromArgb(46, 204, 113)));
        using var borde = new Pen(Color.FromArgb(46, 204, 113), 3);
        e.Graphics.FillRectangle(relleno, rectanguloCliente);
        e.Graphics.DrawRectangle(borde, rectanguloCliente);

        var textoMedidas = $"{rectanguloPantalla.Width} x {rectanguloPantalla.Height}";
        using var fuente = new Font(SystemFonts.DefaultFont.FontFamily, 14, FontStyle.Bold);
        var tamanoEtiqueta = e.Graphics.MeasureString(textoMedidas, fuente);
        var rectanguloEtiqueta = new RectangleF(
            rectanguloCliente.Left,
            Math.Max(8, rectanguloCliente.Top - tamanoEtiqueta.Height - 10),
            tamanoEtiqueta.Width + 18,
            tamanoEtiqueta.Height + 8);
        using var fondoEtiqueta = new SolidBrush(Color.FromArgb(230, 18, 22, 28));
        using var textoEtiqueta = new SolidBrush(Color.White);
        e.Graphics.FillRectangle(fondoEtiqueta, rectanguloEtiqueta);
        e.Graphics.DrawString(textoMedidas, fuente, textoEtiqueta, rectanguloEtiqueta.Left + 9, rectanguloEtiqueta.Top + 4);
    }

    private void DibujarAyuda(Graphics graficos)
    {
        const string ayuda = "Arrastra para seleccionar la region. ESC cancela.";
        using var fuente = new Font(SystemFonts.DefaultFont.FontFamily, 16, FontStyle.Bold);
        var tamano = graficos.MeasureString(ayuda, fuente);
        var punto = new PointF((ClientSize.Width - tamano.Width) / 2, 36);
        using var fondo = new SolidBrush(Color.FromArgb(210, 18, 22, 28));
        using var pincel = new SolidBrush(Color.White);
        graficos.FillRectangle(fondo, punto.X - 16, punto.Y - 10, tamano.Width + 32, tamano.Height + 20);
        graficos.DrawString(ayuda, fuente, pincel, punto);
    }

    private static Rectangle NormalizarRectangulo(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var ancho = Math.Abs(a.X - b.X);
        var alto = Math.Abs(a.Y - b.Y);
        return new Rectangle(x, y, ancho, alto);
    }
}
