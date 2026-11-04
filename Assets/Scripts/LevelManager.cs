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
            GlobalTimeManager.Instance.currentHour = 21f;        // Forzamos el inicio de la noche a las 21:00
        }
    }
}

