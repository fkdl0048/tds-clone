using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 2f; // 몬스터 이동 속도
    [SerializeField] private float climbSpeed = 3f; // 다른 몬스터 위로 올라가는 속도
    
    [Header("레이캐스트 설정")]
    [SerializeField] private Transform headRayPoint; // 머리 레이캐스트 시작점
    [SerializeField] private Transform footRayPoint; // 발 레이캐스트 시작점
    [SerializeField] private float rayDistance = 0.5f; // 레이 거리
    [SerializeField] private LayerMask monsterLayer; // 몬스터 레이어
    
    private bool isClimbing = false; // 현재 올라가고 있는지 상태
    private Transform targetMonster; // 올라갈 대상 몬스터
    private Vector3 originalPosition; // 원래 위치 저장
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0; // 중력 영향 제거
        }
        
        // 물리 충돌 처리를 위해 추가 설정
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        
        // 레이 포인트가 할당되지 않았을 경우 자동 생성
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
            RaycastHit2D footHit = Physics2D.Raycast(footRayPoint.position, Vector2.left, rayDistance, monsterLayer);
            
            // 발 레이가 대상 몬스터를 더 이상 감지하지 못하면 올라가기 취소
            if (footHit.collider == null || footHit.collider.transform != targetMonster)
            {
                Debug.Log("발 레이가 몬스터를 감지하지 못함: 올라가기 취소");
                isClimbing = false;
                targetMonster = null;
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
                isClimbing = false;
                targetMonster = null;
            }
        }
        else
        {
            // 레이캐스트로 앞에 있는 몬스터 감지
            CheckForMonstersAhead();
        }
    }
    
    // 레이캐스트로 앞에 있는 몬스터 확인
    private void CheckForMonstersAhead()
    {
        if (isClimbing) return;
        
        // 머리 레이캐스트
        RaycastHit2D headHit = Physics2D.Raycast(headRayPoint.position, Vector2.left, rayDistance, monsterLayer);
        
        // 발 레이캐스트
        RaycastHit2D footHit = Physics2D.Raycast(footRayPoint.position, Vector2.left, rayDistance, monsterLayer);
        
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
