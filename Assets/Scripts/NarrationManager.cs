using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

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
                text1.fontSize = 40;          // Ajusta tamaño del texto superior
                text2.fontSize = 40;          // Ajusta tamaño del texto inferior
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

    // Carga el archivo del tutorial al arrancar la escena
    private void Start()
    {
        LoadPosters(tutorialFile);
    }

    // Muestra en pantalla el cartel indicado por su índice
    private void DisplayPoster(int index)
    {
        if (index < 0 || index >= _posters.Count)
            return;

        Cartel poster = _posters[index];
        text1.text = poster.topMessage ?? string.Empty;    // Asigna texto superior
        text2.text = poster.bottomMessage ?? string.Empty; // Asigna texto inferior
    }
}