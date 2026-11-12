using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum MoonPhase
{
    New,
    Waxing,
    Full,
    Third
}

/// <summary>
/// Gestor principal y global del juego (GameManager).
/// Controla los niveles de reputación (1 al 5 según el GDD), el sistema de puntaje según el radio de llegada,
/// la pausa del juego para mostrar diálogos entre misiones principales y el ciclo lunar.
/// </summary>
public class GameManager : MonoBehaviour
{
    // Instancia estática (Singleton) para acceder desde cualquier script usando GameManager.Instance
    public static GameManager Instance { get; private set; }

    // =========================================================================
    // CLASE DE CONFIGURACION GLOBAL DE NIVELES Y PUNTOS
    // =========================================================================

    /// <summary>
    /// Clase configurable en el Inspector que define cuántos puntos da cada misión según el nivel
    /// y cuántas misiones principales pertenecen a ese nivel.
    /// </summary>
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
    // VARIABLES GLOBALES DE NIVELES Y PUNTAJE
    // =========================================================================

    [Header("Estado Global de Nivel y Puntaje")]
    [Tooltip("Nivel actual del jugador (1 al 5 según el sistema de reputación del GDD)")]
    public int nivelActual = 1;

    [Tooltip("Puntaje total acumulado por el jugador realizando misiones")]
    public int puntajeTotal = 0;

    [Tooltip("Cantidad de misiones principales completadas en el nivel actual")]
    public int misionesPrincipalesCompletadasEnNivel = 0;

    [Tooltip("Cantidad total de misiones principales completadas en toda la partida")]
    public int totalMisionesPrincipalesCompletadas = 0;

    [Header("Configuración de Puntos por Nivel (1 al 5)")]
    [Tooltip("Lista de niveles con sus respectivos puntajes base y bonos por radio perfecto")]
    public List<ConfiguracionNivel> nivelesConfigurados = new List<ConfiguracionNivel>();

    // =========================================================================
    // SISTEMA DE PAUSA Y CONEXION CON SCRIPT DE DIALOGOS
    // =========================================================================

    [Header("Control de Diálogos entre Misiones Principales")]
    [Tooltip("Referencia al script de diálogos. Si se deja vacío, el GameManager lo busca o lo crea automáticamente.")]
    public SistemaDialogos scriptDeDialogos;

    [Tooltip("Indica si el juego está detenido actualmente mostrando un diálogo")]
    public bool juegoDetenidoPorDialogo = false;

    [Tooltip("Evento opcional por si deseas conectar un script de diálogo externo desde el Inspector al iniciar un diálogo")]
    public UnityEvent alIniciarDialogoEvento;

    [Tooltip("Evento opcional que se dispara cuando termina un diálogo y el juego se reanuda")]
    public UnityEvent alTerminarDialogoEvento;

    private bool tiempoEstabaPausadoAntesDelDialogo = false;

    // =========================================================================
    // CICLO LUNAR (MANTENIDO PARA COMPATIBILIDAD CON LEVELMANAGER Y GLOBALTIMEMANAGER)
    // =========================================================================

    [Header("Ciclo Lunar")]
    public MoonPhase currentMoonPhase = MoonPhase.New;
    public int nightsInCurrentPhase = 0;

    // =========================================================================
    // INICIALIZACION
    // =========================================================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InicializarNivelesPorDefecto();
            AsegurarReferenciaScriptDialogos();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Crea automáticamente la configuración de los 5 niveles del juego si la lista está vacía en el Inspector.
    /// Configura el Nivel 1 con 10 puntos por misión y 5 puntos por el radio perfecto.
    /// </summary>
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

    /// <summary>
    /// Busca el script SistemaDialogos en la escena o lo agrega automáticamente a este objeto si no existe.
    /// </summary>
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

    /// <summary>
    /// Obtiene la configuración de puntos correspondiente al número de nivel indicado.
    /// Si el nivel no existe en la lista, devuelve la configuración del Nivel 1 por seguridad.
    /// </summary>
    public ConfiguracionNivel ObtenerDatosDelNivel(int numeroDeNivel)
    {
        InicializarNivelesPorDefecto();

        ConfiguracionNivel encontrado = nivelesConfigurados.Find(n => n.numeroNivel == numeroDeNivel);
        if (encontrado != null) return encontrado;

        return nivelesConfigurados[0];
    }

