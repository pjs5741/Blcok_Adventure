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
            // ������ ��ġ�� ī�޶� ��ģ���� ���� ��
            transform.localPosition = originalPos + Random.insideUnitSphere * shakeMagnitude;

            shakeDuration -= Time.deltaTime * dampingSpeed;
        }
        else
        {
            shakeDuration = 0f;
            transform.localPosition = originalPos; // ����ġ ����
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