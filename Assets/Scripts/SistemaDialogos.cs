using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Script encargado de mostrar los diálogos turno por turno (ej. Patrón -> Chofer -> Patrón -> Perro -> Chofer),
/// oscurecer la pantalla con un fondo negro semitransparente para destacar los iconos de los personajes,
/// y reproducir música de diálogo o efectos de sonido (como ladridos o voces) en cada línea.
/// </summary>
public class SistemaDialogos : MonoBehaviour
{
    public static SistemaDialogos Instance { get; private set; }

    // =========================================================================
    // CLASE PARA CADA TURNO / LINEA DE DIALOGO (QUIEN HABLA, TEXTO, ICONO Y SONIDO)
    // =========================================================================

    /// <summary>
    /// Representa un turno individual dentro de una conversación.
    /// Permite elegir quién habla en cada línea, qué dice, su icono y su sonido particular.
    /// </summary>
    [Serializable]
    public class LineaDeDialogo
    {
        [Tooltip("Nombre de quien habla en este turno (ej. Patron, Chofer, Cholito, Pasajero)")]
        public string nombrePersonaje = "Chofer";

        [Tooltip("Lo que dice o hace el personaje en este turno")]
        [TextArea(2, 4)]
        public string textoLinea = "";

        [Tooltip("Icono o retrato del personaje en esta línea (Opcional: si lo dejas vacío, usará automáticamente el icono registrado para ese nombre en el GameManager)")]
        public Sprite iconoEspecifico;

        [Tooltip("Sonido o efecto al mostrar esta línea (ej. ladrido de perro, suspiro, voz). Si se deja vacío, usará el sonido del personaje en el GameManager.")]
        public AudioClip sonidoDeEstaLinea;
    }

    // =========================================================================
    // APARTADO 1: DISEÑO VISUAL Y CANVAS PERSONALIZADO (OPCIONAL)
    // =========================================================================

    [Header("1. Diseño de Interfaz en Canvas (Opcional - Para editar en Editor)")]
    [Tooltip("Si asignas tu propio Panel de Canvas aquí, se usará tu diseño de Canvas. Si lo dejas vacío, el script genera el diseño automáticamente por código en OnGUI.")]
    public GameObject panelDialogoCanvas;

    [Tooltip("Imagen UI opcional para el fondo negro semitransparente de toda la pantalla")]
    public Image imagenFondoNegroCanvas;

    [Tooltip("Imagen UI opcional donde se mostrará el icono/retrato del personaje que habla")]
    public Image imagenIconoPersonajeCanvas;

    [Tooltip("Texto UI opcional para mostrar el nombre del personaje que habla en el turno actual")]
    public Text textoNombrePersonajeUI;

    [Tooltip("Texto UI opcional para mostrar el diálogo del turno actual")]
    public Text textoContenidoDialogoUI;

    [Header("2. Ajustes del Diseño Automático en Pantalla (Fondo Negro e Iconos)")]
    [Tooltip("Color y transparencia del fondo negro que cubre toda la pantalla para destacar a los personajes (Alpha 0.68 = negro medio transparente)")]
    public Color colorFondoPantallaOscura = new Color(0f, 0f, 0f, 0.68f);

    [Tooltip("Color de fondo de la caja inferior donde aparece el texto")]
    public Color colorCajaDeTexto = new Color(0.06f, 0.06f, 0.1f, 0.95f);

    [Tooltip("Tamaño en píxeles del icono/retrato del personaje cuando habla")]
    public float tamanoIconoPersonaje = 135f;

    // =========================================================================
    // APARTADO 2: MUSICA Y EFECTOS DE SONIDO DE LOS DIALOGOS
    // =========================================================================

    [Header("3. Música y Sonidos durante los Diálogos")]
    [Tooltip("Música ambiental que sonará de fondo mientras los personajes estén hablando (opcional)")]
    public AudioClip musicaFondoPorDefecto;

    [Tooltip("Volumen de la música de fondo durante los diálogos (0 a 1)")]
    [Range(0f, 1f)] public float volumenMusicaDialogo = 0.45f;

    [Tooltip("Volumen de los efectos de voz o ladridos al cambiar de línea (0 a 1)")]
    [Range(0f, 1f)] public float volumenEfectosVoz = 0.85f;

    private AudioSource fuenteAudioMusicaDialogo;
    private AudioSource fuenteAudioEfectosVoz;

    // =========================================================================
    // ESTADO EN TIEMPO REAL
    // =========================================================================

    [Header("Estado Actual del Diálogo (Solo Lectura)")]
    public bool dialogoEnCurso = false;
    public int indiceTurnoActual = 0;

