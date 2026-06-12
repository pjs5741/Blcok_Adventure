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

        if (colorPool == null || colorPool.Count == 0)
        {
            colorPool = new List<ColorDefinition>
            {
                new ColorDefinition { id = 1, color = Color.red },
                new ColorDefinition { id = 2, color = Color.blue },
                new ColorDefinition { id = 3, color = Color.green },
                new ColorDefinition { id = 4, color = Color.yellow },
                new ColorDefinition { id = 5, color = new Color(0.6f, 0.2f, 0.9f) }
            };
        }
    }

    GameObject LoadBlock(string name)
    {
        GameObject prefab = Resources.Load<GameObject>($"Block/{name}")
                         ?? Resources.Load<GameObject>($"RewardBlock/{name}");
        if (prefab == null) Debug.LogError($"🚨 블록 못 찾음: {name}");
        return prefab;
    }


    // (앞부분 기존과 동일, SpawnBlock 함수만 교체)
    public void SpawnBlock()
    {
        if (blockDeck.Count == 0)
        {
            Debug.LogError("🚨 블록 덱이 비어있습니다!");
            return;
        }

        if (_shuffleQueue.Count == 0) RefillQueue();
        GameObject prefab = _shuffleQueue[0];
        _shuffleQueue.RemoveAt(0);

        // 중앙 위치 계산
        int spawnX = Mathf.RoundToInt(myGrid.data.width / 2f);
        int spawnY = myGrid.data.height;
        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0);

        GameObject newBlock = Instantiate(prefab, spawnPos, Quaternion.identity);

        // 연결
        BlockMovement movement = newBlock.GetComponent<BlockMovement>();
        movement.myGrid = myGrid;
        movement.mySpawner = this;

        // 색상 주입
        if (colorPool.Count > 0)
        {
            ColorDefinition blockColor = colorPool[Random.Range(0, colorPool.Count)];
            SpriteRenderer[] allRenderers = newBlock.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sr in allRenderers)
            {
                BlockColor blockInfo = sr.GetComponent<BlockColor>();
                if (blockInfo == null) blockInfo = sr.gameObject.AddComponent<BlockColor>();
                blockInfo.SetColorInfo(blockColor.id, blockColor.color);
            }
        }

        // ★ [핵심] 모든 세팅이 끝난 뒤에 초기화 실행! (에러 해결)
        movement.Initialize();
    }

    public GameObject SpawnStaticBlock(int x, int y, int colorID, Color color)
    {
        Vector3 spawnPos = new Vector3(x, y, 0);
        
        // 1. 생성
        GameObject newBlock = Instantiate(basicBlockPrefab, spawnPos, Quaternion.identity);
        
        // 2. 색칠 (BlockColor 스크립트 활용)
        BlockColor blockInfo = newBlock.GetComponent<BlockColor>();
        if (blockInfo == null) blockInfo = newBlock.AddComponent<BlockColor>();
        
        blockInfo.SetColorInfo(colorID, color); // 색상 주입

        // 3. (중요) 이 블록은 움직이면 안 되니 BlockMovement가 있다면 꺼버림
        if (newBlock.TryGetComponent(out BlockMovement movement))
        {
            Destroy(movement); // 혹은 movement.enabled = false;
        }

        return newBlock; // 만든 놈을 리턴해줌 (그리드에 넣어야 하니까)
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
        foreach (var c in colorPool)
            if (c.id == id) return c.color;
        return Color.white;
    }
    
    
}