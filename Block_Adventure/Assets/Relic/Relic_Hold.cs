public class Relic_Hold : Relic
{
    public override string Name => "예비 주머니";
    public override string Description => "C키로 블록을 보관했다 꺼낸다 (락 전 1회)";
    public override string IconResourcePath => "RelicIcons/Gladius";   // TODO: 전용 아이콘으로 교체

    // 효과는 BlockSpawner.HoldCurrent()에서 보유 여부로 처리 (stats 변경 없음)
}
