namespace Roadnest.Core
{
    public sealed class CharacterState
    {
        public readonly string Id;
        public readonly string TraitId;     // 예약 필드, 아직 효과 없음 / reserved, no effect yet
        public readonly int MaxHp;          // 캐릭터마다 다름 / differs per character
        public int Hp;

        public bool IsAlive => Hp > 0;

        public CharacterState(string id, string traitId, int maxHp) : this(id, traitId, maxHp, maxHp) { }

        public CharacterState(string id, string traitId, int maxHp, int hp)
        {
            Id = id; TraitId = traitId; MaxHp = maxHp; Hp = hp;
        }
    }
}