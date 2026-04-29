# 🎨 Guía de Theming y Branding — AF-WebChat

## Cambio Rápido para Demo con Cliente

### Paso 1: Abre `branding-presets.json`
Contiene presets listos para diferentes industrias:

| Preset | Tema | Industria | Colores |
|--------|------|-----------|---------|
| `Microsoft_Dark` | Dark | Tecnología | Azul Microsoft |
| `Microsoft_Light` | Light | Tecnología | Azul Microsoft |
| `Corporativo_Dorado_Dark` | Dark | Enterprise | Dorado + Azul |
| `Banco_Azul_Light` | Light | Banca | Azul corporativo |
| `Salud_Verde_Light` | Light | Salud | Verde clínico |
| `Gobierno_Rojo_Dark` | Dark | Gobierno | Rojo + Verde |

### Paso 2: Copia el preset a `appsettings.json`
Reemplaza la sección `"AppBranding"` completa:

```json
{
  "AppBranding": {
    // ... pega aquí el contenido del preset elegido
  }
}
```

### Paso 3: Reinicia la aplicación
```bash
dotnet run
```

¡Listo! La app se verá completamente diferente.

---

## Estructura de Estilos

```
AF-WebChat/
├── appsettings.json              ← Branding activo (textos, colores, tema)
├── branding-presets.json          ← Catálogo de presets por industria
├── Views/Shared/_Layout.cshtml    ← Variables CSS dinámicas (Razor → CSS vars)
├── wwwroot/css/
│   ├── site.css                   ← Estilos globales + light theme overrides
│   └── agent-chat.css             ← Estilos del chat + light theme overrides
└── Models/
    └── AppBrandingSettings.cs     ← Modelo C# del branding
```

### Flujo de datos:
```
appsettings.json → AppBrandingSettings.cs → _Layout.cshtml (CSS vars) → site.css / agent-chat.css
```

---

## Propiedades de Branding

### Identidad
| Propiedad | Descripción | Ejemplo |
|-----------|-------------|---------|
| `LogoUrl` | URL de imagen del logo | `"/images/logo.png"` |
| `LogoIcon` | Icono Bootstrap (si no hay logo) | `"bi-robot"`, `"bi-bank"` |
| `Title` | Nombre de la app en navbar | `"Asistente Bancario IA"` |
| `Subtitle` | Subtítulo descriptivo | `"Plataforma de Atención"` |

### Tema
| Propiedad | Descripción | Valores |
|-----------|-------------|---------|
| `Theme` | Tema visual | `"dark"` o `"light"` |

### Colores
| Propiedad | Qué afecta | Dark default | Light ejemplo |
|-----------|------------|--------------|---------------|
| `PrimaryColor` | Títulos, headings | `#ffffff` | `#1a1a1a` |
| `SecondaryColor` | Links, highlights | `#58a6ff` | `#0078d4` |
| `BackgroundColor` | Fondo principal | `#0d1117` | `#f5f5f5` |
| `NavbarColor` | Barra navegación + sidebar | `#1a1a2e` | `#ffffff` |
| `TextColor` | Texto general | `#ffffff` | `#212529` |
| `AccentColor` | Botones, badges, acentos | `#C9A227` | `#0078d4` |
| `SuccessColor` | Indicadores de éxito | `#238636` | `#198754` |
| `ErrorColor` | Indicadores de error | `#f85149` | `#dc3545` |

### Textos del Chat
| Propiedad | Qué afecta |
|-----------|------------|
| `WelcomeMessage` | Mensaje en la página de inicio |
| `ChatWelcomeTitle` | Título de bienvenida en el chat |
| `ChatWelcomeSubtitle` | Subtítulo de bienvenida |
| `FooterText` | Texto del pie de página (vacío = sin footer) |

### Animaciones de Fondo
| Propiedad | Descripción |
|-----------|-------------|
| `Shape1ColorFrom/To` | Gradiente de la forma animada 1 |
| `Shape2ColorFrom/To` | Gradiente de la forma animada 2 |
| `Shape3ColorFrom/To` | Gradiente de la forma animada 3 |
| `ShapeAnimationSeconds` | Duración del ciclo de animación |

---

## Crear un Preset Nuevo

1. Abre `branding-presets.json`
2. Copia un preset existente
3. Cambia los valores
4. **Tip**: Para tema light, usa colores oscuros en `PrimaryColor`/`TextColor` y claros en `BackgroundColor`/`NavbarColor`

### Regla de oro para Light Theme:
```
BackgroundColor = claro (#f5f5f5, #f8f9fa, #ffffff)
NavbarColor     = claro (#ffffff)
TextColor       = oscuro (#1a1a1a, #212529)
PrimaryColor    = oscuro (#003366, #1a1a1a)
```

### Regla de oro para Dark Theme:
```
BackgroundColor = oscuro (#0d1117, #1a0a0a)
NavbarColor     = oscuro (#1a1a2e, #2d1111)
TextColor       = claro  (#ffffff, #e0e0e0)
PrimaryColor    = claro  (#ffffff)
```

---

## Agregar Logo de Cliente

1. Coloca la imagen en `wwwroot/images/`
2. En `appsettings.json`:
```json
{
  "AppBranding": {
    "LogoUrl": "/images/cliente-logo.png",
    "Title": "Nombre del Cliente"
  }
}
```

---

## Iconos Disponibles para LogoIcon

Usa cualquier icono de [Bootstrap Icons](https://icons.getbootstrap.com/):

| Industria | Icono sugerido |
|-----------|---------------|
| Tecnología | `bi-cpu`, `bi-robot`, `bi-microsoft` |
| Banca | `bi-bank`, `bi-currency-dollar` |
| Salud | `bi-heart-pulse`, `bi-hospital` |
| Gobierno | `bi-flag`, `bi-building` |
| Educación | `bi-mortarboard`, `bi-book` |
| Retail | `bi-shop`, `bi-cart` |
| Logística | `bi-truck`, `bi-box` |
