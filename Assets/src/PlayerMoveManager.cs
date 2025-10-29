using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMoveManager : MonoBehaviour
{
    private Rigidbody2D Player;

    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float initialJumpForce = 10f;
    public float maxJumpHoldTime = 0.8f;
    public float jumpHoldForce = 10f;
    public float touchingWallSlowdownFactor = 0.5f;
    public int surplusAirJumps = 1;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float checkRadius = 0.2f;
    public LayerMask groundLayer;
    private float lastGroundedTime;
    private float groundedRememberTime = 0.1f;
    private float extraCheck = 0.65f;

    [Header("Wall Check")]
    public Transform leftWallCheck;
    public Transform rightWallCheck;
    public float wallCheckDistance = 0.05f;
    public LayerMask wallLayer;

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

    void Start()
    {
        Player = GetComponent<Rigidbody2D>();
        Player.sharedMaterial = null;

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

    void Update()
    {
        GetInput();
        HandleJumpInput();
    }

    void FixedUpdate()
    {
        CheckGrounded();
        CheckWalls();
        HandleMovement();
        HandleJumpPhysics();
    }

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

    private void GetInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        bool canJump = (isGrounded || Time.time - lastGroundedTime <= groundedRememberTime || isTouchingWall) && surplusAirJumps >= 1;

        if (Input.GetKeyDown(KeyCode.Space) && canJump)
        {
            jumpRequested = true;
        }
    }

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
                float holdForceMultiplier = jumpTimeCounter / maxJumpHoldTime;
                float currentJumpForce = jumpHoldForce * holdForceMultiplier;

                Player.velocity = new Vector2(Player.velocity.x, currentJumpForce);

                jumpTimeCounter -= Time.deltaTime;
            }
            else
            {
                isJumping = false;
            }
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            isJumping = false;
        }
    }

    private void HandleJumpPhysics()
    {
    }

    private void StartJump()
    {
        if (isTouchingLeftWall)
        {
            surplusAirJumps -= 1;
            Player.velocity = new Vector2(moveSpeed, initialJumpForce);
        }
        else if (isTouchingRightWall)
        {
            surplusAirJumps -= 1;
            Player.velocity = new Vector2(-moveSpeed, initialJumpForce);
        }
        else
        {
            Player.velocity = new Vector2(Player.velocity.x, initialJumpForce);
        }

        isJumping = true;
        jumpTimeCounter = maxJumpHoldTime;
    }

    private void CheckGrounded()
    {
        bool wasGrounded = isGrounded;

        bool mainCheck = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);

        Vector3 frontOffset = new Vector3(playerHalfWidth * extraCheck, 0, 0);
        Vector3 backOffset = new Vector3(-playerHalfWidth * extraCheck, 0, 0);

        bool frontCheck = Physics2D.OverlapCircle(groundCheck.position + frontOffset, checkRadius, groundLayer);
        bool backCheck = Physics2D.OverlapCircle(groundCheck.position + backOffset, checkRadius, groundLayer);

        isGrounded = mainCheck || frontCheck || backCheck;

        if (isGrounded)
        {
            surplusAirJumps = 1;
            lastGroundedTime = Time.time;
        }

        if (isGrounded && Player.velocity.y <= 0.1f)
        {
            isJumping = false;
        }
    }

    private void CheckWalls()
    {
        Vector2 leftDirection = Vector2.left;
        Vector2 rightDirection = Vector2.right;

        float verticalOffset = 0.2f;

        RaycastHit2D leftHit1 = Physics2D.Raycast(leftWallCheck.position, leftDirection, wallCheckDistance, wallLayer);
        RaycastHit2D leftHit2 = Physics2D.Raycast(leftWallCheck.position + Vector3.up * verticalOffset, leftDirection, wallCheckDistance, wallLayer);
        RaycastHit2D leftHit3 = Physics2D.Raycast(leftWallCheck.position + Vector3.down * verticalOffset, leftDirection, wallCheckDistance, wallLayer);

        RaycastHit2D rightHit1 = Physics2D.Raycast(rightWallCheck.position, rightDirection, wallCheckDistance, wallLayer);
        RaycastHit2D rightHit2 = Physics2D.Raycast(rightWallCheck.position + Vector3.up * verticalOffset, rightDirection, wallCheckDistance, wallLayer);
        RaycastHit2D rightHit3 = Physics2D.Raycast(rightWallCheck.position + Vector3.down * verticalOffset, rightDirection, wallCheckDistance, wallLayer);

        isTouchingLeftWall = leftHit1.collider != null || leftHit2.collider != null || leftHit3.collider != null;
        isTouchingRightWall = rightHit1.collider != null || rightHit2.collider != null || rightHit3.collider != null;

        isTouchingWall = isTouchingLeftWall || isTouchingRightWall;
    }

    private void DebugAll()
    {
        Debug.Log("是否在地面: " + isGrounded);
        Debug.Log("左侧是否接触墙壁: " + isTouchingLeftWall);
        Debug.Log("右侧是否接触墙壁: " + isTouchingRightWall);
        Debug.Log("玩家速度: " + Player.velocity.x + Player.velocity.y);
        Debug.Log("是否在跳跃: " + isJumping);
        Debug.Log("是否可以跳跃:" + jumpRequested);
    }
}
