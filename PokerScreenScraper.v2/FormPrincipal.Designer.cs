namespace PokerScreenScraper;

partial class FormPrincipal
{
    /// <summary>
    ///  Variable requerida por el disenador.
    /// </summary>
    private System.ComponentModel.IContainer components = null!;

    /// <summary>
    ///  Libera los recursos utilizados.
    /// </summary>
    /// <param name="disposing">true si se deben liberar recursos administrados; false en caso contrario.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Metodo requerido por el disenador.
    /// </summary>
    private void InitializeComponent()
    {
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1080, 700);
        this.MinimumSize = new System.Drawing.Size(960, 620);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Poker Screen Scraper";
    }

    #endregion
}
