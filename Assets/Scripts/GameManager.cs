using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum MoonPhase
{
    New,
    Waxing,
    Full,
    Third
}

/// <summary>
/// Gestor principal y global del juego (GameManager).
/// Controla:
/// 1) Los niveles de reputación (1 al 5) y los puntos según el radio de llegada.
/// 2) El catálogo global de personajes para diálogos y el Sprite del bloque de diálogos.
/// 3) La pausa del juego para mostrar conversaciones turno por turno entre Misiones Principales.
/// 4) El Menú de Pausa al presionar la tecla ESCAPE.
/// 5) El ciclo lunar de las noches.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // =========================================================================
    // CLASE 1: CONFIGURACION GLOBAL DE NIVELES Y PUNTOS
    // =========================================================================

    [Serializable]
    public class ConfiguracionNivel
    {
        [Tooltip("Nombre descriptivo del nivel en el Inspector (ej. Nivel 1 - Novato de Zuber)")]
        public string nombreDelNivel = "Nivel 1";

        [Tooltip("Número identificador del nivel (1, 2, 3, 4, 5)")]
        public int numeroNivel = 1;

        [Header("Puntos por Misión en este Nivel")]
        [Tooltip("Puntos base que gana el jugador al dejar al pasajero dentro del radio normal de llegada (Nivel 1 = 10 puntos)")]
        public int puntosBasePorMision = 10;

        [Tooltip("Puntos que gana el jugador al dejar al pasajero en el radio perfecto central (Nivel 1 = 5 puntos)")]
        public int puntosRadioPerfecto = 5;

        [Tooltip("Si está activo, al caer en el radio perfecto suma (Puntos Base + Puntos Radio Perfecto = 15 pts). Si lo desmarcas, da únicamente el valor de Puntos Radio Perfecto.")]
        public bool sumarBonoPerfectoAlBase = true;

        [Tooltip("Puntos que se restan si el jugador deja al pasajero fuera del área de llegada")]
        public int penalizacionFueraDeRadio = 0;

        [Header("Progresión de Misiones Principales del Nivel")]
        [Tooltip("Cantidad recomendada de misiones principales que componen este nivel antes de subir al siguiente")]
        public int cantidadMisionesPrincipalesDelNivel = 2;
    }

    // =========================================================================
    // CLASE 2: CATALOGO DE PERSONAJES (ICONOS Y SONIDOS PARA LOS DIALOGOS)
    // =========================================================================

    [Serializable]
    public class PerfilPersonajeDialogo
    {
        [Tooltip("Nombre del personaje tal como lo escribirás en la línea de diálogo (ej. Patron, Chofer, Perro, Cholito)")]
        public string nombrePersonaje = "Chofer";

        [Tooltip("Imagen (Sprite) del personaje que aparecerá destacada sobre el fondo negro semitransparente")]
        public Sprite iconoPorDefecto;

        [Tooltip("Color con el que se pintará el nombre y el borde de la caja cuando hable este personaje")]
        public Color colorNombre = new Color(0.3f, 0.95f, 0.45f, 1f);

        [Tooltip("Sonido que se reproducirá cuando le toque hablar a este personaje (ej. ladrido para el Perro, voz para el Patrón)")]
        public AudioClip sonidoVozPorDefecto;
    }

    // =========================================================================
    // VARIABLES GLOBALES DE NIVELES Y PUNTAJE
    // =========================================================================

    [Header("1. Estado Global de Nivel y Puntaje")]
    public int nivelActual = 1;
    public int puntajeTotal = 0;
    public int misionesPrincipalesCompletadasEnNivel = 0;
    public int totalMisionesPrincipalesCompletadas = 0;

    [Header("2. Configuración de Puntos por Nivel (1 al 5)")]
    public List<ConfiguracionNivel> nivelesConfigurados = new List<ConfiguracionNivel>();

    // =========================================================================
    // VARIABLES DE PERSONAJES, BLOQUE DE DIALOGOS Y MUSICA
    // =========================================================================

    [Header("3. Catálogo de Personajes para Diálogos (Asigna aquí sus Iconos y Sonidos)")]
    public List<PerfilPersonajeDialogo> catalogoDePersonajes = new List<PerfilPersonajeDialogo>();

    [Header("4. Bloque Visual y Música de los Diálogos")]
    [Tooltip("¡Arrastra aquí el Sprite / Imagen de fondo para el BLOQUE (la caja) donde va el texto de los diálogos!")]
    public Sprite spriteBloqueDeDialogos;

    [Tooltip("Referencia al script SistemaDialogos. Si se deja vacío, el GameManager lo busca o lo crea automáticamente.")]
    public SistemaDialogos scriptDeDialogos;

    [Tooltip("Música general que sonará de fondo cuando se abra un diálogo (si la misión no tiene una música propia)")]
    public AudioClip musicaGeneralDeDialogos;

    [Tooltip("Indica si el juego está detenido actualmente mostrando un diálogo")]
    public bool juegoDetenidoPorDialogo = false;

    public UnityEvent alIniciarDialogoEvento;
    public UnityEvent alTerminarDialogoEvento;

    private bool tiempoEstabaPausadoAntesDelDialogo = false;

    // =========================================================================
    // APARTADO 5: MENU DE PAUSA CON TECLA ESCAPE
    // =========================================================================

    [Header("5. Menú de Pausa (Tecla Escape)")]
    [Tooltip("Si está activo, presionar la tecla Escape abre o cierra el menú de pausa deteniendo el juego")]
    public bool permitirPausaConEscape = true;

    [Tooltip("Indica si el juego está pausado actualmente con el menú de Escape")]
    public bool juegoPausadoPorEscape = false;

    [Tooltip("Panel de Canvas opcional para tu propio Menú de Pausa. Si lo dejas vacío, el GameManager dibuja el menú de pausa automáticamente en pantalla.")]
    public GameObject panelMenuPausaCanvas;

    private bool tiempoRelojEstabaPausadoAntesDeEscape = false;

    // =========================================================================
    // CICLO LUNAR
    // =========================================================================

    [Header("6. Ciclo Lunar")]
    public MoonPhase currentMoonPhase = MoonPhase.New;
    public int nightsInCurrentPhase = 0;

    // =========================================================================
    // INICIALIZACION Y UPDATE (DETECCION DE ESCAPE)
    // =========================================================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InicializarNivelesPorDefecto();
            InicializarCatalogoPersonajesPorDefecto();
            AsegurarReferenciaScriptDialogos();

            if (panelMenuPausaCanvas != null)
            {
                panelMenuPausaCanvas.SetActive(false);
            }
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (permitirPausaConEscape && SePresionoTeclaEscape())
        {
            AlternarMenuDePausa();
        }
    }

    public void InicializarNivelesPorDefecto()
    {
        if (nivelesConfigurados != null && nivelesConfigurados.Count > 0) return;

        nivelesConfigurados = new List<ConfiguracionNivel>
        {
            new ConfiguracionNivel
            {
                nombreDelNivel = "Nivel 1 - Inicio en Zuber",
                numeroNivel = 1,
                puntosBasePorMision = 10,
                puntosRadioPerfecto = 5,
                sumarBonoPerfectoAlBase = true,
                penalizacionFueraDeRadio = 0,
                cantidadMisionesPrincipalesDelNivel = 2
            },
            new ConfiguracionNivel
            {
                nombreDelNivel = "Nivel 2 - Calles de Talca",
                numeroNivel = 2,
                puntosBasePorMision = 15,
                puntosRadioPerfecto = 10,
                sumarBonoPerfectoAlBase = true,
                penalizacionFueraDeRadio = 5,
                cantidadMisionesPrincipalesDelNivel = 2
            },
            new ConfiguracionNivel
            {
                nombreDelNivel = "Nivel 3 - Cochero Confiable",
                numeroNivel = 3,
                puntosBasePorMision = 20,
                puntosRadioPerfecto = 10,
                sumarBonoPerfectoAlBase = true,
                penalizacionFueraDeRadio = 5,
                cantidadMisionesPrincipalesDelNivel = 3
            },
            new ConfiguracionNivel
            {
                nombreDelNivel = "Nivel 4 - Espectro Nocturno",
                numeroNivel = 4,
                puntosBasePorMision = 25,
                puntosRadioPerfecto = 15,
                sumarBonoPerfectoAlBase = true,
                penalizacionFueraDeRadio = 10,
                cantidadMisionesPrincipalesDelNivel = 3
            },
            new ConfiguracionNivel
            {
                nombreDelNivel = "Nivel 5 - Leyenda de Huilquilemu",
                numeroNivel = 5,
                puntosBasePorMision = 30,
                puntosRadioPerfecto = 20,
                sumarBonoPerfectoAlBase = true,
                penalizacionFueraDeRadio = 10,
                cantidadMisionesPrincipalesDelNivel = 3
            }
        };
    }

    public void InicializarCatalogoPersonajesPorDefecto()
    {
        if (catalogoDePersonajes != null && catalogoDePersonajes.Count > 0) return;

        catalogoDePersonajes = new List<PerfilPersonajeDialogo>
        {
            new PerfilPersonajeDialogo
            {
                nombrePersonaje = "Patron",
                colorNombre = new Color(1f, 0.8f, 0.25f, 1f)
            },
            new PerfilPersonajeDialogo
            {
                nombrePersonaje = "Chofer",
                colorNombre = new Color(0.3f, 0.95f, 0.45f, 1f)
            },
            new PerfilPersonajeDialogo
            {
                nombrePersonaje = "Perro",
                colorNombre = new Color(0.35f, 0.8f, 1f, 1f)
            },
            new PerfilPersonajeDialogo
            {
                nombrePersonaje = "Pasajero",
                colorNombre = new Color(0.85f, 0.6f, 1f, 1f)
            }
        };
    }

    public PerfilPersonajeDialogo ObtenerPerfilDePersonaje(string nombreBuscado)
    {
        if (string.IsNullOrEmpty(nombreBuscado) || catalogoDePersonajes == null) return null;

        string limpio = nombreBuscado.Trim().ToLowerInvariant();

        foreach (var perfil in catalogoDePersonajes)
        {
            if (perfil == null || string.IsNullOrEmpty(perfil.nombrePersonaje)) continue;

            string nombrePerfil = perfil.nombrePersonaje.Trim().ToLowerInvariant();
            if (nombrePerfil == limpio) return perfil;

            if ((limpio == "cholito" || limpio == "perro") && (nombrePerfil == "cholito" || nombrePerfil == "perro"))
                return perfil;
            if ((limpio == "cochero" || limpio == "chofer") && (nombrePerfil == "cochero" || nombrePerfil == "chofer"))
                return perfil;
            if ((limpio == "patrón" || limpio == "patron") && (nombrePerfil == "patrón" || nombrePerfil == "patron"))
                return perfil;
        }

        return null;
    }

    private void AsegurarReferenciaScriptDialogos()
    {
        if (scriptDeDialogos != null) return;

        scriptDeDialogos = FindAnyObjectByType<SistemaDialogos>();
        if (scriptDeDialogos == null)
        {
            scriptDeDialogos = gameObject.AddComponent<SistemaDialogos>();
        }
    }

    // =========================================================================
    // FUNCIONES DE PUNTUACION SEGUN EL RADIO DE LLEGADA
    // =========================================================================

    public ConfiguracionNivel ObtenerDatosDelNivel(int numeroDeNivel)
    {
        InicializarNivelesPorDefecto();

        ConfiguracionNivel encontrado = nivelesConfigurados.Find(n => n.numeroNivel == numeroDeNivel);
        if (encontrado != null) return encontrado;

        return nivelesConfigurados[0];
    }

    public int CalcularYOtorgarPuntosPorMision(int nivelDeMision, bool enRadioPerfecto, bool dentroDelRadioMaximo)
    {
        ConfiguracionNivel configNivel = ObtenerDatosDelNivel(nivelDeMision);
        int puntosObtenidos = 0;

        if (enRadioPerfecto)
        {
            puntosObtenidos = configNivel.sumarBonoPerfectoAlBase
                ? (configNivel.puntosBasePorMision + configNivel.puntosRadioPerfecto)
                : configNivel.puntosRadioPerfecto;

            puntajeTotal += puntosObtenidos;

            Debug.Log($"[GameManager] ¡ENTREGA EN RADIO PERFECTO! (Nivel {nivelDeMision}): " +
                      $"+{configNivel.puntosBasePorMision} pts misión +{configNivel.puntosRadioPerfecto} pts radio perfecto = +{puntosObtenidos} pts. " +
                      $"Puntaje Total: {puntajeTotal}");
        }
        else if (dentroDelRadioMaximo)
        {
            puntosObtenidos = configNivel.puntosBasePorMision;
            puntajeTotal += puntosObtenidos;

            Debug.Log($"[GameManager] Entrega en radio normal (Nivel {nivelDeMision}): " +
                      $"+{puntosObtenidos} pts. Puntaje Total: {puntajeTotal}");
        }
        else
        {
            puntosObtenidos = -Mathf.Abs(configNivel.penalizacionFueraDeRadio);
            puntajeTotal = Mathf.Max(0, puntajeTotal + puntosObtenidos);

            Debug.LogWarning($"[GameManager] Entrega fuera del radio de llegada (Nivel {nivelDeMision}). " +
                             $"Puntos aplicados: {puntosObtenidos}. Puntaje Total: {puntajeTotal}");
        }

        return puntosObtenidos;
    }

    // =========================================================================
    // FUNCIONES DE PROGRESION DE MISIONES PRINCIPALES Y NIVELES
    // =========================================================================

    public void RegistrarMisionPrincipalCompletada(int nivelDeLaMision)
    {
        totalMisionesPrincipalesCompletadas++;
        misionesPrincipalesCompletadasEnNivel++;

        ConfiguracionNivel configActual = ObtenerDatosDelNivel(nivelActual);

        Debug.Log($"[GameManager] Misión Principal completada ({misionesPrincipalesCompletadasEnNivel}/{configActual.cantidadMisionesPrincipalesDelNivel} del Nivel {nivelActual}). " +
                  $"Total completadas: {totalMisionesPrincipalesCompletadas}.");

        if (configActual.cantidadMisionesPrincipalesDelNivel > 0 &&
            misionesPrincipalesCompletadasEnNivel >= configActual.cantidadMisionesPrincipalesDelNivel)
        {
            CambiarDeNivel(nivelActual + 1);
        }
    }

    public void CambiarDeNivel(int nuevoNivel)
    {
        int maxNivel = (nivelesConfigurados != null && nivelesConfigurados.Count > 0)
            ? nivelesConfigurados.Count
            : 5;

        nivelActual = Mathf.Clamp(nuevoNivel, 1, maxNivel);
        misionesPrincipalesCompletadasEnNivel = 0;

        MissionManager gestorMisiones = FindAnyObjectByType<MissionManager>();
        if (gestorMisiones != null)
        {
            gestorMisiones.CambiarNivelActual(nivelActual);
        }

        Debug.Log($"[GameManager] ¡Nivel actualizado a Nivel {nivelActual}!");
    }

    // =========================================================================
    // FUNCIONES PARA DETENER EL JUEGO Y EJECUTAR CONVERSACIONES POR TURNOS
    // =========================================================================

    public void DetenerJuegoYMostrarConversacion(
        List<SistemaDialogos.LineaDeDialogo> conversacion,
        AudioClip musicaEspecifica = null,
        Action accionAlFinalizarDialogo = null)
    {
        if (conversacion == null || conversacion.Count == 0)
        {
            accionAlFinalizarDialogo?.Invoke();
            return;
        }

        AsegurarReferenciaScriptDialogos();
        PausarJuegoPorDialogo();

        alIniciarDialogoEvento?.Invoke();

        AudioClip musicaAUsar = (musicaEspecifica != null) ? musicaEspecifica : musicaGeneralDeDialogos;

        if (scriptDeDialogos != null)
        {
            scriptDeDialogos.IniciarConversacion(conversacion, musicaAUsar, () =>
            {
                ReanudarJuegoDespuesDeDialogo();
                alTerminarDialogoEvento?.Invoke();
                accionAlFinalizarDialogo?.Invoke();
            });
        }
        else
        {
            ReanudarJuegoDespuesDeDialogo();
            accionAlFinalizarDialogo?.Invoke();
        }
    }

    public void PausarJuegoPorDialogo()
    {
        juegoDetenidoPorDialogo = true;
        Time.timeScale = 0f;

        if (GlobalTimeManager.Instance != null)
        {
            tiempoEstabaPausadoAntesDelDialogo = GlobalTimeManager.Instance.isTimePaused;
            GlobalTimeManager.Instance.isTimePaused = true;
        }

        Debug.Log("[GameManager] Juego DETENIDO para mostrar conversación.");
    }

    public void ReanudarJuegoDespuesDeDialogo()
    {
        juegoDetenidoPorDialogo = false;

        // Solo restauramos Time.timeScale a 1 si el menú de Escape no está abierto
        if (!juegoPausadoPorEscape)
        {
            Time.timeScale = 1f;
        }

        if (GlobalTimeManager.Instance != null && !juegoPausadoPorEscape)
        {
            GlobalTimeManager.Instance.isTimePaused = tiempoEstabaPausadoAntesDelDialogo;
        }

        Debug.Log("[GameManager] Conversación terminada. Juego REANUDADO.");
    }

    // =========================================================================
    // FUNCIONES DEL MENU DE PAUSA (TECLA ESCAPE)
    // =========================================================================

    /// <summary>
    /// Alterna entre abrir y cerrar el menú de pausa cuando el jugador presiona Escape.
    /// </summary>
    public void AlternarMenuDePausa()
    {
        if (juegoPausadoPorEscape)
        {
            ReanudarJuegoDeEscape();
        }
        else
        {
            PausarJuegoConEscape();
        }
    }

    /// <summary>
    /// Detiene el juego por completo al presionar Escape y muestra el menú de pausa.
    /// </summary>
    public void PausarJuegoConEscape()
    {
        juegoPausadoPorEscape = true;
        Time.timeScale = 0f;

        if (GlobalTimeManager.Instance != null)
        {
            tiempoRelojEstabaPausadoAntesDeEscape = GlobalTimeManager.Instance.isTimePaused;
            GlobalTimeManager.Instance.isTimePaused = true;
        }

        if (panelMenuPausaCanvas != null)
        {
            panelMenuPausaCanvas.SetActive(true);
        }

        Debug.Log("[GameManager] PAUSA ACTIVADA (Tecla Escape).");
    }

    /// <summary>
    /// Cierra el menú de pausa y devuelve el juego a su velocidad normal (respetando si había un diálogo abierto).
    /// </summary>
    public void ReanudarJuegoDeEscape()
    {
        juegoPausadoPorEscape = false;

        if (panelMenuPausaCanvas != null)
        {
            panelMenuPausaCanvas.SetActive(false);
        }

        if (!juegoDetenidoPorDialogo)
        {
            Time.timeScale = 1f;

            if (GlobalTimeManager.Instance != null)
            {
                GlobalTimeManager.Instance.isTimePaused = tiempoRelojEstabaPausadoAntesDeEscape;
            }
        }

        Debug.Log("[GameManager] PAUSA DESACTIVADA. Juego reanudado.");
    }

    /// <summary>
    /// Reinicia la escena actual desde el menú de pausa.
    /// </summary>
    public void ReiniciarEscenaActual()
    {
        juegoPausadoPorEscape = false;
        juegoDetenidoPorDialogo = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Cierra la aplicación (o detiene el modo Play si estás en el Editor de Unity).
    /// </summary>
    public void SalirDelJuego()
    {
        Debug.Log("[GameManager] Saliendo del juego...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Dibuja automáticamente el Menú de Pausa en pantalla si no asignaste un Panel propio en 'panelMenuPausaCanvas'.
    /// </summary>
    private void OnGUI()
    {
        if (!juegoPausadoPorEscape || panelMenuPausaCanvas != null) return;

        Color colorOriginal = GUI.color;

        // Fondo negro medio transparente cubriendo toda la pantalla
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        float anchoMenu = 340f;
        float altoMenu = 260f;
        float xMenu = (Screen.width - anchoMenu) * 0.5f;
        float yMenu = (Screen.height - altoMenu) * 0.5f;

        // Caja central del menú de pausa
        GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        GUI.DrawTexture(new Rect(xMenu, yMenu, anchoMenu, altoMenu), Texture2D.whiteTexture);

        // Borde superior verde/dorado
        GUI.color = new Color(0.2f, 0.88f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(xMenu, yMenu, anchoMenu, 4f), Texture2D.whiteTexture);

        GUI.color = colorOriginal;

        GUIStyle estiloTitulo = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        estiloTitulo.normal.textColor = Color.white;

        GUI.Label(new Rect(xMenu, yMenu + 20f, anchoMenu, 36f), "JUEGO EN PAUSA", estiloTitulo);

        GUIStyle estiloBoton = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };

        float anchoBoton = 240f;
        float altoBoton = 42f;
        float xBoton = xMenu + (anchoMenu - anchoBoton) * 0.5f;

        if (GUI.Button(new Rect(xBoton, yMenu + 80f, anchoBoton, altoBoton), "Reanudar (Esc)", estiloBoton))
        {
            ReanudarJuegoDeEscape();
        }

        if (GUI.Button(new Rect(xBoton, yMenu + 134f, anchoBoton, altoBoton), "Reiniciar Nivel", estiloBoton))
        {
            ReiniciarEscenaActual();
        }

        if (GUI.Button(new Rect(xBoton, yMenu + 188f, anchoBoton, altoBoton), "Salir del Juego", estiloBoton))
        {
            SalirDelJuego();
        }
    }

    private bool SePresionoTeclaEscape()
    {
        bool escape = false;

        try
        {
            if (Input.GetKeyDown(KeyCode.Escape)) escape = true;
        }
        catch { }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escape = true;
        }
#endif

        return escape;
    }

    // =========================================================================
    // FUNCIONES DE CICLO LUNAR
    // =========================================================================

    public void AdvanceMoonPhase()
    {
        nightsInCurrentPhase++;

        int requiredNights = 1;
        if (currentMoonPhase == MoonPhase.Waxing || currentMoonPhase == MoonPhase.Third)
        {
            requiredNights = 3;
        }

        if (nightsInCurrentPhase >= requiredNights)
        {
            switch (currentMoonPhase)
            {
                case MoonPhase.New: currentMoonPhase = MoonPhase.Waxing; break;
                case MoonPhase.Waxing: currentMoonPhase = MoonPhase.Full; break;
                case MoonPhase.Full: currentMoonPhase = MoonPhase.Third; break;
                case MoonPhase.Third: currentMoonPhase = MoonPhase.New; break;
            }

            nightsInCurrentPhase = 0;
            Debug.Log("[GameManager] El ciclo avanzó a la Fase Lunar: " + currentMoonPhase);
        }
    }
}
