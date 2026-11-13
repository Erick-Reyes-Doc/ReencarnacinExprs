using System.Collections.Generic;
using UnityEngine;

// Lista de todos los tipos de reliquia del juego. Vive en Resources para que el mapa y el combate lleguen a ella
// sin referencias de escena.
[CreateAssetMenu(fileName = "RelicCatalog", menuName = "Reencarnacion Express/Relic Catalog")]
public class RelicCatalog : ScriptableObject
{
    public const string ResourceName = "RelicCatalog";

    [SerializeField] private List<RelicData> relics = new List<RelicData>();

    [Tooltip("SOLO DESARROLLO: al empezar una partida nueva entrega una unidad de cada reliquia del catalogo (conjunto de prueba). Debe estar desactivado en el juego normal; solo se aplica en el editor y en builds de desarrollo.")]
    [SerializeField] private bool startWithTestSet = false;

    public bool StartWithTestSet => startWithTestSet;

    public IReadOnlyList<RelicData> Relics => relics;

    private static RelicCatalog instance;

    public static RelicCatalog Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<RelicCatalog>(ResourceName);

            return instance;
        }
    }

    public RelicData Find(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
            return null;

        foreach (RelicData relic in relics)
            if (relic != null && relic.typeId == typeId)
                return relic;

        return null;
    }

#if UNITY_EDITOR
    public void SetRelicsInEditor(List<RelicData> list)
    {
        relics = list;
    }
#endif
}
