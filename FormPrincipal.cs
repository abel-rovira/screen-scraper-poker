using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

namespace PokerScreenScraper
{
    public partial class FormPrincipal : Form
    {
        #region Constantes y Campos
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
        private static readonly Lazy<string?> RutaTesseract = new(BuscarTesseract);

        private readonly NumericUpDown entradaX = new();
        private readonly NumericUpDown entradaY = new();
        private readonly NumericUpDown entradaAncho = new();
        private readonly NumericUpDown entradaAlto = new();
        private readonly NumericUpDown entradaIntervalo = new();
        private readonly NumericUpDown entradaRivales = new();
        private readonly NumericUpDown entradaUmbral = new();

        private readonly TextBox entradaCarpeta = new();
        private readonly TextBox entradaPrefijo = new();
        private readonly TextBox entradaCartasJugador = new();
        private readonly TextBox entradaCartasMesa = new();

        private readonly Button botonSeleccionarRegion = new();
        private readonly Button botonCapturar = new();
        private readonly Button botonIniciarDetener = new();
        private readonly Button botonElegirCarpeta = new();
        private readonly Button botonCalcularProbabilidad = new();
        private readonly Button botonAnalizarCartas = new();
        private readonly Button botonSeleccionarCartasJugador = new();
        private readonly Button botonSeleccionarCartasMesa = new();
        private readonly Button botonBorrarPerfil = new();

        private readonly Label etiquetaEstado = new();
        private readonly Label etiquetaRegion = new();
        private readonly Label etiquetaContador = new();
        private readonly Label etiquetaProbabilidad = new();
        private readonly Label etiquetaCartasDetectadas = new();
        private readonly Label etiquetaPerfil = new();

        private readonly PictureBox vistaPrevia = new();
        private readonly ListBox listaCapturasRecientes = new();
        private readonly System.Windows.Forms.Timer temporizadorCaptura = new();
        private readonly NotifyIcon iconoBandeja = new();
        private readonly ContextMenuStrip menuBandeja = new();

        private bool capturaAutomaticaActiva;
        private bool salirAplicacion;
        private bool capturaEnCurso;
        private int indiceCaptura = 1;
        private string textoProbabilidadActual = "Probabilidad no calculada";
        private string textoManoActual = "Mano no calculada";
        private string textoCartasActuales = "Cartas no detectadas";
        private string ultimoErrorLectura = string.Empty;
        private Bitmap? ultimaImagenCapturada;
        private AppSettings configuracion = new();
        private DateTime ultimaAutodeteccionPantalla = DateTime.MinValue;
        private string ultimaRutaCaptura = string.Empty;
        private MiniApiServer? miniApi;
        #endregion

        #region Modelos de Datos
        public record Card(int Value, Suit Suit);
        public record HandValue(int Fuerza, string Descripcion);
        public record OddsResult(double ProbabilidadGanar, double ProbabilidadPerder, double ProbabilidadEmpatar, string DescripcionMano);

        public enum Suit
        {
            Corazones,
            Diamantes,
            Treboles,
            Picas
        }

        private sealed class AppSettings
        {
            public CaptureSettings Capture { get; set; } = new();
            public GameProfileSettings GameProfile { get; set; } = new();
        }

        private sealed class CaptureSettings
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private sealed class GameProfileSettings
        {
            public NormalizedRegion? PlayerCards { get; set; }
            public NormalizedRegion? BoardCards { get; set; }
        }

        private sealed class NormalizedRegion
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }
        #endregion

        #region Constructor e InicializaciÃ³n
        public FormPrincipal()
        {
            InitializeComponent();
            ConfigurarDPI();
            ConstruirInterfaz();
            ConfigurarValoresIniciales();
            ConfigurarCalculoAutomatico();
            ConfigurarBandejaSistema();
            IniciarMiniApiLocal();

            temporizadorCaptura.Tick += async (_, _) => await CapturarYGuardarAsync();
            Shown += FormPrincipal_Shown;
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
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

        private void ConfigurarBandejaSistema()
        {
            menuBandeja.Items.Add("Mostrar ventana", null, (_, _) => MostrarVentana());
            menuBandeja.Items.Add("Capturar ahora", null, async (_, _) => await CapturarYGuardarAsync());
            menuBandeja.Items.Add("Salir", null, (_, _) => Salir());

            iconoBandeja.Icon = SystemIcons.Application;
            iconoBandeja.Text = "Poker Screen Scraper";
            iconoBandeja.ContextMenuStrip = menuBandeja;
            iconoBandeja.Visible = true;
            iconoBandeja.BalloonTipTitle = "Poker Screen Scraper";
            iconoBandeja.DoubleClick += (_, _) => MostrarVentana();
        }
        #endregion

        #region Interfaz de Usuario
        private void ConstruirInterfaz()
        {
            BackColor = FondoAplicacion;
            ForeColor = TextoPrincipal;
            Font = new Font("Segoe UI", 9.75F, FontStyle.Regular);
            MinimumSize = new Size(1040, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Poker Screen Scraper";

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
            contenido.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, contenido.ClientRectangle, BordeInput, ButtonBorderStyle.Solid);

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

            etiquetaContador.Dock = DockStyle.Fill;
            etiquetaContador.ForeColor = TextoPrincipal;
            etiquetaContador.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
            etiquetaContador.TextAlign = ContentAlignment.MiddleCenter;
            barra.Controls.Add(etiquetaContador, 1, 0);

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
            panel.ClientSizeChanged += (_, _) =>
            {
                disposicion.Width = Math.Max(260, panel.ClientSize.Width - panel.Padding.Left - panel.Padding.Right - SystemInformation.VerticalScrollBarWidth);
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

            AgregarSeccion(disposicion, "Perfil del juego", "Selecciona zonas una vez y luego funciona automatico.");

            botonSeleccionarCartasJugador.Text = "Seleccionar mis cartas";
            EstilizarBoton(botonSeleccionarCartasJugador, Color.FromArgb(78, 128, 88));
            botonSeleccionarCartasJugador.FlatAppearance.MouseOverBackColor = Color.FromArgb(92, 150, 104);
            botonSeleccionarCartasJugador.Click += (_, _) => SeleccionarZonaPerfil(true);
            AgregarFilaCompleta(disposicion, botonSeleccionarCartasJugador, 46);

            botonSeleccionarCartasMesa.Text = "Seleccionar mesa";
            EstilizarBoton(botonSeleccionarCartasMesa, Color.FromArgb(78, 128, 88));
            botonSeleccionarCartasMesa.FlatAppearance.MouseOverBackColor = Color.FromArgb(92, 150, 104);
            botonSeleccionarCartasMesa.Click += (_, _) => SeleccionarZonaPerfil(false);
            AgregarFilaCompleta(disposicion, botonSeleccionarCartasMesa, 46);

            botonBorrarPerfil.Text = "Borrar perfil";
            EstilizarBoton(botonBorrarPerfil, Color.FromArgb(68, 68, 78));
            botonBorrarPerfil.FlatAppearance.MouseOverBackColor = Color.FromArgb(85, 85, 95);
            botonBorrarPerfil.Click += (_, _) => BorrarPerfil();
            AgregarFilaCompleta(disposicion, botonBorrarPerfil, 42);

            etiquetaPerfil.Dock = DockStyle.Fill;
            etiquetaPerfil.ForeColor = TextoSecundario;
            etiquetaPerfil.Font = new Font("Segoe UI", 8.75F, FontStyle.Bold);
            etiquetaPerfil.TextAlign = ContentAlignment.MiddleLeft;
            etiquetaPerfil.Padding = new Padding(0, 4, 0, 8);
            etiquetaPerfil.AutoEllipsis = true;
            AgregarFilaCompleta(disposicion, etiquetaPerfil, 48);

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

            AgregarSeccion(disposicion, "Analisis educativo", "Procesa la imagen de la vista previa.");
            AgregarNumero(disposicion, "Umbral color", entradaUmbral);

            botonAnalizarCartas.Text = "Analizar imagen";
            EstilizarBoton(botonAnalizarCartas, Color.FromArgb(78, 128, 88));
            botonAnalizarCartas.FlatAppearance.MouseOverBackColor = Color.FromArgb(92, 150, 104);
            botonAnalizarCartas.Click += async (_, _) => await BotonAnalizarCartasAsync();
            AgregarFilaCompleta(disposicion, botonAnalizarCartas, 48);

            etiquetaCartasDetectadas.Dock = DockStyle.Fill;
            etiquetaCartasDetectadas.ForeColor = TextoPrincipal;
            etiquetaCartasDetectadas.Font = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold);
            etiquetaCartasDetectadas.TextAlign = ContentAlignment.MiddleLeft;
            etiquetaCartasDetectadas.Padding = new Padding(0, 4, 0, 4);
            etiquetaCartasDetectadas.AutoEllipsis = true;
            AgregarFilaCompleta(disposicion, etiquetaCartasDetectadas, 50);

            AgregarSeccion(disposicion, "Probabilidad de poker", "Texas Hold'em: escribe cartas visibles.");
            AgregarTexto(disposicion, "Tus cartas", entradaCartasJugador);
            AgregarTexto(disposicion, "Mesa", entradaCartasMesa);
            AgregarAyudaFormatoCartas(disposicion);
            AgregarNumero(disposicion, "Rivales", entradaRivales);

            botonCalcularProbabilidad.Text = "Calcular probabilidad";
            EstilizarBoton(botonCalcularProbabilidad, Color.FromArgb(78, 128, 88));
            botonCalcularProbabilidad.FlatAppearance.MouseOverBackColor = Color.FromArgb(92, 150, 104);
            botonCalcularProbabilidad.Click += BotonCalcularProbabilidad_Click;
            AgregarFilaCompleta(disposicion, botonCalcularProbabilidad, 48);

            etiquetaProbabilidad.Dock = DockStyle.Fill;
            etiquetaProbabilidad.ForeColor = TextoPrincipal;
            etiquetaProbabilidad.Font = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold);
            etiquetaProbabilidad.TextAlign = ContentAlignment.MiddleLeft;
            etiquetaProbabilidad.Padding = new Padding(0, 4, 0, 4);
            etiquetaProbabilidad.AutoEllipsis = true;
            AgregarFilaCompleta(disposicion, etiquetaProbabilidad, 70);

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
            marcoVistaPrevia.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, marcoVistaPrevia.ClientRectangle, BordeInput, ButtonBorderStyle.Solid);

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

            entradaX.Minimum = -10000;
            entradaY.Minimum = -10000;
            entradaAncho.Minimum = 1;
            entradaAlto.Minimum = 1;

            var pantalla = ObtenerPantallaCapturaInicial();
            entradaX.Value = Limitar(pantalla.X, entradaX.Minimum, entradaX.Maximum);
            entradaY.Value = Limitar(pantalla.Y, entradaY.Minimum, entradaY.Maximum);
            entradaAncho.Value = Limitar(pantalla.Width, entradaAncho.Minimum, entradaAncho.Maximum);
            entradaAlto.Value = Limitar(pantalla.Height, entradaAlto.Minimum, entradaAlto.Maximum);

            entradaIntervalo.Minimum = 100;
            entradaIntervalo.Maximum = 60000;
            entradaIntervalo.Value = 15000;
            entradaIntervalo.Increment = 100;
            entradaIntervalo.Dock = DockStyle.Fill;
            EstilizarNumero(entradaIntervalo);

            entradaRivales.Minimum = 1;
            entradaRivales.Maximum = 8;
            entradaRivales.Value = 1;
            entradaRivales.Dock = DockStyle.Fill;
            EstilizarNumero(entradaRivales);

