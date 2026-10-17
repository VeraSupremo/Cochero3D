using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarriageHitch : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punto trasero del equipo de caballos (donde va la lanza)")]
    public Transform hitchPoint;
    [Tooltip("Rigidbody del equipo de caballos")]
    public Rigidbody horseRb;

    [Header("Enganche (resorte)")]
    public float hitchDistance = 3f;      // separación en reposo entre enganche y carroza
    public float springStrength = 12000f; // rigidez del enganche
    public float damper = 900f;           // amortiguación (evita rebotes)
    public float maxForce = 60000f;       // límite de seguridad
    [Tooltip("Aplicar fuerza de reacción al caballo (más realista)")]
    public bool applyReactionForce = true;

    [Header("Alineación de Rotación")]
    public float rotationFollowSpeed = 6f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void FixedUpdate()
    {
        if (hitchPoint == null || horseRb == null) return;

        // Posición objetivo: detrás del punto de enganche, mirando igual que el caballo
        Vector3 targetPos = hitchPoint.position - hitchPoint.forward * hitchDistance;

        // Resorte (proporcional a la deformación)
        Vector3 displacement = targetPos - rb.position;
        Vector3 springForce = displacement * springStrength;

        // Amortiguación (proporcional a la velocidad RELATIVA, no absoluta)
        Vector3 relVel = rb.linearVelocity - horseRb.linearVelocity;
        Vector3 dampingForce = -relVel * damper;

        Vector3 totalForce = Vector3.ClampMagnitude(springForce + dampingForce, maxForce);

        rb.AddForce(totalForce, ForceMode.Force);

        // Tercera ley de Newton: el caballo siente el tirón de la carroza
        if (applyReactionForce)
            horseRb.AddForce(-totalForce, ForceMode.Force);

        AlignRotation();
    }

    private void AlignRotation()
    {
        // La carroza apunta hacia el punto de enganche (así "persigue" en curvas)
        Vector3 dir = hitchPoint.position - rb.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        Quaternion newRot = Quaternion.Slerp(rb.rotation, targetRot, rotationFollowSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newRot);
    }
    void Start()
    {
        if (hitchPoint != null)
        {
            Vector3 targetPos = hitchPoint.position - hitchPoint.forward * hitchDistance;
            rb.position = targetPos;
            rb.rotation = Quaternion.LookRotation(hitchPoint.forward);
        }
    }
}
