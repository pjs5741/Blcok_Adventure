using UnityEngine;
using UnityEngine.UI;

//--- 2026-07-01 튜토리얼 오버레이(다중 페이지). 첫 전투에 1회 자동 표시 + 도움말 버튼으로 재열람.
// 텍스트 위주(이미지는 막판 리소스 작업). 조작/규칙 안내.
public class TutorialOverlay : MonoBehaviour
{
    static readonly (string title, string body)[] Pages =
    {
        ("목표",
         "블록 퍼즐 + 로그라이크입니다.\n\n" +
         "그리드가 곧 당신의 체력. 블록이 판 위로 넘쳐 쌓이면 게임 오버입니다.\n" +
         "블록을 지워 몬스터를 쓰러뜨리세요."),

        ("조작",
         "← →  :  좌우 이동\n" +
         "A / S :  회전\n" +
         "Space :  즉시 내려놓기\n" +
         "C  :  홀드 (예비주머니 유물 필요)"),

        ("공격 방법",
         "한 줄을 가득 채우면 그 줄이 사라져요 (줄 지우기). 지운 블록 수만큼 몬스터가 아파요!\n\n" +
         "같은 색 블록이 15개 이상 딱 붙으면 색 매칭! 그 블록들이 펑 터지고 훨씬 센 공격이 돼요.\n" +
         "색 매칭으로 블록이 사라지면, 위에 있던 블록들은 아래로 주르륵 떨어져요 (중력).\n\n" +
         "여러 줄을 한 번에 지우면 보너스 공격!"),

        ("블록 색 = 효과",
         "흰색(칼) : 기본 공격\n" +
         "빨강(화염) · 보라(독) : 매 턴 조금씩 계속 아프게 함\n" +
         "파랑(방패) : 몬스터 공격을 늦춤\n" +
         "갈색(폭탄) : 주변을 부숨 (폭탄으로 부순 블록은 효과가 안 나와요)\n\n" +
         "회색 : 방해 블록! 매칭으로는 못 없애요.\n" +
         "줄을 채워 지우거나 폭탄 등 다른 방법으로 없앨 수 있어요."),

        ("맵 & 덱",
         "맵에서 경로를 골라 진행합니다.\n" +
         "전투 보상으로 새 블록을 얻고, 상점/휴식에서 카드를 제거해 덱을 다듬으세요.\n" +
         "몬스터 머리 위 아이콘 = 다음 공격 예고 (숫자 = 남은 턴, 호버 시 설명)."),
    };

    private int _page;
    private GameObject _root;
    private Text _title, _body;
    private Button _prev, _next;

    // 첫 전투 등에서 1회만 자동 표시
    public static void ShowOnce(Transform canvas)
    {
        if (PlayerPrefs.GetInt("tutorialSeen", 0) != 0) return;
        PlayerPrefs.SetInt("tutorialSeen", 1);
        PlayerPrefs.Save();
        Create(canvas);
    }

    public static void Create(Transform canvas)
    {
        var go = new GameObject("Tutorial");
        go.AddComponent<TutorialOverlay>().Open(canvas);
    }

    public void Open(Transform canvas)
    {
        Build(canvas);
        Show(0);
    }

    void Build(Transform canvas)
    {
        _root = UIBuilder.NewUI("TutorialPanel", canvas, typeof(RectTransform), typeof(Image));
        UIBuilder.Stretch((RectTransform)_root.transform);
        _root.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.98f);

        _title = UIBuilder.Text(_root.transform, "Title", "", 60, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        UIBuilder.SetAnchors(_title.rectTransform, new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.92f));

        _body = UIBuilder.Text(_root.transform, "Body", "", 38, new Color(0.92f, 0.92f, 0.96f), TextAnchor.UpperLeft);
        _body.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIBuilder.SetAnchors(_body.rectTransform, new Vector2(0.15f, 0.28f), new Vector2(0.85f, 0.76f));

        _prev = UIBuilder.Button(_root.transform, "Prev", "이전", 34, () => Show(_page - 1), new Color(0.3f, 0.3f, 0.35f));
        UIBuilder.SetAnchors((RectTransform)_prev.transform, new Vector2(0.15f, 0.1f), new Vector2(0.35f, 0.18f));

        _next = UIBuilder.Button(_root.transform, "Next", "다음", 34, () => Show(_page + 1), new Color(0.25f, 0.4f, 0.55f));
        UIBuilder.SetAnchors((RectTransform)_next.transform, new Vector2(0.65f, 0.1f), new Vector2(0.85f, 0.18f));

        var close = UIBuilder.Button(_root.transform, "Close", "닫기", 34, Close, new Color(0.45f, 0.3f, 0.3f));
        UIBuilder.SetAnchors((RectTransform)close.transform, new Vector2(0.42f, 0.1f), new Vector2(0.58f, 0.18f));
    }

    void Show(int page)
    {
        _page = Mathf.Clamp(page, 0, Pages.Length - 1);
        _title.text = $"{Pages[_page].title}   ({_page + 1}/{Pages.Length})";
        _body.text = Pages[_page].body;
        _prev.interactable = _page > 0;
        _next.interactable = _page < Pages.Length - 1;
    }

    void Close()
    {
        if (_root != null) Destroy(_root);
        Destroy(gameObject);
    }
}