    private List<LineaDeDialogo> secuenciaActual = new List<LineaDeDialogo>();
    private Action callbackAlFinalizar = null;
    private int frameDeInicioDialogo = -1;

    // Variables cacheadas del turno actual para dibujar rápido en pantalla
    private string nombreActualMostrado = "";
    private string textoActualMostrado = "";
    private Sprite iconoActualMostrado = null;
    private Color colorNombreActual = new Color(0.3f, 0.95f, 0.45f, 1f);

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

        ConfigurarFuentesDeAudioAutomaticas();

        if (panelDialogoCanvas != null)
        {
            panelDialogoCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Crea automáticamente los componentes AudioSource para la música del diálogo y las voces/ladridos.
    /// </summary>
    private void ConfigurarFuentesDeAudioAutomaticas()
    {
        if (fuenteAudioMusicaDialogo == null)
        {
            fuenteAudioMusicaDialogo = gameObject.AddComponent<AudioSource>();
            fuenteAudioMusicaDialogo.loop = true;
            fuenteAudioMusicaDialogo.playOnAwake = false;
            fuenteAudioMusicaDialogo.ignoreListenerPause = true;
        }

        if (fuenteAudioEfectosVoz == null)
        {
            fuenteAudioEfectosVoz = gameObject.AddComponent<AudioSource>();
            fuenteAudioEfectosVoz.loop = false;
            fuenteAudioEfectosVoz.playOnAwake = false;
            fuenteAudioEfectosVoz.ignoreListenerPause = true;
        }
    }

    private void Update()
    {
        if (!dialogoEnCurso) return;

        // Evita que la misma tecla E con la que aceptaste la misión salte la primera línea en el mismo frame
        if (Time.frameCount <= frameDeInicioDialogo) return;

        if (SePresionoTeclaParaAvanzarDialogo())
        {
            AvanzarSiguienteTurno();
        }
    }

    // =========================================================================
    // FUNCIONES PRINCIPALES DE CONVERSACION TURNO POR TURNO
    // =========================================================================

    /// <summary>
    /// Inicia una conversación completa donde cada línea tiene su propio hablante (ej. Patrón -> Chofer -> Perro -> Chofer).
    /// Activa el fondo negro semitransparente, la música del diálogo y reproduce el sonido de cada turno.
    /// </summary>
    /// <param name="lineasConversacion">Lista ordenada de turnos de diálogo.</param>
    /// <param name="musicaEspecifica">Música opcional para esta conversación (si es null, usa musicaFondoPorDefecto).</param>
    /// <param name="alFinalizar">Acción que se ejecuta al cerrar el último turno del diálogo.</param>
    public void IniciarConversacion(List<LineaDeDialogo> lineasConversacion, AudioClip musicaEspecifica = null, Action alFinalizar = null)
    {
        if (lineasConversacion == null || lineasConversacion.Count == 0)
        {
            alFinalizar?.Invoke();
            return;
        }

        secuenciaActual = new List<LineaDeDialogo>(lineasConversacion);
        indiceTurnoActual = 0;
        callbackAlFinalizar = alFinalizar;
        dialogoEnCurso = true;
        frameDeInicioDialogo = Time.frameCount;

        if (panelDialogoCanvas != null)
        {
            panelDialogoCanvas.SetActive(true);
            if (imagenFondoNegroCanvas != null)
            {
                imagenFondoNegroCanvas.color = colorFondoPantallaOscura;
                imagenFondoNegroCanvas.gameObject.SetActive(true);
            }
        }

        // Iniciar música de fondo del diálogo si está asignada
        AudioClip clipMusica = (musicaEspecifica != null) ? musicaEspecifica : musicaFondoPorDefecto;
        ReproducirMusicaDeDialogo(clipMusica);

        MostrarTurnoActual();
    }

    /// <summary>
    /// Avanza al siguiente turno de la conversación. Si era el último turno, cierra el diálogo y reanuda el juego.
    /// </summary>
    public void AvanzarSiguienteTurno()
    {
        if (!dialogoEnCurso) return;

        indiceTurnoActual++;

        if (indiceTurnoActual < secuenciaActual.Count)
        {
            MostrarTurnoActual();
        }
        else
        {
            FinalizarDialogo();
        }
    }

    /// <summary>
    /// Carga los datos del personaje del turno actual (nombre, texto, icono y sonido/ladrido) y los muestra en pantalla.
    /// </summary>
    private void MostrarTurnoActual()
    {
        if (indiceTurnoActual < 0 || indiceTurnoActual >= secuenciaActual.Count) return;

        LineaDeDialogo linea = secuenciaActual[indiceTurnoActual];
        nombreActualMostrado = string.IsNullOrEmpty(linea.nombrePersonaje) ? "Narrador" : linea.nombrePersonaje;
        textoActualMostrado = linea.textoLinea;

        // Buscar si el personaje tiene un perfil global (icono, color, sonido) registrado en el GameManager
        GameManager.PerfilPersonajeDialogo perfil = null;
        if (GameManager.Instance != null)
        {
            perfil = GameManager.Instance.ObtenerPerfilDePersonaje(nombreActualMostrado);
        }

        // 1. Determinar qué icono mostrar (prioriza el de esta línea; si está vacío, usa el del GameManager)
        iconoActualMostrado = (linea.iconoEspecifico != null)
            ? linea.iconoEspecifico
            : (perfil != null ? perfil.iconoPorDefecto : null);

        // 2. Determinar el color del nombre del personaje
        colorNombreActual = (perfil != null) ? perfil.colorNombre : new Color(0.3f, 0.95f, 0.45f, 1f);

        // 3. Reproducir sonido o voz de este turno (ej. ladrido cuando habla el perro)
        AudioClip sonidoTurno = (linea.sonidoDeEstaLinea != null)
            ? linea.sonidoDeEstaLinea
            : (perfil != null ? perfil.sonidoVozPorDefecto : null);

        if (sonidoTurno != null && fuenteAudioEfectosVoz != null)
        {
            fuenteAudioEfectosVoz.Stop();
            fuenteAudioEfectosVoz.PlayOneShot(sonidoTurno, volumenEfectosVoz);
        }

        // 4. Actualizar elementos si el usuario conectó un Canvas propio
        ActualizarElementosCanvasPropio();

        Debug.Log($"[SistemaDialogos] Turno {indiceTurnoActual + 1}/{secuenciaActual.Count} - Habla '{nombreActualMostrado}': \"{textoActualMostrado}\"");
    }

    /// <summary>
    /// Cierra el diálogo, detiene la música del diálogo, quita el fondo negro semitransparente y avisa al GameManager.
    /// </summary>
    public void FinalizarDialogo()
    {
        dialogoEnCurso = false;
        DetenerMusicaDeDialogo();

        if (panelDialogoCanvas != null)
        {
            panelDialogoCanvas.SetActive(false);
        }

        Debug.Log("[SistemaDialogos] Conversación finalizada.");

        Action accionPendiente = callbackAlFinalizar;
        callbackAlFinalizar = null;
        accionPendiente?.Invoke();
    }

    // =========================================================================
    // FUNCIONES DE AUDIO (MUSICA Y EFECTOS EN DIALOGOS)
    // =========================================================================

    /// <summary>
    /// Reproduce una pista de música mientras dura la conversación.
    /// </summary>
    public void ReproducirMusicaDeDialogo(AudioClip clipMusica)
    {
        if (clipMusica == null || fuenteAudioMusicaDialogo == null) return;

        fuenteAudioMusicaDialogo.clip = clipMusica;
        fuenteAudioMusicaDialogo.volume = volumenMusicaDialogo;
        fuenteAudioMusicaDialogo.Play();
    }

    /// <summary>
    /// Detiene la música de fondo del diálogo cuando la conversación termina.
    /// </summary>
    public void DetenerMusicaDeDialogo()
    {
        if (fuenteAudioMusicaDialogo != null && fuenteAudioMusicaDialogo.isPlaying)
        {
            fuenteAudioMusicaDialogo.Stop();
        }
    }

    // =========================================================================
    // DISEÑO DE LOS DIALOGOS (CANVAS PROPIO O DIBUJADO AUTOMATICO EN ONGUI)
    // =========================================================================

    /// <summary>
    /// Si asignaste un Canvas propio en el Inspector (Apartado 1), aquí se actualizan sus Textos e Imágenes.
    /// </summary>
    private void ActualizarElementosCanvasPropio()
    {
        if (textoNombrePersonajeUI != null)
        {
            textoNombrePersonajeUI.text = nombreActualMostrado;
            textoNombrePersonajeUI.color = colorNombreActual;
        }

        if (textoContenidoDialogoUI != null)
        {
            textoContenidoDialogoUI.text = textoActualMostrado;
        }

        if (imagenIconoPersonajeCanvas != null)
        {
            if (iconoActualMostrado != null)
            {
                imagenIconoPersonajeCanvas.sprite = iconoActualMostrado;
                imagenIconoPersonajeCanvas.gameObject.SetActive(true);
            }
            else
            {
                imagenIconoPersonajeCanvas.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// AQUÍ SE GENERA EL DISEÑO AUTOMÁTICO EN PANTALLA (si no asignaste un Canvas en 'panelDialogoCanvas').
    /// Dibuja:
    /// 1) El fondo negro medio transparente en toda la pantalla.
    /// 2) El recuadro e icono/retrato del personaje que está hablando para que destaque.
    /// 3) La caja de texto inferior con el nombre del hablante y el contador de turnos.
    /// </summary>
    private void OnGUI()
    {
        if (!dialogoEnCurso || panelDialogoCanvas != null) return;
        if (indiceTurnoActual >= secuenciaActual.Count) return;

        Color colorOriginal = GUI.color;

        // ---------------------------------------------------------------------
        // 1. FONDO NEGRO MEDIO TRANSPARENTE EN TODA LA PANTALLA
        // Oscurece el juego detrás para que destaquen los iconos y el diálogo
        // ---------------------------------------------------------------------
        GUI.color = colorFondoPantallaOscura;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        // Dimensiones de la caja inferior de diálogo
        float anchoCaja = Mathf.Min(820f, Screen.width - 60f);
        float altoCaja = 165f;
        float posX = (Screen.width - anchoCaja) * 0.5f;
        float posY = Screen.height - altoCaja - 35f;

        // ---------------------------------------------------------------------
        // 2. CAJA PRINCIPAL DE DIALOGO
        // ---------------------------------------------------------------------
        GUI.color = colorCajaDeTexto;
        GUI.DrawTexture(new Rect(posX, posY, anchoCaja, altoCaja), Texture2D.whiteTexture);

        // Línea superior brillante con el color del personaje que habla
        GUI.color = colorNombreActual;
        GUI.DrawTexture(new Rect(posX, posY, anchoCaja, 4f), Texture2D.whiteTexture);

        // ---------------------------------------------------------------------
        // 3. ICONO / RETRATO DEL PERSONAJE DESTACADO
        // ---------------------------------------------------------------------
        float margenTextoIzquierdo = 26f;

        if (iconoActualMostrado != null && iconoActualMostrado.texture != null)
        {
            float xIcono = posX + 18f;
            float yIcono = posY + (altoCaja - tamanoIconoPersonaje) * 0.5f;

            // Marco oscuro detrás del retrato del personaje
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(xIcono - 3f, yIcono - 3f, tamanoIconoPersonaje + 6f, tamanoIconoPersonaje + 6f), Texture2D.whiteTexture);

            // Dibujar el Sprite del personaje
            GUI.color = Color.white;
            DibujarSpriteEnOnGUI(new Rect(xIcono, yIcono, tamanoIconoPersonaje, tamanoIconoPersonaje), iconoActualMostrado);

            margenTextoIzquierdo = tamanoIconoPersonaje + 38f;
        }

        GUI.color = colorOriginal;

        // ---------------------------------------------------------------------
        // 4. TEXTOS: NOMBRE DE QUIEN HABLA, MENSAJE E INDICADOR DE CONTINUAR
        // ---------------------------------------------------------------------
        GUIStyle estiloNombre = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        estiloNombre.normal.textColor = colorNombreActual;

        GUIStyle estiloTexto = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        estiloTexto.normal.textColor = Color.white;

        GUIStyle estiloContinuar = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.LowerRight
        };
        estiloContinuar.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);

        float anchoAreaTexto = anchoCaja - margenTextoIzquierdo - 24f;

        GUI.Label(new Rect(posX + margenTextoIzquierdo, posY + 16f, anchoAreaTexto, 28f), nombreActualMostrado, estiloNombre);
        GUI.Label(new Rect(posX + margenTextoIzquierdo, posY + 48f, anchoAreaTexto, 80f), textoActualMostrado, estiloTexto);
        GUI.Label(new Rect(posX + margenTextoIzquierdo, posY + altoCaja - 30f, anchoAreaTexto, 22f),
            $"Presiona [E] o [Espacio] para continuar ({indiceTurnoActual + 1}/{secuenciaActual.Count})",
            estiloContinuar);
    }

    /// <summary>
    /// Dibuja correctamente un Sprite de Unity dentro de un Rect de OnGUI (soportando Sprites individuales o recortados).
    /// </summary>
    private void DibujarSpriteEnOnGUI(Rect areaDestino, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;

        Texture2D tex = sprite.texture;
        Rect rectTex = sprite.textureRect;
        Rect uv = new Rect(
            rectTex.x / tex.width,
            rectTex.y / tex.height,
            rectTex.width / tex.width,
            rectTex.height / tex.height
        );

        GUI.DrawTextureWithTexCoords(areaDestino, tex, uv);
    }

    /// <summary>
    /// Detecta si el jugador presionó E, Espacio, Enter o Clic Izquierdo para pasar al siguiente turno del diálogo.
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
}
