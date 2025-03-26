using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float detectionDistance = 1f;
    [SerializeField] private float climbSpeed = 3f;  // 몬스터 위로 올라가는 속도
    [SerializeField] private float climbOverSpeed = 2f;  // 몬스터 위에서 내려가는 속도
    
    // 레이캐스트 포인트들
    [SerializeField] private Transform headRayPoint;
    [SerializeField] private Transform bodyRayPoint;
    [SerializeField] private Transform feetRayPoint;
    
    private Rigidbody2D rb;
    private bool isJumping = false;
    private bool isClimbing = false;
    private GameObject targetMonster;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        if (rb == null)
        {
            Debug.LogWarning("No Rigidbody found on " + gameObject.name + ". Adding one.");
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        
        rb.gravityScale = 1f;
        
        // 레이캐스트 포인트가 없으면 생성
        if (headRayPoint == null || bodyRayPoint == null || feetRayPoint == null)
        {
            SetupRaycastPoints();
        }
    }

    private void SetupRaycastPoints()
    {
        // 몬스터의 크기 정보
        Collider2D collider = GetComponent<Collider2D>();
        float height = collider ? collider.bounds.size.y : 1f;
        
        // 레이캐스트 포인트 생성 및 설정
        if (headRayPoint == null)
        {
            GameObject headObj = new GameObject("HeadRayPoint");
            headObj.transform.parent = transform;
            headObj.transform.localPosition = new Vector3(0, height * 0.8f, 0);
            headRayPoint = headObj.transform;
        }
        
        if (bodyRayPoint == null)
        {
            GameObject bodyObj = new GameObject("BodyRayPoint");
            bodyObj.transform.parent = transform;
            bodyObj.transform.localPosition = new Vector3(0, height * 0.5f, 0);
            bodyRayPoint = bodyObj.transform;
        }
        
        if (feetRayPoint == null)
        {
            GameObject feetObj = new GameObject("FeetRayPoint");
            feetObj.transform.parent = transform;
            feetObj.transform.localPosition = new Vector3(0, height * 0.1f, 0);
            feetRayPoint = feetObj.transform;
        }
    }

    private void FixedUpdate()
    {
        if (!isJumping && !isClimbing)
        {
            MultipleRaycastDetection();
        }
        
        // 점프/등반 중이 아닐 때만 기본 움직임 적용
        if (!isClimbing)
        {
            Vector2 movement = new Vector2(-1, 0);
            rb.velocity = new Vector2(movement.x * moveSpeed, rb.velocity.y);
        }
    }
    
    private void MultipleRaycastDetection()
    {
        // 세 개의 레이캐스트로 앞에 있는 몬스터 감지
        RaycastHit2D headHit = Physics2D.Raycast(
            headRayPoint.position, 
            Vector2.left, 
            detectionDistance, 
            LayerMask.GetMask("Monster")
        );
        
        RaycastHit2D bodyHit = Physics2D.Raycast(
            bodyRayPoint.position, 
            Vector2.left, 
            detectionDistance, 
            LayerMask.GetMask("Monster")
        );
        
        RaycastHit2D feetHit = Physics2D.Raycast(
            feetRayPoint.position, 
            Vector2.left, 
            detectionDistance, 
            LayerMask.GetMask("Monster")
        );
        
        // 디버그 레이 그리기
        Debug.DrawRay(headRayPoint.position, Vector2.left * detectionDistance, Color.red);
        Debug.DrawRay(bodyRayPoint.position, Vector2.left * detectionDistance, Color.green);
        Debug.DrawRay(feetRayPoint.position, Vector2.left * detectionDistance, Color.blue);
        
        // 머리 또는 몸통 레이에 몬스터가 감지되고 다리 레이에 감지되지 않는 경우
        // = 몬스터 앞에 서 있음
        if ((bodyHit.collider != null || headHit.collider != null) && 
            (bodyHit.collider?.gameObject != gameObject && headHit.collider?.gameObject != gameObject))
        {
            targetMonster = bodyHit.collider != null ? 
                            bodyHit.collider.gameObject : 
                            headHit.collider.gameObject;
            
            // 점프 실행
            JumpOverTarget(targetMonster);
        }
    }
    
    private void JumpOverTarget(GameObject target)
    {
        if (isJumping || isClimbing) return;
        
        Debug.Log("등반 시작: 몬스터 타고 올라가기");
        isClimbing = true;  // 점프가 아닌 등반 상태로 변경
        
        // 대상 몬스터의 콜라이더 정보 가져오기
        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (targetCollider == null) 
        {
            isClimbing = false;
            return;
        }
        
        // 등반 코루틴 시작
        StartCoroutine(ClimbOverMonster(target));
    }
    
    private IEnumerator ClimbOverMonster(GameObject target)
    {
        Collider2D targetCollider = target.GetComponent<Collider2D>();
        
        // 1단계: 목표물 앞까지 접근
        Vector2 approachPosition = new Vector2(
            target.transform.position.x + targetCollider.bounds.extents.x + 0.1f,
            transform.position.y
        );
        
        while (Vector2.Distance(new Vector2(transform.position.x, 0), new Vector2(approachPosition.x, 0)) > 0.1f)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                new Vector2(approachPosition.x, transform.position.y),
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }
        
        // 2단계: 몬스터 위로 올라가기
        float targetTopY = target.transform.position.y + targetCollider.bounds.extents.y + 0.05f;
        Vector2 topPosition = new Vector2(
            target.transform.position.x,
            targetTopY
        );
        
        // 물리 시스템 일시적으로 비활성화
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        
        // 몬스터 위로 올라가는 애니메이션
        while (transform.position.y < targetTopY - 0.1f)
        {
            // 몬스터 위로 올라가는 동작 - 포물선 형태가 아닌 직선으로 올라감
            transform.position = Vector2.MoveTowards(
                transform.position,
                new Vector2(target.transform.position.x, targetTopY),
                climbSpeed * Time.deltaTime
            );
            yield return null;
        }
        
        // 3단계: 몬스터 위에서 건너편으로 이동
        Vector2 overPosition = new Vector2(
            target.transform.position.x - targetCollider.bounds.extents.x - 0.1f,
            targetTopY
        );
        
        while (transform.position.x > overPosition.x + 0.1f)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                overPosition,
                climbOverSpeed * Time.deltaTime
            );
            yield return null;
        }
        
        // 4단계: 다시 물리 기반으로 전환 및 착지
        rb.isKinematic = false;
        rb.velocity = new Vector2(-moveSpeed, 0); // 왼쪽으로 이동하며 자연스럽게 떨어짐
        
        // 착지 완료될 때까지 대기
        float landingTimeout = 1.0f;
        float timer = 0f;
        bool hasLanded = false;
        
        while (!hasLanded && timer < landingTimeout)
        {
            RaycastHit2D groundCheck = Physics2D.Raycast(
                feetRayPoint.position,
                Vector2.down,
                0.2f,
                LayerMask.GetMask("Ground")
            );
            
            // 땅에 닿았거나 거의 정지했을 때
            if (groundCheck.collider != null || (rb.velocity.y > -0.1f && rb.velocity.y < 0.1f))
            {
                hasLanded = true;
                Debug.Log("착지 완료");
            }
            
            timer += Time.deltaTime;
            yield return null;
        }
        
        // 등반 상태 종료
        isClimbing = false;
        Debug.Log("등반 완료");
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 등반 중이 아닐 때만 충돌 처리 (등반 중에는 물리 충돌 무시)
        if (!isClimbing && isJumping && collision.contacts[0].normal.y > 0.5f)
        {
            isJumping = false;
        }
    }
}