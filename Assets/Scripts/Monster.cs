using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private float climbForce = 5f; // 올라갈 때 적용되는 힘
    [SerializeField] private float stabilizationForce = 2f; // 안정화 힘
    
    [Header("Raycast Points")]
    [SerializeField] private Transform headRayPoint;
    [SerializeField] private Transform footRayPoint;
    [SerializeField] private float rayDistance = 0.5f;
    [SerializeField] private LayerMask monsterLayerMask; // 이름 변경: 더 명확한 변수명
    
    [Header("Head Detection")]
    [SerializeField] private Transform headPosition; // 머리 위치
    [SerializeField] private Vector2 headTriggerSize = new Vector2(0.8f, 0.2f); // 머리 트리거 크기
    
    [Header("Physics Settings")]
    [SerializeField] private float rbMass = 1f; // Rigidbody의 질량
    [SerializeField] private float rbDrag = 0.5f; // Rigidbody의 드래그
    [SerializeField] private bool useAlternativeMethod = false; // 대체 이동 방법 사용 여부 (물리가 작동하지 않을 경우)
    
    private bool isClimbing = false; 
    private Transform targetMonster;
    private Vector3 originalPosition;
    private Rigidbody2D rb;
    private bool hasFailedClimbing = false;
    private GameObject headTrigger; // 머리 트리거 게임오브젝트
    private Vector2 climbTargetPosition; // 올라갈 목표 위치
    private bool isStabilizing = false; // 안정화 단계인지 여부

    // 디버그용 변수 추가
    private bool isDebugMode = true; // 디버그 모드 플래그
    private float debugLogInterval = 1f; // 디버그 로그 출력 간격
    private float lastDebugTime = 0f; // 마지막 디버그 로그 시간

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        
        // Rigidbody2D 설정 수정
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        //rb.gravityScale = 0f; // 중력 비활성화
        rb.mass = rbMass; // 질량 설정
        rb.drag = rbDrag; // 드래그 설정
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 회전만 제한
        rb.isKinematic = false; // 키네마틱 비활성화 (중요!)

        Debug.Log($"Rigidbody2D 설정: isKinematic={rb.isKinematic}, 질량={rb.mass}, " +
                 $"드래그={rb.drag}, 중력={rb.gravityScale}, 제약={rb.constraints}");
        
        if (headRayPoint == null)
        {
            GameObject headRayObj = new GameObject("HeadRayPoint");
            headRayPoint = headRayObj.transform;
            headRayPoint.parent = transform;
            headRayPoint.localPosition = new Vector3(-0.1f, 0.4f, 0);
        }
        
        if (footRayPoint == null)
        {
            GameObject footRayObj = new GameObject("FootRayPoint");
            footRayPoint = footRayObj.transform;
            footRayPoint.parent = transform;
            footRayPoint.localPosition = new Vector3(-0.1f, -0.4f, 0);
        }

        // 레이어 초기화 및 하위 스프라이트 레이어 통일
        InitializeLayers();
        
        // 머리 위치 트리거 생성
        CreateHeadTrigger();

        // 시작 시 상태 로그
        Debug.Log($"[시작] {gameObject.name} 초기화 완료: 레이어={LayerMask.LayerToName(gameObject.layer)}, 물리={rb != null}");
    }
    
    // 레이어 초기화 함수 - 기존 InitializeLayerMask를 확장
    private void InitializeLayers()
    {
        // 몬스터 자신의 레이어를 사용하여 레이어마스크 초기화
        int myLayer = gameObject.layer;
        monsterLayerMask = 1 << myLayer;
        Debug.Log($"{gameObject.name} 레이어: {LayerMask.LayerToName(myLayer)}, 레이어마스크: {monsterLayerMask.value}");
        
        // 하위 스프라이트들의 레이어를 자신과 동일하게 설정
        SetChildLayersRecursively(transform, myLayer);
    }
    
    // 자식 게임오브젝트들의 레이어를 재귀적으로 설정
    private void SetChildLayersRecursively(Transform parent, int targetLayer)
    {
        foreach (Transform child in parent)
        {
            // 스프라이트 렌더러가 있는지 확인
            SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                // 스프라이트 오브젝트의 레이어를 몬스터와 동일하게 설정
                spriteRenderer.sortingLayerName = LayerMask.LayerToName(targetLayer);
                Debug.Log($"스프라이트 '{child.name}'의 레이어를 '{LayerMask.LayerToName(targetLayer)}'로 설정");
            }
            
            // 자식 오브젝트가 있다면 재귀적으로 처리
            if (child.childCount > 0)
            {
                SetChildLayersRecursively(child, targetLayer);
            }
        }
    }

    // 머리 위치에 트리거 콜라이더 생성
    private void CreateHeadTrigger()
    {
        // 머리 위치가 할당되지 않았으면 생성
        if (headPosition == null)
        {
            GameObject headObj = new GameObject("HeadPosition");
            headPosition = headObj.transform;
            headPosition.parent = transform;
            headPosition.localPosition = new Vector3(0, 0.5f, 0); // 몬스터 머리 위치 (위쪽)
        }
        
        // 머리 트리거 생성
        headTrigger = new GameObject("HeadTrigger");
        headTrigger.transform.parent = headPosition;
        headTrigger.transform.localPosition = Vector3.zero;
        
        // 트리거 콜라이더 추가
        BoxCollider2D triggerCollider = headTrigger.AddComponent<BoxCollider2D>();
        triggerCollider.size = headTriggerSize;
        triggerCollider.isTrigger = true; // 트리거로 설정
        
        // 트리거 감지용 스크립트 추가
        HeadTrigger headTriggerScript = headTrigger.AddComponent<HeadTrigger>();
        headTriggerScript.Initialize(this);
    }

    // 머리 위에 다른 몬스터가 밟았을 때 호출되는 함수
    public void DisableClimbingAbility()
    {
        hasFailedClimbing = true;
        Debug.Log(gameObject.name + ": 머리 위에 다른 몬스터가 올라옴 - 올라가기 기능 비활성화");
    }

    void Update()
    {
        // 주기적인 디버깅 로그
        if (isDebugMode && Time.time - lastDebugTime > debugLogInterval)
        {
            lastDebugTime = Time.time;
            DebugMonsterStatus();
        }

        // 레이캐스트 디버그 시각화
        Debug.DrawRay(headRayPoint.position, Vector2.left * rayDistance, Color.red);
        Debug.DrawRay(footRayPoint.position, Vector2.left * rayDistance, Color.green);
        
        // 올라가기 상태 확인 및 레이캐스트 검사는 Update에서 유지
        if (isClimbing && targetMonster != null)
        {
            // 발 레이를 통해 계속 같은 몬스터를 감지하는지 확인
            RaycastHit2D footHit = Physics2D.Raycast(footRayPoint.position, Vector2.left, rayDistance, monsterLayerMask);
            
            // 디버그: 레이캐스트 결과 로깅
            if (isDebugMode && Time.time - lastDebugTime > debugLogInterval)
            {
                Debug.Log($"올라가는 중 레이캐스트: footHit={footHit.collider != null}, 타겟과 일치={footHit.collider?.transform == targetMonster}");
            }
            
            if (footHit.collider == null || footHit.collider.transform != targetMonster)
            {
                Debug.Log("발 레이가 몬스터를 감지하지 못함: 올라가기 실패");
                CancelClimbing(true);
                return;
            }
            
            // 목표 위치 계산 (실제 이동은 FixedUpdate에서 수행)
            climbTargetPosition = new Vector2(
                targetMonster.position.x, 
                targetMonster.position.y + targetMonster.localScale.y + 0.1f // 약간 위로 더 올라가도록 0.1f 추가
            );

            // 디버그: 목표 위치와 현재 위치 차이 로깅
            if (isDebugMode && Time.time - lastDebugTime > debugLogInterval)
            {
                float distance = Vector2.Distance(rb.position, climbTargetPosition);
                Debug.Log($"목표 위치까지 거리: {distance:F2}, 현재 상태: isStabilizing={isStabilizing}");
            }

            // 목표 위치에 충분히 가까워졌으면 안정화 단계로 전환
            if (!isStabilizing && Vector2.Distance(rb.position, climbTargetPosition) < 0.3f)
            {
                isStabilizing = true;
                Debug.Log("안정화 단계 시작");
            }
        }
        else if (!hasFailedClimbing)
        {
            CheckForMonstersAhead();
        }
    }
    
    // 디버그용 상태 출력 함수
    private void DebugMonsterStatus()
    {
        string status = $"[상태] {gameObject.name}: " +
                      $"위치=({transform.position.x:F1}, {transform.position.y:F1}), " +
                      $"속도=({rb.velocity.x:F1}, {rb.velocity.y:F1}), " +
                      $"올라가기={isClimbing}, " +
                      $"안정화={isStabilizing}, " +
                      $"실패={hasFailedClimbing}";
        Debug.Log(status);
    }
    
    // 올라가기 취소 함수
    private void CancelClimbing(bool markAsFailed = false)
    {
        isClimbing = false;
        isStabilizing = false;
        targetMonster = null;
        if (markAsFailed)
        {
            hasFailedClimbing = true;
        }
    }

    // 물리 기반 이동을 위한 FixedUpdate
    void FixedUpdate()
    {
        if (!isClimbing)
        {
            // 기본 이동 전 로그
            if (isDebugMode && Time.frameCount % 30 == 0) // 30프레임마다 로그 출력 (너무 많은 로그 방지)
            {
                Debug.Log($"기본 이동 적용 전: 속도=({rb.velocity.x:F1}, {rb.velocity.y:F1})");
            }
            
            // 기본 이동
            rb.velocity = new Vector2(-moveSpeed, rb.velocity.y);
            
            // 기본 이동 후 로그
            if (isDebugMode && Time.frameCount % 30 == 0)
            {
                Debug.Log($"기본 이동 적용 후: 속도=({rb.velocity.x:F1}, {rb.velocity.y:F1})");
            }
        }
        else if (targetMonster != null)
        {
            // 올라가기 물리 처리 전 로그
            if (isDebugMode && Time.frameCount % 10 == 0) // 10프레임마다 로그 출력
            {
                Debug.Log($"올라가기 물리 처리 시작: isStabilizing={isStabilizing}, 속도=({rb.velocity.x:F1}, {rb.velocity.y:F1})");
            }
            
            // 올라가기 물리 처리
            if (isStabilizing)
            {
                // 안정화 단계 - 정확한 위치에 부드럽게 정착
                ApplyStabilizationForces();
                
                // 충분히 안정되었으면 올라가기 완료 로그
                if (Mathf.Abs(rb.velocity.magnitude) < 0.1f && 
                    Vector2.Distance(rb.position, climbTargetPosition) < 0.1f)
                {
                    Debug.Log("올라가기 성공: 속도와 위치가 안정됨");
                    rb.velocity = Vector2.zero; // 속도 초기화
                    rb.position = climbTargetPosition; // 최종 위치 설정
                    CancelClimbing();
                }
            }
            else
            {
                // 올라가는 단계 - 물리력 적용
                if (!useAlternativeMethod)
                {
                    ApplyClimbingForces();
                    
                    // 일정 시간 후에도 움직이지 않는지 확인
                    if (rb.velocity.magnitude < 0.1f && Time.frameCount % 20 == 0)
                    {
                        Debug.LogWarning("물리 이동이 작동하지 않음: 강제 위치 이동 시도");
                        useAlternativeMethod = true;
                    }
                }
                else
                {
                    // 대체 방법: 직접 위치 이동
                    Vector2 moveStep = Vector2.MoveTowards(
                        rb.position, 
                        climbTargetPosition, 
                        climbSpeed * Time.fixedDeltaTime
                    );
                    rb.MovePosition(moveStep);
                    
                    if (isDebugMode && Time.frameCount % 10 == 0)
                    {
                        Debug.Log($"대체 이동 방법 사용 중: 현재={rb.position}, 목표={climbTargetPosition}");
                    }
                }
            }
            
            // 올라가기 물리 처리 후 로그
            if (isDebugMode && Time.frameCount % 10 == 0)
            {
                Debug.Log($"올라가기 물리 처리 후: 속도=({rb.velocity.x:F1}, {rb.velocity.y:F1}), 목표까지 거리={Vector2.Distance(rb.position, climbTargetPosition):F2}");
            }
        }
    }
    
    // 올라가기 위한 물리력 적용
    private void ApplyClimbingForces()
    {
        // 현재 위치에서 목표 위치로 향하는 벡터 계산
        Vector2 direction = (climbTargetPosition - rb.position).normalized;
        
        // 적절한 힘을 계산 (거리에 비례) - 힘 크게 증가
        //float distance = Vector2.Distance(rb.position, climbTargetPosition);
        Vector2 force = direction * climbForce * 1; // 힘을 5배로 증가
        
        // 힘 적용 로그
        if (isDebugMode && Time.frameCount % 10 == 0)
        {
            Debug.Log($"올라가기 힘 적용: 방향=({direction.x:F1}, {direction.y:F1}), 힘 크기={force.magnitude:F1}");
        }
        
        // 이전 속도 저장
        Vector2 prevVelocity = rb.velocity;
        
        // 힘 적용 방법 변경
        rb.AddForce(force, ForceMode2D.Impulse); // Force 대신 Impulse 사용
        
        // 속도 확인 로그
        if (isDebugMode && Mathf.Approximately(rb.velocity.magnitude, prevVelocity.magnitude) && Time.frameCount % 10 == 0)
        {
            Debug.LogWarning($"물리 문제: 힘 적용 전후 속도가 동일함 - 전:{prevVelocity}, 후:{rb.velocity}");
            // 물리 설정 확인
            Debug.LogWarning($"물리 설정: isKinematic={rb.isKinematic}, 질량={rb.mass}, 중력={rb.gravityScale}, 제약={rb.constraints}");
        }
    }
    
    // 안정화를 위한 물리력 적용
    private void ApplyStabilizationForces()
    {
        // 정확한 위치로 천천히 이동
        Vector2 positionDifference = climbTargetPosition - rb.position;
        rb.AddForce(positionDifference * stabilizationForce, ForceMode2D.Force);
        
        // 속도 감쇠 (부드러운 정지를 위함)
        rb.velocity *= 0.9f;
    }
    
    // 레이캐스트로 앞에 있는 몬스터 확인
    private void CheckForMonstersAhead()
    {
        if (isClimbing || hasFailedClimbing) return; // 이미 실패했거나 올라가는 중이면 확인하지 않음
        
        // 머리 레이캐스트 - 변수명 변경된 부분 적용
        RaycastHit2D headHit = Physics2D.Raycast(headRayPoint.position, Vector2.left, rayDistance, monsterLayerMask);
        
        // 발 레이캐스트 - 변수명 변경된 부분 적용
        RaycastHit2D footHit = Physics2D.Raycast(footRayPoint.position, Vector2.left, rayDistance, monsterLayerMask);
        
        // 레이캐스트 결과 로깅 (매 프레임이 아닌 주기적으로)
        if (isDebugMode && Time.frameCount % 30 == 0)
        {
            Debug.Log($"레이캐스트 검사: headHit={headHit.collider != null}, footHit={footHit.collider != null}, " +
                     $"레이어마스크={monsterLayerMask.value}, 현재 레이어={gameObject.layer}");
            
            if (headHit.collider != null)
            {
                Debug.Log($"머리 레이 감지: {headHit.collider.name}, 레이어={headHit.collider.gameObject.layer}");
            }
            
            if (footHit.collider != null)
            {
                Debug.Log($"발 레이 감지: {footHit.collider.name}, 레이어={footHit.collider.gameObject.layer}");
            }
        }
        
        // 머리 레이에 몬스터가 감지되고, 발 레이에도 같은 몬스터가 감지되면 올라가기 시작
        if (headHit.collider != null && footHit.collider != null && 
            headHit.collider.transform == footHit.collider.transform)
        {
            Monster otherMonster = headHit.collider.GetComponent<Monster>();
            if (otherMonster != null)
            {
                Debug.Log("앞에 있는 몬스터 발견: 올라가기 시작");
                isClimbing = true;
                isStabilizing = false;
                useAlternativeMethod = false; // 새 시도에서는 우선 물리 이동 시도
                targetMonster = headHit.collider.transform;
                originalPosition = transform.position;
                
                // 올라가기 시작 시 물리 설정 - 중요 수정
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.Sleep(); // 물리 시스템 리셋
                rb.WakeUp(); // 다시 활성화
                
                // 충돌 문제 확인을 위한 로그
                Collider2D[] overlaps = new Collider2D[5];
                int count = Physics2D.OverlapBoxNonAlloc(transform.position, GetComponent<Collider2D>().bounds.size, 0, overlaps);
                if (count > 1)
                {
                    Debug.LogWarning($"충돌 감지: {count}개의 콜라이더와 겹쳐 있음");
                    for (int i = 0; i < count; i++)
                    {
                        if (overlaps[i] != GetComponent<Collider2D>())
                        {
                            Debug.LogWarning($"- {overlaps[i].name} (레이어: {LayerMask.LayerToName(overlaps[i].gameObject.layer)})");
                        }
                    }
                }
            }
        }
    }
}

// 머리 트리거를 감지하는 보조 클래스
public class HeadTrigger : MonoBehaviour
{
    private Monster parentMonster;
    private int parentLayer; // 부모 몬스터의 레이어 저장
    
    public void Initialize(Monster monster)
    {
        parentMonster = monster;
        parentLayer = parentMonster.gameObject.layer;
        Debug.Log($"HeadTrigger 초기화: 부모 레이어 = {LayerMask.LayerToName(parentLayer)}");
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // 같은 레이어에 속한 몬스터인지 먼저 확인
        if (other.gameObject.layer != parentLayer)
        {
            // 다른 레이어의 객체는 무시
            return;
        }
        
        // 다른 몬스터가 머리 위에 올라왔는지 확인
        Monster otherMonster = other.GetComponent<Monster>();
        if (otherMonster != null && otherMonster != parentMonster)
        {
            Debug.Log($"{otherMonster.name}(레이어: {LayerMask.LayerToName(other.gameObject.layer)})가 {parentMonster.name}의 머리 위에 올라옴");
            // 부모 몬스터의 올라가기 기능 비활성화
            parentMonster.DisableClimbingAbility();
        }
    }
}
