using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Gestor de Misiones (Principales en orden cronológico 1..N y Secundarias estilo Uber).
/// Controla los círculos de aceptación (Verde para Principales, Azul para Secundarias),
/// la verificación de la tecla E con su indicador visual [E] en un cuadrado blanco,
/// la evaluación de puntos con el GameManager y las conversaciones turno por turno entre misiones.
/// </summary>
public class MissionManager : MonoBehaviour
{
    public enum MissionState
    {
        EsperandoNuevaOrden,
        OfertasDisponiblesEnMapa,
        MisionEnCursoHaciaDestino
    }

    public enum MissionCategory
    {
        EncargoUberAleatorio,
        MisionPrincipalHistoria
    }

    // =========================================================================
    // CLASES DE DATOS CONFIGURABLES EN EL INSPECTOR
    // =========================================================================

    [System.Serializable]
    public class MissionOrder
    {
        [Tooltip("Nombre referencial en el Inspector (ej. Misión 1 - Llevar pasajero a la Plaza)")]
        public string nombreReferencia = "Nueva Mision";

        [Tooltip("Nivel al que pertenece esta misión (ej. 1, 2, 3, 4, 5)")]
        public int nivelDeMision = 1;

        [Tooltip("Número cronológico de la misión (1, 2, 3... N). Determina el orden sucesivo obligatorio.")]
        public int numeroDeMision = 1;

        [Header("Pasajero de esta Misión (Opcional)")]
        [Tooltip("Si lo dejas vacío, usará el Prefab de Pasajero por Defecto del MissionManager")]
        public GameObject prefabPasajeroEspecifico;

        [Header("Puntos Vacíos del Mapa (Transforms)")]
        [Tooltip("GameObject vacío donde aparece el círculo de aceptación y el pasajero esperando")]
        public Transform puntoDeInicio;

        [Tooltip("GameObject vacío que actúa como Punto de Llegada / Triunfo (Gran círculo amarillo)")]
        public Transform puntoDeLlegada;

        [Header("Radios del Punto de Inicio y Triunfo")]
        [Tooltip("Radio del círculo donde el jugador debe entrar y presionar E para aceptar la misión")]
        public float radioDeAceptacion = 3.5f;

        [Tooltip("Radio CENTRAL PERFECTO (Dejar al pasajero aquí otorga los +5 puntos extra en Nivel 1)")]
        public float radioOptimoCentro = 2.5f;

        [Tooltip("Radio EXTERIOR TOTAL del área amarilla (Dejar al pasajero aquí otorga los 10 puntos base en Nivel 1)")]
        public float radioMaximoLlegada = 7.0f;

        [Header("1. Conversación al ACEPTAR la Misión (Orden de hablantes turno por turno)")]
        [Tooltip("Si está activo, al presionar E en el círculo verde el juego se detiene y muestra esta conversación turno por turno")]
        public bool mostrarDialogoAlAceptar = false;

        [Tooltip("Música opcional que sonará mientras hablan al aceptar esta misión")]
        public AudioClip musicaDialogoAlAceptar;

        [Tooltip("Agrega con '+' cada turno de conversación (ej. Turno 1: Patron -> Turno 2: Chofer -> Turno 3: Perro -> Turno 4: Chofer)")]
        public List<SistemaDialogos.LineaDeDialogo> conversacionAlAceptar = new List<SistemaDialogos.LineaDeDialogo>();

        [Header("2. Conversación al COMPLETAR la Misión (Entre esta misión y la siguiente)")]
        [Tooltip("Si está activo, al dejar al pasajero en el destino el juego se detiene y muestra esta conversación antes de pasar a la siguiente misión")]
        public bool mostrarDialogoAlCompletar = false;

        [Tooltip("Música opcional que sonará mientras hablan al completar esta misión")]
        public AudioClip musicaDialogoAlCompletar;

        [Tooltip("Agrega con '+' cada turno de conversación en el orden exacto en que hablan los personajes")]
        public List<SistemaDialogos.LineaDeDialogo> conversacionAlCompletar = new List<SistemaDialogos.LineaDeDialogo>();

        [Header("Estado Cronológico (Solo Lectura)")]
        [Tooltip("Se marca automáticamente cuando el jugador completa esta misión en su totalidad")]
        public bool misionCompletada = false;
    }

    [System.Serializable]
    public class LevelMissionPool
    {
        [Tooltip("Etiqueta del nivel en el Inspector (ej. Nivel 1)")]
        public string nombreDelNivel = "Nivel 1";
        [Tooltip("Número del nivel (1, 2, 3...)")]
        public int numeroNivel = 1;
        [Tooltip("Lista de misiones secundarias aleatorias pertenecientes a este nivel")]
        public List<MissionOrder> misionesDelNivel = new List<MissionOrder>();
    }

    // =========================================================================
    // VARIABLES DEL INSPECTOR
    // =========================================================================

