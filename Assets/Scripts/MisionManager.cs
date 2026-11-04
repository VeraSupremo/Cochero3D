using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        [Tooltip("Nombre referencial en el Inspector (ej. Llevar pasajero a la plaza)")]
        public string nombreReferencia = "Nueva Mision";

        [Tooltip("Nivel al que pertenece esta mision (ej. 1, 2, 3...)")]
        public int nivelDeMision = 1;

        [Tooltip("Numero identificador de la mision (ej. 1, 2, 3, 4, 5...)")]
        public int numeroDeMision = 1;

        [Header("Pasajero de esta Mision (Opcional)")]
        [Tooltip("Si lo dejas vacio, usara el Prefab de Pasajero por Defecto del MissionManager")]
        public GameObject prefabPasajeroEspecifico;

        [Header("Puntos Vacios del Mapa (Transforms)")]
        [Tooltip("GameObject vacio donde aparece el marcador y el pasajero para ir a aceptar la mision")]
        public Transform puntoDeInicio;

        [Tooltip("GameObject vacio que actua como Punto de Llegada / Triunfo (Gran punto amarillo)")]
        public Transform puntoDeLlegada;

        [Header("Radios del Punto de Triunfo (Manejables a discrecion)")]
        [Tooltip("Radio de recogida en el punto de inicio")]
        public float radioDeAceptacion = 3.5f;

        [Tooltip("Radio CENTRAL OPTIMO (Terminar cerca del centro = Triunfo Excelente / Maximo Puntaje)")]
        public float radioOptimoCentro = 2.5f;

        [Tooltip("Radio EXTERIOR TOTAL del area amarilla (Terminar lejos del centro = Menor Puntaje)")]
        public float radioMaximoLlegada = 7.0f;
    }

    [System.Serializable]
    public class LevelMissionPool
    {
        [Tooltip("Etiqueta del nivel en el Inspector (ej. Nivel 1)")]
        public string nombreDelNivel = "Nivel 1";
        [Tooltip("Numero del nivel (1, 2, 3...)")]
        public int numeroNivel = 1;
        [Tooltip("Lista de ordenes de mision (1, 2, 3, 4, 5...) pertenecientes a este nivel")]
        public List<MissionOrder> misionesDelNivel = new List<MissionOrder>();
    }

    // =========================================================================
    // VARIABLES DEL INSPECTOR
    // =========================================================================

    [Header("Referencias Principales")]
    [Tooltip("Objeto Caballos (si se deja vacio, busca CrazyCarriageController automaticamente)")]
    public Transform jugadorTransform;
    private CrazyCarriageController carriageController;

    [Header("Configuracion de Pasajeros (Prefabs)")]
    [Tooltip("Arrastra aqui el Prefab de tu pasajero que aparecera dentro del circulo azul")]
    public GameObject prefabPasajeroPorDefecto;
    [Tooltip("Ajuste de altura por si el pivote de tu Prefab queda enterrado en el suelo")]
    public Vector3 offsetPosicionPasajero = new Vector3(0f, 0f, 0f);
    [Tooltip("Si esta activo, el pasajero mira hacia el jugador mientras espera en el punto azul")]
    public bool pasajeroMiraAlJugador = true;
    [Tooltip("Si esta activo, cuando completas la mision el pasajero se baja en el destino y desaparece a los pocos segundos")]
    public bool instanciarPasajeroAlBajarse = true;

    [Header("Configuracion de Nivel y Tiempos (Estilo Uber)")]
    [Tooltip("Nivel en el que inicia el juego (por defecto Nivel 1)")]
    public int nivelActual = 1;
    [Tooltip("Tiempo de espera antes de lanzar el sorteo aleatorio de misiones")]
    public float tiempoEsperaParaRandom = 3.0f;
    [Tooltip("Cantidad de trabajos estilo Uber que aparecen simultaneamente (1 o 2)")]
    [Range(1, 2)] public int cantidadTrabajosSimultaneos = 2;

    [Header("1. Listas de Misiones Separadas por Niveles (Estilo Uber)")]
    public List<LevelMissionPool> nivelesDeMisiones = new List<LevelMissionPool>();

    [Header("2. Apartado Exclusivo: Misiones Principales (Secuencia Propia)")]
    [Tooltip("Lista secuencial de misiones importantes o principales (se ejecutan en orden 0, 1, 2...)")]
    public List<MissionOrder> misionesPrincipalesSecuencia = new List<MissionOrder>();
    [Tooltip("Indice actual dentro de la secuencia de misiones principales")]
    public int indiceMisionPrincipalActual = 0;

    [Header("Condiciones de Entrega y Controles")]
    [Tooltip("Si esta activo, el jugador debe frenar dentro del area amarilla para evaluar el punto de llegada")]
    public bool requiereFrenarEnDestino = true;
    [Tooltip("Velocidad maxima considerada como detenido para confirmar la entrega")]
    public float velocidadMaximaParaEntregar = 1.0f;
    [Tooltip("Permite confirmar o evaluar manualmente presionando la tecla E")]
    public bool permitirEvaluarConTeclaE = true;

    [Header("Colores de los Indicadores en el Mapa")]
    public Color colorIndicadorInicio = new Color(0.1f, 0.55f, 1f, 0.75f);   // Azul para ir a aceptar
    public Color colorIndicadorLlegada = new Color(1f, 0.85f, 0.05f, 0.8f); // Amarillo grande en punto de llegada

    [Header("Estado en Tiempo Real (Solo Lectura)")]
    public MissionState estadoActual = MissionState.EsperandoNuevaOrden;
    public MissionCategory categoriaActiva = MissionCategory.EncargoUberAleatorio;
    public List<MissionOrder> ofertasDisponiblesActuales = new List<MissionOrder>();
    public MissionOrder misionAceptadaActual = null;

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
        AutoDetectarJugador();
        CrearMarcadoresVisualesAutomaticos();
    }

    void Start()
    {
        if (nivelActual < 1) nivelActual = 1;
        IniciarEsperaParaNuevaMision();
    }

    private void AutoDetectarJugador()
    {
        if (jugadorTransform == null)
        {
            CrazyCarriageController encontrado = FindFirstObjectByType<CrazyCarriageController>();
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

    // =========================================================================
    // BUCLE PRINCIPAL (UPDATE)
    // =========================================================================

    void Update()
    {
        if (jugadorTransform == null) return;

        // Si se presiona ENTER, destruye los pasajeros actuales y relanza el random
        if (SePresionoEnter())
        {
            Debug.Log("[MissionManager] ENTER presionado: Relanzando random de misiones para el Nivel " + nivelActual);
            LanzarRandomMisionPorNivel(nivelActual);
            return;
        }

        switch (estadoActual)
        {
            case MissionState.EsperandoNuevaOrden:
                temporizadorRandom += Time.deltaTime;
                if (temporizadorRandom >= tiempoEsperaParaRandom)
                {
                    LanzarRandomMisionPorNivel(nivelActual);
                }
                break;

            case MissionState.OfertasDisponiblesEnMapa:
                ActualizarMiradaDePasajeros();
                VerificarAceptacionDeMisionEnPuntoDeInicio();
                break;

            case MissionState.MisionEnCursoHaciaDestino:
                VerificarLlegadaEnPuntoDeTriunfo();
                break;
        }
    }

    // =========================================================================
    // SISTEMA UBER: SORTEO RANDOM POR NIVEL E INSTANCIACION DE PASAJEROS
    // =========================================================================

    public void LanzarRandomMisionPorNivel(int nivel)
    {
        OcultarTodosLosMarcadores(); // Tambien destruye pasajeros de ofertas anteriores
        ofertasDisponiblesActuales.Clear();
        misionAceptadaActual = null;
        jugadorEstuvoDentroDelArea = false;

        LevelMissionPool poolDelNivel = nivelesDeMisiones.Find(n => n.numeroNivel == nivel);

        if (poolDelNivel == null || poolDelNivel.misionesDelNivel.Count == 0)
        {
            Debug.LogWarning($"[MissionManager] No existen misiones configuradas para el Nivel {nivel} en el Inspector.");
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
                Debug.LogWarning($"[MissionManager] La mision #{misionElegida.numeroDeMision} del Nivel {nivel} carece de Punto de Inicio o Punto de Llegada.");
                continue;
            }

            ofertasDisponiblesActuales.Add(misionElegida);

            // 1. Activar el cilindro azul en el suelo
            ActivarMarcadorOferta(i, misionElegida.puntoDeInicio.position, misionElegida.radioDeAceptacion, colorIndicadorInicio);

            // 2. Instanciar el Prefab del pasajero en ese mismo punto de inicio
            InstanciarPasajeroEnInicio(misionElegida);

            Debug.Log($"[MissionManager] Oferta generada en Nivel {misionElegida.nivelDeMision} | Mision #{misionElegida.numeroDeMision} ({misionElegida.nombreReferencia}). Pasajero esperando en punto AZUL.");
        }

        if (ofertasDisponiblesActuales.Count > 0)
        {
            categoriaActiva = MissionCategory.EncargoUberAleatorio;
            estadoActual = MissionState.OfertasDisponiblesEnMapa;
        }
    }

    private void VerificarAceptacionDeMisionEnPuntoDeInicio()
    {
        for (int i = 0; i < ofertasDisponiblesActuales.Count; i++)
        {
            MissionOrder oferta = ofertasDisponiblesActuales[i];
            if (oferta.puntoDeInicio == null) continue;

            float distanciaAlInicio = DistanciaPlanaXZ(jugadorTransform.position, oferta.puntoDeInicio.position);

            if (distanciaAlInicio <= oferta.radioDeAceptacion)
            {
                ConfirmarYAceptarMision(oferta);
                break;
            }
        }
    }

    private void ConfirmarYAceptarMision(MissionOrder misionConfirmada)
    {
        misionAceptadaActual = misionConfirmada;
        jugadorEstuvoDentroDelArea = false;

        // Oculta los cilindros azules y destruye los pasajeros de la calle (simulando que subio a la carroza)
        OcultarTodosLosMarcadores();

        // El indicador azul pasa a ser amarillo en el Punto de Llegada
        if (misionAceptadaActual.puntoDeLlegada != null)
        {
            ActivarMarcadorDestinoAmarillo(
                misionAceptadaActual.puntoDeLlegada.position,
                misionAceptadaActual.radioOptimoCentro,
                misionAceptadaActual.radioMaximoLlegada
            );
        }

        estadoActual = MissionState.MisionEnCursoHaciaDestino;

        Debug.Log($"[MissionManager] Pasajero recogido y Mision ACEPTADA ({categoriaActiva}): Nivel {misionAceptadaActual.nivelDeMision} - Mision #{misionAceptadaActual.numeroDeMision}. Dirigirse al punto AMARILLO.");
    }

    // =========================================================================
    // LOGICA DE INSTANCIACION Y LIMPIEZA DE PASAJEROS
    // =========================================================================

    private void InstanciarPasajeroEnInicio(MissionOrder mision)
    {
        // Si la mision tiene un prefab propio lo usa; si no, usa el prefab general por defecto
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

        // Aparece a un costado del jugador (como si bajara de la carroza) y se borra a los 4 segundos
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
    // EVALUACION DEL PUNTO DE LLEGADA (PUNTO DE TRIUNFO)
    // =========================================================================

    private void VerificarLlegadaEnPuntoDeTriunfo()
    {
        if (misionAceptadaActual == null || misionAceptadaActual.puntoDeLlegada == null) return;

        float distanciaAlCentroAmarillo = DistanciaPlanaXZ(jugadorTransform.position, misionAceptadaActual.puntoDeLlegada.position);
        float velocidadActual = ObtenerVelocidadJugador();

        bool estaDentroDelRadioMaximo = distanciaAlCentroAmarillo <= misionAceptadaActual.radioMaximoLlegada;
        bool vehiculoDetenido = !requiereFrenarEnDestino || (velocidadActual <= velocidadMaximaParaEntregar);
        bool presionoTeclaEntrega = permitirEvaluarConTeclaE && SePresionoTeclaE();

        if (estaDentroDelRadioMaximo)
        {
            jugadorEstuvoDentroDelArea = true;

            if (vehiculoDetenido || presionoTeclaEntrega)
            {
                EvaluarResultadoDeMision(distanciaAlCentroAmarillo);
            }
        }
        else if ((jugadorEstuvoDentroDelArea && vehiculoDetenido && velocidadActual < 0.2f) || presionoTeclaEntrega)
        {
            EvaluarResultadoDeMision(distanciaAlCentroAmarillo);
        }
    }

    private void EvaluarResultadoDeMision(float distanciaFinalAlCentro)
    {
        int nivel = misionAceptadaActual.nivelDeMision;
        int numMision = misionAceptadaActual.numeroDeMision;
        string nombre = misionAceptadaActual.nombreReferencia;

        // 1. CONDICION DE TRIUNFO CERCANA AL CENTRO (OPTIMA)
        if (distanciaFinalAlCentro <= misionAceptadaActual.radioOptimoCentro)
        {
            Debug.Log($"[MissionManager] TRIUNFO PERFECTO ({categoriaActiva}): Nivel {nivel} - Mision #{numMision} ({nombre}). Distancia al centro: {distanciaFinalAlCentro:F2}m.");

            InstanciarPasajeroBajandoseEnDestino(misionAceptadaActual);

            // =================================================================
            // COMENTARIO DE CONEXION CON GAMEMANAGER (DISTANCIA CERCANA / CENTRO):
            // Agregar aqui la llamada al GameManager para otorgar el PUNTAJE MAXIMO
            // cuando la mision termina muy cerca del centro del punto amarillo.
            // =================================================================
        }
        // 2. CONDICION DE TRIUNFO LEJANA AL CENTRO (MENOR PUNTAJE)
        else if (distanciaFinalAlCentro <= misionAceptadaActual.radioMaximoLlegada)
        {
            Debug.Log($"[MissionManager] TRIUNFO CON DISTANCIA LEJANA ({categoriaActiva}): Nivel {nivel} - Mision #{numMision} ({nombre}). Distancia al centro: {distanciaFinalAlCentro:F2}m.");

            InstanciarPasajeroBajandoseEnDestino(misionAceptadaActual);

            // =================================================================
            // COMENTARIO DE CONEXION CON GAMEMANAGER (DISTANCIA LEJANA):
            // Agregar aqui la llamada al GameManager para otorgar MENOS PUNTAJE
            // cuando la mision se termina lejos del centro del gran punto amarillo.
            // =================================================================
        }
        // 3. CONDICION DE MISION MAL TERMINADA (FUERA DEL RADIO DE TRIUNFO)
        else
        {
            Debug.LogWarning($"[MissionManager] MISION MAL TERMINADA / FUERA DE AREA ({categoriaActiva}): Nivel {nivel} - Mision #{numMision} ({nombre}). Distancia: {distanciaFinalAlCentro:F2}m.");

            // =================================================================
            // COMENTARIO DE CONEXION CON GAMEMANAGER (MISION MAL TERMINADA):
            // Agregar aqui la llamada al GameManager para registrar que la mision
            // fue terminada mal por quedar fuera del radio del punto de triunfo.
            // =================================================================
        }

        if (categoriaActiva == MissionCategory.MisionPrincipalHistoria && distanciaFinalAlCentro <= misionAceptadaActual.radioMaximoLlegada)
        {
            indiceMisionPrincipalActual++;
            Debug.Log($"[MissionManager] Mision Principal superada. Indice de secuencia actualizado a: {indiceMisionPrincipalActual}");
        }

        IniciarEsperaParaNuevaMision();
    }

    private void IniciarEsperaParaNuevaMision()
    {
        OcultarTodosLosMarcadores();
        ofertasDisponiblesActuales.Clear();
        misionAceptadaActual = null;
        jugadorEstuvoDentroDelArea = false;
        temporizadorRandom = 0f;
        estadoActual = MissionState.EsperandoNuevaOrden;
    }

    // =========================================================================
    // APARTADO EXCLUSIVO: MISIONES PRINCIPALES (SECUENCIA PROPIA)
    // =========================================================================

    [ContextMenu("Lanzar Siguiente Mision Principal")]
    public void LanzarSiguienteMisionPrincipal()
    {
        if (misionesPrincipalesSecuencia == null || misionesPrincipalesSecuencia.Count == 0)
        {
            Debug.LogWarning("[MissionManager] No hay Misiones Principales configuradas en la lista secuencial.");
            return;
        }

        if (indiceMisionPrincipalActual >= misionesPrincipalesSecuencia.Count)
        {
            Debug.Log("[MissionManager] Todas las Misiones Principales de la secuencia han sido completadas.");
            return;
        }

        OcultarTodosLosMarcadores();
        ofertasDisponiblesActuales.Clear();
        misionAceptadaActual = null;

        MissionOrder misionPrincipal = misionesPrincipalesSecuencia[indiceMisionPrincipalActual];

        if (misionPrincipal.puntoDeInicio == null || misionPrincipal.puntoDeLlegada == null)
        {
            Debug.LogWarning($"[MissionManager] La Mision Principal #{misionPrincipal.numeroDeMision} ({misionPrincipal.nombreReferencia}) no tiene asignado Punto de Inicio o Punto de Llegada.");
            return;
        }

        categoriaActiva = MissionCategory.MisionPrincipalHistoria;
        ofertasDisponiblesActuales.Add(misionPrincipal);

        ActivarMarcadorOferta(0, misionPrincipal.puntoDeInicio.position, misionPrincipal.radioDeAceptacion, colorIndicadorInicio);
        InstanciarPasajeroEnInicio(misionPrincipal);

        estadoActual = MissionState.OfertasDisponiblesEnMapa;

        Debug.Log($"[MissionManager] MISION PRINCIPAL ACTIVADA (Secuencia #{indiceMisionPrincipalActual + 1}): Nivel {misionPrincipal.nivelDeMision} - Mision #{misionPrincipal.numeroDeMision}.");
    }

    public void CambiarNivelActual(int nuevoNivel)
    {
        nivelActual = nuevoNivel;
        Debug.Log($"[MissionManager] Nivel actual cambiado a: Nivel {nivelActual}");
    }

    // =========================================================================
    // GENERACION Y CONTROL DE MARCADORES VISUALES (AZUL A AMARILLO)
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

        // Limpia tambien cualquier pasajero que haya quedado esperando en los puntos azules
        DestruirPasajerosDeOfertas();
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
                    DibujarGizmosDeMision(m);
                }
            }
        }

        if (misionesPrincipalesSecuencia != null)
        {
            foreach (var m in misionesPrincipalesSecuencia)
            {
                DibujarGizmosDeMision(m);
            }
        }
    }

    private void DibujarGizmosDeMision(MissionOrder m)
    {
        if (m == null) return;

        if (m.puntoDeInicio != null)
        {
            Gizmos.color = Color.cyan;
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
                Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
                Gizmos.DrawLine(m.puntoDeInicio.position, m.puntoDeLlegada.position);
            }
        }
    }
}