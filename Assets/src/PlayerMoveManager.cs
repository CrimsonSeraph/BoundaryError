using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMoveManager : MonoBehaviour
{
    [SerializeField] private Rigidbody2D Player;

    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float initialJumpForce = 10f;
    [SerializeField] private float maxJumpHoldTime = 0.8f;
    [SerializeField] private float jumpHoldForce = 15f;
    [SerializeField] private float touchingWallSlowdownFactor = 0.5f;
    [SerializeField] private int surplusAirJumps = 1;
    [SerializeField] private float wallJumpCooldown = 0.2f;
    [SerializeField] private float fallMultiplier = 2.5f;

    [Header("地砖检测")]
    public Transform groundCheck;
    [SerializeField] private float checkRadius = 0.2f;
    public LayerMask groundLayer;
    private float lastGroundedTime;
    private float groundedRememberTime = 0.1f;

    [Header("墙壁检测")]
    public Transform leftWallCheck;
    public Transform rightWallCheck;
    [SerializeField] private float wallCheckDistance = 0.05f;
    public LayerMask wallLayer;

    [Header("头部检测")]
    public Transform topCheck;
    [SerializeField] private float topCheckDistance = 0.5f;
    public LayerMask topLayer;

    [Header("Debug")]
    public bool startDebug = true;
    private float lastDebugTime = 0f;
    [SerializeField] private float debugInterval = 1f;

    private float horizontalInput;
    private bool isGrounded;
    private bool isJumping;
    private float jumpTimeCounter;
    private bool jumpRequested;
    private bool isTouchingLeftWall;
    private bool isTouchingRightWall;
    private bool isTouchingWall;
    private Collider2D playerCollider;
    private float playerHalfWidth;
    private bool isTouchingTop;
    private float lastWallJumpTime;
    private bool canJump;
    private bool canGroundJump;
    private bool canWallJump;
    private bool canAirJump;

    // 初始化
    void Start()
    {
        Player = GetComponent<Rigidbody2D>();

        playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            playerHalfWidth = playerCollider.bounds.extents.x;
        }
        else
        {
            playerHalfWidth = 0.5f;
        }

        SetupWallCheckPositions();
    }

    // 更新
    void Update()
    {
        GetInput();
        DebugAll();
    }

    // 物理更新
    void FixedUpdate()
    {
        CheckGrounded();
        CheckWalls();
        CheckTop();
        HandleMovement();
        HandleJumpInput();
        HandleJumpPhysics();
        if (Player.velocity.y < 0)
        {
            Player.velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    // 设置墙壁检测位置
    private void SetupWallCheckPositions()
    {
        if (leftWallCheck != null)
        {
            leftWallCheck.localPosition = new Vector3(-playerHalfWidth, 0, 0);
            leftWallCheck.rotation = Quaternion.identity;
        }

        if (rightWallCheck != null)
        {
            rightWallCheck.localPosition = new Vector3(playerHalfWidth, 0, 0);
            rightWallCheck.rotation = Quaternion.identity;
        }
    }

    // 处理按键输入
    private void GetInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        canGroundJump = isGrounded || Time.time - lastGroundedTime <= groundedRememberTime;
        canAirJump = !canGroundJump && surplusAirJumps > 0;
        canWallJump = !canGroundJump && isTouchingWall && Time.time - lastWallJumpTime > wallJumpCooldown;

        canJump = canGroundJump || canAirJump || canWallJump;

        if (Input.GetKeyDown(KeyCode.Space) && canJump)
        {
            jumpRequested = true;
        }
    }

    // 移动管理
    private void HandleMovement()
    {
        if (!isGrounded && ((isTouchingLeftWall && horizontalInput < 0) || (isTouchingRightWall && horizontalInput > 0)))
        {
            Player.velocity = new Vector2(0, Player.velocity.y - touchingWallSlowdownFactor);
        }
        else
        {
            Vector2 velocity = new Vector2(horizontalInput * moveSpeed, Player.velocity.y);
            Player.velocity = velocity;
        }
    }

    // 处理跳跃指令输入
    private void HandleJumpInput()
    {
        if (jumpRequested)
        {
            StartJump();
            jumpRequested = false;
        }

        if (isJumping)
        {
            if (Input.GetKey(KeyCode.Space) && jumpTimeCounter > 0)
            {
                Player.AddForce(Vector2.up * jumpHoldForce * Time.deltaTime, ForceMode2D.Force);
                jumpTimeCounter -= Time.deltaTime;
            }
            else
            {
                jumpTimeCounter = 0;
                isJumping = false;
            }
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            jumpTimeCounter = 0;
            isJumping = false;
        }
    }

    // 跳跃物理事件
    private void HandleJumpPhysics()
    {
    }

    // 开始跳跃
    private void StartJump()
    {
        if (isTouchingWall && !isGrounded)
        {
            Vector2 jumpDir = isTouchingLeftWall ? Vector2.right : Vector2.left;
            Player.velocity = new Vector2(jumpDir.x * moveSpeed, initialJumpForce);
            lastWallJumpTime = Time.time;
        }
        else if (!isGrounded && surplusAirJumps > 0)
        {
            Player.velocity = new Vector2(Player.velocity.x, initialJumpForce);
            surplusAirJumps--;
        }
        else if (isGrounded)
        {
            Player.velocity = new Vector2(Player.velocity.x, initialJumpForce);
        }

        isJumping = true;
        jumpTimeCounter = maxJumpHoldTime;
    }


    // 检测是否在地面
    private void CheckGrounded()
    {
        float width = playerCollider.bounds.size.x * 0.9f;
        float height = checkRadius * 2f;
        Vector2 boxCenter = (Vector2)groundCheck.position;

        isGrounded = Physics2D.OverlapBox(boxCenter, new Vector2(width, height), 0f, groundLayer);

        if (Player.velocity.y <= 0 && isGrounded)
        {
            surplusAirJumps = 1;
            lastGroundedTime = Time.time;
            if (Mathf.Abs(Player.velocity.y) < 0.1f)
                isJumping = false;
        }
    }


    // 检测墙壁接触
    private void CheckWalls()
    {
        float height = playerCollider.bounds.size.y * 0.9f;
        Vector2 leftCenter = (Vector2)transform.position + Vector2.left * (playerHalfWidth + wallCheckDistance / 2);
        Vector2 rightCenter = (Vector2)transform.position + Vector2.right * (playerHalfWidth + wallCheckDistance / 2);

        isTouchingLeftWall = Physics2D.OverlapBox(leftCenter, new Vector2(wallCheckDistance, height), 0, wallLayer);
        isTouchingRightWall = Physics2D.OverlapBox(rightCenter, new Vector2(wallCheckDistance, height), 0, wallLayer);

        isTouchingWall = isTouchingLeftWall || isTouchingRightWall;
    }

    // 检测是否碰头
    private void CheckTop()
    {
        if (isJumping)
        {
            Vector2 upDirection = Vector2.up;
            RaycastHit2D topHit = Physics2D.Raycast(topCheck.position, upDirection, topCheckDistance, topLayer);
            isTouchingTop = topHit.collider != null;
        }
    }

    // Debug信息
    private void DebugAll()
    {
        if (!startDebug) return;
        if (Time.time - lastDebugTime >= debugInterval)
        {
            lastDebugTime = Time.time;

            Debug.Log("是否在地面: " + isGrounded);
            Debug.Log("左侧是否接触墙壁: " + isTouchingLeftWall);
            Debug.Log("右侧是否接触墙壁: " + isTouchingRightWall);
            Debug.Log("玩家速度: " + Player.velocity.x + Player.velocity.y);
            Debug.Log("是否在跳跃: " + isJumping);
            Debug.Log("是否可以跳跃:" + canJump);
            Debug.Log("是否可以地面跳跃:" + canGroundJump);
            Debug.Log("是否可以墙壁跳跃:" + canWallJump);
            Debug.Log("是否可以空中跳跃:" + canAirJump);
            Debug.Log("剩余空中跳跃次数:" + surplusAirJumps);
            Debug.Log("是否碰头:" + isTouchingTop);
        }
    }
}