    [Header("Referencias Principales")]
    [Tooltip("Objeto Caballos (si se deja vacío, busca CrazyCarriageController automáticamente)")]
    public Transform jugadorTransform;
    private CrazyCarriageController carriageController;

    [Header("Configuración de Pasajeros (Prefabs)")]
    [Tooltip("Arrastra aquí el Prefab de tu pasajero que aparecerá dentro del círculo de inicio")]
    public GameObject prefabPasajeroPorDefecto;
    [Tooltip("Ajuste de altura por si el pivote de tu Prefab queda enterrado en el suelo")]
    public Vector3 offsetPosicionPasajero = new Vector3(0f, 0f, 0f);
    [Tooltip("Si está activo, el pasajero mira hacia el jugador mientras espera ser recogido")]
    public bool pasajeroMiraAlJugador = true;
    [Tooltip("Si está activo, cuando completas la misión el pasajero se baja en el destino y desaparece a los pocos segundos")]
    public bool instanciarPasajeroAlBajarse = true;

    [Header("Configuración de Nivel y Tiempos (Estilo Uber)")]
    [Tooltip("Nivel en el que inicia el juego (por defecto Nivel 1)")]
    public int nivelActual = 1;
    [Tooltip("Tiempo de espera antes de lanzar la siguiente misión en el mapa")]
    public float tiempoEsperaParaRandom = 3.0f;
    [Tooltip("Cantidad de trabajos secundarios estilo Uber que aparecen simultáneamente (1 o 2)")]
    [Range(1, 2)] public int cantidadTrabajosSimultaneos = 2;

    [Header("1. Listas de Misiones Secundarias por Niveles (Círculo Azul)")]
    public List<LevelMissionPool> nivelesDeMisiones = new List<LevelMissionPool>();

    [Header("2. Apartado Exclusivo: Misiones Principales (Orden Cronológico 1 al N - Círculo Verde)")]
    [Tooltip("Lista secuencial de Misiones Principales. Se ordenan y ejecutan estrictamente del 1 al N.")]
    public List<MissionOrder> misionesPrincipalesSecuencia = new List<MissionOrder>();

    [Tooltip("Índice actual dentro de la secuencia de misiones principales (0 = Misión Principal 1)")]
    public int indiceMisionPrincipalActual = 0;

    [Tooltip("Si está activo, al dar Play inicia desde la Misión Principal #1 (índice 0) en orden cronológico")]
    public bool reiniciarSecuenciaPrincipalAlIniciar = true;

    [Tooltip("Si está activo, prioriza lanzar las Misiones Principales en orden 1..N automáticamente hasta completarlas")]
    public bool priorizarMisionesPrincipalesEnOrden = true;

    [Header("Condiciones de Aceptación y Entrega (Tecla E)")]
    [Tooltip("Si está activo, las misiones secundarias (azules) también pedirán presionar E para aceptarse (las principales SIEMPRE piden E)")]
    public bool pedirTeclaETambienEnSecundarias = false;

    [Tooltip("Si está activo, el jugador puede frenar dentro del área amarilla o presionar E para entregar al pasajero")]
    public bool requiereFrenarEnDestino = true;

    [Tooltip("Velocidad máxima considerada como detenido para confirmar la entrega")]
    public float velocidadMaximaParaEntregar = 1.0f;

    [Tooltip("Permite confirmar la entrega del pasajero en el punto amarillo presionando la tecla E")]
    public bool permitirEvaluarConTeclaE = true;

    [Tooltip("Objeto UI opcional para el indicador de la tecla E. Si se deja vacío, se dibuja automáticamente un cuadrado blanco con la E abajo en pantalla.")]
    public GameObject indicadorTeclaEPersonalizadoUI;

    [Header("Colores de los Indicadores en el Mapa")]
    [Tooltip("Color VERDE para el radio de aceptación de las Misiones Principales")]
    public Color colorIndicadorMisionPrincipal = new Color(0.1f, 0.88f, 0.22f, 0.78f); // Verde para Misión Principal
    [Tooltip("Color AZUL para el radio de aceptación de las Misiones Secundarias")]
    public Color colorIndicadorInicio = new Color(0.1f, 0.55f, 1f, 0.75f);            // Azul para Misión Secundaria
    [Tooltip("Color AMARILLO para el radio de llegada / entrega del pasajero")]
    public Color colorIndicadorLlegada = new Color(1f, 0.85f, 0.05f, 0.8f);            // Amarillo en punto de llegada

    [Header("Estado en Tiempo Real (Solo Lectura)")]
    public MissionState estadoActual = MissionState.EsperandoNuevaOrden;
    public MissionCategory categoriaActiva = MissionCategory.EncargoUberAleatorio;
    public List<MissionOrder> ofertasDisponiblesActuales = new List<MissionOrder>();
    public MissionOrder misionAceptadaActual = null;
    public bool jugadorEnAreaDeAceptacion = false;
    public bool jugadorEnAreaDeEntrega = false;

