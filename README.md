# PokerScreenScraper

Herramienta WinForms en .NET para capturar una region de pantalla y guardar imagenes PNG.
Incluye interfaz visual oscura, previsualizacion, contador de capturas y lista de PNG recientes.

Uso previsto: capturas locales autorizadas para depuracion, datasets propios o analisis visual manual. No incluye reconocimiento de cartas, decisiones de juego, automatizacion ni integracion con clientes de poker online.

## Ejecutar

```powershell
dotnet run --project .\PokerScreenScraper.csproj
```

## Como usar

1. Pulsa **Seleccionar region** y arrastra sobre la zona de pantalla que quieres extraer.
2. Elige la carpeta de salida o deja la carpeta por defecto en Imagenes.
3. Pulsa **Capturar PNG** para una captura unica.
4. Ajusta **Intervalo ms** y pulsa **Iniciar auto** para guardar capturas periodicas.
5. Haz doble clic en una captura reciente para verla otra vez en la previsualizacion.

Los archivos se guardan como:

```text
prefijo_yyyyMMdd_HHmmss_fff_0001.png
```

## Publicar EXE

```powershell
dotnet publish .\PokerScreenScraper.csproj -c Release -r win-x64 --self-contained false
```
