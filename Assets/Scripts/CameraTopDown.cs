using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cámara de conducción en tercera persona (estilo The Simpsons: Hit & Run).
/// Sigue al jugador desde la espalda de forma rígida, permite un movimiento muy leve y limitado con el mouse,
/// y detecta colisiones con edificios/muros para esquivarlos sin atravesarlos.
/// </summary>
public class CameraTopDown : MonoBehaviour
{
    // =========================================================================
    // 1. OBJETIVO A SEGUIR
    // =========================================================================

    [Header("1. Objetivo a Seguir (Auto-detectado)")]
    [Tooltip("Arrastra aquí el objeto Caballos (si se deja vacío, lo busca automáticamente)")]
    public Transform target;
    private Rigidbody targetRb;
    private CrazyCarriageController carriageController;

    // =========================================================================
    // 2. POSICION DESDE LA ESPALDA (ESTILO HIT & RUN)
    // =========================================================================

    [Header("2. Vista Desde la Espalda (Estilo Hit & Run)")]
    [Tooltip("Distancia hacia atrás desde el vehículo (cubre los caballos y la carroza)")]
    public float distanciaDetras = 10.5f;

    [Tooltip("Altura de la cámara sobre el suelo detrás de la carroza")]
    public float alturaCamara = 5.2f;

    [Tooltip("Altura del punto hacia donde mira la cámara (un poco por encima del techo de la carroza)")]
    public float alturaPuntoDeMira = 2.2f;

    [Tooltip("Cuánto se adelanta el punto de mira hacia el frente para ver mejor la calle")]
    public float adelantoPuntoDeMira = 2.5f;

    // =========================================================================
    // 3. RIGIDEZ DE SEGUIMIENTO Y MOUSE LIMITADO
    // =========================================================================

    [Header("3. Rigidez de Cámara y Límite del Mouse")]
    [Tooltip("Qué tan rígido sigue la cámara la espalda del coche al doblar (Mayor valor = más rígida)")]
    [Range(2f, 20f)] public float rigidezRotacionEspalda = 9.0f;

    [Tooltip("Tiempo de suavizado de la posición (0.04 = muy firme y sin tirones)")]
    [Range(0f, 0.25f)] public float smoothTime = 0.04f;

    [Tooltip("Permite mover un poquito la cámara con el mouse pero con límite rígido")]
    public bool permitirMirarUnPocoConMouse = true;

    [Tooltip("Sensibilidad del leve movimiento con el mouse")]
    [Range(0.2f, 5f)] public float sensibilidadMouse = 1.4f;

    [Tooltip("Ángulo MÁXIMO que puedes girar la cámara a izquierda/derecha con el mouse (18 grados = rígido)")]
    [Range(0f, 45f)] public float limiteMouseHorizontal = 18f;

    [Tooltip("Ángulo MÁXIMO que puedes inclinar la cámara arriba/abajo con el mouse (8 grados = rígido)")]
    [Range(0f, 25f)] public float limiteMouseVertical = 8f;

    [Tooltip("Velocidad con la que la cámara vuelve sola al centro cuando dejas de mover el mouse")]
    public float velocidadRetornoCentroMouse = 4.0f;

    // =========================================================================
    // 4. SISTEMA PARA ESQUIVAR EDIFICIOS (ANTI-ATRAVESAR PAREDES)
    // =========================================================================

    [Header("4. Colisión y Esquive de Edificios")]
    [Tooltip("Activa la detección de edificios para que la cámara los esquive y nunca los atraviese")]
    public bool esquivarEdificios = true;

    [Tooltip("Grosor físico de la esfera invisible que protege la cámara contra muros y edificios")]
    public float radioColisionCamara = 0.45f;

    [Tooltip("Margen extra de separación respecto a la pared para que no se vea el interior del edificio")]
    public float margenSeparacionPared = 0.35f;

    [Tooltip("Cuánto puede desplazarse lateralmente o hacia arriba la cámara para rodear la esquina de un edificio")]
    public float distanciaEsquiveLateral = 2.2f;

    [Tooltip("Distancia mínima a la que puede acercarse la cámara al coche cuando hay un muro pegado atrás")]
    public float distanciaMinimaAlJugador = 2.0f;

    [Tooltip("Capas que la cámara considera como edificios/obstáculos")]
    public LayerMask capasDeEdificios = ~0;

    // =========================================================================
    // VARIABLES INTERNAS
    // =========================================================================

    private Vector3 currentVelocity;
    private float anguloYawActual;
    private float offsetMouseX = 0f;
    private float offsetMouseY = 0f;
    private Vector3 desvioActualPorEdificio = Vector3.zero;
    private float distanciaActualSuavizada;

