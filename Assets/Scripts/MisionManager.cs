using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Gestor de Misiones (Principales en orden cronológico 1..N y Secundarias estilo Uber).
/// Controla los círculos de aceptación (Verde para Principales, Azul para Secundarias),
/// los Puntos Intermedios (1 al N) que activan diálogos automáticamente cuando el jugador pasa por ellos,
/// la verificación de la tecla E con su cuadrado blanco [E] y la evaluación de puntos con el GameManager.
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
    // CLASE PARA PUNTOS INTERMEDIOS DE MISION (1 AL N - ACTIVAN DIALOGOS AL PASAR)
    // =========================================================================

    /// <summary>
    /// Representa un punto intermedio en el camino de una Misión Principal (del 1 al N).
    /// Cuando el jugador pasa por su radio durante la misión, detiene el juego y lanza una conversación
    /// para explicar mecánicas, historia o indicaciones antes de llegar al destino final.
    /// </summary>
    [System.Serializable]
    public class PuntoIntermedioMision
    {
        [Tooltip("Nombre referencial en el Inspector (ej. Punto 1 - Explicación de Derrape en la esquina)")]
        public string nombreReferenciaPunto = "Punto Intermedio 1";

        [Tooltip("Orden sucesivo de este punto intermedio (1, 2, 3... N)")]
        public int ordenDelPunto = 1;

        [Tooltip("GameObject vacío en el mapa por donde debe pasar el jugador para activar el diálogo")]
        public Transform puntoIntermedioTransform;

        [Tooltip("Tamaño del área: apenas el jugador pase dentro de este radio, se activará el diálogo automáticamente")]
        public float radioDeActivacion = 5.5f;

        [Tooltip("Si está activo, muestra un círculo guía en el suelo en este punto intermedio hasta que pases por él")]
        public bool mostrarCirculoEnSuelo = true;

        [Tooltip("Música opcional que sonará durante el diálogo de este punto intermedio")]
        public AudioClip musicaDialogoIntermedio;

        [Tooltip("Conversación turno por turno que aparecerá cuando el jugador pase por este punto intermedio")]
        public List<SistemaDialogos.LineaDeDialogo> conversacionPuntoIntermedio = new List<SistemaDialogos.LineaDeDialogo>();

        [Header("Estado (Solo Lectura)")]
        [Tooltip("Se marca automáticamente cuando el jugador ya pasó por este punto en la misión actual")]
        public bool yaFueActivado = false;
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

        [Header("Puntos Vacíos del Mapa (Inicio y Llegada)")]
        [Tooltip("GameObject vacío donde aparece el círculo de aceptación y el pasajero esperando")]
        public Transform puntoDeInicio;

        [Tooltip("GameObject vacío que actúa como Punto de Llegada / Triunfo (Gran círculo amarillo)")]
        public Transform puntoDeLlegada;

        [Header("Puntos Intermedios de Diálogo (1 al N - Al pasar por el camino)")]
        [Tooltip("Agrega con '+' de 1 a N puntos intermedios. Al pasar por cada uno durante el viaje, se activará su diálogo.")]
        public List<PuntoIntermedioMision> puntosIntermedios = new List<PuntoIntermedioMision>();

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
    [Tooltip("Color VERDE AGUA / TURQUESA para los Puntos Intermedios (1 al N) de diálogo")]
    public Color colorIndicadorPuntoIntermedio = new Color(0.15f, 0.95f, 0.65f, 0.65f); // Verde agua para Punto Intermedio
    [Tooltip("Color AZUL para el radio de aceptación de las Misiones Secundarias")]
    public Color colorIndicadorInicio = new Color(0.1f, 0.55f, 1f, 0.75f);            // Azul para Misión Secundaria
    [Tooltip("Color AMARILLO para el radio de llegada / entrega del pasajero")]
    public Color colorIndicadorLlegada = new Color(1f, 0.85f, 0.05f, 0.8f);            // Amarillo en punto de llegada

    [Header("Estado en Tiempo Real (Solo Lectura)")]
    public MissionState estadoActual = MissionState.EsperandoNuevaOrden;
    public MissionCategory categoriaActiva = MissionCategory.EncargoUberAleatorio;
    public List<MissionOrder> ofertasDisponiblesActuales = new List<MissionOrder>();
    public MissionOrder misionAceptadaActual = null;
    public int indicePuntoIntermedioActual = 0;
    public bool jugadorEnAreaDeAceptacion = false;
    public bool jugadorEnAreaDeEntrega = false;

    private float temporizadorRandom = 0f;
    private List<GameObject> marcadoresVisualesOfertas = new List<GameObject>();
    private List<GameObject> pasajerosInstanciadosOfertas = new List<GameObject>();
    private GameObject marcadorVisualPuntoIntermedio;
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
    /// Ordena la lista de Misiones Principales estrictamente por nivel (1..5) y número de misión (1..N),
    /// y también ordena los Puntos Intermedios (1..N) dentro de cada misión.
    /// </summary>
    public void OrdenarMisionesPrincipalesCronologicamente()
    {
        if (misionesPrincipalesSecuencia == null) return;

        if (misionesPrincipalesSecuencia.Count > 1)
        {
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

        foreach (var mision in misionesPrincipalesSecuencia)
        {
            if (mision != null)
            {
                OrdenarPuntosIntermediosDeMision(mision);
            }
        }
    }

    /// <summary>
    /// Ordena los puntos intermedios de una misión del 1 al N según su campo 'ordenDelPunto'.
    /// </summary>
    private void OrdenarPuntosIntermediosDeMision(MissionOrder mision)
    {
        if (mision == null || mision.puntosIntermedios == null || mision.puntosIntermedios.Count <= 1) return;

        mision.puntosIntermedios.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.ordenDelPunto.CompareTo(b.ordenDelPunto);
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
                // 1. Verificar si el jugador pasó por alguno de los Puntos Intermedios (1 al N) para activar su diálogo
                VerificarPasoPorPuntosIntermedios();
                // 2. Verificar si el jugador llegó al punto amarillo final de destino
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

        // Preparar y ordenar los Puntos Intermedios (1 al N) de esta misión
        PrepararPuntosIntermediosAlIniciarMision(misionAceptadaActual);

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
    // LOGICA DE PUNTOS INTERMEDIOS (1 AL N) QUE ACTIVAN DIALOGOS AL PASAR
    // =========================================================================

    /// <summary>
    /// Reinicia el estado de los puntos intermedios (1 al N) al comenzar la misión y activa el marcador del Punto Intermedio #1.
    /// </summary>
    private void PrepararPuntosIntermediosAlIniciarMision(MissionOrder mision)
    {
        indicePuntoIntermedioActual = 0;

        if (mision == null || mision.puntosIntermedios == null || mision.puntosIntermedios.Count == 0)
        {
            if (marcadorVisualPuntoIntermedio != null) marcadorVisualPuntoIntermedio.SetActive(false);
            return;
        }

        OrdenarPuntosIntermediosDeMision(mision);

        foreach (var punto in mision.puntosIntermedios)
        {
            if (punto != null) punto.yaFueActivado = false;
        }

        ActualizarMarcadorVisualDePuntoIntermedioActual();
    }

    /// <summary>
    /// Verifica en cada frame si el jugador acaba de pasar por el Punto Intermedio actual (1, 2... N).
    /// Al entrar a su radio, detiene el juego automáticamente, muestra el diálogo explicativo y pasa al siguiente punto (2..N).
    /// </summary>
    private void VerificarPasoPorPuntosIntermedios()
    {
        if (misionAceptadaActual == null ||
            misionAceptadaActual.puntosIntermedios == null ||
            indicePuntoIntermedioActual >= misionAceptadaActual.puntosIntermedios.Count)
        {
            return;
        }

        PuntoIntermedioMision puntoActual = misionAceptadaActual.puntosIntermedios[indicePuntoIntermedioActual];

        // Si el punto está vacío o ya fue activado, pasamos al siguiente
        if (puntoActual == null || puntoActual.yaFueActivado || puntoActual.puntoIntermedioTransform == null)
        {
            indicePuntoIntermedioActual++;
            ActualizarMarcadorVisualDePuntoIntermedioActual();
            return;
        }

        float distanciaAlPuntoIntermedio = DistanciaPlanaXZ(jugadorTransform.position, puntoActual.puntoIntermedioTransform.position);

        // Si el jugador pasa por dentro del radio del punto intermedio
        if (distanciaAlPuntoIntermedio <= puntoActual.radioDeActivacion)
        {
            puntoActual.yaFueActivado = true;
            indicePuntoIntermedioActual++;

            // Actualizar el círculo en el suelo para que apunte al siguiente Punto Intermedio (o se apague si era el último)
            ActualizarMarcadorVisualDePuntoIntermedioActual();

            Debug.Log($"[MissionManager] Punto Intermedio #{puntoActual.ordenDelPunto} ({puntoActual.nombreReferenciaPunto}) alcanzado. Activando diálogo intermedio ({indicePuntoIntermedioActual}/{misionAceptadaActual.puntosIntermedios.Count}).");

            // Llamar al GameManager para detener el juego y mostrar la conversación de este punto intermedio
            if (puntoActual.conversacionPuntoIntermedio != null &&
                puntoActual.conversacionPuntoIntermedio.Count > 0 &&
                GameManager.Instance != null)
            {
                GameManager.Instance.DetenerJuegoYMostrarConversacion(
                    puntoActual.conversacionPuntoIntermedio,
                    puntoActual.musicaDialogoIntermedio
                );
            }
        }
    }

    /// <summary>
    /// Muestra en el suelo el círculo del Punto Intermedio actual (si tiene activa la casilla 'mostrarCirculoEnSuelo').
    /// </summary>
    private void ActualizarMarcadorVisualDePuntoIntermedioActual()
    {
        if (marcadorVisualPuntoIntermedio == null) return;

        if (misionAceptadaActual == null ||
            misionAceptadaActual.puntosIntermedios == null ||
            indicePuntoIntermedioActual >= misionAceptadaActual.puntosIntermedios.Count)
        {
            marcadorVisualPuntoIntermedio.SetActive(false);
            return;
        }

        PuntoIntermedioMision puntoActual = misionAceptadaActual.puntosIntermedios[indicePuntoIntermedioActual];

        if (puntoActual != null &&
            puntoActual.puntoIntermedioTransform != null &&
            puntoActual.mostrarCirculoEnSuelo)
        {
            Vector3 pos = puntoActual.puntoIntermedioTransform.position;
            float radio = puntoActual.radioDeActivacion;

            marcadorVisualPuntoIntermedio.SetActive(true);
            marcadorVisualPuntoIntermedio.transform.position = new Vector3(pos.x, pos.y + 0.07f, pos.z);
            marcadorVisualPuntoIntermedio.transform.localScale = new Vector3(radio * 2f, 0.045f, radio * 2f);

            Renderer r = marcadorVisualPuntoIntermedio.GetComponent<Renderer>();
            if (r != null) r.material.color = colorIndicadorPuntoIntermedio;
        }
        else
        {
            marcadorVisualPuntoIntermedio.SetActive(false);
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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CalcularYOtorgarPuntosPorMision(nivel, enRadioPerfecto, dentroDelRadioMaximo);
        }

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

        marcadorVisualPuntoIntermedio = CrearDiscoVisual("Marcador_Punto_Intermedio_Dialogo", colorIndicadorPuntoIntermedio);
        marcadorVisualPuntoIntermedio.SetActive(false);

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
        if (marcadorVisualPuntoIntermedio != null) marcadorVisualPuntoIntermedio.SetActive(false);
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

        Vector3 ultimoPuntoRuta = Vector3.zero;
        bool tienePuntoAnterior = false;

        if (m.puntoDeInicio != null)
        {
            Gizmos.color = colorInicio;
            Gizmos.DrawWireSphere(m.puntoDeInicio.position, m.radioDeAceptacion);
            ultimoPuntoRuta = m.puntoDeInicio.position;
            tienePuntoAnterior = true;
        }

        // Dibujar los Puntos Intermedios (1 al N) y las líneas que los encadenan en la escena
        if (m.puntosIntermedios != null && m.puntosIntermedios.Count > 0)
        {
            Color colorGizmoIntermedio = new Color(0.15f, 0.95f, 0.65f, 0.9f);
            foreach (var puntoIntermedio in m.puntosIntermedios)
            {
                if (puntoIntermedio == null || puntoIntermedio.puntoIntermedioTransform == null) continue;

                Gizmos.color = colorGizmoIntermedio;
                Gizmos.DrawWireSphere(puntoIntermedio.puntoIntermedioTransform.position, puntoIntermedio.radioDeActivacion);

                if (tienePuntoAnterior)
                {
                    Gizmos.color = new Color(0.15f, 0.95f, 0.65f, 0.55f);
                    Gizmos.DrawLine(ultimoPuntoRuta, puntoIntermedio.puntoIntermedioTransform.position);
                }

                ultimoPuntoRuta = puntoIntermedio.puntoIntermedioTransform.position;
                tienePuntoAnterior = true;
            }
        }

        if (m.puntoDeLlegada != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m.puntoDeLlegada.position, m.radioOptimoCentro);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
            Gizmos.DrawWireSphere(m.puntoDeLlegada.position, m.radioMaximoLlegada);

            if (tienePuntoAnterior)
            {
                Gizmos.color = new Color(colorInicio.r, colorInicio.g, colorInicio.b, 0.45f);
                Gizmos.DrawLine(ultimoPuntoRuta, m.puntoDeLlegada.position);
            }
        }
    }
}
