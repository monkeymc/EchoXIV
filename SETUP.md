# Setup del Plugin EchoXIV

## ⚠️ Requisitos Previos

Necesitas tener **XIVLauncher instalado y haber ejecutado FFXIV al menos una vez** para que las DLLs de Dalamud estén disponibles en tu sistema.

## 📂 Paso 1: Verificar DLLs de Dalamud

Asegúrate de que la siguiente ruta existe y contiene las dependencias necesarias:

```
%AppData%\XIVLauncher\addon\Hooks\dev\
```

Archivos requeridos:

- `Dalamud.dll`
- `ImGui.NET.dll`
- `FFXIVClientStructs.dll`
- `Lumina.dll`

## 🔨 Paso 2: Compilar el Plugin

Abre una terminal en la carpeta del proyecto y ejecuta:

```powershell
dotnet clean
dotnet build -c Release
```

## 📦 Paso 3: Instalación Manual

Una vez compilado, debes copiar los archivos a la carpeta de plugins de Dalamud:

1. Crea la carpeta `%AppData%\XIVLauncher\devPlugins\EchoXIV` si no existe.
2. Copia el contenido de `EchoXIV/bin/Release/` a esa carpeta.
   - Asegúrate de incluir `EchoXIV.dll`, `EchoXIV.json` y la carpeta `images`.

## 🎮 Paso 4: Configuración en el Juego

1. Ejecuta FFXIV.
2. Escribe `/xlsettings` -> pestaña **Experimental**.
3. Activa **"Enable plugin testing"**.
4. En **"Dev Plugin Locations"**, añade: `%AppData%\XIVLauncher\devPlugins`.
5. Abre la lista de librerías con `/xlplugins`.
6. Busca e instala **EchoXIV**.

## 🔧 Primeros Pasos

Una vez activo, usa el comando `/echoxiv` para configurar tus idiomas de origen y destino.

---

**¿Problemas?** Revisa los logs en tiempo real con `/xllog` dentro del juego.
