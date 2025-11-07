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
    [SerializeField] private float maxJumpHoldTime = 2f; // 按住跳跃最大持续时间
    [SerializeField] private float jumpHoldForce = 1f;    // 持续跳跃力
    [SerializeField] private float touchingWallSlowdownFactor = 0.5f; // 接触墙壁时水平减速
    [SerializeField] private int surplusAirJumps = 1;      // 空中跳跃次数
    [SerializeField] private int surplusWallJumps = 1;     // 墙壁跳跃次数
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

    void Start()
    {
        if (!InitializeComponents())
        {
            Debug.LogError("[PlayerMoveManager] 组件初始化失败，脚本将被禁用");
            enabled = false;
            return;
        }

        playerHalfWidth = playerCollider != null ? playerCollider.bounds.extents.x : 0.5f;
        SetupWallCheckPositions();
    }

    void Update()
    {
        if (!ValidateComponents()) return;
        if (!ValidateComponents()) return;

        GetInput();
    }

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

    #region 初始化和验证

    /// <summary>
    /// 初始化必要组件
    /// </summary>
    private bool InitializeComponents()
    {
        bool success = true;

        // 获取 Rigidbody2D 组件
        Player = GetComponent<Rigidbody2D>();
        if (Player == null)
        {
            Debug.LogError("[PlayerMoveManager] 未找到 Rigidbody2D 组件！");
            success = false;
        }

        // 获取 Collider2D 组件
        playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null)
        {
            Debug.LogError("[PlayerMoveManager] 未找到 Collider2D 组件！");
            success = false;
        }

        // 验证检测点引用
        if (groundCheck == null)
        {
            Debug.LogError("[PlayerMoveManager] groundCheck 未分配！");
            success = false;
        }

        if (leftWallCheck == null)
        {
            Debug.LogError("[PlayerMoveManager] leftWallCheck 未分配！");
            success = false;
        }

        if (rightWallCheck == null)
        {
            Debug.LogError("[PlayerMoveManager] rightWallCheck 未分配！");
            success = false;
        }

        if (topCheck == null)
        {
            Debug.LogError("[PlayerMoveManager] topCheck 未分配！");
            success = false;
        }

        // 验证 LayerMask
        if (groundLayer.value == 0)
        {
            Debug.LogWarning("[PlayerMoveManager] groundLayer 未设置，可能无法检测地面");
        }

        if (wallLayer.value == 0)
        {
            Debug.LogWarning("[PlayerMoveManager] wallLayer 未设置，可能无法检测墙壁");
        }

        if (topLayer.value == 0)
        {
            Debug.LogWarning("[PlayerMoveManager] topLayer 未设置，可能无法检测头顶碰撞");
        }

        return success;
    }

    /// <summary>
    /// 验证组件是否可用
    /// </summary>
    private bool ValidateComponents()
    {
        if (Player == null)
        {
            Debug.LogError("[PlayerMoveManager] Rigidbody2D 为 null");
            return false;
        }

        if (playerCollider == null)
        {
            Debug.LogError("[PlayerMoveManager] Collider2D 为 null");
            return false;
        }

        if (groundCheck == null || leftWallCheck == null || rightWallCheck == null || topCheck == null)
        {
            Debug.LogError("[PlayerMoveManager] 检测点未分配完整");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证物理参数是否合理
    /// </summary>
    public bool ValidatePhysicsParameters()
    {
        bool valid = true;

        if (moveSpeed <= 0)
        {
            Debug.LogError($"[PlayerMoveManager] moveSpeed 必须大于0，当前值: {moveSpeed}");
            valid = false;
        }

        if (initialJumpForce <= 0)
        {
            Debug.LogError($"[PlayerMoveManager] initialJumpForce 必须大于0，当前值: {initialJumpForce}");
            valid = false;
        }

        if (maxJumpHoldTime <= 0)
        {
            Debug.LogError($"[PlayerMoveManager] maxJumpHoldTime 必须大于0，当前值: {maxJumpHoldTime}");
            valid = false;
        }

        if (jumpHoldForce < 0)
        {
            Debug.LogError($"[PlayerMoveManager] jumpHoldForce 不能为负数，当前值: {jumpHoldForce}");
            valid = false;
        }

        if (touchingWallSlowdownFactor < 0 || touchingWallSlowdownFactor > 1)
        {
            Debug.LogError($"[PlayerMoveManager] touchingWallSlowdownFactor 必须在[0,1]范围内，当前值: {touchingWallSlowdownFactor}");
            valid = false;
        }

        if (surplusAirJumps < 0)
        {
            Debug.LogError($"[PlayerMoveManager] surplusAirJumps 不能为负数，当前值: {surplusAirJumps}");
            valid = false;
        }

        if (surplusWallJumps < 0)
        {
            Debug.LogError($"[PlayerMoveManager] surplusWallJumps 不能为负数，当前值: {surplusWallJumps}");
            valid = false;
        }

        if (wallJumpCooldown < 0)
        {
            Debug.LogError($"[PlayerMoveManager] wallJumpCooldown 不能为负数，当前值: {wallJumpCooldown}");
            valid = false;
        }

        if (fallMultiplier < 1)
        {
            Debug.LogError($"[PlayerMoveManager] fallMultiplier 必须大于等于1，当前值: {fallMultiplier}");
            valid = false;
        }

        return valid;
    }

    #endregion

    #region 核心功能

    /// <summary>
    /// 设置墙壁检测位置
    /// </summary>
    private void SetupWallCheckPositions()
    {
        if (!ValidateComponents()) return;

        try
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

            if (enableDebug) Debug.Log("[PlayerMoveManager] 墙壁检测位置设置完成");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMoveManager] 设置墙壁检测位置时发生错误: {e.Message}");
        }
    }

    /// <summary>
    /// 获取玩家输入并计算可跳状态
    /// </summary>
    private void GetInput()
    {
        try
        {
            horizontalInput = Input.GetAxisRaw("Horizontal");

            canGroundJump = !isTouchingTop && (isGrounded || Time.time - lastGroundedTime <= groundedRememberTime);
            canAirJump = !isTouchingTop && !canGroundJump && surplusAirJumps > 0;
            canWallJump = !isTouchingTop && !canGroundJump && isTouchingWall && Time.time - lastWallJumpTime > wallJumpCooldown && surplusWallJumps > 0;

            canJump = canGroundJump || canAirJump || canWallJump;

            if (Input.GetKeyDown(KeyCode.Space) && canJump)
            {
                jumpRequested = true;
                if (enableDebug) Debug.Log($"[PlayerMoveManager] 跳跃请求 - 地面跳:{canGroundJump}, 空中跳:{canAirJump}, 墙壁跳:{canWallJump}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMoveManager] 获取输入时发生错误: {e.Message}");
        }
    }

    /// <summary>
    /// 左右移动控制，墙壁接触减速
    /// </summary>
    private void HandleMovement()
    {
        try
        {
            if (!isGrounded && ((isTouchingLeftWall && horizontalInput < 0) || (isTouchingRightWall && horizontalInput > 0)))
            {
                Player.velocity = new Vector2(0, Player.velocity.y - touchingWallSlowdownFactor);
                if (enableDebug) Debug.Log("[PlayerMoveManager] 墙壁接触减速");
            }
            else
            {
                Player.velocity = new Vector2(horizontalInput * moveSpeed, Player.velocity.y);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMoveManager] 处理移动时发生错误: {e.Message}");
        }
    }

    /// <summary>
    /// 跳跃输入处理（按住加力）
    /// </summary>
    private void HandleJumpInput()
    {
        try
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
                    float holdEffect = jumpHoldForce * jumpTimeCounter * 0.1f;
                    Player.velocity = new Vector2(Player.velocity.x, Player.velocity.y + holdEffect);
                    jumpTimeCounter -= Time.deltaTime;

                    if (enableDebug && Time.frameCount % 30 == 0)
                        Debug.Log($"[PlayerMoveManager] 跳跃保持中 - 剩余时间: {jumpTimeCounter:F2}");
                }
                else
                {
                    jumpTimeCounter = 0;
                    isJumping = false;
                    if (enableDebug) Debug.Log("[PlayerMoveManager] 跳跃结束");
                }
            }

            if (Input.GetKeyUp(KeyCode.Space))
            {
                jumpTimeCounter = 0;
                isJumping = false;
                if (enableDebug) Debug.Log("[PlayerMoveManager] 跳跃键释放");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMoveManager] 处理跳跃输入时发生错误: {e.Message}");
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
        try
        {
            if (isTouchingWall && !isGrounded && surplusWallJumps > 0)
            {
                // 墙跳
                Vector2 jumpDir = isTouchingLeftWall ? Vector2.right : Vector2.left;
                Player.velocity = new Vector2(Player.velocity.x, initialJumpForce);
                lastWallJumpTime = Time.time;
                surplusWallJumps--;
                if (enableDebug) Debug.Log($"[PlayerMoveManager] 墙壁跳跃 - 方向: {jumpDir}, 剩余墙跳次数: {surplusWallJumps}");
            }
            else if (!isGrounded && !isTouchingWall && surplusAirJumps > 0)
            {
                // 空中跳
                Player.velocity = new Vector2(Player.velocity.x, initialJumpForce);
                surplusAirJumps--;
                if (enableDebug) Debug.Log($"[PlayerMoveManager] 空中跳跃 - 剩余空中跳次数: {surplusAirJumps}");
            }
            else if (isGrounded)
            {
                // 地面跳
                Player.velocity = new Vector2(Player.velocity.x, initialJumpForce);
                if (enableDebug) Debug.Log("[PlayerMoveManager] 地面跳跃");
            }
            else
            {
                if (enableDebug) Debug.LogWarning("[PlayerMoveManager] 跳跃条件不满足，无法跳跃");
                return;
            }

            isJumping = true;
            jumpTimeCounter = maxJumpHoldTime;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMoveManager] 执行跳跃时发生错误: {e.Message}");
        }
    }

    #endregion

    #region 检测逻辑

    /// <summary>
    /// 地面检测（OverlapBox）
    /// </summary>
    private void CheckGrounded()
    {
        try
        {
            if (groundCheck == null) return;

            float width = playerCollider.bounds.size.x * 0.9f;
            float height = checkRadius * 2f;
            Vector2 boxCenter = (Vector2)groundCheck.position;

            bool wasGrounded = isGrounded;
            isGrounded = Physics2D.OverlapBox(boxCenter, new Vector2(width, height), 0f, groundLayer);

            if (Player.velocity.y <= 0 && isGrounded)
            {
                surplusAirJumps = 1;
                surplusWallJumps = 1;
                lastGroundedTime = Time.time;

                if (Mathf.Abs(Player.velocity.y) < 0.1f)
                    isJumping = false;

                if (!wasGrounded && enableDebug)
                    Debug.Log("[PlayerMoveManager] 落地 - 重置跳跃次数");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMoveManager] 地面检测时发生错误: {e.Message}");
        }
    }

    /// <summary>
    /// 墙壁检测（OverlapBox）
    /// </summary>
    private void CheckWalls()
    {
        try
        {
            if (leftWallCheck == null || rightWallCheck == null) return;

            float height = playerCollider.bounds.size.y * 0.9f;
            Vector2 leftCenter = (Vector2)transform.position + Vector2.left * (playerHalfWidth + wallCheckDistance / 2);
            Vector2 rightCenter = (Vector2)transform.position + Vector2.right * (playerHalfWidth + wallCheckDistance / 2);

            bool wasTouchingLeftWall = isTouchingLeftWall;
            bool wasTouchingRightWall = isTouchingRightWall;

            isTouchingLeftWall = Physics2D.OverlapBox(leftCenter, new Vector2(wallCheckDistance, height), 0, wallLayer);
            isTouchingRightWall = Physics2D.OverlapBox(rightCenter, new Vector2(wallCheckDistance, height), 0, wallLayer);

            isTouchingWall = isTouchingLeftWall || isTouchingRightWall;

            // 墙壁接触状态变化调试
            if (enableDebug)
            {
                if (isTouchingLeftWall && !wasTouchingLeftWall)
                    Debug.Log("[PlayerMoveManager] 接触左侧墙壁");
                if (isTouchingRightWall && !wasTouchingRightWall)
                    Debug.Log("[PlayerMoveManager] 接触右侧墙壁");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMoveManager] 墙壁检测时发生错误: {e.Message}");
        }
    }

    /// <summary>
    /// 头顶碰撞检测
    /// </summary>
    private void CheckTop()
    {
        try
        {
            if (!isJumping || topCheck == null) return;

            RaycastHit2D topHit = Physics2D.Raycast(topCheck.position, Vector2.up, topCheckDistance, topLayer);
            bool wasTouchingTop = isTouchingTop;
            isTouchingTop = topHit.collider != null;

            if (isTouchingTop && !wasTouchingTop && enableDebug)
            {
                Debug.Log("[PlayerMoveManager] 头顶碰撞");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMoveManager] 头顶检测时发生错误: {e.Message}");
        }
    }

    #endregion

    #region 公共方法和调试

    /// <summary>
    /// 获取玩家当前状态信息
    /// </summary>
    public string GetStatusInfo()
    {
        return $@"[PlayerMoveManager] 状态信息:
        - 水平输入: {horizontalInput}
        - 是否接地: {isGrounded}
        - 是否跳跃: {isJumping}
        - 接触左墙: {isTouchingLeftWall}
        - 接触右墙: {isTouchingRightWall}
        - 接触头顶: {isTouchingTop}
        - 剩余空中跳: {surplusAirJumps}
        - 剩余墙壁跳: {surplusWallJumps}
        - 速度: {Player.velocity}";
    }

    /// <summary>
    /// 重置跳跃次数
    /// </summary>
    public void ResetJumps()
    {
        surplusAirJumps = 1;
        surplusWallJumps = 1;
        if (enableDebug) Debug.Log("[PlayerMoveManager] 跳跃次数已重置");
    }

    /// <summary>
    /// 设置移动速度
    /// </summary>
    public void SetMoveSpeed(float newSpeed)
    {
        if (newSpeed <= 0)
        {
            Debug.LogError($"[PlayerMoveManager] 移动速度必须大于0，尝试设置: {newSpeed}");
            return;
        }
        moveSpeed = newSpeed;
        if (enableDebug) Debug.Log($"[PlayerMoveManager] 移动速度设置为: {newSpeed}");
    }

    #endregion
}