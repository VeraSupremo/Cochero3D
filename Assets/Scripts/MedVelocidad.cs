using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Script para el Medidor de Velocidad visual de 3 Fases (Verde -> Amarillo -> Rojo MAX).
/// Funciona con 2 planos (UI -> Image) dentro de un Canvas (igual que el minimapa):
/// - Plano 1 (Fondo): La imagen fija con la barra de 3 fases (Verde, Amarillo y Rojo MAX).
/// - Plano 2 (Varilla): La imagen de la varilla/flecha con el caballito que se mueve según la fase o velocidad del protagonista.
/// </summary>
public class MedVelocidad : MonoBehaviour
{
    public enum ModoCalculoVelocidad
    {
        PorEstadosDeFase,     // Se posiciona según las 3 etapas (Lento = Verde, Medio = Amarillo, GalopeRapido = Rojo MAX)
        PorVelocidadContinua  // Se desliza continuamente de izquierda a derecha según la velocidad actual (0 a fastSpeed)
    }

    // =========================================================================
    // 1. REFERENCIA AL PROTAGONISTA (AUTO-DETECTADA)
    // =========================================================================

    [Header("1. Referencia al Protagonista (Caballos)")]
    [Tooltip("Arrastra aquí el objeto Caballos. Si lo dejas vacío, el script lo busca automáticamente al dar Play.")]
    public CrazyCarriageController protagonistaController;

    [Tooltip("Elige si la varilla se mueve por los Estados de Fase (Verde/Amarillo/Rojo MAX) o por la Velocidad Continua")]
    public ModoCalculoVelocidad modoDeFuncionamiento = ModoCalculoVelocidad.PorEstadosDeFase;

    // =========================================================================
    // 2. LOS 2 PLANOS (UI IMAGES) DENTRO DEL CANVAS
    // =========================================================================

    [Header("2. Los 2 Planos en el Canvas (Igual que el Minimapa)")]
    [Tooltip("PLANO 1 (Fondo Fijo): Arrastra aquí el objeto Image del Canvas que tiene la foto de la barra (Verde, Amarillo, Rojo MAX)")]
    public RectTransform planoFondoBarra;

    [Tooltip("PLANO 2 (Varilla Móvil): Arrastra aquí el objeto Image del Canvas que tiene la foto de la varilla/flecha que se mueve")]
    public RectTransform planoVarillaIndicadora;

    [Header("Sprites Opcionales (Si los arrastras aquí se colocan solos en los 2 planos)")]
    [Tooltip("Foto/Sprite de la barra con las 3 fases (Verde, Amarillo, Rojo MAX)")]
    public Sprite fotoBarraTresFases;

    [Tooltip("Foto/Sprite de la varilla / aguja con el caballito que marca la velocidad")]
    public Sprite fotoVarillaIndicadora;

    // =========================================================================
    // 3. POSICIONES DE LA VARILLA EN LAS 3 FASES (EJE X EN EL CANVAS)
    // =========================================================================

    [Header("3. Posiciones Horizontales (X) de la Varilla en cada Fase")]
    [Tooltip("Posición X de la varilla cuando la carroza está totalmente Detenida (borde izquierdo del Verde)")]
    public float posXDetenido = -115f;

    [Tooltip("FASE 1 (VERDE - Velocidad Baja/Lento): Posición X de la varilla en la zona Verde")]
    public float posXFase1Verde = -65f;

    [Tooltip("FASE 2 (AMARILLO - Velocidad Media): Posición X de la varilla en la zona Amarilla central")]
    public float posXFase2Amarillo = 10f;

    [Tooltip("FASE 3 (ROJO MAX - Velocidad Alta/Galope Rápido): Posición X de la varilla en la zona Roja MAX")]
    public float posXFase3RojoMax = 95f;

    [Tooltip("Qué tan rápido y suave se mueve la varilla de una fase a otra")]
    public float velocidadSuavizadoVarilla = 7f;

    // =========================================================================
    // 4. AJUSTE DE CURVA E INCLINACION DE LA VARILLA (SEGUN TU DIBUJO)
    // =========================================================================

    [Header("4. Efecto de Curva e Inclinación de la Varilla (Opcional)")]
    [Tooltip("Altura base (Y) de la varilla respecto al plano de la barra")]
    public float posYBaseVarilla = 0f;

    [Tooltip("Si está activo, la varilla sube un poco en el centro (Amarillo) y se inclina siguiendo el arco de tu dibujo")]
    public bool seguirCurvaDeLaBarra = true;

    [Tooltip("Cuánto sube en el eje Y la varilla cuando pasa por el centro (zona Amarilla)")]
    public float alturaExtraEnCentroCurva = 14f;

    [Tooltip("Ángulo de inclinación (Z) de la varilla cuando está a la izquierda (Verde)")]
    public float anguloIzquierdaVerde = 16f;

    [Tooltip("Ángulo de inclinación (Z) de la varilla cuando llega a la derecha (Rojo MAX)")]
    public float anguloDerechaRojoMax = -16f;

    // =========================================================================
    // ESTADO EN TIEMPO REAL (SOLO LECTURA)
    // =========================================================================

    [Header("Estado Actual del Medidor (Solo Lectura)")]
    [Range(0f, 1f)] public float porcentajeActualMedidor = 0f;
    public CrazyCarriageController.MovementStage faseActualProtagonista;

    private float posXActual;

    private void Awake()
    {
        AutoDetectarPlanosEnCanvas();
        AutoDetectarProtagonista();
        AplicarSpritesSiFueronAsignados();
    }