    void Awake()
    {
        // Desemparentar por si estaba dentro de Caballos
        if (transform.parent != null) transform.SetParent(null);

        // Ajustar el Near Clip Plane para que nunca corte paredes cercanas
        Camera cam = GetComponent<Camera>();
        if (cam != null && cam.nearClipPlane > 0.15f)
        {
            cam.nearClipPlane = 0.15f;
        }

        BuscarObjetivoAutomaticamente();
    }

    void Start()
    {
        BuscarObjetivoAutomaticamente();

        if (target == null)
        {
            Debug.LogError("[CameraTopDown] ¡ERROR! La cámara no tiene a quién seguir. Arrastra el objeto 'Caballos' a la casilla 'Target'.");
            return;
        }

        distanciaActualSuavizada = distanciaDetras;

        Vector3 dirAdelante = ObtenerDireccionAdelanteVehiculo();
        anguloYawActual = Quaternion.LookRotation(dirAdelante, Vector3.up).eulerAngles.y;

        ActualizarTransformDeCamara(true);
    }

    /// <summary>
    /// Busca automáticamente el objeto Caballos y su script CrazyCarriageController.
    /// </summary>
    private void BuscarObjetivoAutomaticamente()
    {
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            if (carriageController == null) carriageController = target.GetComponent<CrazyCarriageController>();
            return;
        }

        CrazyCarriageController nuevoCtrl = FindAnyObjectByType<CrazyCarriageController>();
        if (nuevoCtrl != null)
        {
            carriageController = nuevoCtrl;
            target = nuevoCtrl.transform;
            targetRb = target.GetComponent<Rigidbody>();
            return;
        }

        GameObject objCaballos = GameObject.Find("Caballos");
        if (objCaballos != null)
        {
            target = objCaballos.transform;
            targetRb = target.GetComponent<Rigidbody>();
            carriageController = target.GetComponent<CrazyCarriageController>();
            return;
        }

        GameObject objCarroza = GameObject.Find("Carroza");
        if (objCarroza != null)
        {
            target = objCarroza.transform;
            targetRb = target.GetComponent<Rigidbody>();
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            BuscarObjetivoAutomaticamente();
            if (target == null) return;
        }

        // Si el juego está pausado por diálogo o menú de Escape, no movemos la cámara con el mouse
        bool juegoEnPausa = GameManager.Instance != null &&
                            (GameManager.Instance.juegoDetenidoPorDialogo || GameManager.Instance.juegoPausadoPorEscape);

        if (!juegoEnPausa)
        {
            LeerMovimientoLimitadoDelMouse();
        }

        ActualizarTransformDeCamara(false);
    }

    /// <summary>
    /// Lee el movimiento del mouse pero lo limita rígidamente a pocos grados y lo devuelve suavemente al centro.
    /// </summary>
    private void LeerMovimientoLimitadoDelMouse()
    {
        if (!permitirMirarUnPocoConMouse)
        {
            offsetMouseX = 0f;
            offsetMouseY = 0f;
            return;
        }

        float deltaX = 0f;
        float deltaY = 0f;

        try
        {
            deltaX = Input.GetAxis("Mouse X") * sensibilidadMouse;
            deltaY = Input.GetAxis("Mouse Y") * sensibilidadMouse;
        }
        catch { }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 deltaMouse = Mouse.current.delta.ReadValue();
            if (deltaMouse.sqrMagnitude > 0.001f)
            {
                deltaX = deltaMouse.x * 0.08f * sensibilidadMouse;
                deltaY = deltaMouse.y * 0.08f * sensibilidadMouse;
            }
        }