    private float temporizadorRandom = 0f;
    private List<GameObject> marcadoresVisualesOfertas = new List<GameObject>();
    private List<GameObject> pasajerosInstanciadosOfertas = new List<GameObject>();
    private GameObject marcadorVisualDestinoCentro;
    private GameObject marcadorVisualDestinoArea;
    private bool jugadorEstuvoDentroDelArea = false;

    // =========================================================================
    // INICIALIZACION
    // =========================================================================

    void Awake()
    {
        AsegurarExistenciaDeGameManager();
        AutoDetectarJugador();
        CrearMarcadoresVisualesAutomaticos();
    }

    void Start()
    {
        if (nivelActual < 1) nivelActual = 1;

        OrdenarMisionesPrincipalesCronologicamente();

        if (reiniciarSecuenciaPrincipalAlIniciar)
        {
            indiceMisionPrincipalActual = 0;
            foreach (var m in misionesPrincipalesSecuencia)
            {
                if (m != null) m.misionCompletada = false;
            }
        }

        if (priorizarMisionesPrincipalesEnOrden && HayMisionesPrincipalesPendientes())
        {
            LanzarSiguienteMisionPrincipal();
        }
        else
        {
            IniciarEsperaParaNuevaMision();
        }
    }

    private void AsegurarExistenciaDeGameManager()
    {
        if (GameManager.Instance != null) return;

        GameManager encontrado = FindAnyObjectByType<GameManager>();
        if (encontrado == null)
        {
            GameObject objGM = new GameObject("GameManager");
            objGM.AddComponent<GameManager>();
        }
    }

    private void AutoDetectarJugador()
    {
        if (jugadorTransform == null)
        {
            CrazyCarriageController encontrado = FindAnyObjectByType<CrazyCarriageController>();
            if (encontrado != null)
            {
                carriageController = encontrado;
                jugadorTransform = encontrado.transform;
            }
        }
        else
        {
            carriageController = jugadorTransform.GetComponent<CrazyCarriageController>();
        }
    }

