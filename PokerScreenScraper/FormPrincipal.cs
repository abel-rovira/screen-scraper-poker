using System.Runtime.InteropServices;
using System.Transactions;

namespace PokerScreenScraper;

public partial class FormPrincipal : Form
{
    private static readonly Color FondoAplicacion = Color.FromArgb(32, 32, 32);
    private static readonly Color FondoPanel = Color.FromArgb(45, 45, 48);
    private static readonly Color FondoCampo = Color.FromArgb(63, 63, 70);
    private static readonly Color TextoPrincipal = Color.FromArgb(241, 241, 241);
    private static readonly Color TextoSecundario = Color.FromArgb(181, 181, 186);
    private static readonly Color Acento = Color.FromArgb(0, 122, 204);
    private static readonly Color AcentoAzul = Color.FromArgb(0, 150, 199);
    private static readonly Color Peligro = Color.FromArgb(231, 72, 86);
    private static readonly Color BordeInput = Color.FromArgb(84, 84, 88);
    private static readonly Color HoverButton = Color.FromArgb(0, 102, 184);
    private static readonly Color HoverDanger = Color.FromArgb(200, 50, 65);

    private readonly NumericUpDown entradaX = new();
    private readonly NumericUpDown entradaY = new();
    private readonly NumericUpDown entradaAncho = new();
    private readonly NumericUpDown entradaAlto = new();
    private readonly NumericUpDown entradaIntervalo = new();

    private readonly TextBox entradaCarpeta = new();
    private readonly TextBox entradaPrefijo = new();

    private readonly Button botonSeleccionarRegion = new();
    private readonly Button botonCapturar = new();
    private readonly Button botonIniciarDetener = new();
    private readonly Button botonElegirCarpeta = new();

    private readonly Label etiquetaEstado = new();
    private readonly Label etiquetaRegion = new();
    private readonly Label etiquetaContador = new();

    private readonly PictureBox vistaPrevia = new();
    private readonly ListBox listaCapturasRecientes = new();
    private readonly System.Windows.Forms.Timer temporizadorCaptura = new();

    private bool capturaAutomaticaActiva;
    private int indiceCaptura = 1;

    public FormPrincipal()
    {
        InitializeComponent();
        ConfigurarDPI();
        ConstruirInterfaz();
        ConfigurarValoresIniciales();

        temporizadorCaptura.Tick += TemporizadorCaptura_Tick;
    }

    private void ConfigurarDPI()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        if (Environment.OSVersion.Version.Major >= 6)
        {
            SetProcessDPIAware();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    private void ConstruirInterfaz()
    {
        BackColor = FondoAplicacion;
        ForeColor = TextoPrincipal;
        Font = new Font("Segoe UI", 9.75F, FontStyle.Regular);
        MinimumSize = new Size(1040, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Poker Screen Scraper - Herramienta profesional";

        var contenedor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(20),
            BackColor = FondoAplicacion,
        };

        contenedor.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        contenedor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(contenedor);

        contenedor.Controls.Add(CrearBarraSuperior(), 0, 0);

        var contenido = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = FondoAplicacion,
        };

        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        contenido.Controls.Add(CrearPanelControles(), 0, 0);
        contenido.Controls.Add(CrearPanelVistaPrevia(), 1, 0);

        contenido.Paint += (s, e) =>
        {
            ControlPaint.DrawBorder(
                e.Graphics,
                contenido.ClientRectangle,
                BordeInput,
                ButtonBorderStyle.Solid);
        };

        contenedor.Controls.Add(contenido, 0, 1);
    }

    private Control CrearBarraSuperior()
    {
        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = FondoAplicacion,
        };

        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