    private void Start()
    {
        AutoDetectarPlanosEnCanvas();
        AutoDetectarProtagonista();
        posXActual = posXDetenido;
        ActualizarPosicionYRotacionDeVarilla(true);
    }

    /// <summary>
    /// Si pusiste el script MedVelocidad en el Plano 1 (Fondo) y el Plano 2 (Varilla) es su hijo,
    /// los detecta automáticamente aunque olvides arrastrarlos en el Inspector.
    /// </summary>
    private void AutoDetectarPlanosEnCanvas()
    {
        if (planoFondoBarra == null)
        {
            planoFondoBarra = GetComponent<RectTransform>();
        }

        if (planoVarillaIndicadora == null && transform.childCount > 0)
        {
            planoVarillaIndicadora = transform.GetChild(0).GetComponent<RectTransform>();
        }
    }

    private void Update()
    {
        if (protagonistaController == null)
        {
            AutoDetectarProtagonista();
            if (protagonistaController == null) return;
        }

        if (planoVarillaIndicadora == null) return;

        ActualizarPosicionYRotacionDeVarilla(false);
    }

    /// <summary>
    /// Busca automáticamente el script CrazyCarriageController (Caballos) en la escena si no fue arrastrado.
    /// </summary>
    private void AutoDetectarProtagonista()
    {
        if (protagonistaController == null)
        {
            protagonistaController = FindAnyObjectByType<CrazyCarriageController>();
        }
    }

    /// <summary>
    /// Si arrastraste las fotos directamente a este script, se las asigna a los componentes Image de los 2 planos.
    /// </summary>
    private void AplicarSpritesSiFueronAsignados()
    {
        if (planoFondoBarra != null && fotoBarraTresFases != null)
        {
            Image imgFondo = planoFondoBarra.GetComponent<Image>();
            if (imgFondo != null) imgFondo.sprite = fotoBarraTresFases;
        }

        if (planoVarillaIndicadora != null && fotoVarillaIndicadora != null)
        {
            Image imgVarilla = planoVarillaIndicadora.GetComponent<Image>();
            if (imgVarilla != null) imgVarilla.sprite = fotoVarillaIndicadora;
        }
    }

    /// <summary>
    /// Calcula en qué punto de las 3 fases (Verde, Amarillo o Rojo MAX) debe estar la varilla
    /// y actualiza su posición y rotación dentro del Canvas.
    /// </summary>
    private void ActualizarPosicionYRotacionDeVarilla(bool instantaneo)
    {
        faseActualProtagonista = protagonistaController.currentStage;
        float posXObjetivo = posXDetenido;

        if (modoDeFuncionamiento == ModoCalculoVelocidad.PorEstadosDeFase)
        {
            // MODO 1: Basado directamente en las 3 Fases de movimiento del protagonista
            switch (faseActualProtagonista)
            {
                case CrazyCarriageController.MovementStage.Detenido:
                    posXObjetivo = posXDetenido;
                    break;

                case CrazyCarriageController.MovementStage.Lento:
                    // FASE 1: Zona Verde
                    posXObjetivo = posXFase1Verde;
                    break;

                case CrazyCarriageController.MovementStage.Medio:
                    // FASE 2: Zona Amarilla
                    posXObjetivo = posXFase2Amarillo;
                    break;

                case CrazyCarriageController.MovementStage.GalopeRapido:
                    // FASE 3: Zona Roja MAX
                    posXObjetivo = posXFase3RojoMax;
                    break;
            }
        }
        else
        {
            // MODO 2: Basado en la velocidad continua (currentSpeed respecto a fastSpeed)
            float velMax = Mathf.Max(1f, protagonistaController.fastSpeed);
            float tVelocidad = Mathf.Clamp01(protagonistaController.currentSpeed / velMax);
            posXObjetivo = Mathf.Lerp(posXDetenido, posXFase3RojoMax, tVelocidad);
        }

        // Mover suavemente la varilla hacia la posición objetivo
        if (instantaneo)
        {
            posXActual = posXObjetivo;
        }
        else
        {
            posXActual = Mathf.Lerp(posXActual, posXObjetivo, velocidadSuavizadoVarilla * Time.deltaTime);
        }

        // Calcular qué porcentaje (0 a 1) lleva recorrido la varilla de izquierda (Verde) a derecha (Rojo MAX)
        porcentajeActualMedidor = Mathf.InverseLerp(posXDetenido, posXFase3RojoMax, posXActual);

        float posYFinal = posYBaseVarilla;
        float anguloZFinal = 0f;

        // Si está activado el efecto de curva (como el arco del dibujo), ajusta la altura Y y la inclinación Z
        if (seguirCurvaDeLaBarra)
        {
            // Parábola suave que vale 0 en los extremos (izquierda/derecha) y 1 en el centro (Amarillo)
            float factorArcoCentro = 4f * porcentajeActualMedidor * (1f - porcentajeActualMedidor);
            posYFinal += factorArcoCentro * alturaExtraEnCentroCurva;

            // Inclina la varilla desde el ángulo izquierdo (Verde) hasta el ángulo derecho (Rojo MAX)
            anguloZFinal = Mathf.Lerp(anguloIzquierdaVerde, anguloDerechaRojoMax, porcentajeActualMedidor);
        }

        planoVarillaIndicadora.anchoredPosition = new Vector2(posXActual, posYFinal);
        planoVarillaIndicadora.localRotation = Quaternion.Euler(0f, 0f, anguloZFinal);
    }
}
