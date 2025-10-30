using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 移动管理器
/// 功能：
///— 支持左右移动、地面跳跃、空中跳跃、墙壁跳跃
///— 支持跳跃按住加力、下落加速（fall multiplier）
///— 支持墙壁缓速移动
///— 支持调试输出（可通过 enableDebug 控制）
/// </summary>
public class PlayerMoveManager : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private Rigidbody2D Player;           // 玩家刚体
    private Collider2D playerCollider;                      // 玩家碰撞体
    private float playerHalfWidth;

    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 8f;         // 水平移动速度
    [SerializeField] private float initialJumpForce = 10f; // 起跳初始向上力
    [SerializeField] private float maxJumpHoldTime = 0.8f; // 按住跳跃最大持续时间
    [SerializeField] private float jumpHoldForce = 15f;    // 持续跳跃力
    [SerializeField] private float touchingWallSlowdownFactor = 0.5f; // 接触墙壁时水平减速
    [SerializeField] private int surplusAirJumps = 1;      // 空中跳跃次数
    [SerializeField] private float wallJumpCooldown = 0.2f; // 墙跳冷却
    [SerializeField] private float fallMultiplier = 2.5f;  // 下落加速倍率

    [Header("地面检测")]
    public Transform groundCheck;                           // 地面检测点
    [SerializeField] private float checkRadius = 0.2f;
    public LayerMask groundLayer;
    private float lastGroundedTime;
    [SerializeField] private float groundedRememberTime = 0.1f; // 宽容地面记忆

    [Header("墙壁检测")]
    public Transform leftWallCheck;
    public Transform rightWallCheck;
    [SerializeField] private float wallCheckDistance = 0.05f;
    public LayerMask wallLayer;

    [Header("头部检测")]
    public Transform topCheck;
    [SerializeField] private float topCheckDistance = 0.5f;
    public LayerMask topLayer;

    [Header("调试设置")]
    [SerializeField] private bool enableDebug = false;
    [SerializeField] private float debugInterval = 1f;
    private float lastDebugTime = 0f;

    // 内部状态
    private float horizontalInput;
    private bool isGrounded;
    private bool isJumping;
    private float jumpTimeCounter;
    private bool jumpRequested;
    private bool isTouchingLeftWall;
    private bool isTouchingRightWall;
    private bool isTouchingWall;
    private bool isTouchingTop;
    private float lastWallJumpTime;
    private bool canJump, canGroundJump, canAirJump, canWallJump;

    // 初始化
    void Start()
    {
        Player = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        playerHalfWidth = playerCollider != null ? playerCollider.bounds.extents.x : 0.5f;

        SetupWallCheckPositions();
    }

    // 每帧更新：获取输入、调试输出
    void Update()
    {
        GetInput();
        DebugAll();
    }

    // 固定更新：物理运动、检测
    void FixedUpdate()
    {
        CheckGrounded();
        CheckWalls();
        CheckTop();

        HandleMovement();
        HandleJumpInput();
        HandleJumpPhysics();

        // 下落加速
        if (Player.velocity.y < 0)
        {
            Player.velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    #region 核心功能

    /// <summary>
    /// 设置墙壁检测位置
    /// </summary>
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

    /// <summary>
    /// 获取玩家输入并计算可跳状态
    /// </summary>
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

    /// <summary>
    /// 左右移动控制，墙壁接触减速
    /// </summary>
    private void HandleMovement()
    {
        if (!isGrounded && ((isTouchingLeftWall && horizontalInput < 0) || (isTouchingRightWall && horizontalInput > 0)))
        {
            Player.velocity = new Vector2(0, Player.velocity.y - touchingWallSlowdownFactor);
        }
        else
        {
            Player.velocity = new Vector2(horizontalInput * moveSpeed, Player.velocity.y);
        }
    }

    /// <summary>
    /// 跳跃输入处理（按住加力）
    /// </summary>
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

    /// <summary>
    /// 空方法，保留扩展物理处理（可加缓冲、碰撞反弹等）
    /// </summary>
    private void HandleJumpPhysics() { }

    /// <summary>
    /// 执行跳跃：地面跳、墙壁跳、空中跳
    /// </summary>
    private void StartJump()
    {
        if (isTouchingWall && !isGrounded)
        {
            // 墙跳
            Vector2 jumpDir = isTouchingLeftWall ? Vector2.right : Vector2.left;
            Player.velocity = new Vector2(jumpDir.x * moveSpeed, initialJumpForce);
            lastWallJumpTime = Time.time;
        }
        else if (!isGrounded && surplusAirJumps > 0)
        {
            // 空中跳
            Player.velocity = new Vector2(Player.velocity.x, initialJumpForce);
            surplusAirJumps--;
        }
        else if (isGrounded)
        {
            // 地面跳
            Player.velocity = new Vector2(Player.velocity.x, initialJumpForce);
        }

        isJumping = true;
        jumpTimeCounter = maxJumpHoldTime;
    }

    #endregion

    #region 检测逻辑

    /// <summary>
    /// 地面检测（OverlapBox）
    /// </summary>
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

    /// <summary>
    /// 墙壁检测（OverlapBox）
    /// </summary>
    private void CheckWalls()
    {
        float height = playerCollider.bounds.size.y * 0.9f;
        Vector2 leftCenter = (Vector2)transform.position + Vector2.left * (playerHalfWidth + wallCheckDistance / 2);
        Vector2 rightCenter = (Vector2)transform.position + Vector2.right * (playerHalfWidth + wallCheckDistance / 2);

        isTouchingLeftWall = Physics2D.OverlapBox(leftCenter, new Vector2(wallCheckDistance, height), 0, wallLayer);
        isTouchingRightWall = Physics2D.OverlapBox(rightCenter, new Vector2(wallCheckDistance, height), 0, wallLayer);

        isTouchingWall = isTouchingLeftWall || isTouchingRightWall;
    }

    /// <summary>
    /// 头顶碰撞检测
    /// </summary>
    private void CheckTop()
    {
        if (!isJumping) return;

        RaycastHit2D topHit = Physics2D.Raycast(topCheck.position, Vector2.up, topCheckDistance, topLayer);
        isTouchingTop = topHit.collider != null;
    }

    #endregion

    #region 调试

    /// <summary>
    /// 调试输出玩家状态，可通过 enableDebug 开关控制，输出间隔 debugInterval
    /// </summary>
    private void DebugAll()
    {
        if (!enableDebug) return;
        if (Time.time - lastDebugTime < debugInterval) return;

        lastDebugTime = Time.time;

        Debug.Log($"地面: {isGrounded}, 左墙: {isTouchingLeftWall}, 右墙: {isTouchingRightWall}");
        Debug.Log($"速度: {Player.velocity}, 跳跃中: {isJumping}, 碰头: {isTouchingTop}");
        Debug.Log($"可跳: {canJump}, 地面跳: {canGroundJump}, 墙跳: {canWallJump}, 空中跳: {canAirJump}");
        Debug.Log($"剩余空中跳跃次数: {surplusAirJumps}");
    }

    #endregion
}
