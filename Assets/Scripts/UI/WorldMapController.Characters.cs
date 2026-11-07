using UnityEngine;

// Integracion de las fichas de personajes (CharacterPanelController) con el mapa. Mientras una ficha esta abierta el
// resto del mapa no admite seleccion de lugares, tienda, pausa ni inicio de combates.
public partial class WorldMapController
{
    [Header("Fichas de personajes")]
    [SerializeField] private CharacterPanelController characters;

    private string selectedHeroId;   // tarjeta resaltada en la barra inferior (solo una)

    public bool CharactersOpen => characters != null && characters.IsOpen;

    // Clic en una tarjeta de la barra inferior.
    public bool OpenCharacter(string unitId)
    {
        if (characters == null || loading || eventOpen || PauseOpen || (shop != null && shop.IsOpen))
            return false;

        confirmMenu = false;
        selectedHeroId = unitId;
        return characters.Open(unitId);
    }

    // Conecta las tarjetas del HUD con la ficha (los oyentes no se guardan en la escena).
    private void WireHeroCards()
    {
        foreach (HudHero hero in hudHeroes)
        {
            if (hero == null || hero.button == null)
                continue;

            string id = hero.unitId;
            hero.button.onClick.RemoveAllListeners();
            hero.button.onClick.AddListener(() => OpenCharacter(id));
        }
    }
}
