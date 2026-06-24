public class Relic_Clairvoyance : Relic
{
    public override string Name => "천리안";
    public override string Description => "다음에 나올 블록 2개를 미리 본다";
    public override string IconResourcePath => "RelicIcons/Gladius";   // TODO: 전용 아이콘으로 교체

    // 효과는 BlockSpawner.HasClairvoyance() / 미리보기 UI에서 보유 여부로 처리 (stats 변경 없음)
}
