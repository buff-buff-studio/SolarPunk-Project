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
using Solis.Interface;
using Solis.Interface.Input;
using Solis.Misc;
using Solis.Misc.Integrations;
using Solis.Misc.Multicam;
using Solis.Misc.Props;
using Solis.Packets;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Solis.Player
{
    public enum InteractionType : int
    {
        None = -1,
        Lever = 0,
        Button = 1,
        Scanner = 2,
        Valve = 3,
        Box = 4,
        Cable
    }

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
            GrapplingHook,
            SpawnCloud
        }

        /// <summary>
        /// Represents the death type of the player.
        /// </summary>
        public enum Death
        {
            Stun,
            Fall,
            Void
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

        [Header("FOCUS")]
        public Transform focusBody;
        public Transform focusLookAt;
        public float focusAcceleration = 10;
        public float focusMaxVelocity = 10;
        public Vector2 angleLimits = new Vector2(30, 30);
        private Vector2 _focusVelocity;

        [Header("MAGNETIZED")]
        public Vector3 magnetReferenceLocalPosition = new Vector3(0, 2, 0);
        public Transform magnetAnchor;

        [Header("HAND")]
        public Transform handPosition;
        public Transform boxPlacedPosition;
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
        private protected bool _isJumpCut;
        private float _lastJumpHeight;
        private float _lastJumpVelocity;
        
        private bool _isFalling;
        
        private bool _isCinematicRunning = false;

        private bool _positionReset;
        private float _respawnTimer;
        private float _interactTimer;
        private float _multiplier;

        private bool _isColliding;
        private LayerMask _defaultExcludeMask;

        private Vector3 _lastSafePosition;
        private Transform _camera;

        private bool _waitingForInteract;
        private protected bool _isInteracting;
        private InteractionType _lastInteractionType;
        private protected bool _isCarrying;

        private bool _isFocused;

        private static readonly int Respawning = Shader.PropertyToID("_Respawning");

        //Cheats
        private bool _godMode;
        private protected bool _flyMode;
        private bool _noClip;

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
        private bool CanJump => !IsJumping && (IsGrounded || _coyoteTimer > 0) && _jumpTimer <= 0 && !isPaused.Value && !DialogPanel.IsDialogPlaying && !_isFocused;

        private protected bool CanJumpCut =>
            IsJumping && (transform.position.y - _startJumpPos) >= JumpCutMinHeight;
        private bool IsPlayerLocked => _isCinematicRunning || isRespawning.Value || _isInteracting || _isFocused;
        private Vector3 HeadOffset => headOffset.position;
        private Animator Animator => animator.Animator;
        #endregion

        #region Unity Callbacks

        protected virtual void OnEnable()
        {
            WithValues(isRespawning, isPaused, username);
            isRespawning.OnValueChanged += _OnRespawningChanged;
            isPaused.OnValueChanged += OnPausedChanged;

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

            _defaultExcludeMask = controller.excludeLayers;

            if (controller == null) TryGetComponent(out controller);
            InvokeRepeating(nameof(_Tick), 0, 1f / tickRate);

            CheatsManager.Instance?.ChangeScene(this);
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

            if(SolisInput.GetKeyDown("Cheat"))
            {
                if (CheatsManager.IsCheatsEnabled)
                {
                    CheatsManager.Instance.DisableCheats();
                    SetCheatsValue();
                }
                else
                {
                    CheatsManager.Instance.EnableCheats(this);
                }
            }

            if (IsPlayerLocked)
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0, Deceleration);
                velocity.z = Mathf.MoveTowards(velocity.z, 0, Deceleration);

                if(state == State.Normal && _isFocused)
                {
                    GrapplingHook();
                    _Focus();
                }

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
                    _Focus();
                    GrapplingHook();
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
                        ExitGrapplingHook();
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
                        HandleGrapplingHookRemote();
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
                    HandleGrapplingHook();
                    break;

                case State.SpawnCloud:
                    controller.Move(new Vector3(0, Mathf.Max(velocity.y, .1f), 0) * Time.fixedDeltaTime);
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
                    //if (_interactTimer > 0) move = Vector3.zero;

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

#if UNITY_EDITOR
                        debugNextBoxPos = boxNextPos;
#endif

                        if(!_noClip)
                        {
                            var size = Physics.OverlapSphere(
                                boxNextPos, carriedObject.objectSize.extents.x,
                                ~LayerMask.GetMask("Box", "CarriedIgnore", "PressurePlate",
                                    (CharacterType == CharacterType.Human ? "Human" : "Robot")),
                                QueryTriggerInteraction.Ignore);

                            if (size.Length > 0)
                            {
                                walking = false;
                                velocity.x = velocity.z = 0;
                                move = Vector3.zero;
                                _isColliding = true;

                                Debug.Log(
                                    CharacterType.ToString() + " is carrying a box that is colliding with: " +
                                    string.Join(", ", size.ToList().Select(x => x.name)), size[0]);
                            }
                            else _isColliding = false;
                        }
                    }

                    controller.Move(new Vector3(move.x, velocity.y, move.z) * Time.fixedDeltaTime);
                    if(IsGrounded && Physics.Raycast(nextPos, Vector3.down, out var hit, 0.1f, groundMask))
                    {
                        _lastSafePosition = transform.position;
                    }

                    animator.SetBool("Grounded", !_isFalling && IsGrounded);
                    animator.SetBool("Falling", _isFalling || (_flyMode && !IsGrounded));
                    animator.SetFloat("Running",
                        Mathf.Lerp(animator.GetFloat("Running"), walking ? 1 : 0, Time.deltaTime * 7f));

                    if (_isInteracting)
                    {
                        //Ele chama o void OnEndInteract() quando termina a animação da blend tree
                        var anim = Animator.GetCurrentAnimatorStateInfo(0);
                        if(anim.normalizedTime >= 1f)
                        {
                            _isInteracting = false;
                            OnEndInteract();
                        }
                    }

                    if (transform.position.y < -15)
                    {
                        SendPacket(new PlayerDeathPacket()
                        {
                            Type = Death.Void,
                            Id = this.Id
                        });
                    }

                    break;
            }
        }

        #region Grappling Hook
        protected virtual void GrapplingHook() {}
        protected virtual void HandleGrapplingHook() {}
        protected virtual void ExitGrapplingHook() {}
        protected virtual void HandleGrapplingHookRemote()
        {
        }
        #endregion

        public void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if(carriedObject)
            {
                var size = Physics.OverlapSphere(
                    debugNextBoxPos, carriedObject.objectSize.extents.x,
                    ~LayerMask.GetMask("Box", "CarriedIgnore", (CharacterType == CharacterType.Human ? "Human" : "Robot")), QueryTriggerInteraction.Ignore);

                var placed = Physics.OverlapBox(
                    boxPlacedPosition.position, carriedObject.objectSize.extents, carriedObject.transform.rotation,
                    ~LayerMask.GetMask("Box", "CarriedIgnore", (CharacterType == CharacterType.Human ? "Human" : "Robot")), QueryTriggerInteraction.Ignore);

                Gizmos.color = size.Length > 0 ? Color.red : Color.green;
                Gizmos.DrawWireSphere(debugNextBoxPos, carriedObject.objectSize.extents.x);

                Gizmos.color = placed.Length > 0 ? Color.red : Color.green;
                Gizmos.DrawWireCube(boxPlacedPosition.position, carriedObject.objectSize.extents);
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

        public void PlayInteraction(InteractionType type)
        {
            if (!HasAuthority || !IsOwnedByClient) return;

            _lastInteractionType = type;
            _waitingForInteract = false;
            switch (type)
            {
                case InteractionType.None:
                    animator.SetFloat("Interaction", 0);
                    Debug.Log("Maybe you're interacting nothing");
                    break;
                case InteractionType.Box:
                    animator.SetFloat("Interaction", 1);
                    break;
                default:
                    animator.SetFloat("Interaction", 0);
                    Debug.Log($"Interaction type {type} does not have an specific animation");
                    break;
            }
            animator.SetBool("Interact", true);
            _isInteracting = true;
        }

        public void OnEndInteract()
        {
            Debug.Log("End Interact");
            switch (_lastInteractionType)
            {
                case InteractionType.Box:
                    if (_isCarrying)
                    {
                        Debug.Log("Dropping Box");
                        _isCarrying = false;
                        carriedObject?.DropBox();
                        carriedObject = null;
                    }else _isCarrying = true;

                    animator.SetFloat("CarryingBox", _isCarrying ? 1 : 0);
                    break;
            }
            animator.SetBool("Interact", false);
        }
        #endregion

        #region Network Callbacks
        public override void OnSpawned(bool isRetroactive)
        {
            if (!HasAuthority)
                return;

            Crosshair.Instance.characterType = CharacterType;
            _camera = MulticamCamera.Instance.SetPlayerTarget(transform, lookAt, focusBody, focusLookAt);
            username.Value = NetworkManager.Instance.Name;

            if (!HasAuthority || !IsOwnedByClient) return;
            if (DiscordController.Instance)
                DiscordController.Instance!.SetGameActivity(CharacterType, false, null);
        }
        
        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            switch (packet)
            {
                case PlayerBodyLerpPacket bodyLerpPacket:
                    if (clientId == OwnerId)
                        ServerBroadcastPacket(bodyLerpPacket);
                    break;
                case PlayerDeathPacket deathPacket:
                    if (clientId == OwnerId)
                        ServerBroadcastPacket(deathPacket);
                    break;
                
                case PlayerGrapplingHookPacket grapplingHook:
                    if (clientId == OwnerId)
                        ServerBroadcastPacket(grapplingHook);
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
                case InteractObjectPacket interactObjectPacket:
                    if (interactObjectPacket.Id == Id)
                        PlayInteraction(interactObjectPacket.Interaction);
                    break;
                
                case PlayerGrapplingHookPacket grapplingHook:
                    if (HasAuthority)
                        return;
                    animator.SetBool("Hooking", false);
                    state = grapplingHook.Grappled ? State.GrapplingHook : State.Normal;
                    break;
            }
        }
        #endregion
        
        #region Private Methods
        protected virtual void _Timer()
        {
            var deltaTime = Time.deltaTime;
            _coyoteTimer = IsGrounded ? CoyoteTime : _coyoteTimer - deltaTime;
            _interactTimer = _interactTimer > 0 ? _interactTimer - deltaTime : 0;
            if (_respawnTimer <= 0)
            {
                isRespawning.Value = false;
                _respawnTimer = RespawnCooldown;
                RespawnHUD.Instance.CloseHUD();
            }
            else
            {
                _respawnTimer = isRespawning.Value ? _respawnTimer - deltaTime : RespawnCooldown;
                if (RespawnCooldown - _respawnTimer >= .5f && !_positionReset)
                {
                    _positionReset = true;
                    transform.position = _lastSafePosition + Vector3.up;
                    velocity = Vector3.zero;
                    landParticles.Play();
                }
            }
            
            if(IsGrounded) _jumpTimer -= deltaTime;
        }

        private void _OnPause(bool isPaused)
        {
            if (!this.isPaused.CheckPermission()) return;
            
            this.isPaused.Value = isPaused;
        }

        private void _Interact()
        {
            if (SolisInput.GetKeyDown("Interact") && _interactTimer <= 0 && (IsGrounded || _flyMode))
            {
                if(_isCarrying)
                {
                    var size = Physics.OverlapSphere(
                        boxPlacedPosition.position, carriedObject.objectSize.extents.x,
                        ~LayerMask.GetMask("Box", "CarriedIgnore", "PressurePlate", "CubeTrigger", (CharacterType == CharacterType.Human ? "Human" : "Robot")), QueryTriggerInteraction.Ignore);

                    if (size.Length > 0)
                    {
                        Debug.LogWarning(CharacterType.ToString() + " is trying to place a box inside of " + size[0].gameObject.name, size[0]);
                        return;
                    }
                }

                _waitingForInteract = true;
                _interactTimer = InteractCooldown;

                SendPacket(new PlayerInteractPacket
                {
                    Id = Id
                }, true);
            }
            if(DialogPanel.IsDialogPlaying)
                if(SolisInput.GetKeyDown("Skip"))
                    SendPacket(new PlayerInputPackage { Key = KeyCode.Return, Id = Id, CharacterType = this.CharacterType}, true);
            
            if(InteractablePanel.IsDialogPlaying)
                if(SolisInput.GetKeyDown("Skip"))
                    SendPacket(new PlayerInputPackage { Key = KeyCode.Return, Id = Id, CharacterType = this.CharacterType}, true);
        }

        private void _Focus()
        {
            if (SolisInput.GetKeyDown("Focus") && !IsPlayerLocked && IsGrounded)
            {
                SetFocus(true);
            }
            else if(_isFocused)
            {
                var camInput = SolisInput.GetVector2("Look");

                var target = camInput * focusMaxVelocity;
                var accelerationValue = focusAcceleration * Time.deltaTime;
                _focusVelocity.x = Mathf.MoveTowards(_focusVelocity.x, target.x, accelerationValue);
                _focusVelocity.y = Mathf.MoveTowards(_focusVelocity.y, target.y, accelerationValue);

                FocusLimitsCheck();

                focusBody.Rotate(Vector3.up, _focusVelocity.x);
                focusBody.Rotate(Vector3.right, -_focusVelocity.y);

                if (SolisInput.GetKeyUp("Focus"))
                {
                    SetFocus(false);
                }
            }
        }

        private void FocusLimitsCheck()
        {
            if (focusBody.localEulerAngles.x > angleLimits.x && focusBody.localEulerAngles.x < 360 - angleLimits.x)
            {
                if (focusBody.localEulerAngles.x > 180)
                    focusBody.localEulerAngles = new Vector3(360 - angleLimits.x, focusBody.localEulerAngles.y, focusBody.localEulerAngles.z);
                else
                    focusBody.localEulerAngles = new Vector3(angleLimits.x, focusBody.localEulerAngles.y, focusBody.localEulerAngles.z);
            }

            if(focusBody.localEulerAngles.y > angleLimits.y && focusBody.localEulerAngles.y < 360 - angleLimits.y)
            {
                if (focusBody.localEulerAngles.y > 180)
                    focusBody.localEulerAngles = new Vector3(focusBody.localEulerAngles.x, 360 - angleLimits.y, focusBody.localEulerAngles.z);
                else
                    focusBody.localEulerAngles = new Vector3(focusBody.localEulerAngles.x, angleLimits.y, focusBody.localEulerAngles.z);
            }
        }

        private protected void SetFocus(bool focus)
        {
            Debug.Log("Focus: " + focus);

            if (focus)
            {
                focusBody.eulerAngles = new Vector3(_camera.eulerAngles.x, _camera.eulerAngles.y, 0);
                Debug.Log("Focus Position: " + focusBody.localRotation.eulerAngles);
            }

            MulticamCamera.Instance.SetFocus(focus);
            _isFocused = focus;
            
            Crosshair.Instance.SetCrosshaiActive(focus);
        }
        
        protected virtual void _Special() { }

        private void _Move()
        {
            var moveInput = (!isPaused.Value && !DialogPanel.IsDialogPlaying) ? MoveInput.normalized : Vector2.zero;
            var maxSpeedTarget = _inJumpState ? MaxSpeed * AccelInJumpMultiplier : MaxSpeed;

            if (_flyMode)
            {
                maxSpeedTarget = 14;
                var moveYInput = SolisInput.GetAxis("Fly");
                var targetY = moveYInput * maxSpeedTarget;
                var accelOrDecelY = (Mathf.Abs(moveYInput) > 0.01f);
                var accelerationValueY = ((accelOrDecelY ? Acceleration : Deceleration)) * Time.deltaTime;

                velocity.y = Mathf.MoveTowards(velocity.y, targetY, accelerationValueY);
            }

            var target = moveInput * maxSpeedTarget;
            var accelOrDecel = (Mathf.Abs(moveInput.magnitude) > 0.01f);
            var accelerationValue = ((accelOrDecel ? Acceleration : Deceleration)) * Time.deltaTime;

            velocity.x = Mathf.MoveTowards(velocity.x, target.x, accelerationValue);
            velocity.z = Mathf.MoveTowards(velocity.z, target.y, accelerationValue);
        }

        private void _Jump()
        {
            if(_flyMode) return;
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
            if (_flyMode) return;
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
            SendPacket(packet);
        }

        public static Action onRespawn;
        private void _Respawn()
        {
            if (HasAuthority && IsOwnedByClient)
            {
                isRespawning.Value = true;
                SetFocus(false);
                RespawnHUD.Instance.ShowHUD(CharacterType, 1);
            }
            
            //onRespawn?.Invoke();
            velocity = Vector3.zero;
            _positionReset = false;

            _respawnTimer = RespawnCooldown;
        }

        #endregion

        #region Public Methods

        public void Respawn()
        {
            if(CheatsManager.IsCheatsEnabled)
                _Respawn();
        }

        public void PlayerDeath(Death death)
        {
            Debug.Log("Player ID: " + Id + " died with type: " + death);
            onRespawn?.Invoke();
            switch (death)
            {
                case Death.Stun:
                    Debug.Log("Death Smash");
                    DeathReset();
                    break;
                case Death.Fall:
                    if(_godMode) return;
                    DeathReset();
                    _Respawn();
                    AudioSystem.PlayVfxStatic("Death");
                    break;
                case Death.Void:
                    if(_godMode && _flyMode) return;
                    DeathReset();
                    _Respawn();
                    AudioSystem.PlayVfxStatic("Death");
                    Debug.Log($"Player {this.Id} has died by Falling into the Void");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(death), death, null);
            }
            controller.enabled = true;
        }

        private void DeathReset()
        {
            controller.enabled = false;

            _isCarrying = false;
            animator.SetFloat("CarryingBox", 0);
            animator.SetBool("Interact", false);
            if(carriedObject)
            {
                carriedObject._Reset();
                carriedObject = null;
            }
            if (HasAuthority && IsOwnedByClient)
            {
                SolisInput.Instance.RumblePulse(0.25f, 0.75f, 0.25f);
            }
        }

        protected internal void SetCheatsValue(int id = -1, bool value = false)
        {
            if(!CheatsManager.IsCheatsEnabled)
            {
                _godMode = _flyMode = _noClip = false;
                controller.excludeLayers = _defaultExcludeMask;
                Debug.LogWarning("Cheats are not enabled");
                return;
            }

            switch (id)
            {
                case -1:
                    SetCheatsValue(0);
                    SetCheatsValue(1);
                    SetCheatsValue(2);
                    break;
                case 0:
                    _godMode = value;
                    Debug.Log("God Mode: " + _godMode);
                    break;
                case 1:
                    _flyMode = value;
                    if (_flyMode)
                    {
                        velocity.y = 0;
                        _inJumpState = false;
                        _isFalling = false;
                        _isJumpingEnd = true;
                    }
                    Debug.Log("Fly Mode: " + _flyMode);
                    break;
                case 2:
                    _noClip = value;
                    controller.excludeLayers = _noClip ? ~0 : _defaultExcludeMask;
                    Debug.Log("No Clip: " + _noClip);
                    break;
            }
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