using UnityEngine;

public class GlobalTimeManager : MonoBehaviour
{
    public static GlobalTimeManager Instance { get; private set; }

    [Header("Configuración de Tiempo")]
    [Tooltip("Segundos reales que dura una hora de juego")]
    public float realSecondsPerHour = 60f; // 1 minuto IRL por defecto

    [Header("Current Status")]
    [Range(0f, 24f)] public float currentHour = 21f; // Empieza a las 21:00 de la noche

    [HideInInspector] public bool isTimePaused = false; // Nueva variable para congelar el tiempo (ej. en el tutorial)

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // Tiempo pausado en Tutorial
        if (isTimePaused) return;

        // Calcula cuántas horas de juego avanzan por cada segundo real transcurrido
        float hoursPerRealSecond = 1f / realSecondsPerHour;

        // Avanza la hora del juego usando DeltaTime
        currentHour += Time.deltaTime * hoursPerRealSecond;

        // Corrección del ciclo de 24 horas estándar
        if (currentHour >= 24f)
        {
            currentHour -= 24f;
        }

        // CONTROL DE FIN DE LA NOCHE: Si llega o pasa de las 6:00 AM (pero viene desde la noche)
        // Usamos un pequeño margen (ej. entre 6:00 y 6:10) para evitar que se active erróneamente al inicializar
        if (currentHour >= 6f && currentHour < 7f)
        {
            currentHour = 6f;
            isTimePaused = true;

            //Aquí es donde realmente el juego ejecuta el cambio de noche y fase lunar
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AdvanceMoonPhase();
            }
        }
    }
}

