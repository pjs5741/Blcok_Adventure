using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BlockSpawner : MonoBehaviour
{
    [Header("Block Prefabs (모양)")]
    public GameObject[] tetrominoPrefabs; // 로드된 모양들

    [Header("Color Pool (색상)")]
    // 인스펙터에서 이 리스트에 색깔을 추가/삭제하면 됨
    public List<ColorDefinition> colorPool;

    [Header("References")]
    public BlockGrid myGrid;

    // 색상 정의용 구조체 (ID와 색상값 짝꿍)
    [System.Serializable]
    public struct ColorDefinition
    {
        public int id;      // 예: 1
        public Color color; // 예: 빨간색
    }

    void Awake()
    {
        // 1. 모양 프리팹 자동 로드 (기존 기능)
        GameObject[] allBlocks = Resources.LoadAll<GameObject>("Block");
        tetrominoPrefabs = allBlocks.Where(b => b.name.StartsWith("Block_")).ToArray();
    }

    void Start()
    {
        // 시작 시 테스트용 색상 데이터가 없으면 기본값 추가 (안전장치)
        if (colorPool == null || colorPool.Count == 0)
        {
            colorPool = new List<ColorDefinition>
            {
                new ColorDefinition { id = 1, color = Color.red },
                new ColorDefinition { id = 2, color = Color.blue },
                new ColorDefinition { id = 3, color = Color.green },
                new ColorDefinition { id = 4, color = Color.yellow }
            };
        }

        SpawnBlock();
    }

    // (앞부분 기존과 동일, SpawnBlock 함수만 교체)
    public void SpawnBlock()
    {
        if (tetrominoPrefabs.Length == 0)
        {
            Debug.LogError("🚨 로드된 블록이 없습니다! Resources/Block 폴더를 확인하세요.");
            return;
        }

        int randomIndex = Random.Range(0, tetrominoPrefabs.Length);

        // 중앙 위치 계산
        int spawnX = Mathf.RoundToInt(myGrid.data.width / 2f);
        int spawnY = myGrid.data.height;
        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0);

        GameObject newBlock = Instantiate(tetrominoPrefabs[randomIndex], spawnPos, Quaternion.identity);

        // 연결
        BlockMovement movement = newBlock.GetComponent<BlockMovement>();
        movement.myGrid = myGrid;
        movement.mySpawner = this;

        // 색상 주입
        if (colorPool.Count > 0)
        {
            SpriteRenderer[] allRenderers = newBlock.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sr in allRenderers)
            {
                ColorDefinition randomColor = colorPool[Random.Range(0, colorPool.Count)];
                BlockColor blockInfo = sr.GetComponent<BlockColor>();
                if (blockInfo == null) blockInfo = sr.gameObject.AddComponent<BlockColor>();
                blockInfo.SetColorInfo(randomColor.id, randomColor.color);
            }
        }

        // ★ [핵심] 모든 세팅이 끝난 뒤에 초기화 실행! (에러 해결)
        movement.Initialize();
    }

    // ★ [기능 추가] 게임 도중 색상 추가하고 싶을 때 호출
    public void AddColorToPool(int id, Color color)
    {
        colorPool.Add(new ColorDefinition { id = id, color = color });
    }

    // ★ [기능 추가] 게임 도중 색상 빼고 싶을 때 호출
    public void RemoveColorFromPool(int id)
    {
        colorPool.RemoveAll(c => c.id == id);
    }
}