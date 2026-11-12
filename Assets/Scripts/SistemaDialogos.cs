using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Script encargado de mostrar los diálogos en pantalla y avanzar línea por línea.
/// Es llamado automáticamente por el GameManager entre las Misiones Principales.
/// Funciona automáticamente dibujando una caja de diálogo en pantalla (OnGUI),
/// o bien puedes conectarle tus propios objetos de Canvas UI en el Inspector.
/// </summary>
public class SistemaDialogos : MonoBehaviour
{
    public static SistemaDialogos Instance { get; private set; }

    [Header("Referencias de UI en Canvas (Opcionales)")]
    [Tooltip("Si asignas un Panel de Canvas aquí, se usará ese Panel. Si lo dejas vacío, el script dibujará el cuadro de diálogo automáticamente en pantalla.")]
    public GameObject panelDialogoCanvas;

    [Tooltip("Texto UI opcional para mostrar el nombre de quien habla")]
    public Text textoNombrePersonajeUI;

    [Tooltip("Texto UI opcional para mostrar el contenido del diálogo")]
    public Text textoContenidoDialogoUI;

    [Header("Estado Actual del Diálogo (Solo Lectura)")]
    [Tooltip("Indica si actualmente hay un diálogo mostrándose en pantalla")]
    public bool dialogoEnCurso = false;

    private string nombrePersonajeActual = "";
    private List<string> lineasDeDialogoActuales = new List<string>();
    private int indiceLineaActual = 0;
    private Action callbackAlFinalizar = null;

    // Evita que la misma pulsación de tecla que abrió el diálogo lo pase en el mismo frame
    private int frameDeInicioDialogo = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (panelDialogoCanvas != null)
        {
            panelDialogoCanvas.SetActive(false);
        }
    }

    private void Update()
    {
        if (!dialogoEnCurso) return;

        // Ignorar input en el mismo frame en que se abrió el diálogo
        if (Time.frameCount <= frameDeInicioDialogo) return;

        // Permitir avanzar el diálogo con E, Espacio, Enter o Clic Izquierdo
        if (SePresionoTeclaParaAvanzarDialogo())
        {
            AvanzarSiguienteLinea();
        }
    }

    /// <summary>
    /// Inicia una secuencia de diálogo en pantalla y guarda la acción que debe ejecutarse al terminar.
    /// </summary>
    /// <param name="nombrePersonaje">Nombre del personaje que habla (ej. "Cholito", "Pasajero").</param>
    /// <param name="lineas">Lista de frases que se mostrarán una por una.</param>
    /// <param name="alFinalizar">Función que se ejecutará cuando el jugador cierre la última línea.</param>
    public void IniciarDialogo(string nombrePersonaje, List<string> lineas, Action alFinalizar = null)
    {
        if (lineas == null || lineas.Count == 0)
        {
            alFinalizar?.Invoke();
            return;
        }

        nombrePersonajeActual = string.IsNullOrEmpty(nombrePersonaje) ? "Narrador" : nombrePersonaje;
        lineasDeDialogoActuales = new List<string>(lineas);
        indiceLineaActual = 0;
        callbackAlFinalizar = alFinalizar;
        dialogoEnCurso = true;
        frameDeInicioDialogo = Time.frameCount;

        if (panelDialogoCanvas != null)
        {
            panelDialogoCanvas.SetActive(true);
        }

        ActualizarTextoEnPantalla();
        Debug.Log($"[SistemaDialogos] Diálogo iniciado con '{nombrePersonajeActual}': \"{lineasDeDialogoActuales[0]}\"");
    }

    /// <summary>
    /// Pasa a la siguiente frase del diálogo. Si ya no quedan más frases, cierra el diálogo y reanuda el juego.
    /// </summary>
    public void AvanzarSiguienteLinea()
    {
        if (!dialogoEnCurso) return;

        indiceLineaActual++;

        if (indiceLineaActual < lineasDeDialogoActuales.Count)
        {
            ActualizarTextoEnPantalla();
            Debug.Log($"[SistemaDialogos] ({nombrePersonajeActual}): \"{lineasDeDialogoActuales[indiceLineaActual]}\"");
        }
        else
        {
            FinalizarDialogo();
        }
    }

    /// <summary>
    /// Cierra la ventana de diálogo y avisa al GameManager para que despauce el juego.
    /// </summary>
    public void FinalizarDialogo()
    {
        dialogoEnCurso = false;

        if (panelDialogoCanvas != null)
        {
            panelDialogoCanvas.SetActive(false);
        }

        Debug.Log("[SistemaDialogos] Diálogo finalizado.");

        Action accionPendiente = callbackAlFinalizar;
        callbackAlFinalizar = null;
        accionPendiente?.Invoke();
    }

    /// <summary>
    /// Actualiza los componentes de texto del Canvas si fueron asignados en el Inspector.
    /// </summary>
    private void ActualizarTextoEnPantalla()
    {
        if (textoNombrePersonajeUI != null)
        {
            textoNombrePersonajeUI.text = nombrePersonajeActual;
        }

        if (textoContenidoDialogoUI != null && indiceLineaActual < lineasDeDialogoActuales.Count)
        {
            textoContenidoDialogoUI.text = lineasDeDialogoActuales[indiceLineaActual];
        }
    }

    /// <summary>
    /// Detecta si el jugador presionó E, Espacio, Enter o Clic para avanzar el texto (incluso con Time.timeScale = 0).
    /// </summary>
    private bool SePresionoTeclaParaAvanzarDialogo()
    {
        bool avanzar = false;

        try
        {
            if (Input.GetKeyDown(KeyCode.E) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetMouseButtonDown(0))
            {
                avanzar = true;
            }
        }
        catch { }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
            {
                avanzar = true;
            }
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            avanzar = true;
        }
