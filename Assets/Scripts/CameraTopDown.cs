using UnityEngine;

public class CameraTopDown : MonoBehaviour
{
    [Header("Objetivo a Seguir")]
    [Tooltip("Arrastra aquí desde la Hierarchy el objeto que se mueve (Caballos)")]
    public Transform target;
    private Rigidbody targetRb;

    [Header("Posición y Altura (Ejes X / Y / Z)")]
    public float cameraHeight = 22f;
    public Vector2 xzOffset = new Vector2(0f, -4f);
    public bool mantenerOffsetInicialDeEscena = false;

    [Header("Rotación Fija (No rota con el personaje)")]
    public Vector3 fixedRotationAngles = new Vector3(75f, 0f, 0f);

    [Header("Suavizado de Seguimiento")]
    [Range(0f, 0.5f)] public float smoothTime = 0.08f;

    private Vector3 currentVelocity;

    void Awake()
    {
        // Desemparentar por si estaba dentro de otro objeto
        if (transform.parent != null) transform.SetParent(null);
        BuscarObjetivoAutomaticamente();
    }

    void Start()
    {
        BuscarObjetivoAutomaticamente();

        if (target == null)
        {
            Debug.LogError("[TopDownCameraFollow] ¡ERROR! La cámara no tiene a quién seguir. Arrastra el objeto 'Caballos' a la casilla 'Target' en el Inspector de la Main Camera.");
            return;
        }

        Debug.Log($"[TopDownCameraFollow] Cámara siguiendo correctamente a: {target.name}");

        if (mantenerOffsetInicialDeEscena)
        {
            cameraHeight = transform.position.y;
            xzOffset = new Vector2(transform.position.x - target.position.x, transform.position.z - target.position.z);
            fixedRotationAngles = transform.eulerAngles;
        }
        else
        {
            transform.rotation = Quaternion.Euler(fixedRotationAngles);
            transform.position = new Vector3(target.position.x + xzOffset.x, cameraHeight, target.position.z + xzOffset.y);
        }
    }

    private void BuscarObjetivoAutomaticamente()
    {
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            return;
        }

        // 1. Intenta buscar por el script nuevo
        CrazyCarriageController nuevoCtrl = FindFirstObjectByType<CrazyCarriageController>();
        if (nuevoCtrl != null)
        {
            target = nuevoCtrl.transform;
            targetRb = target.GetComponent<Rigidbody>();
            return;
        }

        // 2. Intenta buscar por nombre en la jerarquía ("Caballos" o "Carroza")
        GameObject objCaballos = GameObject.Find("Caballos");
        if (objCaballos != null)
        {
            target = objCaballos.transform;
            targetRb = target.GetComponent<Rigidbody>();
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
        if (target == null) return;

        // Mantener rotación fija sin girar con los caballos
        transform.rotation = Quaternion.Euler(fixedRotationAngles);

        // Si el objeto tiene Rigidbody, usamos rb.position que es la posición física real
        Vector3 posObjetivo = (targetRb != null) ? targetRb.position : target.position;

        Vector3 desiredPosition = new Vector3(
            posObjetivo.x + xzOffset.x,
            cameraHeight,
            posObjetivo.z + xzOffset.y
        );

        if (smoothTime <= 0.001f)
            transform.position = desiredPosition;
        else
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
    }
}