    /// <summary>
    /// Calcula y otorga los puntos al jugador en base al nivel de la misión y al radio de llegada donde dejó al pasajero.
    /// En Nivel 1: otorga 10 puntos por misión y 5 puntos por dejar al pasajero en el radio perfecto.
    /// </summary>
    /// <param name="nivelDeMision">Nivel al que pertenece la misión completada.</param>
    /// <param name="enRadioPerfecto">True si el jugador dejó al pasajero dentro del radio óptimo central.</param>
    /// <param name="dentroDelRadioMaximo">True si el jugador dejó al pasajero dentro del área amarilla general.</param>
    /// <returns>Cantidad de puntos ganados (o perdidos) en esta entrega.</returns>
    public int CalcularYOtorgarPuntosPorMision(int nivelDeMision, bool enRadioPerfecto, bool dentroDelRadioMaximo)
    {
        ConfiguracionNivel configNivel = ObtenerDatosDelNivel(nivelDeMision);
        int puntosObtenidos = 0;

        // 1. Dejó al pasajero en el Radio Perfecto (Centro)
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
        // 2. Dejó al pasajero dentro del Radio Normal de Llegada (lejos del centro pero válido)
        else if (dentroDelRadioMaximo)
        {
            puntosObtenidos = configNivel.puntosBasePorMision;
            puntajeTotal += puntosObtenidos;

            Debug.Log($"[GameManager] Entrega en radio normal (Nivel {nivelDeMision}): " +
                      $"+{puntosObtenidos} pts. Puntaje Total: {puntajeTotal}");
        }
        // 3. Dejó al pasajero fuera del área de llegada (Misión Fallida)
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

    /// <summary>
    /// Registra que una misión principal fue completada exitosamente y verifica si corresponde subir de nivel.
    /// </summary>
    public void RegistrarMisionPrincipalCompletada(int nivelDeLaMision)
    {
        totalMisionesPrincipalesCompletadas++;
        misionesPrincipalesCompletadasEnNivel++;

        ConfiguracionNivel configActual = ObtenerDatosDelNivel(nivelActual);

        Debug.Log($"[GameManager] Misión Principal completada ({misionesPrincipalesCompletadasEnNivel}/{configActual.cantidadMisionesPrincipalesDelNivel} del Nivel {nivelActual}). " +
                  $"Total completadas: {totalMisionesPrincipalesCompletadas}.");

        // Si ya completó todas las misiones principales requeridas en este nivel, avanza al siguiente nivel
        if (configActual.cantidadMisionesPrincipalesDelNivel > 0 &&
            misionesPrincipalesCompletadasEnNivel >= configActual.cantidadMisionesPrincipalesDelNivel)
        {
            CambiarDeNivel(nivelActual + 1);
        }
    }

    /// <summary>
    /// Cambia el nivel global actual y sincroniza el nivel con el MissionManager si está en la escena.
    /// </summary>
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
    // FUNCIONES PARA DETENER EL JUEGO Y EJECUTAR DIALOGOS ENTRE MISIONES
    // =========================================================================

    /// <summary>
    /// Detiene el juego por completo (pausa físicas y tiempo), muestra el diálogo llamando al script de diálogos,
    /// y reanuda automáticamente la partida cuando el jugador termina de leer todas las líneas.
    /// </summary>
    /// <param name="nombrePersonaje">Nombre de quien habla en el diálogo (ej. "Cholito").</param>
    /// <param name="lineasDeDialogo">Lista de textos que aparecerán en el diálogo.</param>
    /// <param name="accionAlFinalizarDialogo">Acción opcional que se ejecutará al cerrarse el diálogo (ej. pasar a la siguiente misión).</param>
    public void DetenerJuegoYMostrarDialogo(string nombrePersonaje, List<string> lineasDeDialogo, Action accionAlFinalizarDialogo = null)
    {
        // Si no hay líneas configuradas, continuamos de inmediato sin detener el juego
        if (lineasDeDialogo == null || lineasDeDialogo.Count == 0)
        {
            accionAlFinalizarDialogo?.Invoke();
            return;
        }

        AsegurarReferenciaScriptDialogos();
        PausarJuegoPorDialogo();

        alIniciarDialogoEvento?.Invoke();

        if (scriptDeDialogos != null)
        {
            scriptDeDialogos.IniciarDialogo(nombrePersonaje, lineasDeDialogo, () =>
            {
                ReanudarJuegoDespuesDeDialogo();
                alTerminarDialogoEvento?.Invoke();
                accionAlFinalizarDialogo?.Invoke();
            });
        }
        else
        {
            // Respaldo de seguridad si por alguna razón se destruyó el componente de diálogos
            ReanudarJuegoDespuesDeDialogo();
            accionAlFinalizarDialogo?.Invoke();
        }
    }

    /// <summary>
    /// Congela el tiempo del juego (Time.timeScale = 0) y pausa el reloj nocturno mientras el diálogo está activo.
    /// </summary>
    public void PausarJuegoPorDialogo()
    {
        juegoDetenidoPorDialogo = true;
        Time.timeScale = 0f;

        if (GlobalTimeManager.Instance != null)
        {
            tiempoEstabaPausadoAntesDelDialogo = GlobalTimeManager.Instance.isTimePaused;
            GlobalTimeManager.Instance.isTimePaused = true;
        }

        Debug.Log("[GameManager] Juego DETENIDO para mostrar secuencia de diálogo.");
    }

    /// <summary>
    /// Restaura la velocidad normal del juego (Time.timeScale = 1) y el reloj nocturno al terminar el diálogo.
    /// </summary>
    public void ReanudarJuegoDespuesDeDialogo()
    {
        juegoDetenidoPorDialogo = false;
        Time.timeScale = 1f;

        if (GlobalTimeManager.Instance != null)
        {
            GlobalTimeManager.Instance.isTimePaused = tiempoEstabaPausadoAntesDelDialogo;
        }

        Debug.Log("[GameManager] Diálogo terminado. Juego REANUDADO.");
    }

    // =========================================================================
    // FUNCIONES DE CICLO LUNAR
    // =========================================================================

    /// <summary>
    /// Avanza la fase lunar al terminar una noche (llamado desde GlobalTimeManager).
    /// </summary>
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
