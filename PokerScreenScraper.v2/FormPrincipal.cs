using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        private readonly Label etiquetaEstado = new();
        private readonly Label etiquetaRegion = new();
        private readonly Label etiquetaContador = new();
        private readonly Label etiquetaProbabilidad = new();
        private readonly Label etiquetaCartasDetectadas = new();

        private readonly PictureBox vistaPrevia = new();
        private readonly ListBox listaCapturasRecientes = new();
        private readonly System.Windows.Forms.Timer temporizadorCaptura = new();
        private readonly NotifyIcon iconoBandeja = new();
        private readonly ContextMenuStrip menuBandeja = new();

        private bool capturaAutomaticaActiva;
        private bool salirAplicacion;
        private int indiceCaptura = 1;
        private string textoProbabilidadActual = "Probabilidad no calculada";
        #endregion

        #region Modelos de Datos
        public record Card(int Value, Suit Suit);
        public record HandValue(int Fuerza, string Descripcion);
        public record OddsResult(double ProbabilidadGanar, double ProbabilidadEmpatar, string DescripcionMano);

        public enum Suit
        {
            Corazones,
            Diamantes,
            Treboles,
            Picas
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

            temporizadorCaptura.Tick += async (_, _) => await CapturarYGuardarAsync();
            Shown += FormPrincipal_Shown;
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

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
            AgregarFilaCompleta(disposicion, etiquetaProbabilidad, 50);

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

            var pantalla = SystemInformation.VirtualScreen;
            entradaX.Value = Limitar(pantalla.X, entradaX.Minimum, entradaX.Maximum);
            entradaY.Value = Limitar(pantalla.Y, entradaY.Minimum, entradaY.Maximum);
            entradaAncho.Value = Limitar(pantalla.Width, entradaAncho.Minimum, entradaAncho.Maximum);
            entradaAlto.Value = Limitar(pantalla.Height, entradaAlto.Minimum, entradaAlto.Maximum);

            entradaIntervalo.Minimum = 100;
            entradaIntervalo.Maximum = 60000;
            entradaIntervalo.Value = 5000;
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
            etiquetaEstado.Text = "Listo. Captura automatica configurada cada 5 segundos.";
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

            var deteccion = await CardDetector.DetectarCartasAsync(imagen);
            var cartas = deteccion.Error is null && (deteccion.PlayerCards.Count > 0 || deteccion.BoardCards.Count > 0)
                ? (Jugador: deteccion.PlayerCards, Mesa: deteccion.BoardCards)
                : AnalizarCartasEnImagen(imagen, (int)entradaUmbral.Value);

            etiquetaCartasDetectadas.Text = deteccion.Error is null
                ? $"API local: Jugador {string.Join(' ', cartas.Jugador)} | Mesa {string.Join(' ', cartas.Mesa)}"
                : $"API local no disponible ({deteccion.Error}). Local: Jugador {string.Join(' ', cartas.Jugador)} | Mesa {string.Join(' ', cartas.Mesa)}";

            if (cartas.Jugador.Count == 2)
            {
                entradaCartasJugador.Text = string.Join(' ', cartas.Jugador);
                entradaCartasMesa.Text = string.Join(' ', cartas.Mesa);
                CalcularProbabilidad(true);
                etiquetaEstado.Text = "Cartas analizadas y probabilidad actualizada.";
            }
        }

        private void ConfigurarCalculoAutomatico()
        {
            entradaCartasJugador.TextChanged += (_, _) => CalcularProbabilidad(false);
            entradaCartasMesa.TextChanged += (_, _) => CalcularProbabilidad(false);
            entradaRivales.ValueChanged += (_, _) => CalcularProbabilidad(false);
            CalcularProbabilidad(false);
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
            temporizadorCaptura.Dispose();
            iconoBandeja.Dispose();
            menuBandeja.Dispose();
            vistaPrevia.Image?.Dispose();
            base.OnFormClosed(e);
        }
        #endregion

        #region Funcionalidad Principal

        private async Task CapturarYGuardarAsync()
        {
            await Task.Yield();

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

                CalcularProbabilidad(false);
                DibujarProbabilidadEnCaptura(imagen);

                var nombreArchivo = $"{LimpiarPrefijo(entradaPrefijo.Text)}" +
                                    $"_{DateTime.Now:yyyyMMdd_HHmmss_fff}" +
                                    $"_{indiceCaptura:0000}.png";
                var rutaArchivo = Path.Combine(entradaCarpeta.Text, nombreArchivo);
                imagen.Save(rutaArchivo, ImageFormat.Png);
                indiceCaptura++;

                vistaPrevia.Image?.Dispose();
                vistaPrevia.Image = new Bitmap(imagen);

                etiquetaEstado.Text = $"Guardado: {Path.GetFileName(rutaArchivo)}";
                AgregarCapturaReciente(rutaArchivo);
                ActualizarContadorCapturas();
                ActualizarEtiquetaRegion();
            }
            catch (Exception ex) when (ex is ExternalException or ArgumentException
                                            or IOException or UnauthorizedAccessException)
            {
                etiquetaEstado.Text = $"Error: {ex.Message}";
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
                : entradaCartasJugador.Text.Trim();
            var cartasMesa = string.IsNullOrWhiteSpace(entradaCartasMesa.Text)
                ? "Sin mesa"
                : entradaCartasMesa.Text.Trim();
            var probabilidad = string.IsNullOrWhiteSpace(textoProbabilidadActual)
                ? "Probabilidad no disponible"
                : textoProbabilidadActual;

            var ancho = Math.Min(430, Math.Max(300, imagen.Width / 2));
            var rectangulo = new Rectangle(12, 12, ancho, 168);

            graficos.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graficos.FillRectangle(fondo, rectangulo);
            ControlPaint.DrawBorder(graficos, rectangulo, Color.FromArgb(120, 230, 145), ButtonBorderStyle.Solid);
            graficos.DrawString("Poker odds", fuenteTitulo, textoAcento, rectangulo.Left + 12, rectangulo.Top + 10);
            graficos.DrawString($"Cartas: {cartasJugador}", fuenteTexto, textoPrincipal, rectangulo.Left + 12, rectangulo.Top + 42);
            graficos.DrawString($"Mesa: {cartasMesa}", fuenteTexto, textoPrincipal, rectangulo.Left + 12, rectangulo.Top + 68);
            graficos.DrawString(probabilidad, fuenteTexto, textoAcento, rectangulo.Left + 12, rectangulo.Top + 96);
            graficos.DrawString($"Rivales: {(int)entradaRivales.Value}", fuenteTexto, textoPrincipal, rectangulo.Left + 12, rectangulo.Top + 122);

            if (cartasJugador == "No detectadas")
            {
                graficos.DrawString("Pulsa Analizar imagen o introduce cartas", fuentePequena, textoAviso, rectangulo.Left + 12, rectangulo.Top + 146);
            }
        }

        private void CalcularProbabilidad(bool mostrarErrores)
        {
            try
            {
                var resultado = PokerOddsCalculator.Calcular(entradaCartasJugador.Text, entradaCartasMesa.Text, (int)entradaRivales.Value);
                textoProbabilidadActual = $"Ganar: {resultado.ProbabilidadGanar:P1} | Empate: {resultado.ProbabilidadEmpatar:P1}";
                etiquetaProbabilidad.Text = textoProbabilidadActual;
                etiquetaEstado.Text = $"Mejor mano actual: {resultado.DescripcionMano}";
            }
            catch (ArgumentException ex)
            {
                textoProbabilidadActual = "Probabilidad no disponible";
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

        private void Salir()
        {
            salirAplicacion = true;
            temporizadorCaptura.Stop();
            iconoBandeja.Visible = false;
            Close();
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
            etiquetaRegion.Text = $"Region activa: X {(int)entradaX.Value} | Y {(int)entradaY.Value} | {(int)entradaAncho.Value} x {(int)entradaAlto.Value} px";
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
                    throw new ArgumentException("La mesa no puede tener mÃ¡s de 5 cartas");

                var baraja = CrearBarajaCompleta();
                baraja = baraja.Except(cartasJugador).Except(cartasMesa).ToList();

                int victorias = 0;
                int empates = 0;
                var random = new Random();

                for (int i = 0; i < Simulaciones; i++)
                {
                    var barajaMezclada = baraja.OrderBy(x => random.Next()).ToList();
                    var mesaCompleta = new List<Card>(cartasMesa);

                    // Completar mesa si es necesario
                    int cartasFaltantes = 5 - mesaCompleta.Count;
                    if (cartasFaltantes > 0)
                        mesaCompleta.AddRange(barajaMezclada.Take(cartasFaltantes));

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

                    if (gano) victorias++;
                    if (hayEmpate) empates++;
                }

                return new OddsResult(
                    (double)victorias / Simulaciones,
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
            // Coordenadas ajustables segÃºn tu cliente de poker
            var regionesCartas = new Dictionary<string, Rectangle[]>
            {
                ["Jugador"] = new[]
                {
                    new Rectangle(50, 200, 80, 120),  // Carta 1 jugador
                    new Rectangle(150, 200, 80, 120)  // Carta 2 jugador
                },
                ["Mesa"] = new[]
                {
                    new Rectangle(250, 150, 80, 120),  // Flop 1
                    new Rectangle(350, 150, 80, 120),  // Flop 2
                    new Rectangle(450, 150, 80, 120),  // Flop 3
                    new Rectangle(550, 150, 80, 120),  // Turn
                    new Rectangle(650, 150, 80, 120)   // River
                }
            };

            var resultado = (
                Jugador: new List<string>(),
                Mesa: new List<string>()
            );

            foreach (var region in regionesCartas["Jugador"])
            {
                var carta = RecortarCarta(imagen, region);
                if (carta != null)
                {
                    var valor = ReconocerCarta(carta, umbral);
                    if (!string.IsNullOrEmpty(valor)) resultado.Jugador.Add(valor);
                }
            }

            foreach (var region in regionesCartas["Mesa"])
            {
                var carta = RecortarCarta(imagen, region);
                if (carta != null)
                {
                    var valor = ReconocerCarta(carta, umbral);
                    if (!string.IsNullOrEmpty(valor)) resultado.Mesa.Add(valor);
                }
            }

            return resultado;
        }

        private Bitmap? RecortarCarta(Bitmap imagen, Rectangle region)
        {
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
                // 1. Convertir a escala de grises
                using var imagenGris = ConvertirAGrises(imagenCarta);

                // 2. Procesar imagen (umbral)
                using var imagenProcesada = AplicarUmbral(imagenGris, umbral);

                // 3. Detectar valor
                var valor = DetectarValorCarta(imagenProcesada);
                if (string.IsNullOrEmpty(valor)) return null;

                // 4. Detectar palo
                var palo = DetectarPaloCarta(imagenCarta);

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

        private string DetectarPaloCarta(Bitmap imagen)
        {
            // Analizar zona central donde estÃ¡ el palo
            var regionPalo = new Rectangle(
                imagen.Width / 4,
                imagen.Height / 3,
                imagen.Width / 2,
                imagen.Height / 3
            );

            int rojos = 0;
            int muestras = 0;

            for (int x = regionPalo.Left; x < regionPalo.Right; x += 3)
            {
                for (int y = regionPalo.Top; y < regionPalo.Bottom; y += 3)
                {
                    var pixel = imagen.GetPixel(x, y);
                    if (pixel.R > pixel.G * 1.3 && pixel.R > pixel.B * 1.3)
                        rojos++;
                    muestras++;
                }
            }

            // Si mÃ¡s del 20% de los pÃ­xeles son rojizos -> Corazones (h), sino Picas (s)
            // (Simplificado para demo - deberÃ­as agregar detecciÃ³n de diamantes/trÃ©boles)
            return (muestras > 0 && rojos > muestras * 0.2) ? "h" : "s";
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
