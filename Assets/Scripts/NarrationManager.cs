using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NarrationManager : MonoBehaviour
{
    // Objeto que guarda los textos de cada cartel
    private class Cartel
    {
        public string topMessage;
        public string bottomMessage;
    }

    // Referencias a los de la escena
    [SerializeField] private TMP_Text text1;
    [SerializeField] private TMP_Text text2;

    private readonly List<Cartel> _posters = new List<Cartel>();

    [SerializeField] private TextAsset tutorialFile;     // Texto durante el tutorial
    [SerializeField] private TextAsset postTutorialFile; // Texto al terminar el tutorial
    [SerializeField] private TextAsset standardFile;     // Texto por defecto tras pasar el tutorial

    [SerializeField] private float fadeDuration = 0.5f;   // Duración del fade de entrada/salida
    [SerializeField] private float posterDuration = 4f;   // Segundos visibles antes de salir
#if UNITY_EDITOR
    [SerializeField] private SceneAsset nextSceneAsset; // Arrastra aquí la escena desde Project
#endif
    [SerializeField] private string nextSceneName;        // Escena a cargar al terminar todos los carteles

#if UNITY_EDITOR
    // Sincroniza el nombre de la escena cuando la arrastras en el Inspector
    private void OnValidate()
    {
        if (nextSceneAsset != null)
        {
            nextSceneName = nextSceneAsset.name;
        }
    }
#endif

    // Asigna la escena a cargar al terminar los carteles
    public void SetNextScene(string sceneName)
    {
        nextSceneName = sceneName;
    }

    // Lee un TextAsset y llena _posters según las reglas '*' y '/'
    private void LoadPosters(TextAsset sourceFile)
    {
        _posters.Clear(); // Limpia carteles anteriores antes de cargar

        if (sourceFile == null || string.IsNullOrEmpty(sourceFile.text))
            return;

        Cartel currentPoster = new Cartel();
        bool writingBottom = false; // false = topMessage, true = bottomMessage

        foreach (char c in sourceFile.text)
        {
            if (c == '*')
            {
                writingBottom = true; // A partir de aquí va a text2
            }
            else if (c == '/')
            {
                _posters.Add(currentPoster); // Guarda el cartel completo
                currentPoster = new Cartel(); // Prepara el siguiente cartel
                writingBottom = false;        // Vuelve a empezar por text1
            }
            else
            {
                if (writingBottom)
                    currentPoster.bottomMessage += c;
                else
                    currentPoster.topMessage += c;
            }
        }

        // Guarda el último cartel si el archivo no termina con '/'
        if (!string.IsNullOrEmpty(currentPoster.topMessage) || !string.IsNullOrEmpty(currentPoster.bottomMessage))
        {
            _posters.Add(currentPoster);
        }
    }

    // Carga el archivo del tutorial al arrancar la escena e inicia la secuencia
    private void Start()
    {
        LoadPosters(tutorialFile);
        if (_posters.Count > 0)
            StartCoroutine(PlayPostersSequence());

        Debug.Log($"Carteles cargados: {_posters.Count}");
    }

    // Muestra en pantalla el cartel indicado por su índice
    private void DisplayPoster(int index)
    {
        if (index < 0 || index >= _posters.Count)
            return;

        Cartel poster = _posters[index];

        if (index >= 1)
        {
            text1.fontSize = 20;
            text2.fontSize = 20;
        }

        text1.text = poster.topMessage ?? string.Empty;
        text2.text = poster.bottomMessage ?? string.Empty;
    }

    // Recorre todos los carteles en orden y carga la siguiente escena al terminar
    private IEnumerator PlayPostersSequence()
    {
        int posterCount = _posters.Count; // Cantidad total de carteles

        for (int i = 0; i < posterCount; i++)
        {
            yield return StartCoroutine(ShowAndHidePosterRoutine(i));
        }

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    // Muestra un cartel con entrada secuencial, espera y salida conjunta
    private IEnumerator ShowAndHidePosterRoutine(int index)
    {
        if (index < 0 || index >= _posters.Count)
            yield break;

        DisplayPoster(index);

        text1.alpha = 0f;
        text2.alpha = 0f;

        // Fade de entrada: primero text1, luego text2
        yield return StartCoroutine(FadeTextAlpha(text1, 1f, fadeDuration));
        yield return StartCoroutine(FadeTextAlpha(text2, 1f, fadeDuration));

        // Tiempo en pantalla
        yield return new WaitForSeconds(posterDuration);

        // Fade de salida: ambos bajan a 0 a la vez
        Coroutine fadeOut1 = StartCoroutine(FadeTextAlpha(text1, 0f, fadeDuration));
        Coroutine fadeOut2 = StartCoroutine(FadeTextAlpha(text2, 0f, fadeDuration));
        yield return fadeOut1;
        yield return fadeOut2;
    }

    // maneja opacidades
    private IEnumerator FadeTextAlpha(TMP_Text targetText, float targetAlpha, float duration)
    {
        if (targetText == null)
            yield break;

        float startAlpha = targetText.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            targetText.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        targetText.alpha = targetAlpha;
    }
}