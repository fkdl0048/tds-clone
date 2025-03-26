using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float climbSpeed = 3f;
    
    [Header("Raycast Points")]
    [SerializeField] private Transform headRayPoint;
    [SerializeField] private Transform footRayPoint;
    [SerializeField] private float rayDistance = 0.5f;
    [SerializeField] private LayerMask monsterLayerMask; // 이름 변경: 더 명확한 변수명
    
    [Header("Head Detection")]
    [SerializeField] private Transform headPosition; // 머리 위치
    [SerializeField] private Vector2 headTriggerSize = new Vector2(0.8f, 0.2f); // 머리 트리거 크기
    
    private bool isClimbing = false; 
    private Transform targetMonster;
    private Vector3 originalPosition;
    private Rigidbody2D rb;
    private bool hasFailedClimbing = false;
    private GameObject headTrigger; // 머리 트리거 게임오브젝트

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        
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
        // 레이캐스트 디버그 시각화
        Debug.DrawRay(headRayPoint.position, Vector2.left * rayDistance, Color.red);
        Debug.DrawRay(footRayPoint.position, Vector2.left * rayDistance, Color.green);
        
        // 올라가기 상태 처리
        if (isClimbing && targetMonster != null)
        {
            // 발 레이를 통해 계속 같은 몬스터를 감지하는지 확인
            RaycastHit2D footHit = Physics2D.Raycast(footRayPoint.position, Vector2.left, rayDistance, monsterLayerMask);
            
            // 발 레이가 대상 몬스터를 더 이상 감지하지 못하면 올라가기 취소 및 실패 처리
            if (footHit.collider == null || footHit.collider.transform != targetMonster)
            {
                Debug.Log("발 레이가 몬스터를 감지하지 못함: 올라가기 실패");
                isClimbing = false;
                targetMonster = null;
                hasFailedClimbing = true; // 올라가기 실패 플래그 설정
                return;
            }
            
            // 다른 몬스터 위로 올라가는 로직
            Vector3 targetPosition = new Vector3(
                targetMonster.position.x, 
                targetMonster.position.y + targetMonster.localScale.y, 
                transform.position.z
            );
            
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, climbSpeed * Time.deltaTime);
            
            // 목표 위치에 도달하면 올라가기 완료
            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                Debug.Log("올라가기 성공");
                isClimbing = false;
                targetMonster = null;
            }
        }
        else if (!hasFailedClimbing) // 올라가기 실패한 적이 없을 때만 확인
        {
            // 레이캐스트로 앞에 있는 몬스터 감지
            CheckForMonstersAhead();
        }
    }
    
    // 레이캐스트로 앞에 있는 몬스터 확인
    private void CheckForMonstersAhead()
    {
        if (isClimbing || hasFailedClimbing) return; // 이미 실패했거나 올라가는 중이면 확인하지 않음
        
        // 머리 레이캐스트 - 변수명 변경된 부분 적용
        RaycastHit2D headHit = Physics2D.Raycast(headRayPoint.position, Vector2.left, rayDistance, monsterLayerMask);
        
        // 발 레이캐스트 - 변수명 변경된 부분 적용
        RaycastHit2D footHit = Physics2D.Raycast(footRayPoint.position, Vector2.left, rayDistance, monsterLayerMask);
        
        // 머리 레이에 몬스터가 감지되고, 발 레이에도 같은 몬스터가 감지되면 올라가기 시작
        if (headHit.collider != null && footHit.collider != null && 
            headHit.collider.transform == footHit.collider.transform)
        {
            Monster otherMonster = headHit.collider.GetComponent<Monster>();
            if (otherMonster != null)
            {
                Debug.Log("앞에 있는 몬스터 발견: 올라가기 시작");
                isClimbing = true;
                targetMonster = headHit.collider.transform;
                originalPosition = transform.position;
            }
        }
    }
    
    // 물리 기반 이동을 위한 FixedUpdate 추가
    void FixedUpdate()
    {
        if (!isClimbing)
        {
            // 물리 기반 이동 (-x축 방향으로 이동)
            rb.velocity = new Vector2(-moveSpeed, rb.velocity.y);
        }
        else
        {
            // 올라갈 때는 물리 이동 중지
            rb.velocity = Vector2.zero;
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
