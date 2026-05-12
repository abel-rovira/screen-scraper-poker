namespace PokerScreenScraper;

static class Program
{
    /// <summary>
    ///  Punto de entrada principal de la aplicacion.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new FormPrincipal());
    }    
}
