using UnityEngine;

public class CameraTopDown : MonoBehaviour
{
    [Header("Objetivo a Seguir")]
    [Tooltip("Arrastra aquí el objeto Caballos (si lo dejas vacío, lo busca automáticamente)")]
    public Transform target;
    private Rigidbody targetRb;

    [Header("Posición y Altura (Ejes X / Y / Z)")]
    [Tooltip("Altura fija de la cámara sobre el mapa (Eje Y)")]
    public float cameraHeight = 22f;
    [Tooltip("Desplazamiento en X y Z respecto al jugador (Pon 0, 0 para estar exactamente encima, o Z negativo si la cámara tiene algo de inclinación)")]
    public Vector2 xzOffset = new Vector2(0f, -4f);
    [Tooltip("Si está activo, usa la posición y rotación que le hayas puesto manualmente en la escena al dar Play")]
    public bool mantenerOffsetInicialDeEscena = false;

    [Header("Rotación Fija (No rota con el personaje)")]
    [Tooltip("Ángulo fijo de la cámara. 90 en X = totalmente vertical (GTA 1/GBA); 70 en X = ligeramente inclinada")]
    public Vector3 fixedRotationAngles = new Vector3(75f, 0f, 0f);

    [Header("Suavizado de Seguimiento")]
    [Tooltip("Tiempo de suavizado (0 = pegada rígidamente al personaje; 0.12 = seguimiento suave de cine)")]
    [Range(0f, 0.5f)] public float smoothTime = 0.10f;

    [Header("Efecto GTA GBA: Adelantar Vista al Correr (Opcional)")]
    [Tooltip("Cuánto se adelanta la cámara en la dirección de avance para ver mejor las calles (Pon 0 para desactivar)")]
    public float lookAheadFactor = 0.25f;
    [Tooltip("Distancia máxima que puede adelantarse la cámara")]
    public float maxLookAheadDistance = 5f;
    [Tooltip("Cuánto sube la cámara (zoom out) cuando vas a máxima velocidad (Pon 0 para altura 100% fija)")]
    public float speedZoomOutExtraHeight = 4f;

    private Vector3 currentVelocity;
    private Vector3 currentLookAhead;
    private Quaternion lockedRotation;

    void Awake()
    {
        // Si por error pusiste la cámara como hija de Caballos, la sacamos para que no rote con él
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        AutoFindTarget();
    }

    void Start()
    {
        AutoFindTarget();

        if (maintainsInitialSetup()) return;

        // Fijar rotación inicial bloqueada
        lockedRotation = Quaternion.Euler(fixedRotationAngles);
        transform.rotation = lockedRotation;

        // Colocar la cámara instantáneamente sobre el jugador al arrancar
        if (target != null)
        {
            transform.position = CalcularPosicionObjetivo(0f);
        }
    }

    private bool maintainsInitialSetup()
    {
        if (!mantenerOffsetInicialDeEscena || target == null) return false;

        cameraHeight = transform.position.y;
        xzOffset = new Vector2(transform.position.x - target.position.x, transform.position.z - target.position.z);
        fixedRotationAngles = transform.eulerAngles;
        lockedRotation = transform.rotation;
        return true;
    }

    private void AutoFindTarget()
    {
        if (target == null)
        {
            CrazyCarriageController carriage = FindFirstObjectByType<CrazyCarriageController>();
            if (carriage != null)
            {
                target = carriage.transform;
            }
        }

        if (target != null && targetRb == null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Mantener la rotación 100% bloqueada (NUNCA gira aunque los caballos den vueltas)
        transform.rotation = Quaternion.Euler(fixedRotationAngles);

        // 2. Calcular la posición deseada en X y Z (más altura en Y)
        Vector3 desiredPosition = CalcularPosicionObjetivo(Time.deltaTime);

        // 3. Mover la cámara suavemente hacia esa posición
        if (smoothTime <= 0.001f)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
        }
    }

    private Vector3 CalcularPosicionObjetivo(float deltaTime)
    {
        Vector3 flatVelocity = Vector3.zero;
        if (targetRb != null)
        {
            flatVelocity = new Vector3(targetRb.linearVelocity.x, 0f, targetRb.linearVelocity.z);
        }

        // Calcular adelantamiento de cámara según hacia dónde se mueve la carroza
        Vector3 targetLookAhead = Vector3.ClampMagnitude(flatVelocity * lookAheadFactor, maxLookAheadDistance);
        if (deltaTime > 0f)
        {
            currentLookAhead = Vector3.Lerp(currentLookAhead, targetLookAhead, 4f * deltaTime);
        }
        else
        {
            currentLookAhead = targetLookAhead;
        }

        // Calcular zoom out suave según velocidad
        float speedPercent = Mathf.Clamp01(flatVelocity.magnitude / 22f);
        float dynamicHeight = cameraHeight + (speedPercent * speedZoomOutExtraHeight);

        // Posición final solo en X/Z siguiendo al jugador + altura Y
        return new Vector3(
            target.position.x + xzOffset.x + currentLookAhead.x,
            dynamicHeight,
            target.position.z + xzOffset.y + currentLookAhead.z
        );
    }
}