            entradaUmbral.Minimum = 0;
            entradaUmbral.Maximum = 255;
            entradaUmbral.Value = 150;
            entradaUmbral.Dock = DockStyle.Fill;
            EstilizarNumero(entradaUmbral);

            entradaCarpeta.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PokerScreenScraper");
            entradaPrefijo.Text = "poker_screenshot";
            entradaCartasJugador.Text = string.Empty;
            entradaCartasMesa.Text = string.Empty;

            EstilizarCajaTexto(entradaCarpeta);
            EstilizarCajaTexto(entradaPrefijo);
            EstilizarCajaTexto(entradaCartasJugador);
            EstilizarCajaTexto(entradaCartasMesa);

            etiquetaProbabilidad.Text = "Cartas no detectadas.";
            etiquetaCartasDetectadas.Text = "Analisis educativo listo.";
            etiquetaEstado.Text = "Listo. Region inicial configurada en la pantalla principal.";
            CargarConfiguracion();
            AutodetectarPantallaPoker(true);
            ActualizarEtiquetaRegion();
            ActualizarEtiquetaPerfil();
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
                Font = new Font("Segoe UI", 8.5F),
                AutoEllipsis = true,
            }, 0, 1);

            AgregarFilaCompleta(panel, bloque, 58);
        }

        private static void AgregarNumero(TableLayoutPanel panel, string textoEtiqueta, NumericUpDown entrada)
        {
            var fila = AgregarFila(panel, 46);
            panel.Controls.Add(CrearEtiqueta(textoEtiqueta), 0, fila);
            panel.Controls.Add(entrada, 1, fila);
        }

        private static void AgregarTexto(TableLayoutPanel panel, string textoEtiqueta, TextBox entrada)
        {
            var fila = AgregarFila(panel, 50);
            panel.Controls.Add(CrearEtiqueta(textoEtiqueta), 0, fila);
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

        private static void AgregarAyudaFormatoCartas(TableLayoutPanel panel)
        {
            var ayuda = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = TextoSecundario,
                BackColor = FondoPanel,
                Font = new Font("Consolas", 8.75F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 4, 0, 4),
                AutoEllipsis = false,
                Text = "Formato: valor+palo separados por espacio\r\nValores: 2 3 4 5 6 7 8 9 T J Q K A\r\nPalos: h=♥ corazones  d=♦ diamantes  s=♠ picas  c=♣ treboles\r\nEjemplos: Tus cartas 2s 8d  |  Mesa Ah Kd Qc",
            };

            AgregarFilaCompleta(panel, ayuda, 92);
        }

        private static int AgregarFila(TableLayoutPanel panel, int alto)
        {
            var fila = panel.RowCount;
            panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, alto));
            return fila;
        }
        #endregion

        #region Event Handlers
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
                GuardarConfiguracion();
            }

            Show();
            Activate();
            WindowState = FormWindowState.Normal;
        }

        private void SeleccionarZonaPerfil(bool esJugador)
        {
            var regionCaptura = ObtenerRegionCapturaActual();
            WindowState = FormWindowState.Normal;
            Hide();

            using var selector = new FormSelectorRegion();
            if (selector.ShowDialog() == DialogResult.OK)
            {
                var seleccion = Rectangle.Intersect(selector.RegionSeleccionada, regionCaptura);
                if (seleccion.Width <= 0 || seleccion.Height <= 0)
                {
                    etiquetaEstado.Text = "La zona seleccionada debe estar dentro de la region de captura.";
                }
                else
                {
                    var normalizada = NormalizarRegion(seleccion, regionCaptura);
                    if (esJugador)
                    {
                        configuracion.GameProfile.PlayerCards = normalizada;
                        etiquetaEstado.Text = "Zona de tus cartas guardada.";
                    }
                    else
                    {
                        configuracion.GameProfile.BoardCards = normalizada;
                        etiquetaEstado.Text = "Zona de mesa guardada.";
                    }

                    GuardarConfiguracion();
                    ActualizarEtiquetaPerfil();
                }
            }

            Show();
            Activate();
            WindowState = FormWindowState.Normal;
        }

        private void BorrarPerfil()
        {
            configuracion.GameProfile.PlayerCards = null;
            configuracion.GameProfile.BoardCards = null;
            GuardarConfiguracion();
            ActualizarEtiquetaPerfil();
            etiquetaEstado.Text = "Perfil borrado. Se usara deteccion automatica.";
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

        private async void BotonCapturar_Click(object? sender, EventArgs e)
            => await CapturarYGuardarAsync();

        private void BotonIniciarDetener_Click(object? sender, EventArgs e)
        {
            if (!capturaAutomaticaActiva)
            {
                IniciarCapturaAutomatica();
                return;
            }

            capturaAutomaticaActiva = false;
            temporizadorCaptura.Stop();
            botonIniciarDetener.Text = "Iniciar automatico";
            botonIniciarDetener.BackColor = AcentoAzul;
            botonIniciarDetener.FlatAppearance.MouseOverBackColor = HoverButton;
            etiquetaEstado.Text = "Captura automatica detenida.";
        }

        private void BotonCalcularProbabilidad_Click(object? sender, EventArgs e)
        {
            CalcularProbabilidad(true);
        }

        private async Task BotonAnalizarCartasAsync()
        {
            if (vistaPrevia.Image == null)
            {
                etiquetaEstado.Text = "No hay imagen para analizar. Captura una imagen primero.";
                return;
            }

            using var imagen = new Bitmap(vistaPrevia.Image);
            etiquetaEstado.Text = "Analizando imagen con API local...";

            await AnalizarCartasYActualizarProbabilidadAsync(imagen, true);
        }

        private void ConfigurarCalculoAutomatico()
        {
            entradaCartasJugador.TextChanged += (_, _) => ActualizarResultadoManual();
            entradaCartasMesa.TextChanged += (_, _) => ActualizarResultadoManual();
            entradaRivales.ValueChanged += (_, _) => ActualizarResultadoManual();
            CalcularProbabilidad(false);
        }

        private void ActualizarResultadoManual()
        {
            CalcularProbabilidad(false);
            etiquetaCartasDetectadas.Text = $"Manual: Jugador {entradaCartasJugador.Text.Trim()} | Mesa {entradaCartasMesa.Text.Trim()}";
            textoCartasActuales = $"Jugador {entradaCartasJugador.Text.Trim()} | Mesa {entradaCartasMesa.Text.Trim()}";
            GuardarResultadoManualEnImagen();
        }

        private void GuardarResultadoManualEnImagen()
        {
            try
            {
                using var baseImagen = ultimaImagenCapturada is not null
                    ? new Bitmap(ultimaImagenCapturada)
                    : new Bitmap(Math.Max(900, (int)entradaAncho.Value), Math.Max(520, (int)entradaAlto.Value));

                if (ultimaImagenCapturada is null)
                {
                    using var g = Graphics.FromImage(baseImagen);
                    g.Clear(Color.FromArgb(20, 80, 35));
                }

                DibujarProbabilidadEnCaptura(baseImagen);
                if (DebugCartasActivo())
                {
                    GuardarUltimoResultadoDebug(baseImagen);
                }
            }
            catch
            {
                // La imagen de resultado no debe bloquear la edicion manual.
            }
        }

        private void FormPrincipal_Shown(object? sender, EventArgs e)
        {
            BeginInvoke((Action)(() =>
            {
                OcultarEnSegundoPlano();
                IniciarCapturaAutomatica();
            }));
        }

        private void TemporizadorCaptura_Tick(object? sender, EventArgs e)
        {
            _ = CapturarYGuardarAsync();
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!salirAplicacion)
            {
                e.Cancel = true;
                OcultarEnSegundoPlano();
                etiquetaEstado.Text = "La aplicacion sigue capturando en segundo plano.";
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            miniApi?.Dispose();
            temporizadorCaptura.Dispose();
            iconoBandeja.Dispose();
            menuBandeja.Dispose();
            vistaPrevia.Image?.Dispose();
            ultimaImagenCapturada?.Dispose();
            base.OnFormClosed(e);
        }
        #endregion

        #region Funcionalidad Principal

        private async Task CapturarYGuardarAsync()
        {
            if (capturaEnCurso)
            {
                return;
            }

            capturaEnCurso = true;
            await Task.Yield();

            AutodetectarPantallaPoker(false);
            var region = ObtenerRegionCapturaActual();

            if (region.Width <= 0 || region.Height <= 0)
            {
                etiquetaEstado.Text = "La region debe tener ancho y alto mayores que cero.";
                capturaEnCurso = false;
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

                await AnalizarCartasYActualizarProbabilidadAsync(imagen, false);
                CalcularProbabilidad(false);
                DibujarProbabilidadEnCaptura(imagen);
                if (DebugCartasActivo())
                {
                    GuardarUltimoResultadoDebug(imagen);
                }

                var nombreArchivo = $"{LimpiarPrefijo(entradaPrefijo.Text)}" +
                                    $"_{DateTime.Now:yyyyMMdd_HHmmss_fff}" +
                                    $"_{indiceCaptura:0000}.png";
                var rutaArchivo = Path.Combine(entradaCarpeta.Text, nombreArchivo);
                imagen.Save(rutaArchivo, ImageFormat.Png);
                ultimaRutaCaptura = rutaArchivo;
                indiceCaptura++;

                vistaPrevia.Image?.Dispose();
                vistaPrevia.Image = new Bitmap(imagen);
                ultimaImagenCapturada?.Dispose();
                ultimaImagenCapturada = new Bitmap(imagen);

                etiquetaEstado.Text = $"Guardado: {Path.GetFileName(rutaArchivo)} | {textoProbabilidadActual}";
                ActualizarBandejaConResultado();
                AgregarCapturaReciente(rutaArchivo);
                ActualizarContadorCapturas();
                ActualizarEtiquetaRegion();
            }
            catch (Exception ex) when (ex is ExternalException or ArgumentException
                                            or IOException or UnauthorizedAccessException)
            {
                etiquetaEstado.Text = $"Error: {ex.Message}";
            }
            finally
            {
                capturaEnCurso = false;
            }
        }

        private async Task AnalizarCartasYActualizarProbabilidadAsync(Bitmap imagen, bool mostrarErrores)
        {
            await Task.Yield();

            var cartas = AnalizarCartasEnImagen(imagen, (int)entradaUmbral.Value);
            ultimoErrorLectura = string.Empty;

            if (cartas.Jugador.Count != 2)
            {
                if (HayLecturaAnteriorValida())
                {
                    CalcularProbabilidad(false);
                    etiquetaCartasDetectadas.Text = $"Ultima lectura valida: {entradaCartasJugador.Text.Trim()} | Mesa {entradaCartasMesa.Text.Trim()}";
                    etiquetaEstado.Text = "Lectura actual no valida. Manteniendo ultima probabilidad calculada.";
                    return;
                }

                textoProbabilidadActual = "Esperando lectura valida";
                textoManoActual = "Aun no hay 2 cartas fiables";
                textoCartasActuales = "Sin lectura valida";
                etiquetaProbabilidad.Text = textoProbabilidadActual;
                etiquetaCartasDetectadas.Text = "Esperando cartas legibles para calcular.";

                if (mostrarErrores)
                {
                    etiquetaEstado.Text = "No se detectaron exactamente 2 cartas del jugador.";
                }

                return;
            }

            entradaCartasJugador.Text = string.Join(' ', cartas.Jugador);
            entradaCartasMesa.Text = string.Join(' ', cartas.Mesa);
            textoCartasActuales = $"Jugador {entradaCartasJugador.Text} | Mesa {entradaCartasMesa.Text}";
            etiquetaCartasDetectadas.Text = $"Lectura OK: Jugador {entradaCartasJugador.Text} | Mesa {entradaCartasMesa.Text}";
            ultimoErrorLectura = string.Empty;
            CalcularProbabilidad(mostrarErrores);
            etiquetaEstado.Text = "Cartas analizadas y probabilidad actualizada.";
        }

        private bool HayLecturaAnteriorValida()
        {
            try
            {
                PokerOddsCalculator.Calcular(entradaCartasJugador.Text, entradaCartasMesa.Text, (int)entradaRivales.Value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void DibujarProbabilidadEnCaptura(Bitmap imagen)
        {
            using var graficos = Graphics.FromImage(imagen);
            using var fuenteTitulo = new Font("Segoe UI Semibold", 15F, FontStyle.Bold);
            using var fuenteTexto = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            using var fuentePequena = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            using var fondo = new SolidBrush(Color.FromArgb(205, 18, 18, 22));
            using var textoPrincipal = new SolidBrush(Color.White);
            using var textoAcento = new SolidBrush(Color.FromArgb(120, 230, 145));
            using var textoAviso = new SolidBrush(Color.FromArgb(255, 210, 105));

            var cartasJugador = string.IsNullOrWhiteSpace(entradaCartasJugador.Text)
                ? "No detectadas"
                : FormatearCartasVisual(entradaCartasJugador.Text);
            var cartasMesa = string.IsNullOrWhiteSpace(entradaCartasMesa.Text)
                ? "Sin mesa"
                : FormatearCartasVisual(entradaCartasMesa.Text);
            var probabilidad = string.IsNullOrWhiteSpace(textoProbabilidadActual)
                ? "Esperando cartas validas"
                : textoProbabilidadActual;

            var ancho = Math.Min(520, Math.Max(360, imagen.Width / 2));
            var rectangulo = new Rectangle(12, 12, ancho, 198);

            graficos.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graficos.FillRectangle(fondo, rectangulo);
            ControlPaint.DrawBorder(graficos, rectangulo, Color.FromArgb(120, 230, 145), ButtonBorderStyle.Solid);
            graficos.DrawString("Poker odds", fuenteTitulo, textoAcento, rectangulo.Left + 12, rectangulo.Top + 10);
            graficos.DrawString($"Cartas: {cartasJugador}", fuenteTexto, textoPrincipal, rectangulo.Left + 12, rectangulo.Top + 42);
            graficos.DrawString($"Mesa: {cartasMesa}", fuenteTexto, textoPrincipal, rectangulo.Left + 12, rectangulo.Top + 68);
            graficos.DrawString(probabilidad, fuenteTexto, textoAcento, rectangulo.Left + 12, rectangulo.Top + 96);
            graficos.DrawString($"Mano: {textoManoActual}", fuenteTexto, textoPrincipal, rectangulo.Left + 12, rectangulo.Top + 122);
            graficos.DrawString($"Rivales: {(int)entradaRivales.Value}", fuenteTexto, textoPrincipal, rectangulo.Left + 12, rectangulo.Top + 148);

            if (cartasJugador == "No detectadas")
            {
                graficos.DrawString("Pulsa Analizar imagen o introduce cartas", fuentePequena, textoAviso, rectangulo.Left + 12, rectangulo.Top + 172);
            }
        }

        private static string FormatearCartasVisual(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            return string.Join(' ', texto
                .Split(new[] { ' ', ',', ';', '/', '|', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(FormatearCartaVisual));
        }

        private static string FormatearCartaVisual(string carta)
        {
            var limpia = carta.Trim();
            if (limpia.Length < 2)
            {
                return limpia;
            }

            var valor = limpia[..^1].ToUpperInvariant().Replace("10", "T", StringComparison.OrdinalIgnoreCase);
            var palo = char.ToLowerInvariant(limpia[^1]) switch
            {
                'h' => "♥",
                'd' => "♦",
                's' => "♠",
                'c' => "♣",
                _ => limpia[^1].ToString()
            };

            return $"{valor}{palo}";
        }

        private void GuardarUltimoResultadoDebug(Bitmap imagen)
        {
            try
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "PokerScreenScraper",
                    "debug");
                Directory.CreateDirectory(carpeta);

                var rutaImagen = Path.Combine(carpeta, "last-result.png");
                imagen.Save(rutaImagen, ImageFormat.Png);

            }
            catch
            {
                // El debug no debe bloquear la captura automatica.
            }
        }

        private void CalcularProbabilidad(bool mostrarErrores)
        {
            try
            {
                var resultado = PokerOddsCalculator.Calcular(entradaCartasJugador.Text, entradaCartasMesa.Text, (int)entradaRivales.Value);
                textoProbabilidadActual = $"Ganar: {resultado.ProbabilidadGanar:P1} | Perder: {resultado.ProbabilidadPerder:P1} | Empate: {resultado.ProbabilidadEmpatar:P1}";
                textoManoActual = resultado.DescripcionMano;
                textoCartasActuales = $"Jugador {entradaCartasJugador.Text.Trim()} | Mesa {entradaCartasMesa.Text.Trim()}";
                etiquetaProbabilidad.Text = textoProbabilidadActual;
                etiquetaEstado.Text = $"Mejor mano actual: {resultado.DescripcionMano}";
            }
            catch (ArgumentException ex)
            {
                textoProbabilidadActual = "Esperando cartas validas";
                textoManoActual = ex.Message;
                textoCartasActuales = $"Jugador {entradaCartasJugador.Text.Trim()} | Mesa {entradaCartasMesa.Text.Trim()}";
                etiquetaProbabilidad.Text = textoProbabilidadActual;

                if (!mostrarErrores)
                {
                    return;
                }

                etiquetaProbabilidad.Text = "No se pudo calcular.";
                etiquetaEstado.Text = ex.Message;
            }
        }

        private void IniciarCapturaAutomatica()
        {
            capturaAutomaticaActiva = true;
            entradaIntervalo.Value = 5000;
            temporizadorCaptura.Interval = 5000;
            temporizadorCaptura.Start();
            botonIniciarDetener.Text = "Detener automatico";
            botonIniciarDetener.BackColor = Peligro;
            botonIniciarDetener.FlatAppearance.MouseOverBackColor = HoverDanger;
            etiquetaEstado.Text = "Captura automatica activa cada 5 segundos.";
            _ = CapturarYGuardarAsync();
        }

        private void OcultarEnSegundoPlano()
        {
            Hide();
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
        }

        private void MostrarVentana()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ActualizarBandejaConResultado()
        {
            var resumen = $"{textoCartasActuales} | {textoProbabilidadActual}";
            iconoBandeja.Text = resumen.Length > 63 ? resumen[..63] : resumen;

            if (textoProbabilidadActual.Contains("Ganar:", StringComparison.OrdinalIgnoreCase))
            {
                iconoBandeja.BalloonTipText = resumen.Length > 255 ? resumen[..255] : resumen;
                iconoBandeja.ShowBalloonTip(3500);
            }
        }

        private void Salir()
        {
            salirAplicacion = true;
            temporizadorCaptura.Stop();
            miniApi?.Dispose();
            iconoBandeja.Visible = false;
            Close();
        }

        private void IniciarMiniApiLocal()
        {
            try
            {
                miniApi = new MiniApiServer(
                    5056,
                    () => BeginInvoke((Action)IniciarCapturaAutomatica),
                    () => BeginInvoke((Action)DetenerCapturaAutomaticaDesdeApi),
                    () => BeginInvoke((Action)(async () => await CapturarYGuardarAsync())),
                    CrearEstadoMiniApi);
                miniApi.Start();
            }
            catch
            {
                miniApi = null;
            }
        }

        private void DetenerCapturaAutomaticaDesdeApi()
        {
            capturaAutomaticaActiva = false;
            temporizadorCaptura.Stop();
            botonIniciarDetener.Text = "Iniciar automatico";
            botonIniciarDetener.BackColor = AcentoAzul;
            botonIniciarDetener.FlatAppearance.MouseOverBackColor = HoverButton;
            etiquetaEstado.Text = "Captura automatica detenida desde API local.";
        }

        private object CrearEstadoMiniApi()
        {
            return new
            {
                running = capturaAutomaticaActiva,
                captureInProgress = capturaEnCurso,
                intervalMs = temporizadorCaptura.Interval,
                captures = indiceCaptura - 1,
                cards = textoCartasActuales,
                odds = textoProbabilidadActual,
                hand = textoManoActual,
                lastError = ultimoErrorLectura,
                lastCapture = ultimaRutaCaptura,
                control = "http://127.0.0.1:5056",
                endpoints = new[] { "GET /status", "POST /start", "POST /stop", "POST /capture" }
            };
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
            var region = ObtenerRegionCapturaActual();
            var pantalla = Screen.AllScreens
                .OrderByDescending(screen => Rectangle.Intersect(screen.Bounds, region).Width * Rectangle.Intersect(screen.Bounds, region).Height)
                .FirstOrDefault();
            var nombrePantalla = pantalla is null
                ? "pantalla desconocida"
                : pantalla.Primary ? "pantalla principal" : "pantalla secundaria";

            etiquetaRegion.Text = $"Region activa ({nombrePantalla}): X {(int)entradaX.Value} | Y {(int)entradaY.Value} | {(int)entradaAncho.Value} x {(int)entradaAlto.Value} px";
        }

        private void AutodetectarPantallaPoker(bool forzar)
        {
            if (!forzar && DateTime.Now - ultimaAutodeteccionPantalla < TimeSpan.FromSeconds(10))
            {
                return;
            }

            ultimaAutodeteccionPantalla = DateTime.Now;

            try
            {
                if (TryObtenerVentanaPoker(out var ventanaPoker))
                {
                    AplicarRegionAutodetectada(ventanaPoker, "ventana de poker detectada");
                    return;
                }

                var mejor = Screen.AllScreens
                    .Select(screen => new { Screen = screen, Score = PuntuarPantallaPoker(screen.Bounds) })
                    .OrderByDescending(item => item.Score)
                    .FirstOrDefault();

                if (mejor is null || mejor.Score < 80)
                {
                    return;
                }

                AplicarRegionAutodetectada(mejor.Screen.Bounds, "pantalla de poker detectada");
            }
            catch
            {
                // Si Windows bloquea una captura puntual, seguimos con la region actual.
            }
        }

        private void AplicarRegionAutodetectada(Rectangle region, string origen)
        {
            if (region.Width <= 50 || region.Height <= 50)
            {
                return;
            }

            if ((int)entradaX.Value == region.X
                && (int)entradaY.Value == region.Y
                && (int)entradaAncho.Value == region.Width
                && (int)entradaAlto.Value == region.Height)
            {
                return;
            }

            entradaX.Value = Limitar(region.X, entradaX.Minimum, entradaX.Maximum);
            entradaY.Value = Limitar(region.Y, entradaY.Minimum, entradaY.Maximum);
            entradaAncho.Value = Limitar(region.Width, entradaAncho.Minimum, entradaAncho.Maximum);
            entradaAlto.Value = Limitar(region.Height, entradaAlto.Minimum, entradaAlto.Maximum);
            etiquetaEstado.Text = $"{origen}: X={region.X}, Y={region.Y}, {region.Width}x{region.Height}.";
            ActualizarEtiquetaRegion();
        }

        private static bool TryObtenerVentanaPoker(out Rectangle region)
        {
            region = Rectangle.Empty;
            var encontrada = Rectangle.Empty;
            var keywords = new[] { "poker", "hold", "casino", "texas" };

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                var length = GetWindowTextLength(hWnd);
                if (length <= 0)
                {
                    return true;
                }

                var title = new StringBuilder(length + 1);
                GetWindowText(hWnd, title, title.Capacity);
                var text = title.ToString();
                if (!keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                if (!GetWindowRect(hWnd, out var nativeRect))
                {
                    return true;
                }

                var rect = Rectangle.FromLTRB(nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom);
                if (rect.Width < 300 || rect.Height < 250)
                {
                    return true;
                }

                encontrada = rect;
                return false;
            }, IntPtr.Zero);

            region = encontrada;
            return !region.IsEmpty;
        }

        private static int PuntuarPantallaPoker(Rectangle pantalla)
        {
            using var captura = new Bitmap(Math.Max(1, pantalla.Width), Math.Max(1, pantalla.Height));
            using (var g = Graphics.FromImage(captura))
            {
                g.CopyFromScreen(pantalla.Location, Point.Empty, pantalla.Size);
            }

            var score = 0;
            var pasoX = Math.Max(1, captura.Width / 160);
            var pasoY = Math.Max(1, captura.Height / 90);
            var inicioY = captura.Height / 12;

            for (var y = inicioY; y < captura.Height; y += pasoY)
            {
                for (var x = 0; x < captura.Width; x += pasoX)
                {
                    var pixel = captura.GetPixel(x, y);
                    if (EsColorMesaPoker(pixel))
                    {
                        score += 2;
                    }
                    else if (EsColorCartaPoker(pixel))
                    {
                        score += 1;
                    }
                    else if (EsColorPaloRojo(pixel) || EsColorPaloNegro(pixel))
                    {
                        score += 1;
                    }
                }
            }

            return score;
        }

        private static bool EsColorMesaPoker(Color pixel)
        {
            return pixel.G > 55
                && pixel.G > pixel.R * 1.25
                && pixel.G > pixel.B * 1.15
                && pixel.R < 90
                && pixel.B < 100;
        }

        private static bool EsColorCartaPoker(Color pixel)
        {
            return pixel.R > 185 && pixel.G > 185 && pixel.B > 185;
        }

        private static bool EsColorPaloRojo(Color pixel)
        {
            return pixel.R > 140 && pixel.G < 80 && pixel.B < 80;
        }

        private static bool EsColorPaloNegro(Color pixel)
        {
            return pixel.R < 45 && pixel.G < 45 && pixel.B < 45;
        }

        private Rectangle ObtenerRegionCapturaActual()
        {
            return new Rectangle(
                (int)entradaX.Value,
                (int)entradaY.Value,
                (int)entradaAncho.Value,
                (int)entradaAlto.Value);
        }

        private static Rectangle? DesnormalizarRegion(NormalizedRegion? region, Size size)
        {
            if (region is null || region.Width <= 0 || region.Height <= 0)
            {
                return null;
            }

            var rect = new Rectangle(
                (int)Math.Round(region.X * size.Width),
                (int)Math.Round(region.Y * size.Height),
                (int)Math.Round(region.Width * size.Width),
                (int)Math.Round(region.Height * size.Height));
            return Rectangle.Intersect(new Rectangle(Point.Empty, size), rect);
        }

        private static NormalizedRegion NormalizarRegion(Rectangle seleccion, Rectangle regionCaptura)
        {
            return new NormalizedRegion
            {
                X = (seleccion.X - regionCaptura.X) / (double)regionCaptura.Width,
                Y = (seleccion.Y - regionCaptura.Y) / (double)regionCaptura.Height,
                Width = seleccion.Width / (double)regionCaptura.Width,
                Height = seleccion.Height / (double)regionCaptura.Height,
            };
        }

        private void ActualizarEtiquetaPerfil()
        {
            var jugador = configuracion.GameProfile.PlayerCards is null ? "mis cartas no" : "mis cartas si";
            var mesa = configuracion.GameProfile.BoardCards is null ? "mesa no" : "mesa si";
            etiquetaPerfil.Text = $"Perfil: {jugador} | {mesa}. Si falta algo, se usa deteccion automatica.";
        }

        private void CargarConfiguracion()
        {
            try
            {
                var ruta = ObtenerRutaConfiguracion();
                if (!File.Exists(ruta))
                {
                    return;
                }

                var cargada = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ruta));
                if (cargada is null)
                {
                    return;
                }

                configuracion = cargada;
                if (configuracion.Capture.Width > 0 && configuracion.Capture.Height > 0)
                {
                    entradaX.Value = Limitar(configuracion.Capture.X, entradaX.Minimum, entradaX.Maximum);
                    entradaY.Value = Limitar(configuracion.Capture.Y, entradaY.Minimum, entradaY.Maximum);
                    entradaAncho.Value = Limitar(configuracion.Capture.Width, entradaAncho.Minimum, entradaAncho.Maximum);
                    entradaAlto.Value = Limitar(configuracion.Capture.Height, entradaAlto.Minimum, entradaAlto.Maximum);
                }
            }
            catch
            {
                configuracion = new AppSettings();
            }
        }

        private void GuardarConfiguracion()
        {
            try
            {
                var region = ObtenerRegionCapturaActual();
                configuracion.Capture = new CaptureSettings
                {
                    X = region.X,
                    Y = region.Y,
                    Width = region.Width,
                    Height = region.Height,
                };

                var ruta = ObtenerRutaConfiguracion();
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                File.WriteAllText(ruta, JsonSerializer.Serialize(configuracion, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // La configuracion ayuda, pero no debe impedir capturar.
            }
        }

        private static string ObtenerRutaConfiguracion()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PokerScreenScraper",
                "settings.json");
        }

        private static Rectangle ObtenerPantallaCapturaInicial()
        {
            return Screen.PrimaryScreen?.Bounds ?? SystemInformation.VirtualScreen;
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
            var textoLimpio = new string(valor.Select(caracter => caracteresInvalidos.Contains(caracter) ? '_' : caracter).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(textoLimpio) ? "screenshot" : textoLimpio;
        }
        #endregion

        #region Poker Odds Calculator
        private static class PokerOddsCalculator
        {
            private const int Simulaciones = 10000;

            public static OddsResult Calcular(string cartasJugadorTexto, string cartasMesaTexto, int rivales)
            {
                var cartasJugador = ParsearCartas(cartasJugadorTexto).ToList();
                var cartasMesa = ParsearCartas(cartasMesaTexto).ToList();

                if (cartasJugador.Count != 2)
                    throw new ArgumentException("Debes especificar exactamente 2 cartas para el jugador");

                if (cartasMesa.Count > 5)
                    throw new ArgumentException("La mesa no puede tener mas de 5 cartas");

                var baraja = CrearBarajaCompleta();
                baraja = baraja.Except(cartasJugador).Except(cartasMesa).ToList();

                var conocidas = cartasJugador.Concat(cartasMesa).ToList();
                if (conocidas.Distinct().Count() != conocidas.Count)
                    throw new ArgumentException("Hay cartas repetidas. Revisa tus cartas y la mesa.");

                int victorias = 0;
                int derrotas = 0;
                int empates = 0;
                var random = new Random();

                for (int i = 0; i < Simulaciones; i++)
                {
                    var barajaMezclada = baraja.OrderBy(x => random.Next()).ToList();
                    var mesaCompleta = new List<Card>(cartasMesa);

                    // Completar mesa si es necesario
                    int cartasFaltantes = 5 - mesaCompleta.Count;
                    if (cartasFaltantes > 0)
                    {
                        mesaCompleta.AddRange(barajaMezclada.Take(cartasFaltantes));
                        barajaMezclada.RemoveRange(0, cartasFaltantes);
                    }


                    // Evaluar mano del jugador
                    var manoJugador = EvaluarMejorMano(cartasJugador.Concat(mesaCompleta).ToList());

                    // Simular manos de los rivales
                    bool gano = true;
                    bool hayEmpate = false;

                    for (int r = 0; r < rivales; r++)
                    {
                        var cartasRival = new List<Card>
                        {
                            barajaMezclada[r * 2],
                            barajaMezclada[r * 2 + 1]
                        };

                        var manoRival = EvaluarMejorMano(cartasRival.Concat(mesaCompleta).ToList());
                        int comparacion = CompararManos(manoJugador, manoRival);

                        if (comparacion < 0) { gano = false; break; }
                        if (comparacion == 0) hayEmpate = true;
                    }

                    if (gano && hayEmpate)
                    {
                        empates++;
                    }
                    else if (gano)
                    {
                        victorias++;
                    }
                    else
                    {
                        derrotas++;
                    }
                }

                return new OddsResult(
                    (double)victorias / Simulaciones,
                    (double)derrotas / Simulaciones,
                    (double)empates / Simulaciones,
                    EvaluarMejorMano(cartasJugador.Concat(cartasMesa).ToList()).Descripcion);
            }

            private static List<Card> ParsearCartas(string texto)
            {
                if (string.IsNullOrWhiteSpace(texto))
                    return new List<Card>();

                return texto.Split(' ')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(ParsearCarta)
                    .ToList();
            }

            private static Card ParsearCarta(string texto)
            {
                if (texto.Length < 2)
                    throw new ArgumentException($"Formato de carta invÃ¡lido: '{texto}'");

                char valorChar = texto[0];
                char paloChar = texto[^1]; // Ãšltimo carÃ¡cter

                int valor = valorChar switch
                {
                    '2' => 2,
                    '3' => 3,
                    '4' => 4,
                    '5' => 5,
                    '6' => 6,
                    '7' => 7,
                    '8' => 8,
                    '9' => 9,
                    'T' => 10,
                    'J' => 11,
                    'Q' => 12,
                    'K' => 13,
                    'A' => 14,
                    _ => throw new ArgumentException($"Valor no reconocido: '{valorChar}'")
                };

                Suit palo = paloChar switch
                {
                    'h' => Suit.Corazones,
                    'd' => Suit.Diamantes,
                    'c' => Suit.Treboles,
                    's' => Suit.Picas,
                    _ => throw new ArgumentException($"Palo no reconocido: '{paloChar}'")
                };

                return new Card(valor, palo);
            }

            private static List<Card> CrearBarajaCompleta()
            {
                var baraja = new List<Card>();
                foreach (Suit palo in Enum.GetValues(typeof(Suit)))
                    for (int valor = 2; valor <= 14; valor++)
                        baraja.Add(new Card(valor, palo));
                return baraja;
            }

            private static HandValue EvaluarMejorMano(List<Card> cartas)
            {
                cartas = cartas.OrderByDescending(c => c.Value).ToList();

                var grupos = cartas.GroupBy(c => c.Value)
                                 .OrderByDescending(g => g.Count())
                                 .ThenByDescending(g => g.Key)
                                 .ToList();

                bool esEscalera = EsEscalera(cartas);
                bool esColor = EsColor(cartas);

                if (esColor && esEscalera)
                    return new HandValue(8, "Escalera de color");
                if (grupos[0].Count() == 4)
                    return new HandValue(7, $"PÃ³ker de {NombreValor(grupos[0].Key)}");
                if (grupos.Count >= 2 && grupos[0].Count() == 3 && grupos[1].Count() >= 2)
                    return new HandValue(6, $"Full de {NombreValor(grupos[0].Key)}");
                if (esColor)
                    return new HandValue(5, "Color");
                if (esEscalera)
                    return new HandValue(4, "Escalera");
                if (grupos[0].Count() == 3)
                    return new HandValue(3, $"Trio de {NombreValor(grupos[0].Key)}");
                if (grupos.Count >= 2 && grupos[0].Count() == 2 && grupos[1].Count() == 2)
                    return new HandValue(2, $"Doble pareja");
                if (grupos[0].Count() == 2)
                    return new HandValue(1, $"Pareja de {NombreValor(grupos[0].Key)}");

                return new HandValue(0, $"Carta alta {NombreValor(grupos[0].Key)}");
            }

            private static bool EsEscalera(List<Card> cartas)
            {
                var valores = cartas.Select(c => c.Value).Distinct().OrderBy(v => v).ToList();
                if (valores.Count < 5) return false;

                for (int i = 1; i < 5; i++)
                    if (valores[i] != valores[i - 1] + 1)
                        return false;

                return true;
            }

            private static bool EsColor(List<Card> cartas)
            {
                return cartas.GroupBy(c => c.Suit).Any(g => g.Count() >= 5);
            }

            private static int CompararManos(HandValue a, HandValue b)
            {
                return a.Fuerza.CompareTo(b.Fuerza);
            }

            private static string NombreValor(int valor)
            {
                return valor switch
                {
                    10 => "10",
                    11 => "J",
                    12 => "Q",
                    13 => "K",
                    14 => "A",
                    _ => valor.ToString()
                };
            }
        }
        #endregion

        #region AnÃ¡lisis de ImÃ¡genes
        private (List<string> Jugador, List<string> Mesa) AnalizarCartasEnImagen(Bitmap imagen, int umbral)
        {
            var resultado = (
                Jugador: new List<string>(),
                Mesa: new List<string>()
            );

            var desdeSlots = AnalizarCartasPorSlotsMesa(imagen, umbral);
            if (desdeSlots.Jugador.Count == 2)
            {
                return desdeSlots;
            }

            var desdeLayout = AnalizarCartasPorLayoutConocido(imagen, umbral);
            if (desdeLayout.Jugador.Count == 2)
            {
                return desdeLayout;
            }

            var regiones = DetectarRegionesCartasLocales(imagen);
            if (regiones.Count == 0)
            {
                return resultado;
            }

            var centroX = imagen.Width / 2.0;
            var jugador = regiones
                .Where(region => region.Top > imagen.Height * 0.42)
                .Where(region => Math.Abs((region.Left + region.Right) / 2.0 - centroX) < imagen.Width * 0.34)
                .OrderByDescending(region => region.Bottom)
                .Take(2)
                .OrderBy(region => region.Left)
                .ToList();

            if (jugador.Count < 2)
            {
                jugador = ObtenerRegionesJugadorFallback(imagen).ToList();
            }

            var mesa = regiones
                .Where(region => region.Top > imagen.Height * 0.22 && region.Top < imagen.Height * 0.58)
                .Where(region => Math.Abs((region.Left + region.Right) / 2.0 - centroX) < imagen.Width * 0.32)
                .OrderBy(region => region.Left)
                .Take(5)
                .ToList();

            GuardarDebugRegiones(imagen, jugador, mesa);

            foreach (var region in jugador)
            {
                using var carta = RecortarCarta(imagen, Rectangle.Inflate(region, 8, 8));
                GuardarRecorteDebug(carta, $"player-card-{resultado.Jugador.Count + 1}.png");
                var valor = carta is null ? null : ReconocerCarta(carta, umbral);
                if (!string.IsNullOrEmpty(valor) && !resultado.Jugador.Contains(valor))
                {
                    resultado.Jugador.Add(valor);
                }
            }

            foreach (var region in mesa)
            {
                using var carta = RecortarCarta(imagen, Rectangle.Inflate(region, 8, 8));
                GuardarRecorteDebug(carta, $"board-card-{resultado.Mesa.Count + 1}.png");
                var valor = carta is null ? null : ReconocerCarta(carta, umbral);
                if (!string.IsNullOrEmpty(valor) && !resultado.Mesa.Contains(valor))
                {
                    resultado.Mesa.Add(valor);
                }
            }

            if (resultado.Jugador.Count == 2)
            {
                return resultado;
            }

            var deteccionesOnnx = OnnxCardDetector.Detect(imagen);
            if (deteccionesOnnx.Count > 0)
            {
                var desdeModelo = ClasificarDeteccionesOnnx(imagen, deteccionesOnnx, umbral);
                if (desdeModelo.Jugador.Count == 2)
                {
                    return desdeModelo;
                }
            }

            return resultado;
        }

        private (List<string> Jugador, List<string> Mesa) AnalizarCartasPorSlotsMesa(Bitmap imagen, int umbral)
        {
            var resultado = (
                Jugador: new List<string>(),
                Mesa: new List<string>()
            );

            // Slots calibrados para la mesa 247freepoker dentro del navegador: ignoran anuncios, barra superior y fichas.
            var jugadorSlots = new[]
            {
                CrearSlot(imagen.Size, 0.348, 0.565, 0.070, 0.185),
                CrearSlot(imagen.Size, 0.402, 0.565, 0.070, 0.185),
            };

            var mesaSlots = new[]
            {
                CrearSlot(imagen.Size, 0.249, 0.377, 0.060, 0.160),
                CrearSlot(imagen.Size, 0.317, 0.377, 0.060, 0.160),
                CrearSlot(imagen.Size, 0.384, 0.377, 0.060, 0.160),
                CrearSlot(imagen.Size, 0.452, 0.377, 0.060, 0.160),
                CrearSlot(imagen.Size, 0.519, 0.377, 0.060, 0.160),
            };

            foreach (var slot in jugadorSlots)
            {
                using var carta = RecortarCarta(imagen, slot);
                var valor = carta is null ? null : ReconocerCarta(carta, umbral);
                GuardarRecorteDebug(carta, $"slot-player-{resultado.Jugador.Count + 1}-{(valor ?? "NO")}.png");
                if (!string.IsNullOrWhiteSpace(valor) && !resultado.Jugador.Contains(valor))
                {
                    resultado.Jugador.Add(valor);
                }
            }

            foreach (var slot in mesaSlots)
            {
                using var carta = RecortarCarta(imagen, slot);
                if (carta is null || !EsRegionValidaDeCarta(carta))
                {
                    continue;
                }

                var valor = ReconocerCarta(carta, umbral);
                GuardarRecorteDebug(carta, $"slot-board-{resultado.Mesa.Count + 1}-{(valor ?? "NO")}.png");
                if (!string.IsNullOrWhiteSpace(valor) && !resultado.Mesa.Contains(valor) && !resultado.Jugador.Contains(valor))
                {
                    resultado.Mesa.Add(valor);
                }
            }

            return resultado;
        }

        private static Rectangle CrearSlot(Size size, double x, double y, double width, double height)
        {
            return Rectangle.Intersect(
                new Rectangle(Point.Empty, size),
                new Rectangle(
                    (int)Math.Round(size.Width * x),
                    (int)Math.Round(size.Height * y),
                    (int)Math.Round(size.Width * width),
                    (int)Math.Round(size.Height * height)));
        }

        private (List<string> Jugador, List<string> Mesa) ClasificarDeteccionesOnnx(Bitmap imagen, IReadOnlyList<CardDetection> detecciones, int umbral)
        {
            var imagenSize = imagen.Size;
            var centroX = imagenSize.Width / 2.0;
            var unicas = detecciones
                .GroupBy(d => d.Card)
                .Select(g => g.OrderByDescending(d => d.Confidence).First())
                .ToList();

            var jugadorDetecciones = unicas
                .Where(d => d.Box.Top > imagenSize.Height * 0.42)
                .Where(d => Math.Abs((d.Box.Left + d.Box.Right) / 2.0 - centroX) < imagenSize.Width * 0.38)
                .OrderByDescending(d => d.Box.Bottom)
                .Take(2)
                .OrderBy(d => d.Box.Left)
                .ToList();

            if (jugadorDetecciones.Count < 2)
            {
                jugadorDetecciones = unicas
                    .Where(d => d.Box.Top > imagenSize.Height * 0.32)
                    .OrderByDescending(d => d.Box.Bottom)
                    .ThenBy(d => Math.Abs((d.Box.Left + d.Box.Right) / 2.0 - centroX))
                    .Take(2)
                    .OrderBy(d => d.Box.Left)
                    .ToList();
            }

            var jugador = jugadorDetecciones
                .Select(d => LeerCartaDesdeDeteccion(imagen, d, umbral))
                .Where(card => !string.IsNullOrWhiteSpace(card))
                .Select(card => card!)
                .Distinct()
                .ToList();

            var mesa = unicas
                .Where(d => d.Box.Top > imagenSize.Height * 0.18 && d.Box.Top < imagenSize.Height * 0.62)
                .Where(d => Math.Abs((d.Box.Left + d.Box.Right) / 2.0 - centroX) < imagenSize.Width * 0.38)
                .OrderBy(d => d.Box.Left)
                .Take(5)
                .Select(d => LeerCartaDesdeDeteccion(imagen, d, umbral))
                .Where(card => !string.IsNullOrWhiteSpace(card))
                .Select(card => card!)
                .Where(card => !jugador.Contains(card))
                .Distinct()
                .ToList();

            return (jugador, mesa);
        }

        private (List<string> Jugador, List<string> Mesa) AnalizarCartasPorLayoutConocido(Bitmap imagen, int umbral)
        {
            var resultado = (
                Jugador: new List<string>(),
                Mesa: new List<string>()
            );

            var jugadorZona = new Rectangle(
                (int)(imagen.Width * 0.29),
                (int)(imagen.Height * 0.48),
                (int)(imagen.Width * 0.24),
                (int)(imagen.Height * 0.34));

            var mesaZona = new Rectangle(
                (int)(imagen.Width * 0.20),
                (int)(imagen.Height * 0.28),
                (int)(imagen.Width * 0.43),
                (int)(imagen.Height * 0.30));

            var jugadorRegiones = DetectarRegionesCartasLocalesEnZona(imagen, jugadorZona)
                .OrderByDescending(r => r.Bottom)
                .ThenBy(r => r.Left)
                .Take(4)
                .OrderBy(r => r.Left)
                .ToList();

            jugadorRegiones = NormalizarRegionesJugador(jugadorRegiones, jugadorZona)
                .Take(2)
                .ToList();

            var mesaRegiones = DetectarRegionesCartasLocalesEnZona(imagen, mesaZona)
                .OrderBy(r => r.Left)
                .Take(5)
                .ToList();

            foreach (var region in jugadorRegiones)
            {
                using var carta = RecortarCarta(imagen, Rectangle.Inflate(region, 10, 10));
                var valor = carta is null ? null : ReconocerCarta(carta, umbral);
                if (!string.IsNullOrWhiteSpace(valor) && !resultado.Jugador.Contains(valor))
                {
                    resultado.Jugador.Add(valor);
                }
            }

            foreach (var region in mesaRegiones)
            {
                using var carta = RecortarCarta(imagen, Rectangle.Inflate(region, 10, 10));
                var valor = carta is null ? null : ReconocerCarta(carta, umbral);
                if (!string.IsNullOrWhiteSpace(valor) && !resultado.Mesa.Contains(valor) && !resultado.Jugador.Contains(valor))
                {
                    resultado.Mesa.Add(valor);
                }
            }

            return resultado;
        }

        private static List<Rectangle> NormalizarRegionesJugador(IReadOnlyList<Rectangle> regiones, Rectangle zonaJugador)
        {
            if (regiones.Count == 0)
            {
                return CrearRegionesJugadorPorLayout(zonaJugador);
            }

            var cartas = new List<Rectangle>();
            foreach (var region in regiones.OrderByDescending(r => r.Width * r.Height))
            {
                var ratio = region.Width / (double)Math.Max(1, region.Height);
                if (ratio > 0.82 || region.Width > zonaJugador.Width * 0.44)
                {
                    cartas.AddRange(PartirRegionJugadorEnDos(region));
                }
                else
                {
                    cartas.Add(region);
                }

                if (cartas.Count >= 2)
                {
                    break;
                }
            }

            if (cartas.Count < 2)
            {
                foreach (var region in CrearRegionesJugadorPorLayout(zonaJugador))
                {
                    if (cartas.All(carta => Rectangle.Intersect(carta, region).Width * Rectangle.Intersect(carta, region).Height < carta.Width * carta.Height * 0.25))
                    {
                        cartas.Add(region);
                    }

                    if (cartas.Count >= 2)
                    {
                        break;
                    }
                }
            }

            return cartas
                .OrderByDescending(r => r.Width * r.Height)
                .Take(2)
                .OrderBy(r => r.Left)
                .ToList();
        }

        private static IEnumerable<Rectangle> PartirRegionJugadorEnDos(Rectangle region)
        {
            var cardWidth = Math.Max(1, (int)Math.Round(region.Width * 0.58));
            var overlap = Math.Max(1, (int)Math.Round(cardWidth * 0.22));
            var left = new Rectangle(region.Left, region.Top, cardWidth, region.Height);
            var right = new Rectangle(region.Right - cardWidth, region.Top, cardWidth, region.Height);

            yield return Rectangle.Inflate(left, 4, 4);
            yield return Rectangle.Inflate(right, 4, 4);
        }

        private static List<Rectangle> CrearRegionesJugadorPorLayout(Rectangle zonaJugador)
        {
            var cardWidth = Math.Max(1, (int)Math.Round(zonaJugador.Width * 0.30));
            var cardHeight = Math.Max(1, (int)Math.Round(zonaJugador.Height * 0.58));
            var top = zonaJugador.Top + (int)Math.Round(zonaJugador.Height * 0.16);
            var left1 = zonaJugador.Left + (int)Math.Round(zonaJugador.Width * 0.30);
            var left2 = zonaJugador.Left + (int)Math.Round(zonaJugador.Width * 0.46);

            return new List<Rectangle>
            {
                new(left1, top, cardWidth, cardHeight),
                new(left2, top, cardWidth, cardHeight),
            };
        }

        private string? LeerCartaDesdeDeteccion(Bitmap imagen, CardDetection deteccion, int umbral)
        {
            var rect = Rectangle.Round(deteccion.Box);
            rect = Rectangle.Inflate(rect, 10, 10);
            using var carta = RecortarCarta(imagen, rect);
            if (carta is null)
            {
                return null;
            }

            return ReconocerCarta(carta, umbral);
        }

        private static void GuardarDebugRegiones(Bitmap origen, IReadOnlyList<Rectangle> jugador, IReadOnlyList<Rectangle> mesa)
        {
            if (!DebugCartasActivo())
            {
                return;
            }

            try
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "PokerScreenScraper",
                    "debug");
                Directory.CreateDirectory(carpeta);

                using var marcado = new Bitmap(origen);
                using (var g = Graphics.FromImage(marcado))
                using (var fuente = new Font("Segoe UI", 18F, FontStyle.Bold))
                using (var jugadorPen = new Pen(Color.Lime, 5F))
                using (var mesaPen = new Pen(Color.DeepSkyBlue, 5F))
                using (var fondo = new SolidBrush(Color.FromArgb(210, 0, 0, 0)))
                using (var texto = new SolidBrush(Color.White))
                {
                    for (var i = 0; i < jugador.Count; i++)
                    {
                        g.DrawRectangle(jugadorPen, jugador[i]);
                        g.FillRectangle(fondo, jugador[i].Left, Math.Max(0, jugador[i].Top - 32), 150, 32);
                        g.DrawString($"PLAYER {i + 1}", fuente, texto, jugador[i].Left + 4, Math.Max(0, jugador[i].Top - 34));
                    }

                    for (var i = 0; i < mesa.Count; i++)
                    {
                        g.DrawRectangle(mesaPen, mesa[i]);
                        g.FillRectangle(fondo, mesa[i].Left, Math.Max(0, mesa[i].Top - 32), 130, 32);
                        g.DrawString($"BOARD {i + 1}", fuente, texto, mesa[i].Left + 4, Math.Max(0, mesa[i].Top - 34));
                    }
                }
                marcado.Save(Path.Combine(carpeta, "detected-card-regions.png"), ImageFormat.Png);

                const int thumbW = 220;
                const int thumbH = 300;
                const int labelH = 34;
                const int margin = 16;
                var total = Math.Max(1, jugador.Count + mesa.Count);
                var cols = Math.Min(5, total);
                var rows = (int)Math.Ceiling(total / (double)cols);
                using var sheet = new Bitmap(cols * (thumbW + margin) + margin, rows * (thumbH + labelH + margin) + margin);
                using (var g = Graphics.FromImage(sheet))
                using (var fuente = new Font("Segoe UI", 14F, FontStyle.Bold))
                using (var texto = new SolidBrush(Color.Black))
                using (var borde = new Pen(Color.Black, 2F))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                    var index = 0;
                    foreach (var item in jugador.Select((region, i) => (Nombre: $"PLAYER {i + 1}", Region: region))
                                 .Concat(mesa.Select((region, i) => (Nombre: $"BOARD {i + 1}", Region: region))))
                    {
                        var col = index % cols;
                        var row = index / cols;
                        var x = margin + col * (thumbW + margin);
                        var y = margin + row * (thumbH + labelH + margin);

                        g.DrawString(item.Nombre, fuente, texto, x, y);
                        g.DrawRectangle(borde, x, y + labelH, thumbW, thumbH);
                        var region = Rectangle.Intersect(new Rectangle(Point.Empty, origen.Size), Rectangle.Inflate(item.Region, 12, 12));
                        if (region.Width > 0 && region.Height > 0)
                        {
                            g.DrawImage(origen, new Rectangle(x, y + labelH, thumbW, thumbH), region, GraphicsUnit.Pixel);
                        }
                        index++;
                    }
                }
                sheet.Save(Path.Combine(carpeta, "card-debug-sheet.png"), ImageFormat.Png);
            }
            catch
            {
                // Debug visual no debe bloquear el analisis.
            }
        }

        private static void GuardarRecorteDebug(Bitmap? imagen, string nombreArchivo)
        {
            if (!DebugCartasActivo())
            {
                return;
            }

            if (imagen is null)
            {
                return;
            }

            try
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "PokerScreenScraper",
                    "debug",
                    "cards");
                Directory.CreateDirectory(carpeta);
                imagen.Save(Path.Combine(carpeta, nombreArchivo), ImageFormat.Png);
            }
            catch
            {
                // Los recortes son diagnostico; no deben bloquear la app.
            }
        }

        private static bool DebugCartasActivo()
        {
            var valor = Environment.GetEnvironmentVariable("POKER_DEBUG_CARDS");
            return valor is "1" or "true" or "TRUE" or "yes" or "YES";
        }

        private static IEnumerable<Rectangle> ObtenerRegionesJugadorFallback(Bitmap imagen)
        {
            var ancho = (int)(imagen.Width * 0.09);
            var alto = (int)(imagen.Height * 0.18);
            var centroX = imagen.Width / 2;
            var top = (int)(imagen.Height * 0.54);
            var separacion = (int)(imagen.Width * 0.035);

            yield return Rectangle.Intersect(
                new Rectangle(Point.Empty, imagen.Size),
                new Rectangle(centroX - separacion - ancho, top, ancho, alto));
            yield return Rectangle.Intersect(
                new Rectangle(Point.Empty, imagen.Size),
                new Rectangle(centroX + separacion / 2, top, ancho, alto));
        }

        private static List<Rectangle> DetectarRegionesCartasLocales(Bitmap imagen)
        {
            var scaleWidth = 900;
            using var reducida = new Bitmap(scaleWidth, Math.Max(1, (int)Math.Round(imagen.Height * (scaleWidth / (double)imagen.Width))));
            using (var g = Graphics.FromImage(reducida))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(imagen, 0, 0, reducida.Width, reducida.Height);
            }

            var visitado = new bool[reducida.Width, reducida.Height];
            var regiones = new List<Rectangle>();
            for (var y = 0; y < reducida.Height; y += 2)
            {
                for (var x = 0; x < reducida.Width; x += 2)
                {
                    if (visitado[x, y] || !EsPixelBlancoCarta(reducida.GetPixel(x, y)))
                    {
                        continue;
                    }

                    var region = FloodFillLocal(reducida, visitado, x, y);
                    var ratio = region.Width / (double)Math.Max(1, region.Height);
                    var area = region.Width * region.Height;
                    if (area > reducida.Width * reducida.Height * 0.0016
                        && region.Height > reducida.Height * 0.065
                        && ratio > 0.30
                        && ratio < 1.05)
                    {
                        regiones.Add(new Rectangle(
                            (int)Math.Round(region.X * imagen.Width / (double)reducida.Width),
                            (int)Math.Round(region.Y * imagen.Height / (double)reducida.Height),
                            (int)Math.Round(region.Width * imagen.Width / (double)reducida.Width),
                            (int)Math.Round(region.Height * imagen.Height / (double)reducida.Height)));
                    }
                }
            }

            return regiones
                .OrderByDescending(region => region.Width * region.Height)
                .Take(12)
                .ToList();
        }

        private static List<Rectangle> DetectarRegionesCartasLocalesEnZona(Bitmap imagen, Rectangle zona)
        {
            zona = Rectangle.Intersect(new Rectangle(Point.Empty, imagen.Size), zona);
            if (zona.Width <= 0 || zona.Height <= 0)
            {
                return new List<Rectangle>();
            }

            using var recorte = new Bitmap(zona.Width, zona.Height);
            using (var g = Graphics.FromImage(recorte))
            {
                g.DrawImage(imagen, new Rectangle(0, 0, zona.Width, zona.Height), zona, GraphicsUnit.Pixel);
            }

            return DetectarRegionesCartasLocales(recorte)
                .Select(r => new Rectangle(r.X + zona.X, r.Y + zona.Y, r.Width, r.Height))
                .Where(r => r.Width > imagen.Width * 0.035 && r.Height > imagen.Height * 0.08)
                .OrderByDescending(r => r.Width * r.Height)
                .Take(8)
                .ToList();
        }

        private static bool EsPixelBlancoCarta(Color pixel)
        {
            return pixel.R > 168 && pixel.G > 168 && pixel.B > 168 && Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) < 68;
        }

        private static Rectangle FloodFillLocal(Bitmap imagen, bool[,] visitado, int startX, int startY)
        {
            var cola = new Queue<Point>();
            cola.Enqueue(new Point(startX, startY));
            visitado[startX, startY] = true;
            var minX = startX;
            var maxX = startX;
            var minY = startY;
            var maxY = startY;

            while (cola.Count > 0)
            {
                var punto = cola.Dequeue();
                minX = Math.Min(minX, punto.X);
                maxX = Math.Max(maxX, punto.X);
                minY = Math.Min(minY, punto.Y);
                maxY = Math.Max(maxY, punto.Y);

                foreach (var vecino in new[]
                {
                    new Point(punto.X + 2, punto.Y),
                    new Point(punto.X - 2, punto.Y),
                    new Point(punto.X, punto.Y + 2),
                    new Point(punto.X, punto.Y - 2),
                })
                {
                    if (vecino.X < 0 || vecino.Y < 0 || vecino.X >= imagen.Width || vecino.Y >= imagen.Height || visitado[vecino.X, vecino.Y])
                    {
                        continue;
                    }

                    visitado[vecino.X, vecino.Y] = true;
                    if (EsPixelBlancoCarta(imagen.GetPixel(vecino.X, vecino.Y)))
                    {
                        cola.Enqueue(vecino);
                    }
                }
            }

            return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        private Bitmap? RecortarCarta(Bitmap imagen, Rectangle region)
        {
            region = Rectangle.Intersect(new Rectangle(Point.Empty, imagen.Size), region);
            if (region.Width <= 0 || region.Height <= 0) return null;

            try
            {
                var carta = new Bitmap(region.Width, region.Height);
                using (var g = Graphics.FromImage(carta))
                {
                    g.DrawImage(imagen, new Rectangle(0, 0, region.Width, region.Height),
                               region, GraphicsUnit.Pixel);
                }
                return carta;
            }
            catch
            {
                return null;
            }
        }

        private string? ReconocerCarta(Bitmap imagenCarta, int umbral)
        {
            if (!EsRegionValidaDeCarta(imagenCarta)) return null;

            try
            {
                // 1. Convertir a escala de grises.
                using var imagenGris = ConvertirAGrises(imagenCarta);

                // 2. Procesar imagen (umbral).
                using var imagenProcesada = AplicarUmbral(imagenGris, umbral);

                // 3. Detectar valor con OCR si existe; si no, usar plantillas locales generadas.
                var valor = DetectarValorCartaConTesseractSiDisponible(imagenCarta)
                    ?? DetectarValorCartaPorPlantilla(imagenCarta)
                    ?? DetectarValorCartaPorPlantilla(imagenProcesada);
                if (string.IsNullOrEmpty(valor)) return null;

                // 4. Detectar palo con plantillas locales.
                var palo = DetectarPaloCarta(imagenCarta);
                if (string.IsNullOrEmpty(palo)) return null;

                return $"{valor}{palo}";
            }
            catch
            {
                return null;
            }
        }

        private bool EsRegionValidaDeCarta(Bitmap imagen)
        {
            // Verificar que la regiÃ³n contiene una carta (no estÃ¡ vacÃ­a)
            int puntosClaros = 0;
            for (int x = 0; x < imagen.Width; x += 5)
            {
                for (int y = 0; y < imagen.Height; y += 5)
                {
                    var pixel = imagen.GetPixel(x, y);
                    if ((pixel.R + pixel.G + pixel.B) / 3 > 180)
                        puntosClaros++;
                }
            }
            return puntosClaros > 10; // Ajustar segÃºn necesidad
        }

        private Bitmap ConvertirAGrises(Bitmap imagen)
        {
            var gris = new Bitmap(imagen.Width, imagen.Height);
            for (int x = 0; x < imagen.Width; x++)
            {
                for (int y = 0; y < imagen.Height; y++)
                {
                    var pixel = imagen.GetPixel(x, y);
                    int grisVal = (int)(pixel.R * 0.3 + pixel.G * 0.59 + pixel.B * 0.11);
                    gris.SetPixel(x, y, Color.FromArgb(grisVal, grisVal, grisVal));
                }
            }
            return gris;
        }

        private Bitmap AplicarUmbral(Bitmap imagen, int umbral)
        {
            var resultado = new Bitmap(imagen.Width, imagen.Height);
            for (int x = 0; x < imagen.Width; x++)
            {
                for (int y = 0; y < imagen.Height; y++)
                {
                    var pixel = imagen.GetPixel(x, y);
                    resultado.SetPixel(x, y, pixel.R > umbral ? Color.White : Color.Black);
                }
            }
            return resultado;
        }

        private static string? DetectarValorCartaConTesseractSiDisponible(Bitmap imagenCarta)
        {
            var desactivarTesseract = Environment.GetEnvironmentVariable("POKER_DISABLE_TESSERACT");
            if (desactivarTesseract is "1" or "true" or "TRUE" or "yes" or "YES")
            {
                return null;
            }

            var tesseract = RutaTesseract.Value;
            if (string.IsNullOrWhiteSpace(tesseract))
            {
                return null;
            }

            try
            {
                foreach (var angulo in new[] { 0F, -18F, 18F })
                {
                    using var rotada = Math.Abs(angulo) < 0.1F ? new Bitmap(imagenCarta) : RotarImagen(imagenCarta, angulo);
                    foreach (var psm in new[] { 10, 13, 8 })
                    {
                        using var recorteValor = PrepararRecorteValorParaOcr(rotada);
                        var temp = Path.Combine(Path.GetTempPath(), $"poker_ocr_{Guid.NewGuid():N}.png");
                        try
                        {
                            recorteValor.Save(temp, ImageFormat.Png);

                            using var proceso = new Process();
                            proceso.StartInfo = new ProcessStartInfo
                            {
                                FileName = tesseract,
                                Arguments = $"\"{temp}\" stdout --psm {psm} -c tessedit_char_whitelist=A23456789TJQK10",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true,
                            };

                            proceso.Start();
                            if (!proceso.WaitForExit(700))
                            {
                                try { proceso.Kill(true); } catch { }
                                continue;
                            }

                            var texto = proceso.StandardOutput.ReadToEnd();
                            var valor = NormalizarValorOcr(texto);
                            if (valor is not null)
                            {
                                return valor;
                            }
                        }
                        finally
                        {
                            try { File.Delete(temp); } catch { }
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string? DetectarValorCartaPorPlantilla(Bitmap imagenCarta)
        {
            var mejorValor = default(string);
            var mejorScore = 0.0;

            foreach (var angulo in new[] { 0F, -18F, 18F })
            {
                using var rotada = Math.Abs(angulo) < 0.1F ? new Bitmap(imagenCarta) : RotarImagen(imagenCarta, angulo);
                using var recorte = PrepararRecorteValorParaOcr(rotada);
                var normalizada = NormalizarMascara(recorte);
                if (normalizada is null)
                {
                    continue;
                }

                foreach (var valor in new[] { "A", "K", "Q", "J", "T", "9", "8", "7", "6", "5", "4", "3", "2" })
                {
                    foreach (var familia in new[] { "Arial", "Segoe UI" })
                    {
                        foreach (var plantilla in CrearPlantillasTexto(valor, normalizada.GetLength(0), normalizada.GetLength(1), familia))
                        {
                            using (plantilla)
                            {
                                var score = CompararMascaras(normalizada, MascaraDesdeBitmap(plantilla));
                                if (score > mejorScore)
                                {
                                    mejorScore = score;
                                    mejorValor = valor;
                                }
                            }
                        }
                    }
                }
            }

            return mejorScore >= 0.075 ? mejorValor : null;
        }

        private static Bitmap RotarImagen(Bitmap imagen, float angulo)
        {
            var salida = new Bitmap(imagen.Width, imagen.Height);
            using var g = Graphics.FromImage(salida);
            g.Clear(Color.White);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.TranslateTransform(imagen.Width / 2F, imagen.Height / 2F);
            g.RotateTransform(angulo);
            g.TranslateTransform(-imagen.Width / 2F, -imagen.Height / 2F);
            g.DrawImage(imagen, Point.Empty);
            return salida;
        }

        private static Bitmap PrepararRecorteValorParaOcr(Bitmap imagenCarta)
        {
            var ancho = Math.Max(26, (int)(imagenCarta.Width * 0.30));
            var alto = Math.Max(26, (int)(imagenCarta.Height * 0.24));
            var origen = new Rectangle(
                Math.Max(0, (int)(imagenCarta.Width * 0.02)),
                Math.Max(0, (int)(imagenCarta.Height * 0.02)),
                Math.Min(ancho, imagenCarta.Width),
                Math.Min(alto, imagenCarta.Height));
            var escala = 5;
            var salida = new Bitmap(origen.Width * escala, origen.Height * escala);

            using var g = Graphics.FromImage(salida);
            g.Clear(Color.White);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(imagenCarta, new Rectangle(0, 0, salida.Width, salida.Height), origen, GraphicsUnit.Pixel);

            for (var x = 0; x < salida.Width; x++)
            {
                for (var y = 0; y < salida.Height; y++)
                {
                    var pixel = salida.GetPixel(x, y);
                    var esMarca = pixel.R < 120 && pixel.G < 120 && pixel.B < 120
                        || pixel.R > 130 && pixel.G < 110 && pixel.B < 110;
                    salida.SetPixel(x, y, esMarca ? Color.Black : Color.White);
                }
            }

            return salida;
        }

        private static string? NormalizarValorOcr(string texto)
        {
            var limpio = new string(texto
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());

            if (string.IsNullOrWhiteSpace(limpio))
            {
                return null;
            }

            limpio = limpio
                .Replace("10", "T", StringComparison.OrdinalIgnoreCase)
                .Replace("O", "Q", StringComparison.OrdinalIgnoreCase)
                .Replace("I", "1", StringComparison.OrdinalIgnoreCase)
                .Replace("L", "1", StringComparison.OrdinalIgnoreCase);

            foreach (var caracter in limpio)
            {
                var valor = caracter switch
                {
                    'A' => "A",
                    'K' => "K",
                    'Q' => "Q",
                    'J' => "J",
                    'T' => "T",
                    '9' => "9",
                    '8' => "8",
                    '7' => "7",
                    '6' => "6",
                    '5' => "5",
                    '4' => "4",
                    '3' => "3",
                    '2' => "2",
                    _ => null
                };

                if (valor is not null)
                {
                    return valor;
                }
            }

            return null;
        }

        private static string? BuscarTesseract()
        {
            var rutas = new[]
            {
                Environment.GetEnvironmentVariable("TESSERACT_EXE"),
                "tesseract.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tesseract.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Tesseract-OCR", "tesseract.exe"),
            };

            foreach (var ruta in rutas.Where(ruta => !string.IsNullOrWhiteSpace(ruta)))
            {
                try
                {
                    if (Path.IsPathRooted(ruta!) && File.Exists(ruta))
                    {
                        return ruta;
                    }

                    using var proceso = new Process();
                    proceso.StartInfo = new ProcessStartInfo
                    {
                        FileName = ruta!,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    proceso.Start();
                    if (proceso.WaitForExit(800) && proceso.ExitCode == 0)
                    {
                        return ruta;
                    }
                }
                catch
                {
                    // Probar la siguiente ruta.
                }
            }

            return null;
        }

        private string DetectarValorCarta(Bitmap imagenBinaria)
        {
            // Analizar la esquina superior izquierda donde estÃ¡ el valor
            var regionValor = new Rectangle(5, 5, 30, 20);
            int pixelesNegros = ContarPixelesNegros(imagenBinaria, regionValor);

            return pixelesNegros switch
            {
                > 100 => "A",
                > 80 => "K",
                > 60 => "Q",
                > 45 => "J",
                > 35 => "T",
                > 25 => "9",
                > 18 => "8",
                > 12 => "7",
                > 8 => "6",
                > 5 => "5",
                > 3 => "4",
                > 1 => "3",
                _ => "2"
            };
        }

        private int ContarPixelesNegros(Bitmap imagen, Rectangle region)
        {
            int count = 0;
            for (int x = region.Left; x < region.Right && x < imagen.Width; x++)
            {
                for (int y = region.Top; y < region.Bottom && y < imagen.Height; y++)
                {
                    if (imagen.GetPixel(x, y).R == 0) count++;
                }
            }
            return count;
        }

        private static string? DetectarPaloCarta(Bitmap imagen)
        {
            var mejorPalo = default(string);
            var mejorScore = 0.0;

            foreach (var angulo in new[] { 0F, -18F, 18F })
            {
                using var rotada = Math.Abs(angulo) < 0.1F ? new Bitmap(imagen) : RotarImagen(imagen, angulo);
                using var recorte = PrepararRecortePaloParaPlantilla(rotada, out var colorDetectado);
                var normalizada = NormalizarMascara(recorte);
                if (normalizada is null)
                {
                    continue;
                }

                var candidatos = colorDetectado == SuitColor.Red
                    ? new[] { ("h", "♥"), ("d", "♦") }
                    : colorDetectado == SuitColor.Black
                        ? new[] { ("s", "♠"), ("c", "♣") }
                        : new[] { ("h", "♥"), ("d", "♦"), ("s", "♠"), ("c", "♣") };

                foreach (var (palo, simbolo) in candidatos)
                {
                    foreach (var familia in new[] { "Segoe UI Symbol", "Arial" })
                    {
                        foreach (var plantilla in CrearPlantillasTexto(simbolo, normalizada.GetLength(0), normalizada.GetLength(1), familia))
                        {
                            using (plantilla)
                            {
                                var score = CompararMascaras(normalizada, MascaraDesdeBitmap(plantilla));
                                if (score > mejorScore)
                                {
                                    mejorScore = score;
                                    mejorPalo = palo;
                                }
                            }
                        }
                    }
                }
            }

            return mejorScore >= 0.055 ? mejorPalo : null;
        }

        private enum SuitColor
        {
            Unknown,
            Red,
            Black
        }

        private static Bitmap PrepararRecortePaloParaPlantilla(Bitmap imagenCarta, out SuitColor colorDetectado)
        {
            var ancho = Math.Max(28, (int)(imagenCarta.Width * 0.64));
            var alto = Math.Max(34, (int)(imagenCarta.Height * 0.48));
            var x0 = Math.Max(0, (int)(imagenCarta.Width * 0.18));
            var y = Math.Max(0, (int)(imagenCarta.Height * 0.30));
            var origen = new Rectangle(
                x0,
                y,
                Math.Min(ancho, imagenCarta.Width - x0),
                Math.Min(alto, imagenCarta.Height - y));
            var escala = 5;
            var salida = new Bitmap(Math.Max(1, origen.Width * escala), Math.Max(1, origen.Height * escala));
            var rojos = 0;
            var negros = 0;

            using (var g = Graphics.FromImage(salida))
            {
                g.Clear(Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(imagenCarta, new Rectangle(0, 0, salida.Width, salida.Height), origen, GraphicsUnit.Pixel);
            }

            for (var x = 0; x < salida.Width; x++)
            {
                for (var yy = 0; yy < salida.Height; yy++)
                {
                    var pixel = salida.GetPixel(x, yy);
                    var esRojo = pixel.R > 120 && pixel.R > pixel.G * 1.25 && pixel.R > pixel.B * 1.25;
                    var esNegro = pixel.R < 120 && pixel.G < 120 && pixel.B < 120;
                    if (esRojo) rojos++;
                    if (esNegro) negros++;
                    salida.SetPixel(x, yy, esRojo || esNegro ? Color.Black : Color.White);
                }
            }

            colorDetectado = rojos > negros * 1.15 ? SuitColor.Red : negros > 0 ? SuitColor.Black : SuitColor.Unknown;
            return salida;
        }

        private static Bitmap CrearPlantillaTexto(string texto, int ancho, int alto, string familiaFuente)
        {
            var salida = new Bitmap(ancho, alto);
            using var g = Graphics.FromImage(salida);
            g.Clear(Color.White);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

            using var fuente = new Font(familiaFuente, alto * 0.78F, FontStyle.Bold, GraphicsUnit.Pixel);
            var medida = g.MeasureString(texto, fuente);
            var x = (ancho - medida.Width) / 2F;
            var y = (alto - medida.Height) / 2F;
            g.DrawString(texto, fuente, Brushes.Black, x, y);

            return salida;
        }

        private static IEnumerable<Bitmap> CrearPlantillasTexto(string texto, int ancho, int alto, string familiaFuente)
        {
            foreach (var factor in new[] { 0.72F, 0.84F })
            {
                var salida = new Bitmap(ancho, alto);
                using var g = Graphics.FromImage(salida);
                g.Clear(Color.White);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                using var fuente = new Font(familiaFuente, alto * factor, FontStyle.Bold, GraphicsUnit.Pixel);
                var medida = g.MeasureString(texto, fuente);
                var x = (ancho - medida.Width) / 2F;
                var y = (alto - medida.Height) / 2F;
                g.DrawString(texto, fuente, Brushes.Black, x, y);
                yield return salida;
            }
        }

        private static bool[,]? NormalizarMascara(Bitmap imagen)
        {
            var mascara = MascaraDesdeBitmap(imagen);
            var minX = imagen.Width;
            var minY = imagen.Height;
            var maxX = -1;
            var maxY = -1;

            for (var x = 0; x < imagen.Width; x++)
            {
                for (var y = 0; y < imagen.Height; y++)
                {
                    if (!mascara[x, y])
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return null;
            }

            var origen = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            using var recorte = new Bitmap(origen.Width, origen.Height);
            using (var g = Graphics.FromImage(recorte))
            {
                g.Clear(Color.White);
                g.DrawImage(imagen, new Rectangle(0, 0, recorte.Width, recorte.Height), origen, GraphicsUnit.Pixel);
            }

            using var salida = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(salida))
            {
                g.Clear(Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                var escala = Math.Min(56.0 / recorte.Width, 56.0 / recorte.Height);
                var w = Math.Max(1, (int)Math.Round(recorte.Width * escala));
                var h = Math.Max(1, (int)Math.Round(recorte.Height * escala));
                var x = (64 - w) / 2;
                var y = (64 - h) / 2;
                g.DrawImage(recorte, new Rectangle(x, y, w, h));
            }

            return MascaraDesdeBitmap(salida);
        }

        private static bool[,] MascaraDesdeBitmap(Bitmap imagen)
        {
            var mascara = new bool[imagen.Width, imagen.Height];
            for (var x = 0; x < imagen.Width; x++)
            {
                for (var y = 0; y < imagen.Height; y++)
                {
                    var pixel = imagen.GetPixel(x, y);
                    mascara[x, y] = pixel.R < 150 && pixel.G < 150 && pixel.B < 150;
                }
            }

            return mascara;
        }

        private static double CompararMascaras(bool[,] a, bool[,] b)
        {
            var ancho = Math.Min(a.GetLength(0), b.GetLength(0));
            var alto = Math.Min(a.GetLength(1), b.GetLength(1));
            var interseccion = 0;
            var union = 0;

            for (var x = 0; x < ancho; x++)
            {
                for (var y = 0; y < alto; y++)
                {
                    var av = a[x, y];
                    var bv = b[x, y];
                    if (av && bv) interseccion++;
                    if (av || bv) union++;
                }
            }

            return union == 0 ? 0 : interseccion / (double)union;
        }
        #endregion

        #region Clases Auxiliares
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

        private sealed class MiniApiServer : IDisposable
        {
            private readonly int port;
            private readonly Action startCapture;
            private readonly Action stopCapture;
            private readonly Action captureNow;
            private readonly Func<object> getStatus;
            private readonly CancellationTokenSource cancellation = new();
            private TcpListener? listener;

            public MiniApiServer(
                int port,
                Action startCapture,
                Action stopCapture,
                Action captureNow,
                Func<object> getStatus)
            {
                this.port = port;
                this.startCapture = startCapture;
                this.stopCapture = stopCapture;
                this.captureNow = captureNow;
                this.getStatus = getStatus;
            }

            public void Start()
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start(4);
                _ = Task.Run(AcceptLoopAsync);
            }

            private async Task AcceptLoopAsync()
            {
                while (!cancellation.IsCancellationRequested && listener is not null)
                {
                    try
                    {
                        var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                        _ = Task.Run(() => HandleClientAsync(client));
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        await Task.Delay(250, cancellation.Token).ContinueWith(_ => { });
                    }
                }
            }

            private async Task HandleClientAsync(TcpClient client)
            {
                using var _ = client;

                try
                {
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var requestLine = await reader.ReadLineAsync() ?? string.Empty;
                    var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var method = parts.Length > 0 ? parts[0].ToUpperInvariant() : "GET";
                    var path = parts.Length > 1 ? parts[1].Split('?')[0].TrimEnd('/') : "/status";

                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync()))
                    {
                        // Descarta cabeceras HTTP; la API no necesita cuerpo.
                    }

                    object payload = path.ToLowerInvariant() switch
                    {
                        "/start" when method is "GET" or "POST" => Do(startCapture, "automatic capture started"),
                        "/stop" when method is "GET" or "POST" => Do(stopCapture, "automatic capture stopped"),
                        "/capture" when method is "GET" or "POST" => Do(captureNow, "capture requested"),
                        "/status" or "" => getStatus(),
                        _ => new { error = "not found", endpoints = new[] { "GET /status", "POST /start", "POST /stop", "POST /capture" } }
                    };

                    var statusCode = payload.GetType().GetProperty("error") is null ? "200 OK" : "404 Not Found";
                    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                    var body = Encoding.UTF8.GetBytes(json);
                    var header =
                        $"HTTP/1.1 {statusCode}\r\n" +
                        "Content-Type: application/json; charset=utf-8\r\n" +
                        $"Content-Length: {body.Length}\r\n" +
                        "Connection: close\r\n\r\n";

                    var headerBytes = Encoding.ASCII.GetBytes(header);
                    await stream.WriteAsync(headerBytes);
                    await stream.WriteAsync(body);
                }
                catch
                {
                    // La API local no debe afectar a la captura de pantalla.
                }
            }

            private static object Do(Action action, string message)
            {
                action();
                return new { ok = true, message };
            }

            public void Dispose()
            {
                cancellation.Cancel();
                listener?.Stop();
                cancellation.Dispose();
            }
        }

        private class FormSelectorRegion : Form
        {
            private Point inicioSeleccion;
            private Point finSeleccion;
            private bool seleccionando;
            public Rectangle RegionSeleccionada { get; private set; }

            public FormSelectorRegion()
            {
                WindowState = FormWindowState.Maximized;
                FormBorderStyle = FormBorderStyle.None;
                BackColor = Color.Black;
                Opacity = 0.5;
                DoubleBuffered = true;
                Cursor = Cursors.Cross;
                TopMost = true;
                ShowInTaskbar = false;

                MouseDown += (s, e) =>
                {
                    inicioSeleccion = e.Location;
                    seleccionando = true;
                };

                MouseMove += (s, e) =>
                {
                    if (seleccionando)
                    {
                        finSeleccion = e.Location;
                        Refresh();
                    }
                };

                MouseUp += (s, e) =>
                {
                    seleccionando = false;
                    int x = Math.Min(inicioSeleccion.X, finSeleccion.X);
                    int y = Math.Min(inicioSeleccion.Y, finSeleccion.Y);
                    int width = Math.Abs(finSeleccion.X - inicioSeleccion.X);
                    int height = Math.Abs(finSeleccion.Y - inicioSeleccion.Y);

                    RegionSeleccionada = new Rectangle(x, y, width, height);
                    DialogResult = DialogResult.OK;
                };

                KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        DialogResult = DialogResult.Cancel;
                    }
                };

                Paint += (s, e) =>
                {
                    if (seleccionando)
                    {
                        int x = Math.Min(inicioSeleccion.X, finSeleccion.X);
                        int y = Math.Min(inicioSeleccion.Y, finSeleccion.Y);
                        int width = Math.Abs(finSeleccion.X - inicioSeleccion.X);
                        int height = Math.Abs(finSeleccion.Y - inicioSeleccion.Y);

                        using var pen = new Pen(Color.Red, 2);
                        e.Graphics.DrawRectangle(pen, x, y, width, height);
                    }
                };
            }
        }
        #endregion
    }
}