#endif

        if (Mathf.Abs(deltaX) > 0.01f || Mathf.Abs(deltaY) > 0.01f)
        {
            offsetMouseX = Mathf.Clamp(offsetMouseX + deltaX, -limiteMouseHorizontal, limiteMouseHorizontal);
            offsetMouseY = Mathf.Clamp(offsetMouseY - deltaY, -limiteMouseVertical, limiteMouseVertical);
        }
        else
        {
            // Regresa suavemente al centro detrás del coche cuando sueltas el mouse
            offsetMouseX = Mathf.Lerp(offsetMouseX, 0f, velocidadRetornoCentroMouse * Time.deltaTime);
            offsetMouseY = Mathf.Lerp(offsetMouseY, 0f, velocidadRetornoCentroMouse * Time.deltaTime);
        }
    }

    /// <summary>
    /// Calcula la posición detrás de la espalda del jugador, esquiva edificios si hay colisión y orienta la cámara.
    /// </summary>
    private void ActualizarTransformDeCamara(bool instantaneo)
    {
        Vector3 posBaseVehiculo = ObtenerCentroVehiculo();
        Vector3 dirAdelante = ObtenerDireccionAdelanteVehiculo();

        // Ángulo hacia donde mira la espalda del vehículo
        float yawObjetivoVehiculo = Quaternion.LookRotation(dirAdelante, Vector3.up).eulerAngles.y;

        if (instantaneo)
        {
            anguloYawActual = yawObjetivoVehiculo;
        }
        else
        {
            // Seguimiento rígido detrás de la espalda del vehículo
            anguloYawActual = Mathf.LerpAngle(anguloYawActual, yawObjetivoVehiculo, rigidezRotacionEspalda * Time.deltaTime);
        }

        // Aplicar el pequeño ángulo permitido del mouse
        float yawFinal = anguloYawActual + offsetMouseX;
        Quaternion rotacionHorizontal = Quaternion.Euler(0f, yawFinal, 0f);
        Vector3 direccionMiradaPlana = rotacionHorizontal * Vector3.forward;

        // Punto central al que mira la cámara (ligeramente adelantado y elevado sobre el vehículo)
        Vector3 puntoDeMira = posBaseVehiculo + (direccionMiradaPlana * adelantoPuntoDeMira) + (Vector3.up * alturaPuntoDeMira);

        // Posición ideal detrás de la espalda del jugador
        float alturaConMouse = Mathf.Max(1.5f, alturaCamara + (offsetMouseY * 0.18f));
        Vector3 posicionIdealDetras = posBaseVehiculo - (direccionMiradaPlana * distanciaDetras) + (Vector3.up * alturaConMouse);

        // Verificar si hay un edificio entre el vehículo y la cámara para esquivarlo sin atravesarlo
        Vector3 posicionFinalSinEdificios = posicionIdealDetras;
        if (esquivarEdificios)
        {
            posicionFinalSinEdificios = CalcularPosicionEsquivandoEdificios(posBaseVehiculo + Vector3.up * alturaPuntoDeMira, posicionIdealDetras, rotacionHorizontal, instantaneo);
        }

        if (instantaneo || smoothTime <= 0.001f)
        {
            transform.position = posicionFinalSinEdificios;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, posicionFinalSinEdificios, ref currentVelocity, smoothTime);
        }

        // Mirar siempre hacia el frente del vehículo
        Vector3 direccionVista = puntoDeMira - transform.position;
        if (direccionVista.sqrMagnitude > 0.001f)
        {
            Quaternion rotacionVista = Quaternion.LookRotation(direccionVista.normalized, Vector3.up);
            transform.rotation = instantaneo
                ? rotacionVista
                : Quaternion.Slerp(transform.rotation, rotacionVista, 18f * Time.deltaTime);
        }
    }

    /// <summary>
    /// Detecta si un edificio o pared se interpone detrás del vehículo.
    /// Si hay un edificio, acerca la cámara por delante del muro y la desplaza lateralmente o hacia arriba para esquivarlo.
    /// </summary>
    private Vector3 CalcularPosicionEsquivandoEdificios(Vector3 origenPivoteJugador, Vector3 destinoDeseadoCamara, Quaternion rotacionCamara, bool instantaneo)
    {
        Vector3 direccionHaciaCamara = destinoDeseadoCamara - origenPivoteJugador;
        float distanciaTotalDeseada = direccionHaciaCamara.magnitude;

        if (distanciaTotalDeseada < 0.05f) return destinoDeseadoCamara;

        Vector3 dirNormalizada = direccionHaciaCamara / distanciaTotalDeseada;
        float distanciaSegura = distanciaTotalDeseada;
        Vector3 desvioObjetivo = Vector3.zero;

        // Lanzamos un SphereCast desde el jugador hacia atrás para detectar cualquier edificio antes de atravesarlo
        if (DetectarImpactoConEdificio(origenPivoteJugador, dirNormalizada, distanciaTotalDeseada, out RaycastHit impactoPrincipal))
        {
            // 1. Recortar la distancia para quedar delante de la pared del edificio
            distanciaSegura = Mathf.Max(distanciaMinimaAlJugador, impactoPrincipal.distance - margenSeparacionPared);

            // 2. Probar desvíos hacia la derecha, izquierda o arriba para esquivar la esquina del edificio suavemente
            Vector3 derecha = rotacionCamara * Vector3.right;
            Vector3 arriba = Vector3.up;

            Vector3[] direccionesDeEsquive = new Vector3[]
            {
                derecha * distanciaEsquiveLateral,
                -derecha * distanciaEsquiveLateral,
                arriba * (distanciaEsquiveLateral * 0.9f),
                (derecha + arriba * 0.5f).normalized * distanciaEsquiveLateral,
                (-derecha + arriba * 0.5f).normalized * distanciaEsquiveLateral
            };

            float mejorEspacioLibre = impactoPrincipal.distance;

            foreach (Vector3 intentoDesvio in direccionesDeEsquive)
            {
                Vector3 candidatoDestino = destinoDeseadoCamara + intentoDesvio;
                Vector3 dirCandidata = (candidatoDestino - origenPivoteJugador);
                float distCandidata = dirCandidata.magnitude;

                if (!DetectarImpactoConEdificio(origenPivoteJugador, dirCandidata.normalized, distCandidata, out RaycastHit impactoCandidato))
                {
                    // Este lado está totalmente libre del edificio: esquivamos hacia allá
                    desvioObjetivo = intentoDesvio;
                    distanciaSegura = distanciaTotalDeseada;
                    break;
                }
                else if (impactoCandidato.distance > mejorEspacioLibre + 0.5f)
                {
                    mejorEspacioLibre = impactoCandidato.distance;
                    desvioObjetivo = intentoDesvio * 0.6f;
                    distanciaSegura = Mathf.Max(distanciaMinimaAlJugador, impactoCandidato.distance - margenSeparacionPared);
                }
            }
        }

        if (instantaneo)
        {
            distanciaActualSuavizada = distanciaSegura;
            desvioActualPorEdificio = desvioObjetivo;
        }
        else
        {
            // Se acerca rápido si aparece un muro de golpe, y vuelve suave cuando ya pasó el edificio
            float velDistancia = (distanciaSegura < distanciaActualSuavizada) ? 22f : 7f;
            distanciaActualSuavizada = Mathf.Lerp(distanciaActualSuavizada, distanciaSegura, velDistancia * Time.deltaTime);
            desvioActualPorEdificio = Vector3.Lerp(desvioActualPorEdificio, desvioObjetivo, 8f * Time.deltaTime);
        }

        Vector3 posicionEsquivada = origenPivoteJugador + (dirNormalizada * distanciaActualSuavizada) + desvioActualPorEdificio;

        // Evitar que la cámara baje del nivel del suelo
        float alturaMinimaSuelo = origenPivoteJugador.y - 0.5f;
        if (posicionEsquivada.y < alturaMinimaSuelo)
        {
            posicionEsquivada.y = alturaMinimaSuelo;
        }

        return posicionEsquivada;
    }

    /// <summary>
    /// Lanza un SphereCast ignorando los colliders propios de Caballos, Carroza y Pasajeros.
    /// </summary>
    private bool DetectarImpactoConEdificio(Vector3 origen, Vector3 direccion, float distanciaMaxima, out RaycastHit impactoMasCercano)
    {
        impactoMasCercano = default;
        RaycastHit[] impactos = Physics.SphereCastAll(origen, radioColisionCamara, direccion, distanciaMaxima, capasDeEdificios, QueryTriggerInteraction.Ignore);

        if (impactos == null || impactos.Length == 0) return false;

        float menorDistancia = float.MaxValue;
        bool encontroEdificio = false;

        foreach (RaycastHit hit in impactos)
        {
            if (hit.collider == null) continue;

            // Ignorar al propio jugador (Caballos, Carroza) y a los pasajeros
            if (EsColliderDelJugadorOPasajero(hit.collider.transform)) continue;

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                impactoMasCercano = hit;
                encontroEdificio = true;
            }
        }

        return encontroEdificio;
    }

    /// <summary>
    /// Verifica si un Transform pertenece a Caballos, Carroza o un Pasajero para que la cámara no choque con ellos.
    /// </summary>
    private bool EsColliderDelJugadorOPasajero(Transform t)
    {
        if (t == null) return false;
        if (target != null && (t == target || t.IsChildOf(target))) return true;
        if (carriageController != null && carriageController.carrozaTransform != null &&
            (t == carriageController.carrozaTransform || t.IsChildOf(carriageController.carrozaTransform)))
            return true;

        string nombre = t.name.ToLowerInvariant();
        if (nombre.Contains("caballo") || nombre.Contains("carroza") || nombre.Contains("pasajero"))
            return true;

        return false;
    }

    /// <summary>
    /// Obtiene la posición central entre los caballos y la carroza para que ambos se vean bien desde atrás.
    /// </summary>
    private Vector3 ObtenerCentroVehiculo()
    {
        Vector3 posCaballos = (targetRb != null) ? targetRb.position : target.position;

        if (carriageController != null && carriageController.carrozaTransform != null)
        {
            return Vector3.Lerp(posCaballos, carriageController.carrozaTransform.position, 0.45f);
        }

        return posCaballos;
    }

    /// <summary>
    /// Obtiene la dirección real hacia adelante del vehículo usando CrazyCarriageController.
    /// </summary>
    private Vector3 ObtenerDireccionAdelanteVehiculo()
    {
        if (carriageController != null)
        {
            Vector3 dir = carriageController.GetWorldForward();
            if (dir.sqrMagnitude > 0.001f) return dir.normalized;
        }

        Vector3 forwardPlano = target.forward;
        forwardPlano.y = 0f;
        return forwardPlano.sqrMagnitude > 0.001f ? forwardPlano.normalized : Vector3.forward;
    }
}
