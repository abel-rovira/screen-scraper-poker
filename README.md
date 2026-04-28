# PokerScreenScraper

Herramienta WinForms en .NET para capturar una region de pantalla, guardar imagenes PNG y mostrar informacion de poker calculada a partir de cartas introducidas manualmente o detectadas por una API local.

Incluye interfaz visual oscura, previsualizacion, contador de capturas, lista de PNG recientes, calculo de probabilidades de Texas Hold'em y una API local auxiliar para intentar detectar cartas desde las capturas.

Uso previsto: capturas locales autorizadas, depuracion, datasets propios, analisis visual manual y calculo educativo de probabilidades. Revisa siempre las normas del juego o plataforma antes de usar cualquier herramienta externa sobre un cliente de poker.

## Requisitos

- Windows.
- .NET SDK/runtime 10 instalado.
- PowerShell.

El proyecto se ha actualizado para compilar con:

```text
net10.0-windows
```

La API local tambien usa:

```text
net10.0-windows
```

## Proyectos incluidos

```text
PokerScreenScraper.csproj
PokerCardApi\PokerCardApi.csproj
```

- `PokerScreenScraper`: aplicacion WinForms principal.
- `PokerCardApi`: API local en `http://127.0.0.1:5055`.

## Ejecutar API local

Abre una ventana de PowerShell:

```powershell
cd "C:\Users\Usuario\Documents\2026-04-27\haz-un-screen-carper-para-extrar\PokerScreenScraper"
& "C:\Program Files\dotnet\dotnet.exe" run --project ".\PokerCardApi\PokerCardApi.csproj"
```

Dejala abierta. La API escucha en:

```text
http://127.0.0.1:5055
```

Endpoint principal:

```text
POST /detect
```

## Ejecutar aplicacion principal

Abre otra ventana de PowerShell:

```powershell
cd "C:\Users\Usuario\Documents\2026-04-27\haz-un-screen-carper-para-extrar\PokerScreenScraper"
& "C:\Program Files\dotnet\dotnet.exe" run --project ".\PokerScreenScraper.csproj"
```

La app puede ocultarse en la bandeja del sistema. Usa el icono de bandeja para mostrar la ventana, capturar ahora o salir.

## Como usar

1. Ejecuta primero la API local.
2. Ejecuta la app principal.
3. Pulsa **Seleccionar region** y arrastra sobre la zona que quieres capturar.
4. Elige la carpeta de salida o deja la carpeta por defecto en Imagenes.
5. Pulsa **Capturar ahora** para una captura unica.
6. Ajusta **Intervalo ms** e inicia la captura automatica.
7. Haz doble clic en una captura reciente para verla otra vez en la previsualizacion.

Los archivos se guardan como:

```text
prefijo_yyyyMMdd_HHmmss_fff_0001.png
```

## Calculo manual de probabilidad

La app permite introducir cartas manualmente y calcular:

```text
Ganar | Perder | Empate | Mano actual
```

Formato de cartas:

```text
Ah Kh
3h 3s
4d Qh
```

Valores permitidos:

```text
2 3 4 5 6 7 8 9 T J Q K A
```

Palos:

```text
h = corazones
d = diamantes
s = picas
c = treboles
```

Ejemplos:

```text
Ah = As de corazones
Kd = Rey de diamantes
Ts = Diez de picas
7c = Siete de treboles
```

## Flujo automatico

En cada captura, la aplicacion intenta hacer este flujo:

```text
Captura pantalla
-> envia imagen a PokerCardApi
-> recibe playerCards y boardCards
-> rellena las cajas de cartas
-> calcula probabilidad
-> dibuja el resultado encima de la captura
-> guarda PNG
```

La respuesta esperada de la API es:

```json
{
  "playerCards": ["4d", "Qh"],
  "boardCards": [],
  "confidence": 0.92,
  "message": "Cartas detectadas correctamente",
  "mode": "local-template"
}
```

## Detector local gratuito

La API local incluye un modo gratuito basado en plantillas.

Primero guarda recortes de cartas boca arriba en:

```text
PokerCardApi\bin\Debug\net10.0-windows\training-crops
```

Despues se pueden crear plantillas en:

```text
PokerCardApi\bin\Debug\net10.0-windows\card-templates
```

Los archivos de plantilla deben llamarse con el valor real de la carta:

```text
4d.png
Qh.png
3s.png
3h.png
Ah.png
```

Cuando existan plantillas, la API comparara los recortes nuevos contra esas imagenes y devolvera las cartas detectadas.

## Detector con OpenAI Vision opcional

La API tambien puede usar vision mediante una variable de entorno:

```powershell
$env:OPENAI_API_KEY="TU_CLAVE_AQUI"
```

Luego ejecuta la API:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" run --project ".\PokerCardApi\PokerCardApi.csproj"
```

Si no defines `OPENAI_API_KEY`, la API funciona solo con el detector local por plantillas.

## Probar la API con una imagen

Con la API arrancada:

```powershell
curl.exe -X POST "http://127.0.0.1:5055/detect" `
  -F "image=@C:\ruta\a\captura.png"
```

Respuesta esperada:

```json
{
  "playerCards": ["4d", "Qh"],
  "boardCards": [],
  "confidence": 0.9,
  "message": "...",
  "mode": "..."
}
```

## Publicar EXE

```powershell
dotnet publish .\PokerScreenScraper.csproj -c Release -r win-x64 --self-contained false
```

El ejecutable principal se genera como:

```text
PokerScreenScraper.exe
```

## Archivos principales

- `Program.cs`: inicia la aplicacion WinForms.
- `FormPrincipal.cs`: interfaz, captura, bandeja del sistema, overlay y calculo.
- `CardDetector.cs`: cliente HTTP que envia capturas a la API local.
- `PokerOddsCalculator.cs`: calculadora de probabilidades.
- `PokerCardApi\Program.cs`: API local de deteccion.
- `FormSelectorRegion.cs`: selector visual de region.
