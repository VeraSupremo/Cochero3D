using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CocheController : MonoBehaviour
{
    public enum MovementStage { Detenido, Lento, Medio, GalopeRapido }

    [Header("Referencias")]
    public Rigidbody carriageRb;
    public Transform horsesTransform;

    [Header("Configuración de Etapas")]
    [Tooltip("Tiempo máximo entre pulsaciones para mantener o subir de etapa")]
    public float InputWindowTime = 1.2f; 
    [Tooltip("Tiempo que dura el galope rápido antes de agotarse automáticamente")]
    public float maxSprintDuration = 3f;

    [Header("Ajustes de Giro")]
    public float turnSpeed = 40f;
    public float followSpeed = 5f;

    [Header("Visualización en Tiempo Real")]
    public MovementStage currentStage = MovementStage.Detenido;
    public float currentSpeed;

    private float lastInputTime;
    private float sprintTimer;
    private int spacePressCount;

    // Parámetros de cada etapa: { Fuerza de empuje, Velocidad Máxima }
    private float currentAccForce;
    private float currentMaxSpeed;

    private void Start()
    {
        if (carriageRb != null) carriageRb.linearDamping = 0.8f; // Fricción natural del suelo
        SetStage(MovementStage.Detenido);
    }

    private void Update()
    {
        private void Update()
    {
        // [Tus otros códigos de Espacio y Timers se quedan igual...]
        if (Input.GetKeyDown(KeyCode.Space)) { /* ... */ }
          HandleTimers();

        // ==========================================
        // 3. NUEVO SISTEMA DE GIRO LIMITADO POR VELOCIDAD
        // ==========================================
        float turnInput = Input.GetAxis("Horizontal");

        // Calculamos qué tan rápido va el carruaje en relación a su velocidad máxima absoluta (Galope Rápido)
        // 0 = Detenido, 1 = Velocidad Máxima de Galope (22f)
        float maxAbsoluteSpeed = 22f; 
        float speedPercent = Mathf.Clamp01(carriageRb.linearVelocity.magnitude / maxAbsoluteSpeed);

        // Modificador de giro: Si speedPercent es 0 (lento), mantiene el 100% del giro.
        // Si speedPercent se acerca a 1 (máxima velocidad), el giro cae a 0%.
        float turnModifier = Mathf.Lerp(1f, 0f, speedPercent);

        // Aplicamos el modificador a la velocidad de giro original
        float currentTurnSpeed = turnSpeed * turnModifier;

        // Rotamos los caballos con la velocidad de giro ya reducida
        horsesTransform.Rotate(Vector3.up, turnInput * currentTurnSpeed * Time.deltaTime);

        currentSpeed = carriageRb.linearVelocity.magnitude;
    }
    }

    private void FixedUpdate()
    {
        // Limitar la velocidad según la etapa actual
        if (carriageRb.linearVelocity.magnitude > currentMaxSpeed)
        {
            carriageRb.linearVelocity = carriageRb.linearVelocity.normalized * currentMaxSpeed;
        }

        // La carroza sigue la dirección de los caballos
        if (carriageRb.linearVelocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horsesTransform.forward);
            carriageRb.MoveRotation(Quaternion.Slerp(carriageRb.rotation, targetRotation, followSpeed * Time.fixedDeltaTime));
            carriageRb.linearVelocity = carriageRb.transform.forward * carriageRb.linearVelocity.magnitude;
        }
    }

    private void Accelerate()
    {
        carriageRb.AddForce(carriageRb.transform.forward * currentAccForce, ForceMode.VelocityChange);
    }

    private void EvaluateStageProgression()
    {
        // Cambia de etapa según cuántas veces seguidas se ha presionado el espacio en la ventana de tiempo
        if (spacePressCount >= 12 && currentStage != MovementStage.GalopeRapido)
        {
            SetStage(MovementStage.GalopeRapido);
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
        // Si el jugador tarda mucho en presionar Espacio, el carruaje pierde ritmo
        if (Time.time - lastInputTime > InputWindowTime && currentStage != MovementStage.Detenido)
        {
            spacePressCount = Mathf.Max(0, spacePressCount - 2); // Baja el contador gradualmente
            
            if (currentStage == MovementStage.GalopeRapido) SetStage(MovementStage.Medio);
            else if (currentStage == MovementStage.Medio) SetStage(MovementStage.Lento);
            else if (currentStage == MovementStage.Lento && carriageRb.linearVelocity.magnitude < 0.5f) SetStage(MovementStage.Detenido);
            
            lastInputTime = Time.time; // Resetea la ventana para la siguiente bajada
        }

        // Control temporal de la Etapa 3 (Galope Rápido)
        if (currentStage == MovementStage.GalopeRapido)
        {
            sprintTimer += Time.deltaTime;
            if (sprintTimer >= maxSprintDuration)
            {
                // Se agota el sprint, castiga al jugador bajando a etapa Media y reiniciando parte del contador
                spacePressCount = 6; 
                SetStage(MovementStage.Medio);
            }
        }
    }

    private void SetStage(MovementStage newStage)
    {
        currentStage = newStage;

        switch (currentStage)
        {
            case MovementStage.Detenido:
                currentAccForce = 0f;
                currentMaxSpeed = 0f;
                break;

            case MovementStage.Lento: // Etapa 1: Partiendo lento, pesado
                currentAccForce = 2f; 
                currentMaxSpeed = 4f;
                sprintTimer = 0f;
                break;

            case MovementStage.Medio: // Etapa 2: Velocidad de crucero estable
                currentAccForce = 4f;
                currentMaxSpeed = 10f;
                sprintTimer = 0f;
                break;

            case MovementStage.GalopeRapido: // Etapa 3: Impulso muy rápido pero temporal
                currentAccForce = 9f;
                currentMaxSpeed = 22f;
                sprintTimer = 0f; // Empieza a contar el tiempo límite de esta etapa
                break;
        }
    }
}
