# PokerScreenScraper

Aplicacion de escritorio para Windows que captura automaticamente una zona de pantalla, guarda imagenes PNG y dibuja un panel de informacion encima de cada captura.

El ejecutable final se llama:

```text
Logitech Controller.exe
```

## Que hace

- Captura pantalla automaticamente cada 5 segundos.
- Guarda las capturas en la carpeta de imagenes del usuario.
- Dibuja un overlay sobre la captura con cartas, mano y probabilidades cuando hay datos validos.
- Intenta reconocer cartas visibles con OCR local y plantillas internas.
- Si existe un modelo YOLO/ONNX local, lo usa primero para detectar cartas como `2h`, `3d`, `As`, etc.
- Permite introducir cartas manualmente desde la interfaz.
- Calcula probabilidades educativas de Texas Hold'em.
- Se puede quedar funcionando en segundo plano desde la bandeja del sistema.
- Incluye una mini API local dentro del propio ejecutable para consultar estado o controlar la captura.

## Que no hace

- No abre una API externa aparte.
- No necesita `PokerCardApi.exe`.
- No usa OpenAI ni Ollama.
- No automatiza acciones dentro de ningun juego.
- No garantiza reconocimiento automatico perfecto de cartas en cualquier web o cliente. Cada juego puede cambiar cartas, zoom, colores y posiciones. Si una pantalla no se lee bien, usa la seleccion de zonas o introduce las cartas manualmente.

## Reconocimiento de cartas

El ejecutable intenta detectar cartas sin API externa:

```text
captura
-> si existe models\card-detector.onnx, detecta cartas con YOLO/ONNX
-> busca regiones con cartas visibles
-> recorta las cartas
-> reconoce el valor con Tesseract si esta instalado
-> si no hay Tesseract, usa plantillas internas generadas por el propio programa
-> reconoce el palo comparando contra plantillas internas: corazones, diamantes, picas y treboles
-> convierte el resultado a strings como 3d, 4s, Ah, Tc
```

Tesseract es opcional. Si esta instalado, mejora el OCR de los valores:

```text
C:\Program Files\Tesseract-OCR\tesseract.exe
```

Tambien se puede indicar manualmente con:

```powershell
$env:TESSERACT_EXE="C:\Program Files\Tesseract-OCR\tesseract.exe"
```

### Modelo YOLO/ONNX

La ruta esperada para un modelo entrenado es:

```text
models\card-detector.onnx
```

Las etiquetas estan en:

```text
models\labels.txt
```

Formato de etiquetas esperado:

```text
2h
2d
2s
2c
...
Ah
Ad
As
Ac
```

Si el modelo existe, el programa lo usa antes que OCR/plantillas. Si no existe, el ejecutable sigue funcionando con el detector local y entrada manual.

## Ejecutable

Para generar el ejecutable unico:

```powershell
.\Publish-App.cmd
```

El archivo queda en:

```text
dist\Logitech Controller Single\Logitech Controller.exe
```

Ruta completa habitual:

```text
C:\Users\Usuario\Documents\2026-04-27\haz-un-screen-carper-para-extrar\PokerScreenScraper\dist\Logitech Controller Single\Logitech Controller.exe
```

Para ejecutarlo desde PowerShell:

```powershell
& "C:\Users\Usuario\Documents\2026-04-27\haz-un-screen-carper-para-extrar\PokerScreenScraper\dist\Logitech Controller Single\Logitech Controller.exe"
```

Al abrirse, la aplicacion se oculta y empieza a capturar automaticamente. Se puede abrir de nuevo desde el icono de la bandeja del sistema.

## Donde guarda las capturas

Por defecto:

```text
C:\Users\Usuario\Pictures\PokerScreenScraper
```

Los nombres siguen este formato:

```text
poker_screenshot_yyyyMMdd_HHmmss_fff_0001.png
```

## Mini API local integrada

La mini API va dentro del mismo ejecutable. No hay que abrir otra consola ni otro programa.

URL:

```text
http://127.0.0.1:5056
```

Consultar estado:

```powershell
Invoke-RestMethod http://127.0.0.1:5056/status
```

Iniciar captura automatica:

```powershell
Invoke-RestMethod http://127.0.0.1:5056/start -Method Post
```

Detener captura automatica:

```powershell
Invoke-RestMethod http://127.0.0.1:5056/stop -Method Post
```

Forzar una captura:

```powershell
Invoke-RestMethod http://127.0.0.1:5056/capture -Method Post
```

## Formato de cartas

Las cartas se escriben con valor + palo:

```text
Ah Kd 3s Tc
```

Valores:

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
3d = 3 de diamantes
4s = 4 de picas
Qh = reina de corazones
Tc = diez de treboles
```

## Ejecutar desde codigo

Requisitos:

- Windows.
- .NET SDK 10.

Comando:

```powershell
dotnet run --project .\PokerScreenScraper.csproj
```

## Compilar

```powershell
dotnet build .\PokerScreenScraper.csproj
```

## Archivos principales

- `Program.cs`: arranque de la aplicacion.
- `FormPrincipal.cs`: interfaz, captura automatica, overlay, bandeja del sistema y mini API local.
- `FormSelectorRegion.cs`: selector visual de region.
- `PokerOddsCalculator.cs`: calculo de probabilidades.
- `Publish-App.cmd`: genera el ejecutable unico.

## Subir el ejecutable al repo

La carpeta `dist/` esta ignorada para evitar subir builds por accidente. Si quieres subir el ejecutable igualmente:

```powershell
git add -f "dist/Logitech Controller Single/Logitech Controller.exe"
git add .
git commit -m "Add executable and update documentation"
git push -u origin main
```
