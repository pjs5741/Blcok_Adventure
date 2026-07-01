using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BlockSpawner : MonoBehaviour
{
    [Header("Block Deck (모양)")]
    //--- 2026-06-29 색 고정 덱빌딩으로 전환되며 미사용. 덱은 _deck(모양+색)으로 관리.
    // public List<GameObject> blockDeck = new List<GameObject>();
    public GameObject basicBlockPrefab;

    [Header("Color Pool (색상)")]
    // 인스펙터에서 이 리스트에 색깔을 추가/삭제하면 됨
    public List<ColorDefinition> colorPool;

    [Header("References")]
    public BlockGrid myGrid;

    //--- 2026-06-29 덱이 (모양+색) 카드 단위로 셔플됨. 색은 고정(스폰 시 랜덤색 폐기).
    private struct DeckCard { public GameObject prefab; public int colorID; }
    private List<DeckCard> _deck = new List<DeckCard>();         // 셔플백 원본
    private List<DeckCard> _shuffleQueue = new List<DeckCard>(); // 현재 셔플 큐

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
        //--- 2026-06-29 덱을 (모양+색)으로 로드. 색은 카드에 고정되어 있음(스폰 시 랜덤 X). 색값은 BlockColors 단일 소스.
        _deck.Clear();
        foreach (var entry in Run.deck)
        {
            GameObject prefab = LoadBlock(entry.blockName);
            if (prefab != null) _deck.Add(new DeckCard { prefab = prefab, colorID = entry.colorID });
        }
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
        if (_deck.Count == 0)
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
        if (_deck.Count == 0) return;   // 덱 비면 중단 (무한루프 방지)
        while (_previewBuffer.Count < PREVIEW_AHEAD + 1)
        {
            if (_shuffleQueue.Count == 0) RefillQueue();
            DeckCard card = _shuffleQueue[0];
            _shuffleQueue.RemoveAt(0);
            // 색은 카드에 고정 — 더 이상 랜덤 X
            _previewBuffer.Add(new NextBlock { prefab = card.prefab, colorID = card.colorID, color = BlockColors.Get(card.colorID) });
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

    //--- 2026-07-01 미리보기/홀드: 상수는 Tuning 단일 소스로 정리(스케일/간격만). 배치는 사용자 요구대로 고정.
    //  항상 미리보기=판 왼쪽 바깥, 홀드=판 오른쪽. y는 고정(PreviewY), x만 보드 폭 따라 변함. (판 뒤집혀도 위/아래로 안 감)
    private float MiniScale => Tuning.PreviewMiniScale;
    private float FrameSize => Tuning.PreviewFrameSize;
    private float PreviewStepDist => FrameSize + Tuning.PreviewFrameGap;      // 프레임 사이 간격(겹침 방지)

    Vector3 PreviewBase => new Vector3(-3.2f, Tuning.PreviewY, 0f);                     // 판 왼쪽 바깥(x 고정, y 고정)
    Vector3 PreviewStep => Vector3.down * PreviewStepDist;                              // 아래로 스택
    Vector3 HoldPos     => new Vector3(myGrid.data.width + 2.2f, Tuning.PreviewY, 0f);  // 판 오른쪽(x만 폭 따라, y 고정)

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
            Vector3 pos = PreviewBase + PreviewStep * i;
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
        f.transform.position = pos;
        f.transform.localScale = new Vector3(FrameSize, FrameSize, 1f);
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

        // 셀 평균 위치만큼 보정해 액자 중앙에 정렬 (모양마다 펼침 방향이 달라서)
        Vector3 childCenter = Vector3.zero; int n = 0;
        foreach (Transform child in g.transform) { childCenter += child.localPosition; n++; }
        if (n > 0) childCenter /= n;
        g.transform.position = pos - childCenter * MiniScale;

        foreach (var sr in g.GetComponentsInChildren<SpriteRenderer>())
        {
            BlockColor bc = sr.GetComponent<BlockColor>();
            if (bc == null) bc = sr.gameObject.AddComponent<BlockColor>();
            bc.SetColorInfo(nb.colorID, nb.color);
            Color c = sr.color; c.a = 1f; sr.color = c;
            sr.sortingOrder = 0;
        }

        // 호버 툴팁 (색/효과)
        var tip = g.AddComponent<WorldHoverTooltip>();
        tip.title = BlockColors.Name(nb.colorID);
        tip.body = BlockColors.Desc(nb.colorID);
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
        _shuffleQueue = new List<DeckCard>(_deck);
        for (int i = _shuffleQueue.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_shuffleQueue[i], _shuffleQueue[j]) = (_shuffleQueue[j], _shuffleQueue[i]);
        }
    }

    public void AddBlockToPool(GameObject prefab, int colorID)
    {
        DeckCard card = new DeckCard { prefab = prefab, colorID = colorID };
        _deck.Add(card);
        Run.deck.Add(new DeckEntry(prefab.name, colorID));
        // 현재 큐 랜덤 위치에 끼워넣어 곧 등장하도록
        int insertAt = Random.Range(0, _shuffleQueue.Count + 1);
        _shuffleQueue.Insert(insertAt, card);
    }

    public void RemoveBlockFromPool(GameObject prefab)
    {
        int i = _deck.FindIndex(c => c.prefab == prefab);
        if (i >= 0) _deck.RemoveAt(i);
    }

    //--- 2026-06-30 덱 뷰어용: 전체 덱 / 남은 드로우 더미(셔플큐+미리보기) 조회
    public List<(string shape, int color)> GetDeckAll()
    {
        var list = new List<(string, int)>();
        foreach (var c in _deck) if (c.prefab != null) list.Add((c.prefab.name, c.colorID));
        return list;
    }

    public List<(string shape, int color)> GetDrawPile()
    {
        var list = new List<(string, int)>();
        foreach (var c in _shuffleQueue) if (c.prefab != null) list.Add((c.prefab.name, c.colorID));
        foreach (var nb in _previewBuffer) if (nb.prefab != null) list.Add((nb.prefab.name, nb.colorID));
        return list;
    }

    public void AddColorToPool(int id, Color color)
    {
        colorPool.Add(new ColorDefinition { id = id, color = color });
    }

    public void RemoveColorFromPool(int id)
    {
        colorPool.RemoveAll(c => c.id == id);
    }

    public Color GetColorByID(int id) => BlockColors.Get(id);   //--- 2026-06-29 단일 소스로 위임
    
    
}