    /// <summary>
    /// Ordena la lista de Misiones Principales estrictamente por nivel (1..5) y número de misión (1..N)
    /// para garantizar que siempre se ejecuten en orden cronológico sucesivo.
    /// </summary>
    public void OrdenarMisionesPrincipalesCronologicamente()
    {
        if (misionesPrincipalesSecuencia == null || misionesPrincipalesSecuencia.Count <= 1) return;

        misionesPrincipalesSecuencia.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int comparacionNivel = a.nivelDeMision.CompareTo(b.nivelDeMision);
            if (comparacionNivel != 0) return comparacionNivel;

            return a.numeroDeMision.CompareTo(b.numeroDeMision);
        });
    }

    // =========================================================================
    // BUCLE PRINCIPAL (UPDATE)
    // =========================================================================

    void Update()
    {
        if (jugadorTransform == null) return;

        // Si el GameManager detuvo el juego por una conversación o por el Menú de Pausa (Escape), no procesamos misiones
        if (GameManager.Instance != null && (GameManager.Instance.juegoDetenidoPorDialogo || GameManager.Instance.juegoPausadoPorEscape))
        {
            ActualizarIndicadorPersonalizadoE(false);
            return;
        }

        // Leemos la tecla E una sola vez por frame para diferenciar entre Aceptar Misión y Dejar Pasajero
        bool presionoEEnEsteFrame = SePresionoTeclaE();

        if (SePresionoEnter())
        {
            if (categoriaActiva == MissionCategory.MisionPrincipalHistoria &&
                (estadoActual == MissionState.OfertasDisponiblesEnMapa || estadoActual == MissionState.MisionEnCursoHaciaDestino))
            {
                Debug.Log("[MissionManager] Tienes una Misión Principal activa. Debes completarla en orden antes de cambiar de misión.");
            }
            else
            {
                Debug.Log("[MissionManager] ENTER presionado: Relanzando sorteo de misiones secundarias para el Nivel " + nivelActual);
                LanzarRandomMisionPorNivel(nivelActual);
                return;
            }
        }

        switch (estadoActual)
        {
            case MissionState.EsperandoNuevaOrden:
                jugadorEnAreaDeAceptacion = false;
                jugadorEnAreaDeEntrega = false;
                temporizadorRandom += Time.deltaTime;

                if (temporizadorRandom >= tiempoEsperaParaRandom)
                {
                    if (priorizarMisionesPrincipalesEnOrden && HayMisionesPrincipalesPendientes())
                    {
                        LanzarSiguienteMisionPrincipal();
                    }
                    else
                    {
                        LanzarRandomMisionPorNivel(nivelActual);
                    }
                }
                break;

            case MissionState.OfertasDisponiblesEnMapa:
                jugadorEnAreaDeEntrega = false;
                ActualizarMiradaDePasajeros();
                VerificarAceptacionDeMisionEnPuntoDeInicio(presionoEEnEsteFrame);
                break;

            case MissionState.MisionEnCursoHaciaDestino:
                jugadorEnAreaDeAceptacion = false;
                VerificarLlegadaEnPuntoDeTriunfo(presionoEEnEsteFrame);
                break;
        }

        ActualizarIndicadorPersonalizadoE(jugadorEnAreaDeAceptacion || jugadorEnAreaDeEntrega);
    }

    // =========================================================================
    // SISTEMA UBER: SORTEO RANDOM POR NIVEL E INSTANCIACION DE PASAJEROS
    // =========================================================================

    public void LanzarRandomMisionPorNivel(int nivel)
    {
        OcultarTodosLosMarcadores();
        ofertasDisponiblesActuales.Clear();
        misionAceptadaActual = null;
        jugadorEstuvoDentroDelArea = false;
        jugadorEnAreaDeAceptacion = false;
        jugadorEnAreaDeEntrega = false;

        LevelMissionPool poolDelNivel = nivelesDeMisiones.Find(n => n.numeroNivel == nivel);

        if (poolDelNivel == null || poolDelNivel.misionesDelNivel.Count == 0)
        {
            Debug.LogWarning($"[MissionManager] No existen misiones secundarias configuradas para el Nivel {nivel} en el Inspector.");
            estadoActual = MissionState.EsperandoNuevaOrden;
            temporizadorRandom = 0f;
            return;
        }

        List<MissionOrder> candidatas = new List<MissionOrder>(poolDelNivel.misionesDelNivel);
        int cantidadAElegir = Mathf.Min(cantidadTrabajosSimultaneos, candidatas.Count);

        for (int i = 0; i < cantidadAElegir; i++)
        {
            int indiceRandom = Random.Range(0, candidatas.Count);
            MissionOrder misionElegida = candidatas[indiceRandom];
            candidatas.RemoveAt(indiceRandom);

            if (misionElegida.puntoDeInicio == null || misionElegida.puntoDeLlegada == null)
            {
                Debug.LogWarning($"[MissionManager] La misión secundaria #{misionElegida.numeroDeMision} del Nivel {nivel} carece de Punto de Inicio o Punto de Llegada.");
                continue;
            }

            ofertasDisponiblesActuales.Add(misionElegida);

            ActivarMarcadorOferta(i, misionElegida.puntoDeInicio.position, misionElegida.radioDeAceptacion, colorIndicadorInicio);
            InstanciarPasajeroEnInicio(misionElegida);

            Debug.Log($"[MissionManager] Oferta secundaria generada en Nivel {misionElegida.nivelDeMision} | Misión #{misionElegida.numeroDeMision} ({misionElegida.nombreReferencia}). Pasajero esperando en punto AZUL.");
        }

        if (ofertasDisponiblesActuales.Count > 0)
        {
            categoriaActiva = MissionCategory.EncargoUberAleatorio;
            estadoActual = MissionState.OfertasDisponiblesEnMapa;
        }
    }

    // =========================================================================
    // VERIFICACION DE ACEPTACION CON TECLA "E" Y CUADRADO BLANCO
    // =========================================================================

    private void VerificarAceptacionDeMisionEnPuntoDeInicio(bool presionoTeclaE)
    {
        jugadorEnAreaDeAceptacion = false;

        for (int i = 0; i < ofertasDisponiblesActuales.Count; i++)
        {
            MissionOrder oferta = ofertasDisponiblesActuales[i];
            if (oferta == null || oferta.puntoDeInicio == null) continue;

            float distanciaAlInicio = DistanciaPlanaXZ(jugadorTransform.position, oferta.puntoDeInicio.position);

            if (distanciaAlInicio <= oferta.radioDeAceptacion)
            {
                bool requierePresionarE = (categoriaActiva == MissionCategory.MisionPrincipalHistoria) || pedirTeclaETambienEnSecundarias;

                if (requierePresionarE)
                {
                    jugadorEnAreaDeAceptacion = true;

                    if (presionoTeclaE)
                    {
                        ConfirmarYAceptarMision(oferta);
                        break;
                    }
                }
                else
                {
                    ConfirmarYAceptarMision(oferta);
                    break;
                }
            }
        }
    }

    private void ConfirmarYAceptarMision(MissionOrder misionConfirmada)
    {
        misionAceptadaActual = misionConfirmada;
        jugadorEstuvoDentroDelArea = false;
        jugadorEnAreaDeAceptacion = false;

        OcultarTodosLosMarcadores();

        if (misionAceptadaActual.puntoDeLlegada != null)
        {
            ActivarMarcadorDestinoAmarillo(
                misionAceptadaActual.puntoDeLlegada.position,
                misionAceptadaActual.radioOptimoCentro,
                misionAceptadaActual.radioMaximoLlegada
            );
        }

        estadoActual = MissionState.MisionEnCursoHaciaDestino;

        Debug.Log($"[MissionManager] Pasajero recogido con tecla E y Misión ACEPTADA ({categoriaActiva}): Nivel {misionAceptadaActual.nivelDeMision} - Misión #{misionAceptadaActual.numeroDeMision} ({misionAceptadaActual.nombreReferencia}). Dirigirse al punto AMARILLO.");

        // Si es una Misión Principal y tiene conversación al aceptar, el GameManager detiene el juego y muestra los turnos
        if (categoriaActiva == MissionCategory.MisionPrincipalHistoria &&
            misionAceptadaActual.mostrarDialogoAlAceptar &&
            misionAceptadaActual.conversacionAlAceptar != null &&
            misionAceptadaActual.conversacionAlAceptar.Count > 0 &&
            GameManager.Instance != null)
        {
            GameManager.Instance.DetenerJuegoYMostrarConversacion(
                misionAceptadaActual.conversacionAlAceptar,
                misionAceptadaActual.musicaDialogoAlAceptar
            );
        }
    }

    // =========================================================================
    // LOGICA DE INSTANCIACION Y LIMPIEZA DE PASAJEROS
    // =========================================================================

    private void InstanciarPasajeroEnInicio(MissionOrder mision)
    {
        GameObject prefabAUsar = (mision.prefabPasajeroEspecifico != null)
            ? mision.prefabPasajeroEspecifico
            : prefabPasajeroPorDefecto;

        if (prefabAUsar == null || mision.puntoDeInicio == null) return;

        Vector3 posSpawn = mision.puntoDeInicio.position + offsetPosicionPasajero;
        Quaternion rotSpawn = mision.puntoDeInicio.rotation;

        GameObject nuevoPasajero = Instantiate(prefabAUsar, posSpawn, rotSpawn);
        nuevoPasajero.name = $"Pasajero_N{mision.nivelDeMision}_M{mision.numeroDeMision}";

        pasajerosInstanciadosOfertas.Add(nuevoPasajero);
    }

    private void ActualizarMiradaDePasajeros()
    {
        if (!pasajeroMiraAlJugador || jugadorTransform == null) return;

        foreach (GameObject pasajero in pasajerosInstanciadosOfertas)
        {
            if (pasajero == null) continue;

            Vector3 dirAlJugador = jugadorTransform.position - pasajero.transform.position;
            dirAlJugador.y = 0f;

            if (dirAlJugador.sqrMagnitude > 0.01f)
            {
                Quaternion rotacionObjetivo = Quaternion.LookRotation(dirAlJugador.normalized, Vector3.up);
                pasajero.transform.rotation = Quaternion.Slerp(pasajero.transform.rotation, rotacionObjetivo, 5f * Time.deltaTime);
            }
        }
    }

    private void InstanciarPasajeroBajandoseEnDestino(MissionOrder mision)
    {
        if (!instanciarPasajeroAlBajarse) return;

        GameObject prefabAUsar = (mision.prefabPasajeroEspecifico != null)
            ? mision.prefabPasajeroEspecifico
            : prefabPasajeroPorDefecto;

        if (prefabAUsar == null) return;

        Vector3 posBajada = jugadorTransform.position + (jugadorTransform.right * 1.8f) + offsetPosicionPasajero;
        GameObject pasajeroEntregado = Instantiate(prefabAUsar, posBajada, Quaternion.identity);
        pasajeroEntregado.name = "Pasajero_Entregado";

        Destroy(pasajeroEntregado, 4f);
    }

    private void DestruirPasajerosDeOfertas()
    {
        for (int i = 0; i < pasajerosInstanciadosOfertas.Count; i++)
        {
            if (pasajerosInstanciadosOfertas[i] != null)
            {
                Destroy(pasajerosInstanciadosOfertas[i]);
            }
        }
        pasajerosInstanciadosOfertas.Clear();
    }

    // =========================================================================
    // EVALUACION DEL PUNTO DE LLEGADA (PUNTO DE TRIUNFO) Y PUNTOS
    // =========================================================================

    private void VerificarLlegadaEnPuntoDeTriunfo(bool presionoTeclaE)
    {
        if (misionAceptadaActual == null || misionAceptadaActual.puntoDeLlegada == null) return;

        float distanciaAlCentroAmarillo = DistanciaPlanaXZ(jugadorTransform.position, misionAceptadaActual.puntoDeLlegada.position);
        float velocidadActual = ObtenerVelocidadJugador();

        bool estaDentroDelRadioMaximo = distanciaAlCentroAmarillo <= misionAceptadaActual.radioMaximoLlegada;
        bool vehiculoDetenido = !requiereFrenarEnDestino || (velocidadActual <= velocidadMaximaParaEntregar);
        bool presionoTeclaEntrega = permitirEvaluarConTeclaE && presionoTeclaE;

        jugadorEnAreaDeEntrega = estaDentroDelRadioMaximo;

        if (estaDentroDelRadioMaximo)
        {
            jugadorEstuvoDentroDelArea = true;

            if (vehiculoDetenido || presionoTeclaEntrega)
            {
                EvaluarResultadoDeMision(distanciaAlCentroAmarillo);
            }
        }
        else if (jugadorEstuvoDentroDelArea && ((vehiculoDetenido && velocidadActual < 0.2f) || presionoTeclaEntrega))
        {
            EvaluarResultadoDeMision(distanciaAlCentroAmarillo);
        }
    }

    private void EvaluarResultadoDeMision(float distanciaFinalAlCentro)
    {
        MissionOrder misionEvaluada = misionAceptadaActual;
        int nivel = misionEvaluada.nivelDeMision;
        int numMision = misionEvaluada.numeroDeMision;
        string nombre = misionEvaluada.nombreReferencia;

        bool enRadioPerfecto = distanciaFinalAlCentro <= misionEvaluada.radioOptimoCentro;
        bool dentroDelRadioMaximo = distanciaFinalAlCentro <= misionEvaluada.radioMaximoLlegada;

        jugadorEnAreaDeEntrega = false;

        if (enRadioPerfecto)
        {
            Debug.Log($"[MissionManager] ¡TRIUNFO EN RADIO PERFECTO! ({categoriaActiva}): Nivel {nivel} - Misión #{numMision} ({nombre}). Distancia al centro: {distanciaFinalAlCentro:F2}m.");
            InstanciarPasajeroBajandoseEnDestino(misionEvaluada);
        }
        else if (dentroDelRadioMaximo)
        {
            Debug.Log($"[MissionManager] TRIUNFO EN RADIO NORMAL ({categoriaActiva}): Nivel {nivel} - Misión #{numMision} ({nombre}). Distancia al centro: {distanciaFinalAlCentro:F2}m.");
            InstanciarPasajeroBajandoseEnDestino(misionEvaluada);
        }
        else
        {
            Debug.LogWarning($"[MissionManager] MISIÓN FALLIDA / FUERA DE ÁREA ({categoriaActiva}): Nivel {nivel} - Misión #{numMision} ({nombre}). Distancia: {distanciaFinalAlCentro:F2}m.");
        }

        // Otorgar puntos en el GameManager según el radio de llegada (Nivel 1: 10 pts misión + 5 pts radio perfecto)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CalcularYOtorgarPuntosPorMision(nivel, enRadioPerfecto, dentroDelRadioMaximo);
        }

        // Verificar avance cronológico estricto de las Misiones Principales
        if (categoriaActiva == MissionCategory.MisionPrincipalHistoria)
        {
            if (dentroDelRadioMaximo)
            {
                misionEvaluada.misionCompletada = true;
                indiceMisionPrincipalActual++;

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RegistrarMisionPrincipalCompletada(nivel);
                }

                Debug.Log($"[MissionManager] Misión Principal #{numMision} ({nombre}) completada en su totalidad. Avanzando al índice cronológico: {indiceMisionPrincipalActual}");

                IniciarEsperaParaNuevaMision();

                // Si tiene conversación al completarse, el GameManager pausa el juego y muestra los turnos
                if (misionEvaluada.mostrarDialogoAlCompletar &&
                    misionEvaluada.conversacionAlCompletar != null &&
                    misionEvaluada.conversacionAlCompletar.Count > 0 &&
                    GameManager.Instance != null)
                {
                    GameManager.Instance.DetenerJuegoYMostrarConversacion(
                        misionEvaluada.conversacionAlCompletar,
                        misionEvaluada.musicaDialogoAlCompletar,
                        () =>
                        {
                            if (priorizarMisionesPrincipalesEnOrden && HayMisionesPrincipalesPendientes())
                            {
                                LanzarSiguienteMisionPrincipal();
                            }
                        }
                    );
                }
                return;
            }
            else
            {
                Debug.LogWarning($"[MissionManager] La Misión Principal #{numMision} NO se completó dentro del área de llegada. Se mantendrá activa para realizarse en orden cronológico.");
                LanzarSiguienteMisionPrincipal();
                return;
            }
        }

        IniciarEsperaParaNuevaMision();
    }

    private void IniciarEsperaParaNuevaMision()
    {
        OcultarTodosLosMarcadores();
        ofertasDisponiblesActuales.Clear();
        misionAceptadaActual = null;
        jugadorEstuvoDentroDelArea = false;
        jugadorEnAreaDeAceptacion = false;
        jugadorEnAreaDeEntrega = false;
        temporizadorRandom = 0f;
        estadoActual = MissionState.EsperandoNuevaOrden;
    }

    // =========================================================================
    // APARTADO EXCLUSIVO: MISIONES PRINCIPALES (ORDEN CRONOLOGICO 1 AL N)
    // =========================================================================

    public bool HayMisionesPrincipalesPendientes()
    {
        return misionesPrincipalesSecuencia != null &&
               misionesPrincipalesSecuencia.Count > 0 &&
               indiceMisionPrincipalActual < misionesPrincipalesSecuencia.Count;
    }

    [ContextMenu("Lanzar Siguiente Mision Principal")]
    public void LanzarSiguienteMisionPrincipal()
    {
        if (misionesPrincipalesSecuencia == null || misionesPrincipalesSecuencia.Count == 0)
        {
            Debug.LogWarning("[MissionManager] No hay Misiones Principales configuradas en la lista secuencial.");
            return;
        }

        OrdenarMisionesPrincipalesCronologicamente();

        if (indiceMisionPrincipalActual >= misionesPrincipalesSecuencia.Count)
        {
            Debug.Log("[MissionManager] ¡Todas las Misiones Principales de la secuencia (1 al N) han sido completadas!");
            return;
        }

        OcultarTodosLosMarcadores();
        ofertasDisponiblesActuales.Clear();
        misionAceptadaActual = null;
        jugadorEstuvoDentroDelArea = false;
        jugadorEnAreaDeAceptacion = false;
        jugadorEnAreaDeEntrega = false;

        MissionOrder misionPrincipal = misionesPrincipalesSecuencia[indiceMisionPrincipalActual];

        if (misionPrincipal.puntoDeInicio == null || misionPrincipal.puntoDeLlegada == null)
        {
            Debug.LogWarning($"[MissionManager] La Misión Principal #{misionPrincipal.numeroDeMision} ({misionPrincipal.nombreReferencia}) no tiene asignado Punto de Inicio o Punto de Llegada.");
            return;
        }

        categoriaActiva = MissionCategory.MisionPrincipalHistoria;
        ofertasDisponiblesActuales.Add(misionPrincipal);

        // Activar el marcador con COLOR VERDE exclusivo para Misiones Principales
        ActivarMarcadorOferta(0, misionPrincipal.puntoDeInicio.position, misionPrincipal.radioDeAceptacion, colorIndicadorMisionPrincipal);
        InstanciarPasajeroEnInicio(misionPrincipal);

        estadoActual = MissionState.OfertasDisponiblesEnMapa;

        Debug.Log($"[MissionManager] MISIÓN PRINCIPAL DISPONIBLE EN EL MAPA (Orden {indiceMisionPrincipalActual + 1}/{misionesPrincipalesSecuencia.Count}): " +
                  $"Nivel {misionPrincipal.nivelDeMision} - Misión #{misionPrincipal.numeroDeMision} ({misionPrincipal.nombreReferencia}). " +
                  $"Entra al círculo VERDE y presiona [E] para aceptarla.");
    }

    public void CambiarNivelActual(int nuevoNivel)
    {
        nivelActual = nuevoNivel;
        Debug.Log($"[MissionManager] Nivel actual cambiado a: Nivel {nivelActual}");
    }

    // =========================================================================
    // GENERACION Y CONTROL DE MARCADORES VISUALES (VERDE / AZUL / AMARILLO)
    // =========================================================================

    private void CrearMarcadoresVisualesAutomaticos()
    {
        for (int i = 0; i < 2; i++)
        {
            GameObject marcador = CrearDiscoVisual($"Marcador_Inicio_Oferta_{i + 1}", colorIndicadorInicio);
            marcador.SetActive(false);
            marcadoresVisualesOfertas.Add(marcador);
        }

        marcadorVisualDestinoCentro = CrearDiscoVisual("Marcador_Llegada_CentroAmarillo", colorIndicadorLlegada);
        marcadorVisualDestinoCentro.SetActive(false);

        Color colorAreaExterior = new Color(colorIndicadorLlegada.r, colorIndicadorLlegada.g, colorIndicadorLlegada.b, 0.28f);
        marcadorVisualDestinoArea = CrearDiscoVisual("Marcador_Llegada_AreaTotal", colorAreaExterior);
        marcadorVisualDestinoArea.SetActive(false);
    }

    private GameObject CrearDiscoVisual(string nombre, Color color)
    {
        GameObject cilindro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cilindro.name = nombre;
        Collider col = cilindro.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = cilindro.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            rend.material = mat;
        }

        return cilindro;
    }

    private void ActivarMarcadorOferta(int indice, Vector3 posicion, float radio, Color color)
    {
        if (indice >= marcadoresVisualesOfertas.Count) return;

        GameObject m = marcadoresVisualesOfertas[indice];
        m.SetActive(true);
        m.transform.position = new Vector3(posicion.x, posicion.y + 0.08f, posicion.z);
        m.transform.localScale = new Vector3(radio * 2f, 0.05f, radio * 2f);

        Renderer r = m.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
    }

    private void ActivarMarcadorDestinoAmarillo(Vector3 posicion, float radioCentro, float radioMaximo)
    {
        marcadorVisualDestinoCentro.SetActive(true);
        marcadorVisualDestinoCentro.transform.position = new Vector3(posicion.x, posicion.y + 0.1f, posicion.z);
        marcadorVisualDestinoCentro.transform.localScale = new Vector3(radioCentro * 2f, 0.06f, radioCentro * 2f);

        marcadorVisualDestinoArea.SetActive(true);
        marcadorVisualDestinoArea.transform.position = new Vector3(posicion.x, posicion.y + 0.04f, posicion.z);
        marcadorVisualDestinoArea.transform.localScale = new Vector3(radioMaximo * 2f, 0.03f, radioMaximo * 2f);
    }

    private void OcultarTodosLosMarcadores()
    {
        foreach (var m in marcadoresVisualesOfertas)
        {
            if (m != null) m.SetActive(false);
        }
        if (marcadorVisualDestinoCentro != null) marcadorVisualDestinoCentro.SetActive(false);
        if (marcadorVisualDestinoArea != null) marcadorVisualDestinoArea.SetActive(false);

        DestruirPasajerosDeOfertas();
    }

    // =========================================================================
    // INTERFAZ VISUAL DE LA TECLA "E" DENTRO DE UN CUADRADO BLANCO ABAJO
    // =========================================================================

    private void ActualizarIndicadorPersonalizadoE(bool mostrar)
    {
        if (indicadorTeclaEPersonalizadoUI != null && indicadorTeclaEPersonalizadoUI.activeSelf != mostrar)
        {
            indicadorTeclaEPersonalizadoUI.SetActive(mostrar);
        }
    }

    /// <summary>
    /// Dibuja en la parte inferior de la pantalla un cuadrado blanco con la letra "E" adentro
    /// cuando el jugador está dentro del área de aceptación (o entrega) de la misión.
    /// </summary>
    private void OnGUI()
    {
        if (GameManager.Instance != null && (GameManager.Instance.juegoDetenidoPorDialogo || GameManager.Instance.juegoPausadoPorEscape)) return;
        if (indicadorTeclaEPersonalizadoUI != null) return;
        if (!jugadorEnAreaDeAceptacion && !jugadorEnAreaDeEntrega) return;

        float tamanoCuadrado = 44f;
        float posX = (Screen.width - tamanoCuadrado) * 0.5f;
        float posY = Screen.height - tamanoCuadrado - 42f;

        Color colorOriginal = GUI.color;

        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        GUI.DrawTexture(new Rect(posX - 3f, posY - 3f, tamanoCuadrado + 6f, tamanoCuadrado + 6f), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(posX, posY, tamanoCuadrado, tamanoCuadrado), Texture2D.whiteTexture);

        GUIStyle estiloLetraE = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        estiloLetraE.normal.textColor = Color.black;

        GUI.Label(new Rect(posX, posY, tamanoCuadrado, tamanoCuadrado), "E", estiloLetraE);

        string textoAccion = jugadorEnAreaDeAceptacion ? "Aceptar Misión" : "Dejar Pasajero";
        GUIStyle estiloSubtitulo = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter
        };
        estiloSubtitulo.normal.textColor = Color.white;

        GUI.Label(new Rect(posX - 60f, posY + tamanoCuadrado + 4f, tamanoCuadrado + 120f, 22f), textoAccion, estiloSubtitulo);

        GUI.color = colorOriginal;
    }

    // =========================================================================
    // UTILIDADES Y ENTRADA DE TECLADO
    // =========================================================================

    private float DistanciaPlanaXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private float ObtenerVelocidadJugador()
    {
        if (carriageController != null) return carriageController.currentSpeed;
        Rigidbody rb = jugadorTransform.GetComponent<Rigidbody>();
        if (rb != null) return new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        return 0f;
    }

    private bool SePresionoEnter()
    {
        bool enter = false;
        try
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) enter = true;
        }
        catch { }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                enter = true;
        }
