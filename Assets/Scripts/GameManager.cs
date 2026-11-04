using UnityEngine;

public enum MoonPhase
{
    New,
    Waxing,
    Full,
    Third
}

public class GameManager : MonoBehaviour
{
    // Instancia estática para que cualquier script pueda acceder al GameManager escribiendo: GameManager.Instance
    public static GameManager Instance { get; private set; }

    [Header("Ciclo Lunar")]
    public MoonPhase currentMoonPhase = MoonPhase.New;

    //Cuenta cuántas noches lleva la fase actual activa
    public int nightsInCurrentPhase = 0;

    private void Awake()
    {
        // Regla del Singleton: Si no existe un GameManager, este se vuelve el principal. 
        // Si ya existía uno de una escena anterior, este se destruye para no duplicarse.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Evita que se borre al cambiar de noche/nivel
        }
        else
        {
            Destroy(gameObject);
        }
    }
    //Avanzar las fases lunares
    public void AdvanceMoonPhase()
    {
        nightsInCurrentPhase++;

        // Determinar cuántas noches debe durar la fase actual usando el Enum
        int requiredNights = 1;

        if (currentMoonPhase == MoonPhase.Waxing || currentMoonPhase == MoonPhase.Third)
        {
            requiredNights = 3; // Las fases de transición duran 3 noches
        }

        //Si ya se cumplieron las noches necesarias, se pasa a la siguiente fase
        if (nightsInCurrentPhase >= requiredNights)
        {
            // Avanza el orden del Enum
            switch (currentMoonPhase)
            {
                case MoonPhase.New: currentMoonPhase = MoonPhase.Waxing; break;
                case MoonPhase.Waxing: currentMoonPhase = MoonPhase.Full; break;
                case MoonPhase.Full: currentMoonPhase = MoonPhase.Third; break;
                case MoonPhase.Third: currentMoonPhase = MoonPhase.New; break;
            }

            nightsInCurrentPhase = 0; // Reinicia el contador para la nueva fase
            Debug.Log("El ciclo avanzó a la Fase Lunar: " + currentMoonPhase);
        }
        else
        {
            
        }
    }
}