        var bloqueTitulo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = FondoAplicacion,
            Padding = new Padding(0, 2, 16, 4),
        };

        bloqueTitulo.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        bloqueTitulo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        bloqueTitulo.Controls.Add(new Label
        {
            Text = "Poker Screen Scraper",
            Dock = DockStyle.Fill,
            ForeColor = TextoPrincipal,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        }, 0, 0);

        etiquetaEstado.Dock = DockStyle.Fill;
        etiquetaEstado.ForeColor = TextoSecundario;
        etiquetaEstado.Font = new Font("Segoe UI", 9F);
        etiquetaEstado.TextAlign = ContentAlignment.TopLeft;
        etiquetaEstado.AutoEllipsis = true;

        bloqueTitulo.Controls.Add(etiquetaEstado, 0, 1);
        barra.Controls.Add(bloqueTitulo, 0, 0);

        var contadorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FondoAplicacion,
            Padding = new Padding(0, 12, 0, 12),
        };

        contadorPanel.Paint += (s, e) =>
        {
            ControlPaint.DrawBorder(
                e.Graphics,
                new Rectangle(0, 12, contadorPanel.Width - 1, contadorPanel.Height - 25),
                BordeInput,
                ButtonBorderStyle.Solid);
        };

        etiquetaContador.Dock = DockStyle.Fill;
        etiquetaContador.ForeColor = TextoPrincipal;
        etiquetaContador.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        etiquetaContador.TextAlign = ContentAlignment.MiddleCenter;

        contadorPanel.Controls.Add(etiquetaContador);
        barra.Controls.Add(contadorPanel, 1, 0);

        return barra;
    }

    private Control CrearPanelControles()
    {
        var panel = CrearPanel();
        panel.Padding = new Padding(18);
        panel.AutoScroll = true;

        var disposicion = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 0,
            BackColor = FondoPanel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        disposicion.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        disposicion.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

        panel.ClientSizeChanged += (s, e) =>
        {
            disposicion.Width = Math.Max(
                260,
                panel.ClientSize.Width
                - panel.Padding.Left
                - panel.Padding.Right
                - SystemInformation.VerticalScrollBarWidth);
        };

        panel.Controls.Add(disposicion);

        AgregarSeccion(disposicion, "Region de captura", "Define el area de pantalla a capturar.");
        AgregarNumero(disposicion, "Posicion X", entradaX);
        AgregarNumero(disposicion, "Posicion Y", entradaY);
        AgregarNumero(disposicion, "Ancho", entradaAncho);
        AgregarNumero(disposicion, "Alto", entradaAlto);

        botonSeleccionarRegion.Text = "Seleccionar region";
        EstilizarBoton(botonSeleccionarRegion, AcentoAzul);
        botonSeleccionarRegion.FlatAppearance.MouseOverBackColor = HoverButton;
        botonSeleccionarRegion.Click += BotonSeleccionarRegion_Click;
        AgregarFilaCompleta(disposicion, botonSeleccionarRegion, 46);

        etiquetaRegion.Dock = DockStyle.Fill;
        etiquetaRegion.ForeColor = Acento;
        etiquetaRegion.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        etiquetaRegion.TextAlign = ContentAlignment.MiddleLeft;
        etiquetaRegion.Padding = new Padding(0, 6, 0, 8);
        etiquetaRegion.AutoEllipsis = true;
        AgregarFilaCompleta(disposicion, etiquetaRegion, 38);

        AgregarSeccion(disposicion, "Configuracion de salida", "Formato PNG con nombre personalizable.");
        AgregarTexto(disposicion, "Carpeta destino", entradaCarpeta);

        botonElegirCarpeta.Text = "Elegir carpeta";
        EstilizarBoton(botonElegirCarpeta, Color.FromArgb(68, 68, 78));
        botonElegirCarpeta.FlatAppearance.MouseOverBackColor = Color.FromArgb(85, 85, 95);
        botonElegirCarpeta.Click += BotonElegirCarpeta_Click;
        AgregarFilaCompleta(disposicion, botonElegirCarpeta, 44);

        AgregarTexto(disposicion, "Prefijo archivo", entradaPrefijo);
        AgregarNumero(disposicion, "Intervalo (ms)", entradaIntervalo);

        botonCapturar.Text = "Capturar ahora";
        EstilizarBoton(botonCapturar, Acento);
        botonCapturar.FlatAppearance.MouseOverBackColor = HoverButton;
        botonCapturar.Click += BotonCapturar_Click;
        AgregarFilaCompleta(disposicion, botonCapturar, 48);

        botonIniciarDetener.Text = "Iniciar automatico";
        EstilizarBoton(botonIniciarDetener, AcentoAzul);
        botonIniciarDetener.FlatAppearance.MouseOverBackColor = HoverButton;
        botonIniciarDetener.Click += BotonIniciarDetener_Click;
        AgregarFilaCompleta(disposicion, botonIniciarDetener, 48);

        return panel;
    }

    private Control CrearPanelVistaPrevia()
    {
        var contenedor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 0, 0, 0),
            BackColor = FondoAplicacion,
        };

        contenedor.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        contenedor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        contenedor.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));

        contenedor.Controls.Add(new Label
        {
            Text = "Vista previa",
            Dock = DockStyle.Fill,
            ForeColor = TextoPrincipal,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        }, 0, 0);

        var marcoVistaPrevia = CrearPanel();
        marcoVistaPrevia.Padding = new Padding(12);

        marcoVistaPrevia.Paint += (s, e) =>
        {
            ControlPaint.DrawBorder(
                e.Graphics,
                marcoVistaPrevia.ClientRectangle,
                BordeInput,
                ButtonBorderStyle.Solid);
        };

        vistaPrevia.BackColor = Color.FromArgb(20, 20, 25);
        vistaPrevia.BorderStyle = BorderStyle.None;
        vistaPrevia.Dock = DockStyle.Fill;
        vistaPrevia.SizeMode = PictureBoxSizeMode.Zoom;

        marcoVistaPrevia.Controls.Add(vistaPrevia);
        contenedor.Controls.Add(marcoVistaPrevia, 0, 1);

        var panelRecientes = CrearPanel();
        panelRecientes.Margin = new Padding(0, 16, 0, 0);
        panelRecientes.Padding = new Padding(12);

        var disposicionRecientes = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = FondoPanel,
        };

        disposicionRecientes.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        disposicionRecientes.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        disposicionRecientes.Controls.Add(new Label
        {
            Text = "Historial reciente",
            Dock = DockStyle.Fill,
            ForeColor = TextoPrincipal,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        }, 0, 0);

        listaCapturasRecientes.BorderStyle = BorderStyle.FixedSingle;
        listaCapturasRecientes.BackColor = FondoCampo;
        listaCapturasRecientes.ForeColor = TextoPrincipal;
        listaCapturasRecientes.Font = new Font("Segoe UI", 9F);
        listaCapturasRecientes.Dock = DockStyle.Fill;
        listaCapturasRecientes.IntegralHeight = false;
        listaCapturasRecientes.DrawMode = DrawMode.OwnerDrawFixed;
        listaCapturasRecientes.ItemHeight = 22;
        listaCapturasRecientes.DrawItem += ListaCapturasRecientes_DrawItem;
        listaCapturasRecientes.DoubleClick += ListaCapturasRecientes_DoubleClick;

        disposicionRecientes.Controls.Add(listaCapturasRecientes, 0, 1);
        panelRecientes.Controls.Add(disposicionRecientes);
        contenedor.Controls.Add(panelRecientes, 0, 2);

        return contenedor;
    }

    private void ConfigurarValoresIniciales()
    {
        foreach (var entrada in new[] { entradaX, entradaY, entradaAncho, entradaAlto })
        {
            entrada.Maximum = 10000;
            entrada.Dock = DockStyle.Fill;
            EstilizarNumero(entrada);
        }

        entradaAncho.Value = 800;
        entradaAlto.Value = 600;

        entradaIntervalo.Minimum = 100;
        entradaIntervalo.Maximum = 60000;
        entradaIntervalo.Value = 2000;
        entradaIntervalo.Increment = 100;
        entradaIntervalo.Dock = DockStyle.Fill;
        EstilizarNumero(entradaIntervalo);

        entradaCarpeta.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PokerScreenScraper");

        entradaPrefijo.Text = "poker_screenshot";

        EstilizarCajaTexto(entradaCarpeta);
        EstilizarCajaTexto(entradaPrefijo);

        etiquetaEstado.Text = "Listo para capturar. Selecciona una region o introduce coordenadas.";
        ActualizarEtiquetaRegion();
        ActualizarContadorCapturas();
    }

    private static Panel CrearPanel()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FondoPanel,
            Margin = new Padding(0),
        };
    }

    private static void EstilizarBoton(Button boton, Color color)
    {
        boton.Dock = DockStyle.Fill;
        boton.FlatStyle = FlatStyle.Flat;
        boton.FlatAppearance.BorderSize = 0;
        boton.BackColor = color;
        boton.ForeColor = Color.White;
        boton.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        boton.Margin = new Padding(0, 8, 0, 0);
        boton.Cursor = Cursors.Hand;
        boton.TextAlign = ContentAlignment.MiddleCenter;
    }

    private static void EstilizarNumero(NumericUpDown entrada)
    {
        entrada.BackColor = FondoCampo;
        entrada.ForeColor = TextoPrincipal;
        entrada.BorderStyle = BorderStyle.FixedSingle;
        entrada.Margin = new Padding(0, 4, 8, 6);
        entrada.Font = new Font("Segoe UI", 9.75F);
    }

    private static void EstilizarCajaTexto(TextBox entrada)
    {
        entrada.BackColor = FondoCampo;
        entrada.ForeColor = TextoPrincipal;
        entrada.BorderStyle = BorderStyle.FixedSingle;
        entrada.Dock = DockStyle.Fill;
        entrada.Margin = new Padding(0, 4, 0, 6);
        entrada.Font = new Font("Segoe UI", 9.75F);
    }

    private static void AgregarSeccion(TableLayoutPanel panel, string titulo, string subtitulo)
    {
        var bloque = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = FondoPanel,
            Margin = new Padding(0, 10, 0, 4),
        };

        bloque.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        bloque.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        bloque.Controls.Add(new Label
        {
            Text = titulo,
            Dock = DockStyle.Fill,
            ForeColor = TextoPrincipal,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            AutoEllipsis = true,
        }, 0, 0);

        bloque.Controls.Add(new Label
        {
            Text = subtitulo,
            Dock = DockStyle.Fill,
            ForeColor = TextoSecundario,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            AutoEllipsis = true,
        }, 0, 1);

        AgregarFilaCompleta(panel, bloque, 58);
    }

    private static void AgregarNumero(TableLayoutPanel panel, string textoEtiqueta, NumericUpDown entrada)
    {
        var etiqueta = CrearEtiqueta(textoEtiqueta);
        var fila = AgregarFila(panel, 46);
        panel.Controls.Add(etiqueta, 0, fila);
        panel.Controls.Add(entrada, 1, fila);
    }

    private static void AgregarTexto(TableLayoutPanel panel, string textoEtiqueta, TextBox entrada)
    {
        var etiqueta = CrearEtiqueta(textoEtiqueta);
        var fila = AgregarFila(panel, 50);
        panel.Controls.Add(etiqueta, 0, fila);
        panel.Controls.Add(entrada, 1, fila);
    }

    private static Label CrearEtiqueta(string textoEtiqueta)
    {
        return new Label
        {
            Text = textoEtiqueta,
            Dock = DockStyle.Fill,
            ForeColor = TextoSecundario,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 2, 8, 2),
            Font = new Font("Segoe UI", 9.25F),
            AutoEllipsis = true,
        };
    }

    private static void AgregarFilaCompleta(TableLayoutPanel panel, Control control, int alto)
    {
        var fila = AgregarFila(panel, alto);
        panel.Controls.Add(control, 0, fila);
        panel.SetColumnSpan(control, 2);
    }

    private static int AgregarFila(TableLayoutPanel panel, int alto)
    {
        var fila = panel.RowCount;
        panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, alto));
        return fila;
    }

    private void BotonSeleccionarRegion_Click(object? sender, EventArgs e)
    {
        WindowState = FormWindowState.Normal;
        Hide();

        using var selector = new FormSelectorRegion();

        if (selector.ShowDialog() == DialogResult.OK)
        {
            var region = selector.RegionSeleccionada;

            entradaX.Value = Limitar(region.X, entradaX.Minimum, entradaX.Maximum);
            entradaY.Value = Limitar(region.Y, entradaY.Minimum, entradaY.Maximum);
            entradaAncho.Value = Limitar(region.Width, entradaAncho.Minimum, entradaAncho.Maximum);
            entradaAlto.Value = Limitar(region.Height, entradaAlto.Minimum, entradaAlto.Maximum);

            etiquetaEstado.Text = $"Region seleccionada: X={region.X}, Y={region.Y}, {region.Width}x{region.Height}";
            ActualizarEtiquetaRegion();
        }

        Show();
        Activate();
        WindowState = FormWindowState.Normal;
    }

    private void BotonElegirCarpeta_Click(object? sender, EventArgs e)
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta para guardar las capturas PNG",
            UseDescriptionForTitle = true,
        };

        if (Directory.Exists(entradaCarpeta.Text))
        {
            dialogo.SelectedPath = entradaCarpeta.Text;
        }

        if (dialogo.ShowDialog() == DialogResult.OK)
        {
            entradaCarpeta.Text = dialogo.SelectedPath;
            etiquetaEstado.Text = $"Carpeta de salida: {dialogo.SelectedPath}";
        }
    }

    private void BotonCapturar_Click(object? sender, EventArgs e)
    {
        CapturarYGuardar();
    }

    private void BotonIniciarDetener_Click(object? sender, EventArgs e)
    {
        capturaAutomaticaActiva = !capturaAutomaticaActiva;

        if (capturaAutomaticaActiva)
        {
            temporizadorCaptura.Interval = (int)entradaIntervalo.Value;
            temporizadorCaptura.Start();

            botonIniciarDetener.Text = "Detener automatico";
            botonIniciarDetener.BackColor = Peligro;
            botonIniciarDetener.FlatAppearance.MouseOverBackColor = HoverDanger;

            etiquetaEstado.Text = $"Captura automatica activa cada {entradaIntervalo.Value} ms.";
            CapturarYGuardar();
            return;
        }

        temporizadorCaptura.Stop();

        botonIniciarDetener.Text = "Iniciar automatico";
        botonIniciarDetener.BackColor = AcentoAzul;
        botonIniciarDetener.FlatAppearance.MouseOverBackColor = HoverButton;

        etiquetaEstado.Text = "Captura automatica detenida.";
    }

    private void TemporizadorCaptura_Tick(object? sender, EventArgs e)
    {
        CapturarYGuardar();
    }

    private void ListaCapturasRecientes_DoubleClick(object? sender, EventArgs e)
    {
        if (listaCapturasRecientes.SelectedItem is CapturaReciente captura && File.Exists(captura.Ruta))
        {
            using var imagen = Image.FromFile(captura.Ruta);

            vistaPrevia.Image?.Dispose();
            vistaPrevia.Image = new Bitmap(imagen);

            etiquetaEstado.Text = $"Previsualizando: {Path.GetFileName(captura.Ruta)}";
        }
    }

    private void ListaCapturasRecientes_DrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();

        if (e.Index >= 0)
        {
            var texto = listaCapturasRecientes.Items[e.Index].ToString() ?? string.Empty;

            using var pincel = new SolidBrush(TextoPrincipal);
            e.Graphics.DrawString(texto, e.Font ?? Font, pincel, e.Bounds);
        }

        e.DrawFocusRectangle();
    }

    private void CapturarYGuardar()
    {
        var region = new Rectangle(
            (int)entradaX.Value,
            (int)entradaY.Value,
            (int)entradaAncho.Value,
            (int)entradaAlto.Value);

        if (region.Width <= 0 || region.Height <= 0)
        {
            etiquetaEstado.Text = "La region debe tener ancho y alto mayores que cero.";
            return;
        }

        try
        {
            Directory.CreateDirectory(entradaCarpeta.Text);

            using var imagen = new Bitmap(region.Width, region.Height);

            using (var graficos = Graphics.FromImage(imagen))
            {
                graficos.CopyFromScreen(region.Location, Point.Empty, region.Size);
            }

            var nombreArchivo = $"{LimpiarPrefijo(entradaPrefijo.Text)}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{indiceCaptura:0000}.png";
            var rutaArchivo = Path.Combine(entradaCarpeta.Text, nombreArchivo);

            imagen.Save(rutaArchivo, System.Drawing.Imaging.ImageFormat.Png);
            indiceCaptura++;

            vistaPrevia.Image?.Dispose();
            vistaPrevia.Image = (Bitmap)imagen.Clone();

            etiquetaEstado.Text = $"Guardado: {Path.GetFileName(rutaArchivo)}";

            AgregarCapturaReciente(rutaArchivo);
            ActualizarContadorCapturas();
            ActualizarEtiquetaRegion();
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            etiquetaEstado.Text = $"Error: {ex.Message}";
        }
    }

    private void AgregarCapturaReciente(string ruta)
    {
        listaCapturasRecientes.Items.Insert(0, new CapturaReciente(ruta));

        while (listaCapturasRecientes.Items.Count > 15)
        {
            listaCapturasRecientes.Items.RemoveAt(listaCapturasRecientes.Items.Count - 1);
        }
    }

    private void ActualizarEtiquetaRegion()
    {
        etiquetaRegion.Text =
            $"Region activa: X {(int)entradaX.Value} | Y {(int)entradaY.Value} | {(int)entradaAncho.Value} x {(int)entradaAlto.Value} px";
    }

    private void ActualizarContadorCapturas()
    {
        etiquetaContador.Text = $"Capturas: {indiceCaptura - 1}";
    }

    private static decimal Limitar(int valor, decimal minimo, decimal maximo)
    {
        return Math.Min(Math.Max(valor, minimo), maximo);
    }

    private static string LimpiarPrefijo(string valor)
    {
        var caracteresInvalidos = Path.GetInvalidFileNameChars();

        var textoLimpio = new string(
            valor.Select(caracter => caracteresInvalidos.Contains(caracter) ? '_' : caracter).ToArray()
        ).Trim();

        return string.IsNullOrWhiteSpace(textoLimpio) ? "screenshot" : textoLimpio;
    }

    private sealed class CapturaReciente
    {
        public CapturaReciente(string ruta)
        {
            Ruta = ruta;
        }

        public string Ruta { get; }

        public override string ToString()
        {
            return Path.GetFileName(Ruta);
        }
    }
}
