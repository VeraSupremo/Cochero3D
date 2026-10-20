using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class CrazyCarriageController : MonoBehaviour
{
    public enum MovementStage { Detenido, Lento, Medio, GalopeRapido }

    [Header("Referencias del Carruaje (Se auto-detectan si están vacías)")]
    public Transform hitchPoint;
    public Transform carrozaTransform;
    [Tooltip("Distancia entre los caballos y la carroza")]
    public float hitchDistance = 2.8f;
    [Tooltip("Velocidad con la que la carroza se alinea al giro de los caballos (estilo GTA GBA)")]
    public float carriageFollowSpeed = 6.0f;

    [Header("Progresión y Aceleración (Espacio)")]
    public float inputWindowTime = 1.5f;
    public float maxSprintDuration = 3.0f;
    public int maxSprintUses = 3;
    public int remainingSprintUses;

    [Header("Velocidades por Etapa")]
    public float slowSpeed = 6f;
    public float mediumSpeed = 13f;
    public float fastSpeed = 22f;
    public float accelerationRate = 15f;
    public float decelerationRate = 8f;

    [Header("Giro estilo GTA GBA")]
    public float turnSpeed = 120f;
    [Range(0.2f, 1f)] public float highSpeedTurnFactor = 0.65f;

    [Header("Estado Actual (Solo Lectura)")]
    public MovementStage currentStage = MovementStage.Detenido;
    public float currentSpeed;
    public int spacePressCount;

    private Rigidbody rb;
    private float targetMaxSpeed;
    private float lastInputTime;
    private float sprintTimer;
    private float turnInput;

    private Vector3 localForwardAxis = Vector3.forward;
    private Quaternion currentCarriageRot;
    private Quaternion carriageRotationOffset = Quaternion.identity;
    private float initialY;
    private float initialCarriageY;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // En estilo GTA GBA evitamos que caiga o se hunda en el plano
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        remainingSprintUses = maxSprintUses;
        AutoFindReferences();
    }

    // Busca automáticamente "Carroza" y "Hitchpoint" en la escena si no los arrastraste en el Inspector
    private void AutoFindReferences()
    {
        if (hitchPoint == null)
        {
            Transform foundHitch = transform.Find("Hitchpoint");
            hitchPoint = (foundHitch != null) ? foundHitch : transform;
        }

        if (carrozaTransform == null)
        {
            GameObject foundCarroza = GameObject.Find("Carroza");
            if (foundCarroza != null) carrozaTransform = foundCarroza.transform;
        }
    }

    void Start()
    {
        AutoFindReferences();
        SetStage(MovementStage.Detenido);

        initialY = transform.position.y;

        if (carrozaTransform != null)
        {
            initialCarriageY = carrozaTransform.position.y;

            // Detecta en qué dirección pusiste la carroza en la escena para saber cuál es "Atrás" y "Adelante"
            Vector3 toCarriage = carrozaTransform.position - transform.position;
            toCarriage.y = 0f;

            if (toCarriage.magnitude > 0.1f)
            {
                hitchDistance = toCarriage.magnitude;
                Vector3 worldForward = -toCarriage.normalized;
                localForwardAxis = transform.InverseTransformDirection(worldForward);
            }

            Vector3 forwardWorld = GetWorldForward();
            Quaternion lookRot = Quaternion.LookRotation(forwardWorld, Vector3.up);
            carriageRotationOffset = Quaternion.Inverse(lookRot) * carrozaTransform.rotation;

            // Asegurarnos de que el Hitchpoint esté un poco hacia la cola de los caballos para que al girar haga palanca
            if (hitchPoint != transform && hitchPoint.localPosition.sqrMagnitude < 0.05f)
            {
                hitchPoint.localPosition = -localForwardAxis * 0.6f;
            }

            carrozaTransform.SetParent(null);
            currentCarriageRot = lookRot;
            UpdateCarriageTrailer(true);
        }
    }

    private Vector3 GetWorldForward()
    {
        Vector3 dir = transform.TransformDirection(localForwardAxis);
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
    }

    void Update()
    {
        bool spacePressed = false;
        turnInput = 0f;

        // Lee Input clásico
        try
        {
            if (Input.GetKeyDown(KeyCode.Space)) spacePressed = true;
            turnInput = Input.GetAxisRaw("Horizontal");
        }
        catch { }

        // Y también lee el New Input System por si está activo en Unity 6
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame) spacePressed = true;
            float newTurn = 0f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) newTurn -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) newTurn += 1f;
            if (Mathf.Abs(newTurn) > 0.01f) turnInput = newTurn;
        }
