using UnityEngine;
using System.Collections;
// omg here too
public class CameraShake : MonoBehaviour
{
    // �̱������� ���� ��𼭵� �θ��� ���� ��
    public static CameraShake Instance;

    private Vector3 originalPos;
    private float shakeDuration = 0f;
    private float shakeMagnitude = 0.7f;
    private float dampingSpeed = 1.0f;

    void Awake()
    {
        Instance = this;
        originalPos = transform.localPosition;
    }

    void OnEnable()
    {
        originalPos = transform.localPosition;
    }

    void Update()
    {
        if (shakeDuration > 0)
        {
            transform.localPosition = originalPos + Random.insideUnitSphere * shakeMagnitude;

            //--- 2026-07-10 timeScale=0(컨텍스트 튜토리얼 일시정지) 중 셰이크가 영원히 안 끝나던 문제 → unscaled 시간 사용
            shakeDuration -= Time.unscaledDeltaTime * dampingSpeed;
        }
        else
        {
            shakeDuration = 0f;
            transform.localPosition = originalPos; // 원위치 복원
        }
    }

    // �ܺο��� �� �Լ��� ȣ���ϸ� ��鸲!
    public void TriggerShake(float duration, float magnitude)
    {
        shakeDuration = duration;
        shakeMagnitude = magnitude;
        originalPos = transform.localPosition; // ���� ��ġ ������ ���
    }
}