using UnityEngine;

public abstract class Relic
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string IconResourcePath { get; }

    public Sprite GetIcon() => Resources.Load<Sprite>(IconResourcePath);

    public virtual void OnAcquire(PlayerStats stats) { }
    public virtual void OnRemove(PlayerStats stats) { }

    //--- 2026-07-01 세이브 로드 시 호출. 스탯 효과는 저장된 stats에 이미 반영되어 있으므로,
    // 이벤트 구독형 유물만 재구독하면 된다(기본은 no-op). OnAcquire를 그대로 재적용하면 스탯 이중 적용되므로 분리.
    public virtual void OnLoad(PlayerStats stats) { }
}