#endif

        return avanzar;
    }

    /// <summary>
    /// Dibuja automáticamente el cuadro de diálogo en pantalla si no se asignó un Canvas personalizado.
    /// </summary>
    private void OnGUI()
    {
        if (!dialogoEnCurso || panelDialogoCanvas != null) return;
        if (indiceLineaActual >= lineasDeDialogoActuales.Count) return;

        float anchoCaja = Mathf.Min(760f, Screen.width - 60f);
        float altoCaja = 150f;
        float posX = (Screen.width - anchoCaja) * 0.5f;
        float posY = Screen.height - altoCaja - 35f;

        // Fondo oscuro del cuadro de diálogo
        Color colorAnterior = GUI.color;
        GUI.color = new Color(0.06f, 0.06f, 0.1f, 0.94f);
        GUI.Box(new Rect(posX, posY, anchoCaja, altoCaja), GUIContent.none);

        // Borde superior decorativo
        GUI.color = new Color(0.2f, 0.85f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(posX, posY, anchoCaja, 4f), Texture2D.whiteTexture);
        GUI.color = colorAnterior;

        // Estilo para el nombre del personaje
        GUIStyle estiloNombre = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        estiloNombre.normal.textColor = new Color(0.3f, 0.95f, 0.45f, 1f);

        // Estilo para el texto del diálogo
        GUIStyle estiloTexto = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        estiloTexto.normal.textColor = Color.white;

        // Estilo para la indicación de continuar
        GUIStyle estiloContinuar = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.LowerRight
        };
        estiloContinuar.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);

        GUI.Label(new Rect(posX + 22f, posY + 14f, anchoCaja - 44f, 26f), nombrePersonajeActual, estiloNombre);
        GUI.Label(new Rect(posX + 22f, posY + 44f, anchoCaja - 44f, 70f), lineasDeDialogoActuales[indiceLineaActual], estiloTexto);
        GUI.Label(new Rect(posX + 22f, posY + altoCaja - 30f, anchoCaja - 44f, 22f),
            $"Presiona [E] o [Espacio] para continuar ({indiceLineaActual + 1}/{lineasDeDialogoActuales.Count})",
            estiloContinuar);
    }
}