#endif
        return enter;
    }

    private bool SePresionoTeclaE()
    {
        bool teclaE = false;
        try
        {
            if (Input.GetKeyDown(KeyCode.E)) teclaE = true;
        }
        catch { }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            teclaE = true;
#endif
        return teclaE;
    }

    // =========================================================================
    // VISUALIZACION DE RADIOS EN EL EDITOR (GIZMOS)
    // =========================================================================

    private void OnDrawGizmos()
    {
        if (nivelesDeMisiones != null)
        {
            foreach (var pool in nivelesDeMisiones)
            {
                if (pool.misionesDelNivel == null) continue;
                foreach (var m in pool.misionesDelNivel)
                {
                    DibujarGizmosDeMision(m, Color.cyan);
                }
            }
        }

        if (misionesPrincipalesSecuencia != null)
        {
            foreach (var m in misionesPrincipalesSecuencia)
            {
                DibujarGizmosDeMision(m, Color.green);
            }
        }
    }

    private void DibujarGizmosDeMision(MissionOrder m, Color colorInicio)
    {
        if (m == null) return;

        if (m.puntoDeInicio != null)
        {
            Gizmos.color = colorInicio;
            Gizmos.DrawWireSphere(m.puntoDeInicio.position, m.radioDeAceptacion);
        }

        if (m.puntoDeLlegada != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m.puntoDeLlegada.position, m.radioOptimoCentro);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
            Gizmos.DrawWireSphere(m.puntoDeLlegada.position, m.radioMaximoLlegada);

            if (m.puntoDeInicio != null)
            {
                Gizmos.color = new Color(colorInicio.r, colorInicio.g, colorInicio.b, 0.45f);
                Gizmos.DrawLine(m.puntoDeInicio.position, m.puntoDeLlegada.position);
            }
        }
    }
}
