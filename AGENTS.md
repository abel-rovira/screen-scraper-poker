# AGENTS.md

Guia para agentes que trabajen en este repositorio.

## Proyecto

`PokerScreenScraper` es una aplicacion WinForms en C#/.NET para Windows. Captura pantalla automaticamente, guarda PNG y muestra un overlay con informacion de cartas/probabilidad cuando hay datos validos.

El proyecto debe mantenerse como una sola aplicacion principal. No hay `PokerCardApi`, OpenAI, Ollama ni API externa.

Target:

```text
net10.0-windows
```

## Archivos principales

- `Program.cs`: entrada de la app.
- `FormPrincipal.cs`: UI, captura, bandeja del sistema, overlay, guardado y mini API local.
- `FormPrincipal.Designer.cs`: wiring parcial del formulario.
- `FormSelectorRegion.cs`: selector visual de region.
- `PokerOddsCalculator.cs`: parser de cartas y calculo de probabilidades.
- `Publish-App.cmd`: publica el ejecutable unico.
- `README.md`: instrucciones para usuario final.

## Reglas

- No reintroducir APIs externas, OpenAI, Ollama ni `PokerCardApi` salvo peticion explicita.
- No subir claves, tokens, capturas personales ni builds.
- No subir `bin/`, `obj/`, `.vs/`, `.dotnet/`, `dist/`, `*.zip` ni `*.bundle`.
- Mantener una ruta manual fiable para introducir cartas.
- No bloquear la UI; usar `async/await` y timeouts cuando aplique.
- No implementar automatizacion de juego ni interaccion automatica con clientes de poker.

## Comandos

Compilar:

```powershell
dotnet build .\PokerScreenScraper.csproj
```

Ejecutar:

```powershell
dotnet run --project .\PokerScreenScraper.csproj
```

Publicar:

```powershell
.\Publish-App.cmd
```

Ejecutable generado:

```text
dist\Logitech Controller Single\Logitech Controller.exe
```

## Mini API local integrada

El ejecutable escucha en:

```text
http://127.0.0.1:5056
```

Endpoints:

```text
GET /status
POST /start
POST /stop
POST /capture
```

## Formato de cartas

Formato:

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

## Validacion

Antes de entregar:

```powershell
dotnet build .\PokerScreenScraper.csproj
```

Si se toca la captura o la mini API, probar tambien:

```powershell
Invoke-RestMethod http://127.0.0.1:5056/status
```
