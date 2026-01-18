using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    // 싱글톤으로 만들어서 어디서든 부르기 쉽게 함
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
            // 랜덤한 위치로 카메라를 미친듯이 떨게 함
            transform.localPosition = originalPos + Random.insideUnitSphere * shakeMagnitude;

            shakeDuration -= Time.deltaTime * dampingSpeed;
        }
        else
        {
            shakeDuration = 0f;
            transform.localPosition = originalPos; // 원위치 복귀
        }
    }

    // 외부에서 이 함수를 호출하면 흔들림!
    public void TriggerShake(float duration, float magnitude)
    {
        shakeDuration = duration;
        shakeMagnitude = magnitude;
        originalPos = transform.localPosition; // 현재 위치 기준점 잡기
    }
}