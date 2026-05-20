using UnityEngine;

public class TorchFlicker : MonoBehaviour
{
    private Light torchLight;
    public float minIntensity = 1.0f;
    public float maxIntensity = 2.5f;
    public float speed = 5.0f; // 일렁이는 속도

    void Start()
    {
        torchLight = GetComponent<Light>();
    }

    void Update()
    {
        // Random.Range보다 Mathf.PerlinNoise를 쓰면 부드러운 불꽃 흔들림이 연출됩니다.
        float noise = Mathf.PerlinNoise(Time.time * speed, 0.0f);
        torchLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}