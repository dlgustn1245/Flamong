using UnityEngine;

// 진화 그림 120장과 「종 · 레벨 → 그림」. 이 에셋을 가리키는 씬은 120장을 모두 메모리에 올린다
// (Lv1 만 쓰는 Hatch 는 가리키지 않고 자기 24장을 갖는다)
[CreateAssetMenu(menuName = "Flamong/Character Sprites")]
public class CharacterSprites : ScriptableObject
{
    public const int levels = 5;

    public Sprite[] sprites;   // [speciesId * 5 + level - 1]

    public int SpeciesCount => sprites.Length / levels;

    // 캐릭터가 아직 없으면(speciesId -1) null
    public Sprite Get(int speciesId, int level)
    {
        int idx = speciesId * levels + level - 1;
        return idx >= 0 && idx < sprites.Length ? sprites[idx] : null;
    }
}