#endif

        if (spacePressed)
        {
            spacePressCount++;
            lastInputTime = Time.time;
            EvaluateStageProgression();
        }

        HandleTimers();
    }

    void FixedUpdate()
    {
        HandleMovementAndSteering();
        UpdateCarriageTrailer(false);
    }

    private void HandleMovementAndSteering()
    {
        // Avanzar o frenar hacia la velocidad objetivo
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetMaxSpeed, 
            ((currentSpeed < targetMaxSpeed) ? accelerationRate : decelerationRate) * Time.fixedDeltaTime);

        // Girar los caballos con A/D o Flechas
        if (Mathf.Abs(turnInput) > 0.01f)
        {
            float speedPercent = Mathf.Clamp01(currentSpeed / fastSpeed);
            float turnMod = Mathf.Lerp(1f, highSpeedTurnFactor, speedPercent);
            float turnAngle = turnInput * turnSpeed * turnMod * Time.fixedDeltaTime;

            Quaternion turnRotation = Quaternion.Euler(0f, turnAngle, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }

        // Mover en la dirección a la que miran los caballos
        Vector3 forwardDir = GetWorldForward();
        rb.linearVelocity = new Vector3(forwardDir.x * currentSpeed, 0f, forwardDir.z * currentSpeed);
    }

    private void UpdateCarriageTrailer(bool instant)
    {
        if (carrozaTransform == null) return;

        Vector3 horseForward = GetWorldForward();
        Quaternion horseLookRot = Quaternion.LookRotation(horseForward, Vector3.up);

        // Punto de enganche detrás de los caballos
        Vector3 anchorPos = (hitchPoint != null && hitchPoint != transform) 
            ? hitchPoint.position 
            : transform.position - (horseForward * 0.5f);

        if (instant)
        {
            currentCarriageRot = horseLookRot;
        }
        else
        {
            // Combinamos el tirón por desplazamiento (cuando avanza) + alineación angular (cuando gira en el lugar)
            Vector3 dirToHitch = anchorPos - carrozaTransform.position;
            dirToHitch.y = 0f;

            Quaternion pullRot = (dirToHitch.sqrMagnitude > 0.001f) 
                ? Quaternion.LookRotation(dirToHitch.normalized, Vector3.up) 
                : horseLookRot;

            // Si va rápido, domina el efecto remolque físico; si gira lento o quieto, sigue el giro de los caballos
            float moveBlend = Mathf.Clamp01(currentSpeed / slowSpeed);
            Quaternion targetRot = Quaternion.Slerp(horseLookRot, pullRot, moveBlend * 0.7f);

            currentCarriageRot = Quaternion.Slerp(currentCarriageRot, targetRot, carriageFollowSpeed * Time.fixedDeltaTime);
        }

        // Posicionar la carroza siempre enganchada detrás de los caballos según su ángulo actual
        float restDistance = (hitchPoint != null && hitchPoint != transform) ? (hitchDistance - 0.5f) : hitchDistance;
        Vector3 newCarriagePos = anchorPos - (currentCarriageRot * Vector3.forward * restDistance);
        newCarriagePos.y = initialCarriageY;

        carrozaTransform.SetPositionAndRotation(newCarriagePos, currentCarriageRot * carriageRotationOffset);
    }

    private void EvaluateStageProgression()
    {
        if (spacePressCount >= 12 && currentStage != MovementStage.GalopeRapido)
        {
            if (remainingSprintUses > 0)
            {
                remainingSprintUses--;
                SetStage(MovementStage.GalopeRapido);
            }
            else
            {
                spacePressCount = 10;
                if (currentStage != MovementStage.Medio) SetStage(MovementStage.Medio);
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
        if (Time.time - lastInputTime > inputWindowTime && currentStage != MovementStage.Detenido)
        {
            spacePressCount = Mathf.Max(0, spacePressCount - 3);

            if (currentStage == MovementStage.GalopeRapido) SetStage(MovementStage.Medio);
            else if (currentStage == MovementStage.Medio && spacePressCount < 5) SetStage(MovementStage.Lento);
            else if (currentStage == MovementStage.Lento && spacePressCount == 0) SetStage(MovementStage.Detenido);

            lastInputTime = Time.time;
        }

        if (currentStage == MovementStage.GalopeRapido)
        {
            sprintTimer += Time.deltaTime;
            if (sprintTimer >= maxSprintDuration)
            {
                spacePressCount = 6;
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
            case MovementStage.Detenido: targetMaxSpeed = 0f; break;
            case MovementStage.Lento: targetMaxSpeed = slowSpeed; break;
            case MovementStage.Medio: targetMaxSpeed = mediumSpeed; break;
            case MovementStage.GalopeRapido: targetMaxSpeed = fastSpeed; break;
        }
    }

    private void OnDrawGizmos()
    {
        AutoFindReferences();
        if (carrozaTransform != null)
        {
            Gizmos.color = Color.green;
            Vector3 start = (hitchPoint != null) ? hitchPoint.position : transform.position;
            Gizmos.DrawLine(start, carrozaTransform.position);
            Gizmos.DrawWireSphere(carrozaTransform.position, 0.35f);
        }
    }
}