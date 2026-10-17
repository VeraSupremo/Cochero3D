using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CrazyCarriageController : MonoBehaviour
{
    public enum MovementStage { Detenido, Lento, Medio, GalopeRapido }

    [Header("Referencias del Carruaje")]
    [Tooltip("Punto trasero de los caballos donde se engancha la carroza")]
    public Transform hitchPoint;
    [Tooltip("El objeto visual de la Carroza que seguirá a los caballos")]
    public Transform carrozaTransform;
    [Tooltip("Distancia entre el Hitchpoint y el centro de la carroza")]
    public float hitchDistance = 3.0f;
    [Tooltip("Qué tan rápido se alinea la carroza al doblar (menor = más derrape visual)")]
    public float carriageFollowSpeed = 8.0f;

    [Header("Progresión y Aceleración (Espacio)")]
    public float inputWindowTime = 1.2f;
    public float maxSprintDuration = 3.0f;
    [Tooltip("Cantidad máxima de veces que puedes activar el Galope Rápido")]
    public int maxSprintUses = 3;
    public int remainingSprintUses;

    [Header("Velocidades por Etapa")]
    public float slowSpeed = 6f;
    public float mediumSpeed = 13f;
    public float fastSpeed = 22f;
    public float accelerationRate = 12f;
    public float decelerationRate = 6f;

    [Header("Giro estilo Arcade")]
    public float turnSpeed = 90f;
    [Range(0.1f, 1f)] public float highSpeedTurnFactor = 0.5f;

    [Header("Estado Actual (Solo Lectura)")]
    public MovementStage currentStage = MovementStage.Detenido;
    public float currentSpeed;

    private Rigidbody rb;
    private float targetMaxSpeed;
    private float lastInputTime;
    private float sprintTimer;
    private int spacePressCount;
    private float turnInput;

    // Guardamos la rotación y posición previa de la carroza para que articule como remolque real
    private Vector3 currentCarriagePos;
    private Quaternion currentCarriageRot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezeRotationY;

        remainingSprintUses = maxSprintUses;
    }

    [Header("Ajustes de Alineación (Por si el modelo viene rotado)")]
    [Tooltip("Si la carroza mira de lado al dar Play, pon 90, -90 o 180 aquí")]
    public float carriageYawOffset = 0f;
    [Tooltip("Ajuste lateral fino (dejar en 0 para estar justo detrás)")]
    public float lateralOffset = 0f;

    void Start()
    {
        SetStage(MovementStage.Detenido);

        if (hitchPoint != null && carrozaTransform != null)
        {
            // 1. Forzamos a que el Hitchpoint esté centrado localmente en X si se movió por error
            // (Opcional: quita esta línea si quieres usar lateralOffset manual desde el Hitchpoint)
            Vector3 localHitch = hitchPoint.localPosition;
            localHitch.x = lateralOffset;
            hitchPoint.localPosition = localHitch;

            // 2. Desemparentar la carroza para que articule libremente
            carrozaTransform.SetParent(null);

            // 3. Colocarla exactamente detrás usando la dirección de los caballos
            currentCarriageRot = transform.rotation;
            currentCarriagePos = hitchPoint.position - (transform.forward * hitchDistance);
            currentCarriagePos.y = hitchPoint.position.y;

            carrozaTransform.position = currentCarriagePos;
            carrozaTransform.rotation = currentCarriageRot * Quaternion.Euler(0f, carriageYawOffset, 0f);
        }
    }

    void Update()
    {
        // 1. Lectura de giro
        turnInput = Input.GetAxis("Horizontal");

        // 2. Lectura de barra espaciadora para acelerar
        if (Input.GetKeyDown(KeyCode.Space))
        {
            spacePressCount++;
            lastInputTime = Time.time;
            EvaluateStageProgression();
        }

        // 3. Control de tiempos y decaimiento de velocidad
        HandleTimers();
    }

    void FixedUpdate()
    {
        HandleMovementAndSteering();
        UpdateCarriageTrailer();
    }

    private void HandleMovementAndSteering()
    {
        // Calcular velocidad actual en plano horizontal
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        currentSpeed = flatVel.magnitude;

        // Acelerar o desacelerar suavemente hacia la velocidad de la etapa actual
        float rate = (currentSpeed < targetMaxSpeed) ? accelerationRate : decelerationRate;
        float newSpeed = Mathf.MoveTowards(currentSpeed, targetMaxSpeed, rate * Time.fixedDeltaTime);

        // Giro más ágil (solo gira si nos estamos moviendo un poco, o si quieres girar en el lugar quita el if)
        if (newSpeed > 0.1f && Mathf.Abs(turnInput) > 0.01f)
        {
            float speedPercent = Mathf.Clamp01(newSpeed / fastSpeed);
            float turnMod = Mathf.Lerp(1f, highSpeedTurnFactor, speedPercent);
            float turnAngle = turnInput * turnSpeed * turnMod * Time.fixedDeltaTime;

            Quaternion turnRotation = Quaternion.Euler(0f, turnAngle, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }

        // Aplicar velocidad directamente en la dirección a la que miran los caballos (conducción tipo Crazy Taxi)
        Vector3 targetVelocity = transform.forward * newSpeed;
        targetVelocity.y = rb.linearVelocity.y; // Respetar la gravedad
        rb.linearVelocity = targetVelocity;
    }

   private void UpdateCarriageTrailer()
    {
        if (hitchPoint == null || carrozaTransform == null) return;

        // Dirección desde la carroza hacia el punto de enganche
        Vector3 dirToHitch = hitchPoint.position - currentCarriagePos;
        dirToHitch.y = 0f;

        if (dirToHitch.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToHitch.normalized, Vector3.up);
            currentCarriageRot = Quaternion.Slerp(currentCarriageRot, targetRot, carriageFollowSpeed * Time.fixedDeltaTime);
        }

        // Mantener la carroza exactamente a 'hitchDistance' detrás del enganche
        currentCarriagePos = hitchPoint.position - (currentCarriageRot * Vector3.forward * hitchDistance);
        currentCarriagePos.y = hitchPoint.position.y;

        // Aplicar posición y rotación (incluyendo la corrección si el modelo 3D viene girado)
        Quaternion finalVisualRot = currentCarriageRot * Quaternion.Euler(0f, carriageYawOffset, 0f);
        carrozaTransform.SetPositionAndRotation(currentCarriagePos, finalVisualRot);
    }

    private void EvaluateStageProgression()
    {
        // Pasar a Galope Rápido (solo si quedan usos disponibles)
        if (spacePressCount >= 12 && currentStage != MovementStage.GalopeRapido)
        {
            if (remainingSprintUses > 0)
            {
                remainingSprintUses--;
                SetStage(MovementStage.GalopeRapido);
            }
            else
            {
                // Si no quedan usos de máxima velocidad, nos quedamos en Medio como tope
                spacePressCount = 10;
                if (currentStage != MovementStage.Medio)
                    SetStage(MovementStage.Medio);
            }
        }
        else if (spacePressCount >= 5 && currentStage == MovementStage.Lento)
        {
            SetStage(MovementStage.Medio);
        }
        else if (spacePressCount >= 1 && currentStage == MovementStage.Detenido)
        {
            SetStage(MovementStage.Lento);
        }
    }

    private void HandleTimers()
    {
        // Si deja de presionar espacio, baja de etapa progresivamente
        if (Time.time - lastInputTime > inputWindowTime && currentStage != MovementStage.Detenido)
        {
            spacePressCount = Mathf.Max(0, spacePressCount - 3);

            if (currentStage == MovementStage.GalopeRapido)
                SetStage(MovementStage.Medio);
            else if (currentStage == MovementStage.Medio && spacePressCount < 5)
                SetStage(MovementStage.Lento);
            else if (currentStage == MovementStage.Lento && spacePressCount == 0)
                SetStage(MovementStage.Detenido);

            lastInputTime = Time.time;
        }

        // Duración máxima del Sprint (Galope Rápido)
        if (currentStage == MovementStage.GalopeRapido)
        {
            sprintTimer += Time.deltaTime;
            if (sprintTimer >= maxSprintDuration)
            {
                spacePressCount = 6; // Bajar al rango de velocidad media
                SetStage(MovementStage.Medio);
            }
        }
    }

    private void SetStage(MovementStage newStage)
    {
        currentStage = newStage;
        sprintTimer = 0f;

        switch (currentStage)
        {
            case MovementStage.Detenido:
                targetMaxSpeed = 0f;
                break;
            case MovementStage.Lento:
                targetMaxSpeed = slowSpeed;
                break;
            case MovementStage.Medio:
                targetMaxSpeed = mediumSpeed;
                break;
            case MovementStage.GalopeRapido:
                targetMaxSpeed = fastSpeed;
                break;
        }
    }










// Esto dibuja una línea amarilla y una esfera en la ventana Scene para que veas dónde quedará la carroza
    private void OnDrawGizmos()
    {
        if (hitchPoint != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 previewPos = hitchPoint.position - (transform.forward * hitchDistance);
            Gizmos.DrawLine(hitchPoint.position, previewPos);
            Gizmos.DrawWireSphere(previewPos, 0.4f);
        }
    }






}