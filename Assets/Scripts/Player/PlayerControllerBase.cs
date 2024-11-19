using System;
using System.Linq;
using System.Threading.Tasks;
using _Scripts.UI;
using Cinemachine;
using NetBuff;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.Audio;
using Solis.Circuit.Components;
using Solis.Core;
using Solis.Data;
using Solis.Interface.Input;
using Solis.Misc.Integrations;
using Solis.Misc.Multicam;
using Solis.Misc.Props;
using Solis.Packets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Solis.Player
{
    /// <summary>
    /// Base class for player controllers.
    /// </summary>
    public abstract class PlayerControllerBase : NetworkBehaviour
    {
        #region Types
        /// <summary>
        /// Represents the state of the player.
        /// </summary>
        public enum State
        {
            Normal,
            Magnetized,
            GrapplingHook
        }

        /// <summary>
        /// Represents the death type of the player.
        /// </summary>
        public enum Death
        {
            Stun,
            Fall
        }
        #endregion

        #region Inspector Fields
        [Header("DATA")]
        public PlayerData data;
#if UNITY_EDITOR
        [HideInInspector]
        public bool playerDataFoldout;
#endif

        [Space]
        [Header("SCRIPTS REFERENCES")]
        public CharacterController controller;
        public NetworkAnimator animator;
        public PlayerEmoteController emoteController;

        [Header("BODY REFERENCES")]
        public Transform body;
        public Transform lookAt;
        public Transform dialogueLookAt;
        public Transform headOffset;
        public new SkinnedMeshRenderer renderer;
        public LayerMask groundMask;

        [Header("FX REFERENCES")]
        public ParticleSystem dustParticles;
        public ParticleSystem jumpParticles;
        public ParticleSystem landParticles;

        [Header("STATE")]
        public State state;
        public Vector3 velocity;
        public BoolNetworkValue isRespawning = new(false);
        public BoolNetworkValue isPaused = new(false);
        
        [Header("NETWORK")]
        public int tickRate = 50;
        public StringNetworkValue username = new("Default");

        [Header("MAGNETIZED")]
        public Vector3 magnetReferenceLocalPosition = new Vector3(0, 2, 0);
        public Transform magnetAnchor;

        [Header("HAND")]
        public Transform handPosition;
        public CarryableObject carriedObject;

#if UNITY_EDITOR
        [Header("DEBUG")]
        public bool debugJump = false;
        public Vector3 debugLastJumpMaxHeight;
        public Vector3 debugNextMovePos;
        public Vector3 debugNextBoxPos;
#endif
        #endregion

        #region Private Fields
        //MOVEMENT NETWORK
        private float _remoteBodyRotation;
        private Vector3 _remoteBodyPosition;
        
        //JUMP
        private float _coyoteTimer;
        private float _jumpTimer;
        private float _startJumpPos;
        internal bool IsJumping;
        private bool _isJumpingEnd;
        private bool _inJumpState;
        private bool _isJumpCut;
        private float _lastJumpHeight;
        private float _lastJumpVelocity;
        
        private bool _isFalling;
        
        private bool _isCinematicRunning = true;

        private float _respawnTimer;
        private float _interactTimer;
        private float _multiplier;

        private bool _isColliding;

        private Vector3 _lastSafePosition;
        private Transform _camera;

        private static readonly int Respawning = Shader.PropertyToID("_Respawning");

        #endregion

        #region Public Properties
        /// <summary>
        /// Returns whether the player is grounded or not.
        /// </summary>
        public bool IsGrounded => controller.isGrounded;
        
        /// <summary>
        /// Returns the character type of the player.
        /// </summary>
        public abstract CharacterType CharacterType { get; }
        
        #endregion

        #region Private Properties

        #region Data Properties

        //MOVEMENT
        private float MaxSpeed => data.maxSpeed;
        private float Acceleration => data.acceleration;
        private float Deceleration => data.deceleration;
        private float AccelInJumpMultiplier => data.accelInJumpMultiplier;
        private float RotationSpeed => data.rotationSpeed;
        //JUMP
        private float JumpMaxHeight => data.jumpMaxHeight;
        private float JumpAcceleration => data.jumpAcceleration;
        private float JumpGravityMultiplier => data.jumpGravityMultiplier;
        private float JumpCutMinHeight => data.jumpCutMinHeight;
        private float JumpCutGravityMultiplier => data.jumpCutGravityMultiplier;
        private float CoyoteTime => data.coyoteTime;
        private float TimeToJump => data.timeToJump;
        //GRAVITY
        private float Gravity => data.gravity;
        private float FallMultiplier => data.fallMultiplier;
        private float MaxFallSpeed => data.maxFallSpeed;
        private float MaxHeightDecel => data.maxHeightDecel;
        private float HitHeadDecel => data.hitHeadDecel;
        //COOLDOWNS
        private float InteractCooldown => data.interactCooldown;
        private float RespawnCooldown => data.respawnCooldown;

        #endregion

        private Vector2 MoveInput => SolisInput.GetVector2("Move");
        private bool InputJump => SolisInput.GetKeyDown("Jump");
        private bool InputJumpUp => SolisInput.GetKeyUp("Jump");
        private bool CanJump => !IsJumping && (IsGrounded || _coyoteTimer > 0) && _jumpTimer <= 0 && !isPaused.Value && !DialogPanel.IsDialogPlaying;

        private protected bool CanJumpCut =>
            IsJumping && (transform.position.y - _startJumpPos) >= JumpCutMinHeight;
        private bool IsPlayerLocked => _isCinematicRunning || isRespawning.Value;
        private Vector3 HeadOffset => headOffset.position;
        #endregion

        #region TEMP - Grappling Hook
        public LineRenderer grapplingLine;

        public Transform attachedTo;
        public Vector3 attachedToLocalPoint;
        
        public FloatNetworkValue grapplingHook = new(0f);
        public Vector3NetworkValue grapplingHookPosition = new(Vector3.zero);
        #endregion
        
        #region Unity Callbacks

        protected virtual void OnEnable()
        {
            WithValues(isRespawning, isPaused, username, grapplingHookPosition, grapplingHook);
            isRespawning.OnValueChanged += _OnRespawningChanged;
            isPaused.OnValueChanged += OnPausedChanged;
            grapplingHook.OnValueChanged += (old, @new) => grapplingLine.enabled = @new > 0;

            PauseManager.OnPause += _OnPause;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _isCinematicRunning = CinematicController.IsPlaying;
            CinematicController.OnCinematicStarted += () => _isCinematicRunning = true;
            CinematicController.OnCinematicEnded += () => _isCinematicRunning = false;


            _remoteBodyRotation = body.localEulerAngles.y;
            _remoteBodyPosition = body.localPosition;
            _multiplier = FallMultiplier;
            dustParticles.Stop();

            if (controller == null) TryGetComponent(out controller);
            InvokeRepeating(nameof(_Tick), 0, 1f / tickRate);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(_Tick));
        }

        private void OnPausedChanged(bool old, bool @new)
        {
            Debug.Log((CharacterType == CharacterType.Human ? "Nina" : "RAM") + (@new ? " paused " : " resumed") + " the game");
            emoteController.SetStatusText(@new ? "stats.pause" : "");
        }

        private void _OnRespawningChanged(bool old, bool @new)
        {
            Debug.Log((CharacterType == CharacterType.Human ? "Nina" : "RAM") + (@new ? " is respawning" : " respawned"));
            renderer.material.SetInt(Respawning, @new ? 1 : 0);
        }

        private void Update()
        {
            if (!HasAuthority || !IsOwnedByClient) return;

            _Timer();

            if (IsPlayerLocked)
            {
                if(DialogPanel.IsDialogPlaying || _isCinematicRunning)
                    if(SolisInput.GetKeyDown("Skip"))
                        SendPacket(new PlayerInputPackage { Key = KeyCode.Return, Id = Id, CharacterType = this.CharacterType}, true);
                return;
            }

            switch (state)
            {
                case State.Normal:
                    _Move();
                    _Jump();
                    _Interact();
                    _Special();
                    _GrapplingHook();
                    break;
                case State.Magnetized:
                    if (magnetAnchor == null)
                    {
                        state = State.Normal;
                        velocity = Vector3.zero;
                        controller.enabled = true;
                    }
                    else
                    {
                        var pos = magnetAnchor.position - magnetReferenceLocalPosition;
                        controller.enabled = false;
                        var playerPos = transform.position;
                        transform.position = Vector3.MoveTowards(playerPos, pos, Time.deltaTime * 15);
                    }

                    break;
                
                case State.GrapplingHook:
                    if(SolisInput.GetKeyDown("GrapplingHook"))
                        _ExitGrapplingHook();
                    break;
            }

            /*
            if (Input.GetKeyDown(KeyCode.Alpha1))
                emoteController.ShowEmote(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                emoteController.ShowEmote(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                emoteController.ShowEmote(2);
            */
        }

        private void FixedUpdate()
        {
            if (!HasAuthority || !IsOwnedByClient)
            {
                body.localEulerAngles = new Vector3(0,
                    Mathf.LerpAngle(body.localEulerAngles.y, _remoteBodyRotation, Time.fixedDeltaTime * 20), 0);
                body.localPosition = Vector3.Lerp(body.localPosition, _remoteBodyPosition, Time.fixedDeltaTime * 20);

                switch (state)
                {
                    case State.GrapplingHook:
                        _HandleGrapplingHookRemote();
                        break;
                    case State.Magnetized when magnetAnchor == null:
                        state = State.Normal;
                        velocity = Vector3.zero;
                        controller.enabled = true;
                        break;
                    case State.Magnetized:
                    {
                        var pos = magnetAnchor.position - magnetReferenceLocalPosition;
                        controller.enabled = false;
                        var playerPos = transform.position;
                        transform.position = Vector3.MoveTowards(playerPos, pos, Time.deltaTime * 15);
                        break;
                    }
                }


                return;
            }

            switch (state)
            {
                case State.GrapplingHook:
                    _HandleGrapplingHook();
                    break;
                
                case State.Normal:
                    _Gravity();
                    _HandlePlatform();

                    var camAngle = _camera!.eulerAngles.y;
                    var moveAngle = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                    var te = transform.eulerAngles;
                    var velocityXZ = new Vector2(velocity.x, velocity.z);
                    transform.eulerAngles = new Vector3(0,
                        Mathf.LerpAngle(te.y, camAngle, velocityXZ.magnitude * Time.fixedDeltaTime * RotationSpeed), 0);
                    body.localEulerAngles = new Vector3(0,
                        Mathf.LerpAngle(body.localEulerAngles.y, moveAngle,
                            velocityXZ.magnitude * Time.fixedDeltaTime * RotationSpeed * 1f));
                    if (velocityXZ.magnitude > 0.8f) dustParticles.Play();
                    else dustParticles.Stop();

                    var move = Quaternion.Euler(0, te.y, 0) * velocity;
                    if (_interactTimer > 0)
                        move = Vector3.zero;

                    var walking = velocityXZ.magnitude > 0.1f;
                    var nextPos = transform.position + (new Vector3(move.x, 0, move.z) * (Time.fixedDeltaTime * data.nextMoveMultiplier));

                    #if UNITY_EDITOR
                    debugNextMovePos = nextPos;
                    #endif

                    Physics.SyncTransforms();
                    if (Physics.CheckSphere(transform.position, 0.5f, LayerMask.GetMask("SafeGround")))
                    {
                        if (!Physics.Raycast(nextPos, Vector3.down, 1.1f) && !IsJumping && IsGrounded)
                        {
                            walking = false;
                            velocity.x = velocity.z = 0;
                            move = Vector3.zero;
                        }
                    }
                    if(carriedObject)
                    {
                        var boxNextPos = _isColliding ?
                            carriedObject.transform.position + ((new Vector3(move.x, 0, move.z) * (Time.fixedDeltaTime * data.nextMoveMultiplier))/2f) :
                            carriedObject.transform.position;
                        var size = Physics.OverlapSphere(
                            boxNextPos, carriedObject.objectSize.extents.x,
                            ~LayerMask.GetMask("Box", "CarriedIgnore", (CharacterType == CharacterType.Human ? "Human" : "Robot")), QueryTriggerInteraction.Ignore);

                        #if UNITY_EDITOR
                        debugNextBoxPos = boxNextPos;
                        #endif

                        if (size.Length > 0)
                        {
                            walking = false;
                            velocity.x = velocity.z = 0;
                            move = Vector3.zero;
                            _isColliding = true;

                            Debug.Log(CharacterType.ToString() + " is carrying a box that is colliding with: " + string.Join(", ", size.ToList().Select(x => x.name)), size[0]);
                        }else _isColliding = false;
                    }

                    controller.Move(new Vector3(move.x, velocity.y, move.z) * Time.fixedDeltaTime);
                    if(IsGrounded && Physics.Raycast(nextPos, Vector3.down, out var hit, 0.1f, groundMask))
                    {
                        _lastSafePosition = transform.position;
                    }

                    animator.SetBool("Grounded", !_isFalling && IsGrounded);
                    animator.SetBool("Falling", _isFalling);
                    animator.SetFloat("Running",
                        Mathf.Lerp(animator.GetFloat("Running"), walking ? 1 : 0, Time.deltaTime * 7f));

                    if (transform.position.y < -15)
                    {
                        Debug.Log($"Player {this.Id} has died by Falling into the Void");

                        SendPacket(new PlayerDeathPacket()
                        {
                            Type = Death.Fall,
                            Id = this.Id
                        });
                    }

                    break;
            }
        }

        public void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if(carriedObject)
            {
                var size = Physics.OverlapSphere(
                    debugNextBoxPos, carriedObject.objectSize.extents.x,
                    ~LayerMask.GetMask("Box", "CarriedIgnore", (CharacterType == CharacterType.Human ? "Human" : "Robot")), QueryTriggerInteraction.Ignore);

                Gizmos.color = size.Length > 0 ? Color.red : Color.green;
                Gizmos.DrawWireSphere(debugNextBoxPos, carriedObject.objectSize.extents.x);
            }

            if (Physics.Raycast(transform.position, Vector3.down, out var hit, 1.1f, groundMask))
            {
                Gizmos.color = hit.collider.gameObject.layer == LayerMask.NameToLayer("SafeGround")
                    ? Color.green
                    : Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }

            if (Physics.Raycast(debugNextMovePos, Vector3.down, 1.1f))
            {
                Gizmos.color = !IsJumping && IsGrounded ? Color.green : Color.yellow;
                Gizmos.DrawRay(debugNextMovePos, hit.point - debugNextMovePos);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(debugNextMovePos, Vector3.down);
            }

            Gizmos.color = IsGrounded ? Color.cyan : Color.blue;
            Gizmos.DrawWireCube(_lastSafePosition, Vector3.one);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(debugLastJumpMaxHeight, 0.5f);
#endif
        }
        #endregion

        #region Network Callbacks
        public override void OnSpawned(bool isRetroactive)
        {
            if (!HasAuthority)
                return;

            _camera = MulticamCamera.Instance.SetPlayerTarget(transform, lookAt);
            username.Value = NetworkManager.Instance.Name;

            if (DiscordController.Instance)
                DiscordController.Instance!.SetGameActivity(CharacterType, false, null);
        }
        
        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            switch (packet)
            {
                case PlayerBodyLerpPacket bodyLerpPacket:
                    if (clientId == OwnerId)
                        ServerBroadcastPacketExceptFor(bodyLerpPacket, clientId);
                    break;
                case PlayerDeathPacket deathPacket:
                    if (clientId == OwnerId)
                        ServerBroadcastPacket(deathPacket);
                    break;
            }
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            switch (packet)
            {
                case PlayerBodyLerpPacket bodyLerpPacket:
                    _remoteBodyRotation = bodyLerpPacket.BodyRotation;
                    _remoteBodyPosition = bodyLerpPacket.BodyPosition;
                    break;
                case PlayerDeathPacket deathPacket:
                    if (deathPacket.Id == Id && !isRespawning.Value)
                        PlayerDeath(deathPacket.Type);
                    break;
            }
        }
        #endregion
        
        #region Private Methods

        protected virtual void _GrapplingHook()
        {
            if (SolisInput.GetKeyDown("GrapplingHook"))
            {
                //raycast grappling hook
                var camRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
                var ray = new Ray(transform.position + camRay.direction, camRay.direction);
                if (Physics.Raycast(ray, out var hit, 100))
                {
                    attachedTo = hit.transform;
                    attachedToLocalPoint = attachedTo.InverseTransformPoint(hit.point);
                    state = State.GrapplingHook;
                }
                else
                {
                    attachedTo = null;
                }
            }
        }

        protected virtual void _ExitGrapplingHook()
        {
            var delta = grapplingHookPosition.Value - transform.position;
            var direction = delta.normalized;
            var v = 10 * grapplingHook.Value * direction;
            
            velocity = v;
            state = State.Normal;

            grapplingHook.Value = 0f;
        }

        protected virtual void _HandleGrapplingHook()
        {
            var deltaTime = Time.fixedDeltaTime;
            grapplingHook.Value = Mathf.Lerp(grapplingHook.Value,  1f, deltaTime * 10f);
            grapplingHookPosition.Value = attachedTo.TransformPoint(attachedToLocalPoint);

            _HandleGrapplingHookRemote();

            var delta = grapplingHookPosition.Value - transform.position;
            var direction = delta.normalized;
            var v = 10 * grapplingHook.Value * direction;

            controller.Move(v * deltaTime);
            velocity = Vector3.zero;
                
            if (delta.magnitude < 0.5f)
            {
                _ExitGrapplingHook();
            }
            else
            {
                var targetRotation = Quaternion.LookRotation(delta);
                var targetEuler = targetRotation.eulerAngles;

                transform.eulerAngles = new Vector3(0,
                    Mathf.LerpAngle(transform.eulerAngles.y, 0, Time.deltaTime * 20), 0);
                body.localEulerAngles = new Vector3(0,
                    Mathf.LerpAngle(body.localEulerAngles.y, targetEuler.y, deltaTime * 20), 0);
            }
        }

        protected virtual void _HandleGrapplingHookRemote()
        {
            var start = handPosition.position;
            grapplingLine.SetPositions(new[] {start, Vector3.Lerp(start, attachedTo.TransformPoint(attachedToLocalPoint), grapplingHook.Value)});
        }
        
        
        protected virtual void _Timer()
        {
            var deltaTime = Time.deltaTime;
            _coyoteTimer = IsGrounded ? CoyoteTime : _coyoteTimer - deltaTime;
            _interactTimer = _interactTimer > 0 ? _interactTimer - deltaTime : 0;
            if (_respawnTimer <= 0)
            {
                isRespawning.Value = false;
                _respawnTimer = RespawnCooldown;
            }
            else
            {
                _respawnTimer = isRespawning.Value ? _respawnTimer - deltaTime : RespawnCooldown;
            }
            
            if(IsGrounded) _jumpTimer -= deltaTime;
        }

        private void _OnPause(bool isPaused)
        {
            if (!this.isPaused.CheckPermission()) return;
            Debug.Log(gameObject.name + " is paused: " + isPaused);
            this.isPaused.Value = isPaused;
        }

        private void _Interact()
        {
            if (SolisInput.GetKeyDown("Interact") && _interactTimer <= 0 && IsGrounded)
            {
                animator.SetTrigger("Punch");
                _interactTimer = InteractCooldown;

                //Run after
                Task.Run(async () =>
                {
                    await Task.Delay(500);

                    SendPacket(new PlayerInteractPacket
                    {
                        Id = Id
                    }, true);
                });
            }
            if(DialogPanel.IsDialogPlaying)
                if(SolisInput.GetKeyDown("Skip"))
                    SendPacket(new PlayerInputPackage { Key = KeyCode.Return, Id = Id, CharacterType = this.CharacterType}, true);
        }
        
        protected virtual void _Special() { }

        private void _Move()
        {
            var moveInput = (!isPaused.Value && !DialogPanel.IsDialogPlaying) ? MoveInput.normalized : Vector2.zero;
            var maxSpeedTarget = _inJumpState ? MaxSpeed * AccelInJumpMultiplier : MaxSpeed;
            var target = moveInput * maxSpeedTarget;
            var accelOrDecel = (Mathf.Abs(moveInput.magnitude) > 0.01f);
            var accelerationValue = ((accelOrDecel ? Acceleration : Deceleration)) * Time.deltaTime;

            velocity.x = Mathf.MoveTowards(velocity.x, target.x, accelerationValue);
            velocity.z = Mathf.MoveTowards(velocity.z, target.y, accelerationValue);
        }

        private void _Jump()
        {
            if (InputJump && CanJump)
            {
                animator.SetTrigger("Jumping");
                IsJumping = true;
                _isJumpingEnd = false;
                _isJumpCut = false;
                _inJumpState = true;
                velocity.y = 0.1f;
                _startJumpPos = transform.position.y;
                _lastJumpHeight = transform.position.y;
                _lastJumpVelocity = velocity.y;
                _multiplier = JumpGravityMultiplier;
                _jumpTimer = TimeToJump;
                jumpParticles.Play();
                AudioSystem.Instance.PlayVfx("Jump").At(transform.position);
            }

            if(InputJumpUp && !_isJumpingEnd)
                _isJumpCut = true;
            
            if (_isJumpCut && CanJumpCut)
            {
                IsJumping = false;
                velocity.y *= 0.5f;
                _multiplier = JumpGravityMultiplier;
            }

            if (IsJumping)
            {
                velocity.y += JumpAcceleration * Time.deltaTime;
                var diff = Mathf.Abs((transform.position.y + (velocity.y*Time.fixedDeltaTime)) - _startJumpPos);
                if (diff >= JumpMaxHeight)
                {
                    IsJumping = false;
                    velocity.y *= MaxHeightDecel;
                    _multiplier = JumpGravityMultiplier;
                    Debug.Log("Max Height Reached");
                }
            }

            if (!_isJumpingEnd)
            {
#if UNITY_EDITOR
                if (debugJump)
                {
                    Debug.Log($"start: {_startJumpPos} current: {transform.position.y} diff: {Mathf.Abs(transform.position.y - _startJumpPos)}");
                }
#endif

                if(velocity.y <= 0)
                {
                    _isJumpingEnd = true;
                    _isJumpCut = false;
                    Debug.Log("Jump End");
#if UNITY_EDITOR
                    debugLastJumpMaxHeight = transform.position;
#endif
                }
            }
        }

        private void _Gravity()
        {
            if (IsGrounded)
            {
                _multiplier = FallMultiplier;
                if (_isFalling)
                {
                    _inJumpState = false;
                    _isFalling = false;
                    landParticles.Play();
                    Debug.Log("Landed");
                }
                return;
            }

            if (!_isFalling && (velocity.y < 0 || (!IsJumping && velocity.y > 1)))
                _isFalling = true;

            if (IsJumping)
            {
                var posY = transform.position.y;
                var expectedYPos = _lastJumpHeight + (_lastJumpVelocity * Time.fixedDeltaTime);
                var diff = Mathf.Abs(expectedYPos - posY);
                if(diff > 0.1f && posY < expectedYPos)
                {
                    IsJumping = false;
                    _isJumpCut = false;
                    _isJumpingEnd = true;
                    velocity.y *= HitHeadDecel;
                    _multiplier = JumpGravityMultiplier;
                    Debug.Log($"Hit head (ExpectedYPos: {expectedYPos} - CurrPos: {posY} - LastPos: {_lastJumpHeight} - Diff: {diff} - Vel: {velocity.y} - LastVel: {_lastJumpVelocity})");
                    return;
                }
                _lastJumpHeight = posY;
                _lastJumpVelocity = velocity.y;

                return;
            }

            velocity.y += Gravity * _multiplier * Time.fixedDeltaTime;
            velocity.y = Mathf.Max(velocity.y, -MaxFallSpeed);
        }

        private void _HandlePlatform()
        {
            if (!IsGrounded)
                return;

            var ray = new Ray(transform.position, Vector3.down);
            if (Physics.Raycast(ray, out var hit, 1.1f))
            {
                var platform = hit.collider.GetComponentInParent<CircuitPlatform>();
                if (platform != null)
                {
                    controller.Move(platform.DeltaSinceLastFrame);
                }
            }
        }

        private void _Tick()
        {
            if (!HasAuthority || !IsOwnedByClient)
                return;

            var pos = body.localPosition;
            var packet = new PlayerBodyLerpPacket
            {
                Id = Id,
                BodyRotation = body.localEulerAngles.y,
                BodyPosition = new Vector3(pos.x, pos.y, pos.z)
            };
            ServerBroadcastPacketExceptFor(packet, OwnerId);
        }

        private void _Respawn()
        {
            if (HasAuthority && IsOwnedByClient)
                isRespawning.Value = true;

            transform.position = _lastSafePosition + Vector3.up;
            velocity = Vector3.zero;

            landParticles.Play();

            _respawnTimer = RespawnCooldown;
        }

        #endregion

        #region Public Methods

        public void PlayerDeath(Death death)
        {
            Debug.Log("Player ID: " + Id + " died with type: " + death);
            controller.enabled = false;
            if(carriedObject)
            {
                if(carriedObject.isOn.CheckPermission())
                    carriedObject.isOn.Value = false;
                carriedObject = null;
            }

            if (HasAuthority && IsOwnedByClient)
            {
                SolisInput.Instance.RumblePulse(0.25f, 0.75f, 0.25f);
            }

            switch (death)
            {
                case Death.Stun:
                    Debug.Log("Death Smash");
                    break;
                case Death.Fall:
                    _Respawn();
                    AudioSystem.PlayVfxStatic("Death");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(death), death, null);
            }
            controller.enabled = true;
        }

        #endregion
    }

    #if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(PlayerControllerBase), true), CanEditMultipleObjects]
    public class PlayerControllerBaseEditor : UnityEditor.Editor
    {
        private PlayerControllerBase _player;
        private Editor _playerDataEditor;
        public override void OnInspectorGUI()
        {
            DrawSettingsEditor(_player.data, null, ref _player.playerDataFoldout, ref _playerDataEditor);

            base.OnInspectorGUI();
        }

        public void DrawSettingsEditor(Object settings, Action onSettingsUpdated, ref bool foldout, ref Editor editor)
        {
            if (settings == null) return;
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                foldout =  EditorGUILayout.InspectorTitlebar(foldout, settings);
                if (foldout)
                {
                    CreateCachedEditor(settings, null, ref editor);
                    editor.OnInspectorGUI();

                    if (check.changed)
                    {
                        onSettingsUpdated?.Invoke();
                    }
                }
            }
        }

        private void OnEnable()
        {
            _player = (PlayerControllerBase) target;
        }
    }
    #endif
}