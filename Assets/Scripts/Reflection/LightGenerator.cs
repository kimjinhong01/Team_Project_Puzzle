using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.HID;

public class LightGenerator : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private GameObject lightBeamPrefab; // Cylinder 프리팹
    [SerializeField] private LayerMask reflectableLayer;
    [SerializeField] private LayerMask blockLayer;
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private float contactTime = 2f;
    [SerializeField] private string targetTag = "TargetPoint";
    [SerializeField] private Color startLightColor;

    private List<GameObject> lightBeams = new List<GameObject>();
    
    private float contactStartTime = 0f;
    private bool isContactingTarget = false;
    private bool isClear = false;

    void Update()
    {
        GenerateLight();
    }

    void GenerateLight()
    {
        // 기존 빛 제거
        foreach (GameObject beam in lightBeams)
        {
            Destroy(beam);
        }
        lightBeams.Clear();

        Vector3 direction = transform.forward;
        Vector3 startPosition = transform.position;

        float currentLightDistance = 0f;        // 현재 빛의 사거리
        bool stillHittingTarget = false;        // 빛이 타겟을 컨택하고 있는 중인지 체크

        Color beamColor = startLightColor;        // 빛 색상 시작 색상으로 지정

        while (currentLightDistance < maxDistance)  // 최대사거리까지만 빛 기둥 생성
        {
            RaycastHit hit;

            // 지정한 레이어와 hit 되었을때
            if (Physics.Raycast(startPosition, direction, out hit, maxDistance - currentLightDistance, reflectableLayer | blockLayer))
            {
                float segmentLength = Vector3.Distance(startPosition, hit.point);
                CreateLightBeam(startPosition, hit.point, beamColor);
                currentLightDistance += segmentLength;

                // 목표지점 태그 되었을때
                if (hit.collider.CompareTag(targetTag))
                {
                    stillHittingTarget = true;

                    // 처음 컨택 되었을때
                    if (!isContactingTarget)
                    {
                        contactStartTime = Time.time;
                        isContactingTarget = true;
                        Debug.Log("목표에 접촉 시작!");
                    }
                    if (Time.time - contactStartTime >= contactTime)
                    {
                        OnLightHitTarget(hit, beamColor);
                        break;
                    }
                }

                // 거울과 접촉 시
                Mirror mirror = hit.collider.GetComponent<Mirror>();
                if (mirror != null)
                {
                    // 첫 반사에는 믹스가 안되게 함
                    if (beamColor == startLightColor)
                    {
                        // 첫 번째 반사일 경우, 거울 색상을 직접 적용
                        beamColor = mirror.GetMirrorColor();
                    }
                    else
                    {
                        // 이후 반사에는 2가지 색상 믹스
                        beamColor = mirror.MixColor(beamColor);
                    }

                    Vector3 newDirection;

                    // Reflect가 true 반환하면 반사, false면 x
                    if (mirror.Reflect(direction, hit.normal, out newDirection))
                    {
                        direction = newDirection;
                        startPosition = hit.point;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            // 그 외에 경우
            else
            {
                // 최대 사거리까지 빛 기둥 생성
                Vector3 endPosition = startPosition + direction * (maxDistance - currentLightDistance);
                CreateLightBeam(startPosition, endPosition, beamColor);
                break;
            }
        }

        if (!stillHittingTarget && isContactingTarget && !isClear)
        {
            Debug.Log("초기화 진행");
            contactStartTime = 0f;
            isContactingTarget = false;
        }
    }

    // 빛 기둥 생성
    void CreateLightBeam(Vector3 start, Vector3 end, Color beamColor)
    {
        if (isClear) return;

        GameObject beam = Instantiate(lightBeamPrefab);
        lightBeams.Add(beam);

        // 위치 설정
        beam.transform.position = (start + end) / 2;

        Vector3 direction = end - start;
        if (direction == Vector3.zero)
        {
            Debug.LogWarning("빛의 시작점과 끝점이 같아서 LookRotation 오류 방지");
            direction = Vector3.forward;
        }

        // 빛의 방향 설정
        Quaternion rotation = Quaternion.LookRotation(end - start);
        beam.transform.rotation = rotation * Quaternion.Euler(-90, 0, 0);

        // 크기 조정
        beam.transform.localScale = new Vector3(
            lightBeamPrefab.transform.localScale.x,   // 프리팹의 X 크기 유지
            (end - start).magnitude / 2,              // 길이 조정
            lightBeamPrefab.transform.localScale.z    // 프리팹의 Z 크기 유지
        );

        // 빛 기둥의 색상 결정
        Renderer beamRenderer = beam.GetComponent<Renderer>();
        if (beamRenderer != null)
        {
            beamRenderer.material.color = beamColor;
            beamRenderer.material.SetColor("_EmissionColor", beamColor * 2f);
        }
    }

    void ClearLightBeams()
    {
        foreach (GameObject beam in lightBeams)
        {
            if (beam != null)
            {
                StartCoroutine(FadeOutAndDestroy(beam));
            }
        }
        lightBeams.Clear();
    }

    IEnumerator FadeOutAndDestroy(GameObject beam)
    {
        Renderer beamRenderer = beam.GetComponent<Renderer>();
        if (beamRenderer == null) yield break;

        Material material = beamRenderer.material;
        Color startColor = material.color;
        Color startEmission = material.GetColor("_EmissionColor"); // 초기 Emission 색상 저장
        Vector3 startScale = beam.transform.localScale; // 초기 크기 저장
        float duration = 1.5f; // 서서히 사라지는 시간 (초)
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float fadeAmount = Mathf.Lerp(1f, 0f, elapsed / duration); // 1 → 0으로 점점 감소

            // 색상 투명도 적용
            material.color = new Color(startColor.r, startColor.g, startColor.b, fadeAmount);

            // Emission 강도 감소
            Color newEmission = startEmission * fadeAmount;
            material.SetColor("_EmissionColor", newEmission);

            beam.transform.localScale = new Vector3(
                startScale.x * fadeAmount,  // 가로 크기 줄이기
                startScale.y,               // 높이(Y)는 그대로 유지
                startScale.z * fadeAmount   // 세로 크기 줄이기
                );

            yield return null;
        }

        Destroy(beam); // 완전히 사라진 후 오브젝트 삭제
    }

    // 목표 지점 hit 되었을때
    void OnLightHitTarget(RaycastHit hit, Color beamColor)
    {
        TargetPoint target = hit.collider.GetComponent<TargetPoint>();
        if (target != null)
        {
            target.OnLightHit(beamColor);  // 목표지점에서 색상 일치 여부를 처리
            isClear = target.isTargetClear();

            if (isClear) 
            {
                ClearLightBeams();
            }
        }
    }
}
