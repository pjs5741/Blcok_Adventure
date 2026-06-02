using UnityEngine;

public abstract class Relic
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string IconResourcePath { get; }

    public Sprite GetIcon() => Resources.Load<Sprite>(IconResourcePath);

    public virtual void OnAcquire(PlayerStats stats) { }
    public virtual void OnRemove(PlayerStats stats) { }
}
