using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BlockSpawner : MonoBehaviour
{
    [Header("Block Deck (모양)")]
    public List<GameObject> blockDeck = new List<GameObject>();
    public GameObject basicBlockPrefab;

    [Header("Color Pool (색상)")]
    // 인스펙터에서 이 리스트에 색깔을 추가/삭제하면 됨
    public List<ColorDefinition> colorPool;

    [Header("References")]
    public BlockGrid myGrid;

    private List<GameObject> _shuffleQueue = new List<GameObject>();

    //--- 2026-06-23 미리보기 큐: 모양+색을 미리 확정해 둠 (천리안/홀드 유물 전제).
    // 색 랜덤성은 유지(결정 시점만 당겨짐). buffer[0] = 다음에 스폰될 블록.
    public struct NextBlock { public GameObject prefab; public int colorID; public Color color; }
    private List<NextBlock> _previewBuffer = new List<NextBlock>();
    private const int PREVIEW_AHEAD = 2;   // 미리 보여줄 블록 수

    private NextBlock _currentBlock;        // 지금 조작 중인 블록의 원본 정보 (홀드용)
    private BlockMovement _activeMovement;  // 지금 조작 중인 블록
    private NextBlock? _holdSlot;           // 홀드 슬롯 (비어있으면 null)
    private bool _holdUsedThisTurn;         // 이번 턴 홀드 사용 여부 (락 전 1회 제한)

    // 색상 정의용 구조체 (ID와 색상값 짝꿍)
    [System.Serializable]
    public struct ColorDefinition
    {
        public int id;      // 예: 1
        public Color color; // 예: 빨간색
    }

    void Awake()
    {
        if (!Run.IsInitialized) Run.StartNew();
        blockDeck.Clear();
        foreach (string name in Run.deckBlockNames)
        {
            GameObject prefab = LoadBlock(name);
            if (prefab != null) blockDeck.Add(prefab);
        }

        // 아이콘 풀 (id 1~5) — 1:칼, 2:분노, 3:독약, 4:방패, 5:폭탄
        if (colorPool == null) colorPool = new List<ColorDefinition>();
        colorPool.Clear();
        colorPool.Add(new ColorDefinition { id = 1, color = new Color(0.95f, 0.95f, 0.95f) });  // 칼 — 흰색 (garbage 회색과 구분)
        colorPool.Add(new ColorDefinition { id = 2, color = new Color(0.85f, 0.2f, 0.2f) });    // 분노 — 빨강
        colorPool.Add(new ColorDefinition { id = 3, color = new Color(0.5f, 0.2f, 0.7f) });     // 독약 — 보라
        colorPool.Add(new ColorDefinition { id = 4, color = new Color(0.2f, 0.5f, 0.85f) });    // 방패 — 파랑
        colorPool.Add(new ColorDefinition { id = 5, color = new Color(0.3f, 0.2f, 0.1f) });     // 폭탄 — 어두운 갈색
    }

    GameObject LoadBlock(string name)
    {
        GameObject prefab = Resources.Load<GameObject>($"Block/{name}")
                         ?? Resources.Load<GameObject>($"RewardBlock/{name}");
        if (prefab == null) Debug.LogError($"🚨 블록 못 찾음: {name}");
        return prefab;
    }


    // 턴 시작 시 호출 — 미리보기 버퍼에서 다음 블록을 꺼내 스폰
    public void SpawnBlock()
    {
        if (blockDeck.Count == 0)
        {
            Debug.LogError("🚨 블록 덱이 비어있습니다!");
            return;
        }

        _holdUsedThisTurn = false;   // 새 턴 → 홀드 다시 가능
        EnsureBuffer();
        NextBlock nb = _previewBuffer[0];
        _previewBuffer.RemoveAt(0);
        SpawnFrom(nb);
        RefreshPreviewUI();
    }

    // 미리보기 버퍼를 최소 (현재 1 + 미리보기 PREVIEW_AHEAD)개로 채움. 모양은 셔플백, 색은 랜덤으로 미리 확정.
    void EnsureBuffer()
    {
        while (_previewBuffer.Count < PREVIEW_AHEAD + 1)
        {
            if (_shuffleQueue.Count == 0) RefillQueue();
            GameObject prefab = _shuffleQueue[0];
            _shuffleQueue.RemoveAt(0);
            ColorDefinition cd = colorPool.Count > 0
                ? colorPool[Random.Range(0, colorPool.Count)]
                : new ColorDefinition { id = 1, color = Color.white };
            _previewBuffer.Add(new NextBlock { prefab = prefab, colorID = cd.id, color = cd.color });
        }
    }

    // 확정된 NextBlock 정보로 실제 블록 인스턴스 생성 + 색 주입 + 초기화
    void SpawnFrom(NextBlock nb)
    {
        int spawnX = Mathf.RoundToInt(myGrid.data.width / 2f);
        int spawnY = myGrid.data.height;
        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0);

        GameObject newBlock = Instantiate(nb.prefab, spawnPos, Quaternion.identity);

        BlockMovement movement = newBlock.GetComponent<BlockMovement>();
        movement.myGrid = myGrid;
        movement.mySpawner = this;

        SpriteRenderer[] allRenderers = newBlock.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in allRenderers)
        {
            BlockColor blockInfo = sr.GetComponent<BlockColor>();
            if (blockInfo == null) blockInfo = sr.gameObject.AddComponent<BlockColor>();
            blockInfo.SetColorInfo(nb.colorID, nb.color);
        }

        movement.Initialize();

        _currentBlock = nb;
        _activeMovement = movement;
    }

    // [홀드 유물] 현재 블록을 홀드 슬롯에 보관(또는 슬롯의 블록과 교환). 락 전 1회만.
    public void HoldCurrent()
    {
        if (!HasHoldRelic() || _holdUsedThisTurn || _activeMovement == null) return;

        Destroy(_activeMovement.gameObject);
        _activeMovement = null;
        NextBlock cur = _currentBlock;

        if (_holdSlot == null)
        {
            _holdSlot = cur;
            EnsureBuffer();
            NextBlock nb = _previewBuffer[0];
            _previewBuffer.RemoveAt(0);
            SpawnFrom(nb);
        }
        else
        {
            NextBlock held = _holdSlot.Value;
            _holdSlot = cur;
            SpawnFrom(held);
        }

        _holdUsedThisTurn = true;
        RefreshPreviewUI();
        RefreshHoldUI();
    }

    // [천리안 유물] 다음 N개 블록 미리보기 정보
    public List<NextBlock> PeekNext(int count)
    {
        EnsureBuffer();
        return _previewBuffer.GetRange(0, Mathf.Min(count, _previewBuffer.Count));
    }

    public NextBlock? HeldBlock => _holdSlot;

    bool HasHoldRelic() => Run.ownedRelics != null && Run.ownedRelics.Any(r => r is Relic_Hold);
    public bool HasClairvoyance() => Run.ownedRelics != null && Run.ownedRelics.Any(r => r is Relic_Clairvoyance);

    private List<GameObject> _previewVisuals = new List<GameObject>();
    private List<GameObject> _holdVisuals = new List<GameObject>();
    private static Sprite _boxSprite;

    // 미니 블록 표시 좌표 — 그리드 크기 기준 상대 위치 (Play 보며 오프셋만 조정)
    private const float PreviewGapY = 5f;   // 미리보기 세로 간격
    private const float MiniScale = 0.7f;
    private static readonly Vector3 FrameOffset = new Vector3(0.8f, 0.4f, 0); // 액자 중심(블록이 우상단으로 펼쳐지므로 보정)
    private static readonly Vector2 FrameSize = new Vector2(4.2f, 4.2f);      // 액자 크기
    Vector3 PreviewTop => new Vector3(-3.5f, myGrid.data.height - 4f, 0);                 // 다음 블록(그리드 왼쪽 바깥)
    Vector3 HoldPos    => new Vector3(myGrid.data.width + 1.5f, myGrid.data.height - 4f, 0); // 홀드(그리드 오른쪽 위)

    // 외부(피벗 등 그리드 변형)에서 미리보기/홀드 표시를 강제 갱신
    public void RefreshVisuals() { RefreshPreviewUI(); RefreshHoldUI(); }

    // [천리안] 다음 블록 PREVIEW_AHEAD개를 그리드 왼쪽에 미니 + 액자로 표시
    void RefreshPreviewUI()
    {
        foreach (var g in _previewVisuals) if (g != null) Destroy(g);
        _previewVisuals.Clear();
        if (!HasClairvoyance()) return;

        var nexts = PeekNext(PREVIEW_AHEAD);
        for (int i = 0; i < nexts.Count; i++)
        {
            Vector3 pos = PreviewTop + Vector3.down * (PreviewGapY * i);
            _previewVisuals.Add(MakeFrame(pos));
            _previewVisuals.Add(MakeMiniVisual(nexts[i], pos));
        }
    }

    // [홀드] 보관 중인 블록을 그리드 오른쪽 위에 미니 + 액자로 표시
    void RefreshHoldUI()
    {
        foreach (var g in _holdVisuals) if (g != null) Destroy(g);
        _holdVisuals.Clear();
        if (_holdSlot == null) return;
        _holdVisuals.Add(MakeFrame(HoldPos));
        _holdVisuals.Add(MakeMiniVisual(_holdSlot.Value, HoldPos));
    }

    // 미니 블록 뒤 액자(반투명 배경 박스)
    GameObject MakeFrame(Vector3 pos)
    {
        GameObject f = new GameObject("MiniFrame", typeof(SpriteRenderer));
        f.transform.position = pos + FrameOffset;
        f.transform.localScale = new Vector3(FrameSize.x, FrameSize.y, 1f);
        var sr = f.GetComponent<SpriteRenderer>();
        sr.sprite = GetBoxSprite();
        sr.color = new Color(0.08f, 0.08f, 0.12f, 0.7f);
        sr.sortingOrder = -5;   // 블록보다 뒤
        return f;
    }

    // 조작 불가 미니 블록 인스턴스 생성 (모양 그대로 재사용)
    GameObject MakeMiniVisual(NextBlock nb, Vector3 pos)
    {
        GameObject g = Instantiate(nb.prefab, pos, Quaternion.identity);
        g.transform.localScale = Vector3.one * MiniScale;
        if (g.TryGetComponent(out BlockMovement m)) Destroy(m);

        foreach (var sr in g.GetComponentsInChildren<SpriteRenderer>())
        {
            BlockColor bc = sr.GetComponent<BlockColor>();
            if (bc == null) bc = sr.gameObject.AddComponent<BlockColor>();
            bc.SetColorInfo(nb.colorID, nb.color);
            Color c = sr.color; c.a = 1f; sr.color = c;
            sr.sortingOrder = 0;
        }
        return g;
    }

    // 액자용 1x1 흰 사각 스프라이트 (코드 생성, 에셋 불필요)
    static Sprite GetBoxSprite()
    {
        if (_boxSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            _boxSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
        return _boxSprite;
    }

    public GameObject SpawnStaticBlock(int x, int y, int colorID, Color color)
    {
        Vector3 spawnPos = new Vector3(x, y, 0);

        GameObject newBlock = Instantiate(basicBlockPrefab, spawnPos, Quaternion.identity);

        BlockColor blockInfo = newBlock.GetComponent<BlockColor>();
        if (blockInfo == null) blockInfo = newBlock.AddComponent<BlockColor>();

        // Sprite icon = LoadIconSprite(colorID);
        blockInfo.SetColorInfo(colorID, color);

        if (newBlock.TryGetComponent(out BlockMovement movement))
            Destroy(movement);

        return newBlock;
    }

    // Resources/BlockIcons/ 폴더에서 아이콘 PNG 로드
    // 1=Sword, 2=Fist, 3=Potion, 4=Shield, 5=Bomb
    public Sprite LoadIconSprite(int iconID)
    {
        string name = iconID switch
        {
            1 => "Sword",
            2 => "Fire",
            3 => "Potion",
            4 => "Shield",
            5 => "Bomb",
            _ => null
        };
        if (name == null) return null;
        return Resources.Load<Sprite>($"BlockIcons/{name}");
    }
    
    void RefillQueue()
    {
        _shuffleQueue = new List<GameObject>(blockDeck);
        for (int i = _shuffleQueue.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_shuffleQueue[i], _shuffleQueue[j]) = (_shuffleQueue[j], _shuffleQueue[i]);
        }
    }

    public void AddBlockToPool(GameObject prefab)
    {
        blockDeck.Add(prefab);
        Run.deckBlockNames.Add(prefab.name);
        // 현재 큐 랜덤 위치에 끼워넣어 곧 등장하도록
        int insertAt = Random.Range(0, _shuffleQueue.Count + 1);
        _shuffleQueue.Insert(insertAt, prefab);
    }

    public void RemoveBlockFromPool(GameObject prefab)
    {
        blockDeck.Remove(prefab);
    }

    public void AddColorToPool(int id, Color color)
    {
        colorPool.Add(new ColorDefinition { id = id, color = color });
    }

    public void RemoveColorFromPool(int id)
    {
        colorPool.RemoveAll(c => c.id == id);
    }

    public Color GetColorByID(int id)
    {
        if (id == 99) return Color.gray;
        // if (id == 5) return new Color(0.15f, 0.15f, 0.15f); // 폭탄 임시색 (이제 풀의 갈색 사용)
        foreach (var c in colorPool)
            if (c.id == id) return c.color;
        return Color.white;
    }
    
    
}