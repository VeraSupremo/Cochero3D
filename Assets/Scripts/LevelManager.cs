using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private bool isTutorialScene;

    void Start()
    {
        if (GlobalTimeManager.Instance == null) return;

        if (isTutorialScene)
        {
            // Modo Tutorial: El tiempo NO transcurre
            GlobalTimeManager.Instance.isTimePaused = true;
            GlobalTimeManager.Instance.currentHour = 21f; 
        }
        else
        {
            // Juego Normal: El tiempo fluye
            GlobalTimeManager.Instance.isTimePaused = false;
            GlobalTimeManager.Instance.realSecondsPerHour = 60f; // 1 hora de juego = 1 minuto real
            GlobalTimeManager.Instance.currentHour = 21f;        // Forzar el inicio de la noche a las 21:00

            //Leer fase lunar desde el GameManager
            if (GameManager.Instance != null)
            {
                // Guarda 'MoonPhase'
                MoonPhase currentPhase = GameManager.Instance.currentMoonPhase;
                ApplyMoonPhaseEffects(currentPhase);
            }

        }
    }

    //función para procesar los efectos de la luna
    private void ApplyMoonPhaseEffects(MoonPhase phase)
    {
        switch (phase)
        {
            case MoonPhase.New:
                // Luna nueva, sin Luz
                //aplicar efectos
                break;

            case MoonPhase.Waxing:
                // Luna Creciente, luz aumenta
                //aplicar efectos
                break;

            case MoonPhase.Full:
                // Luna Llena + Brillo
                //aplicar efectos
                break;

            case MoonPhase.Third:
                // Cuarto Menguante, luz decayente
                //aplicar efectos
                break;
        }
    }
}

