using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HorseTeamController : MonoBehaviour
{
    public enum MovementStage { Detenido, Lento, Medio, GalopeRapido }

    [Header("Configuración de Etapas")]
    public float inputWindowTime = 1.2f;
    public float maxSprintDuration = 3f;

    [Header("Ajustes de Giro")]
    public float turnSpeed = 45f;
    [Range(0f, 1f)] public float minTurnFactor = 0.25f; // giro mínimo a tope
    public float maxAbsoluteSpeed = 22f;

    [Header("Física")]
    public float linearDamping = 0.5f;

    [Header("Estado (solo lectura)")]
    public MovementStage currentStage = MovementStage.Detenido;
    public float currentSpeed;

    private Rigidbody rb;
    private float lastInputTime;
    private float sprintTimer;
    private int spacePressCount;

    private float currentAccForce;
    private float currentMaxSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = linearDamping;
    }

    void Start() => SetStage(MovementStage.Detenido);

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            spacePressCount++;
            lastInputTime = Time.time;
            EvaluateStageProgression();
        }

        HandleTimers();
        HandleSteering();

        currentSpeed = rb.linearVelocity.magnitude;
    }

    void FixedUpdate()
    {
        ApplyMovementForce();
        ClampVelocity();
    }

    private void ApplyMovementForce()
    {
        if (currentStage == MovementStage.Detenido) return;

        Vector3 flat = rb.linearVelocity; flat.y = 0f;
        if (flat.magnitude >= currentMaxSpeed) return;

        rb.AddForce(transform.forward * currentAccForce, ForceMode.Acceleration);
    }

    private void ClampVelocity()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (flat.magnitude > currentMaxSpeed)
        {
            Vector3 limited = flat.normalized * currentMaxSpeed;
            rb.linearVelocity = new Vector3(limited.x, v.y, limited.z);
        }
    }

    private void HandleSteering()
    {
        float turnInput = Input.GetAxis("Horizontal");
        if (Mathf.Abs(turnInput) < 0.01f) return;

        float speedPercent = Mathf.Clamp01(rb.linearVelocity.magnitude / maxAbsoluteSpeed);
        float turnModifier = Mathf.Lerp(1f, minTurnFactor, speedPercent);
        float currentTurnSpeed = turnSpeed * turnModifier;

        transform.Rotate(Vector3.up, turnInput * currentTurnSpeed * Time.deltaTime);
    }

    private void EvaluateStageProgression()
    {
        if (spacePressCount >= 12 && currentStage != MovementStage.GalopeRapido)
            SetStage(MovementStage.GalopeRapido);
        else if (spacePressCount >= 5 && currentStage != MovementStage.GalopeRapido && currentStage != MovementStage.Medio)
            SetStage(MovementStage.Medio);
        else if (spacePressCount >= 1 && currentStage == MovementStage.Detenido)
            SetStage(MovementStage.Lento);
    }

    private void HandleTimers()
    {
        if (Time.time - lastInputTime > inputWindowTime && currentStage != MovementStage.Detenido)
        {
            spacePressCount = Mathf.Max(0, spacePressCount - 2);

            if (currentStage == MovementStage.GalopeRapido) SetStage(MovementStage.Medio);
            else if (currentStage == MovementStage.Medio) SetStage(MovementStage.Lento);
            else if (currentStage == MovementStage.Lento && rb.linearVelocity.magnitude < 0.5f)
                SetStage(MovementStage.Detenido);

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

        // NOTA: los valores de accForce son más altos que en tu código viejo
        // porque ahora el caballo también arrastra ~800kg de carroza.
        switch (currentStage)
        {
            case MovementStage.Detenido:
                currentAccForce = 0f; currentMaxSpeed = 0f; break;
            case MovementStage.Lento:
                currentAccForce = 4f;  currentMaxSpeed = 4f;  sprintTimer = 0f; break;
            case MovementStage.Medio:
                currentAccForce = 8f;  currentMaxSpeed = 10f; sprintTimer = 0f; break;
            case MovementStage.GalopeRapido:
                currentAccForce = 16f; currentMaxSpeed = 22f; sprintTimer = 0f; break;
        }
    }
}