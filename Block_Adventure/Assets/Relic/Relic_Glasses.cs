public class Relic_Glasses : Relic
{
    public override string Name => "안경";
    public override string Description => "몬스터의 시야 가림(은폐)을 무효화한다";
    public override string IconResourcePath => "RelicIcons/Gladius";   // TODO: 전용 아이콘으로 교체

    // 효과는 BlockGrid.ApplyBlind()에서 보유 여부로 처리 (stats 변경 없음)
}
