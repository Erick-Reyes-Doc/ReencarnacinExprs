using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Sonido global al pasar el cursor sobre cualquier boton del juego (menus, mapa, combate, pausa...). Un unico objeto persistente
// (se crea solo al iniciar el juego desde el prefab Resources/UiHoverSound) vigila sobre que boton esta el cursor y, al entrar en
// uno nuevo que este activo, reproduce el sonido. Funciona tambien con botones creados en tiempo de ejecucion.
public class UiHoverSound : MonoBehaviour
{
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [Tooltip("Sonido de una compra valida en la tienda (monedas que se gastan). Vacio = sin sonido.")]
    [SerializeField] private AudioClip purchaseClip;
    [SerializeField, Range(0f, 1f)] private float purchaseVolume = 0.7f;
    [Tooltip("Sonido de una venta valida en la tienda (monedas que se reciben). Vacio = sin sonido.")]
    [SerializeField] private AudioClip saleClip;
    [SerializeField, Range(0f, 1f)] private float saleVolume = 0.7f;
    [Tooltip("Sonido de equipar una reliquia. Vacio = sin sonido.")]
    [SerializeField] private AudioClip equipClip;
    [SerializeField, Range(0f, 1f)] private float equipVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.65f;

    private static UiHoverSound instance;
    private bool wasPressed;

    private AudioSource source;
    private Component lastButton;
    private Vector2 lastMouse = new Vector2(float.NaN, float.NaN);
    private readonly List<RaycastResult> hits = new List<RaycastResult>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
            return;

        GameObject prefab = Resources.Load<GameObject>("UiHoverSound");

        if (prefab == null)
            return;

        GameObject go = Instantiate(prefab);
        go.name = "UiHoverSound";
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Se cargan los datos de audio desde el inicio: si no, el primer uso espera a la carga y el sonido sale tarde o no sale.
        if (hoverClip != null) hoverClip.LoadAudioData();
        if (clickClip != null) clickClip.LoadAudioData();
        if (purchaseClip != null) purchaseClip.LoadAudioData();
        if (saleClip != null) saleClip.LoadAudioData();
        if (equipClip != null) equipClip.LoadAudioData();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        GameSession.PurchaseCompleted += OnPurchaseCompleted;
        GameSession.SaleCompleted += OnSaleCompleted;
    }

    // Equipar una reliquia en la ficha del personaje (lo llama CharacterPanelController al equipar con exito).
    public static void PlayEquip()
    {
        if (instance != null && instance.equipClip != null && instance.source != null)
            instance.source.PlayOneShot(instance.equipClip, instance.equipVolume);
    }

    private void OnSaleCompleted()
    {
        if (saleClip != null && source != null)
            source.PlayOneShot(saleClip, saleVolume);
    }

    private void OnPurchaseCompleted()
    {
        if (purchaseClip != null && source != null)
            source.PlayOneShot(purchaseClip, purchaseVolume);
    }

    private void OnDestroy()
    {
        GameSession.PurchaseCompleted -= OnPurchaseCompleted;
        GameSession.SaleCompleted -= OnSaleCompleted;

        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        EventSystem es = EventSystem.current;
        Mouse mouse = Mouse.current;

        if (es == null || mouse == null || hoverClip == null)
        {
            lastButton = null;
            return;
        }

        Vector2 pos = mouse.position.ReadValue();

        // Clic: suena al presionar sobre un boton activo (se mira en ese instante, antes de que el propio boton cambie la interfaz).
        bool pressedNow = mouse.leftButton.isPressed;
        bool pressedEdge = pressedNow && !wasPressed;
        wasPressed = pressedNow;

        if (clickClip != null && pressedEdge)
        {
            Component pressed = ButtonUnder(es, pos);

            if (IsPlayable(pressed))
            {
                // Un boton puede traer su propio sonido de clic (p. ej. seleccion de habilidad).
                ClickSoundOverride own = pressed.GetComponent<ClickSoundOverride>();

                if (own != null && own.Clip != null)
                    source.PlayOneShot(own.Clip, own.Volume);
                else
                    source.PlayOneShot(clickClip, clickVolume);
            }
        }

        // Hover: solo se vuelve a comprobar cuando el cursor se mueve (o la interfaz cambia bajo el, cada cierto tiempo).
        if (pos == lastMouse && Time.frameCount % 15 != 0)
            return;

        lastMouse = pos;

        Component button = ButtonUnder(es, pos);
        if (button == lastButton)
            return;

        lastButton = button;

        MapHoverSoundGate gate = button != null ? button.GetComponent<MapHoverSoundGate>() : null;

        if (IsPlayable(button) && (gate == null || !gate.silent))
            source.PlayOneShot(hoverClip, volume);
    }

    // El primer resultado del raycast es lo que realmente esta bajo el cursor (lo de encima tapa lo de debajo).
    // Devuelve un Button o, si no hay, una zona marcada con UiSoundZone (habilidades y casillas de reliquia de la ficha).
    private Component ButtonUnder(EventSystem es, Vector2 pos)
    {
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = pos }, hits);

        if (hits.Count == 0)
            return null;

        GameObject top = hits[0].gameObject;
        Button button = top.GetComponentInParent<Button>();

        if (button != null)
            return button;

        return top.GetComponentInParent<UiSoundZone>();
    }

    // Los botones con sonidos propios (menu principal) suenan por su cuenta: aqui se omiten.
    private static bool IsPlayable(Component target)
    {
        if (target == null)
            return false;

        Button button = target as Button;

        if (button != null)
            return button.IsInteractable() && button.GetComponent<MenuButtonSound>() == null;

        return target is UiSoundZone;   // las zonas marcadas siempre suenan mientras esten activas (el raycast ya las ignora si no)
    }
}
