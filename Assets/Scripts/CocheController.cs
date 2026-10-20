using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class CrazyCarriageController : MonoBehaviour
{
    public enum MovementStage { Detenido, Lento, Medio, GalopeRapido }

    [Header("Referencias del Carruaje (Auto-detectadas)")]
    public Transform hitchPoint;
    public Transform carrozaTransform;
    public float hitchDistance = 2.8f;
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

    [Header("Giro Normal (Sin Shift)")]
    public float turnSpeed = 110f;
    [Tooltip("Al ir rápido SIN Shift, cuesta más doblar (te abres en las curvas)")]
    [Range(0.1f, 1f)] public float highSpeedTurnFactor = 0.35f;

    [Header("Derrape (Mantener Shift + Dirección)")]
    [Tooltip("Velocidad mínima para poder derrapar (ej. etapa Medio o Galope Rápido)")]
    public float minSpeedToDrift = 9f;
    [Tooltip("Multiplicador de giro al derrapar con Shift")]
    public float driftTurnMultiplier = 1.85f;
    [Tooltip("Qué tanto patinan los caballos de costado (menor = más resbaladizo)")]
    public float driftTraction = 3.5f;
    [Tooltip("Ángulo extra que se abre la cola de la carroza al derrapar")]
    public float carriageDriftSwingAngle = 38f;
    [Tooltip("Qué tan lento recupera la línea la carroza durante el derrape")]
    public float carriageDriftFollowSpeed = 2.5f;

    [Header("Estado Actual (Solo Lectura)")]
    public MovementStage currentStage = MovementStage.Detenido;
    public float currentSpeed;
    public bool isDrifting;
    public int spacePressCount;

    private Rigidbody rb;
    private float targetMaxSpeed;
    private float lastInputTime;
    private float sprintTimer;
    private float turnInput;
    private bool shiftHeld;

    private Vector3 localForwardAxis = Vector3.forward;
    private Vector3 currentMoveDirection;
    private Quaternion currentCarriageRot;
    private Quaternion carriageRotationOffset = Quaternion.identity;
    private float initialCarriageY;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        remainingSprintUses = maxSprintUses;
        AutoFindReferences();
    }

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

        if (carrozaTransform != null)
        {
            initialCarriageY = carrozaTransform.position.y;

            Vector3 toCarriage = carrozaTransform.position - transform.position;
            toCarriage.y = 0f;

            if (toCarriage.magnitude > 0.1f)
            {
                hitchDistance = toCarriage.magnitude;
                Vector3 worldForward = -toCarriage.normalized;
                localForwardAxis = transform.InverseTransformDirection(worldForward);
            }

            Vector3 forwardWorld = GetWorldForward();
            currentMoveDirection = forwardWorld;

            Quaternion lookRot = Quaternion.LookRotation(forwardWorld, Vector3.up);
            carriageRotationOffset = Quaternion.Inverse(lookRot) * carrozaTransform.rotation;

            if (hitchPoint != transform && hitchPoint.localPosition.sqrMagnitude < 0.05f)
            {
                hitchPoint.localPosition = -localForwardAxis * 0.6f;
            }

            carrozaTransform.SetParent(null);
            currentCarriageRot = lookRot;
            UpdateCarriageTrailer(true);
        }
        else
        {
            currentMoveDirection = GetWorldForward();
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
        shiftHeld = false;

        // Input Clásico
        try
        {
            if (Input.GetKeyDown(KeyCode.Space)) spacePressed = true;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) shiftHeld = true;
            turnInput = Input.GetAxisRaw("Horizontal");
        }
        catch { }

        // New Input System (Unity 6)
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame) spacePressed = true;
            if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed) shiftHeld = true;

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
        HandleMovementAndDrift();
        UpdateCarriageTrailer(false);
    }

    private void HandleMovementAndDrift()
    {
        // 1. Calcular velocidad actual
        float accel = (currentSpeed < targetMaxSpeed) ? accelerationRate : decelerationRate;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetMaxSpeed, accel * Time.fixedDeltaTime);

        // 2. ¿Está derrapando? (Requiere presionar Shift, estar doblando e ir suficientemente rápido)
        isDrifting = shiftHeld && Mathf.Abs(turnInput) > 0.01f && currentSpeed >= minSpeedToDrift;

        // 3. Rotación de los caballos
        if (Mathf.Abs(turnInput) > 0.01f)
        {
            float turnAngle;

            if (isDrifting)
            {
                // Con Shift: Giro agresivo y cerrado sin perder capacidad de doblar por ir rápido
                turnAngle = turnInput * turnSpeed * driftTurnMultiplier * Time.fixedDeltaTime;
            }
            else
            {
                // Sin Shift: A alta velocidad cuesta más doblar (te obliga a usar Shift en curvas cerradas)
                float speedPercent = Mathf.Clamp01(currentSpeed / fastSpeed);
                float turnMod = Mathf.Lerp(1f, highSpeedTurnFactor, speedPercent);
                turnAngle = turnInput * turnSpeed * turnMod * Time.fixedDeltaTime;
            }

            Quaternion turnRotation = Quaternion.Euler(0f, turnAngle, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }

        // 4. Dirección de deslizamiento (Inercia estilo GTA GBA)
        Vector3 desiredForward = GetWorldForward();

        if (isDrifting)
        {
            // Mientras derrapa, la dirección de movimiento tarda en alcanzar hacia dónde miran los caballos (patina de costado)
            currentMoveDirection = Vector3.Slerp(currentMoveDirection, desiredForward, driftTraction * Time.fixedDeltaTime).normalized;
        }
        else
        {
            // Al soltar Shift, recupera el agarre rápidamente
            currentMoveDirection = Vector3.Slerp(currentMoveDirection, desiredForward, 12f * Time.fixedDeltaTime).normalized;
        }

        rb.linearVelocity = new Vector3(currentMoveDirection.x * currentSpeed, 0f, currentMoveDirection.z * currentSpeed);
    }

    private void UpdateCarriageTrailer(bool instant)
    {
        if (carrozaTransform == null) return;

        Vector3 horseForward = GetWorldForward();
        Quaternion horseLookRot = Quaternion.LookRotation(horseForward, Vector3.up);

        Vector3 anchorPos = (hitchPoint != null && hitchPoint != transform)
            ? hitchPoint.position
            : transform.position - (horseForward * 0.5f);

        if (instant)
        {
            currentCarriageRot = horseLookRot;
        }
        else
        {
            Vector3 dirToHitch = anchorPos - carrozaTransform.position;
            dirToHitch.y = 0f;

            Quaternion pullRot = (dirToHitch.sqrMagnitude > 0.001f)
                ? Quaternion.LookRotation(dirToHitch.normalized, Vector3.up)
                : horseLookRot;

            float moveBlend = Mathf.Clamp01(currentSpeed / slowSpeed);
            Quaternion targetRot = Quaternion.Slerp(horseLookRot, pullRot, moveBlend * 0.7f);

            // EFECTO LATIGAZO EN DERRAPE: Si está derrapando, la carroza se abre hacia afuera de la curva
            if (isDrifting)
            {
                Quaternion driftSwing = Quaternion.Euler(0f, -turnInput * carriageDriftSwingAngle, 0f);
                targetRot = horseLookRot * driftSwing;
            }

            float followSpd = isDrifting ? carriageDriftFollowSpeed : carriageFollowSpeed;
            currentCarriageRot = Quaternion.Slerp(currentCarriageRot, targetRot, followSpd * Time.fixedDeltaTime);
        }

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
            Gizmos.color = isDrifting ? Color.red : Color.green;
            Vector3 start = (hitchPoint != null) ? hitchPoint.position : transform.position;
            Gizmos.DrawLine(start, carrozaTransform.position);
            Gizmos.DrawWireSphere(carrozaTransform.position, 0.35f);
        }
    }
}