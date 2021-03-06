using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PlayerController : PortalObject
{
    // Character Variables
    private Rigidbody playerRigidbody;
    private CapsuleCollider playerCollider;

    // Camera Variables
    Camera playerCamera;

    [Header ("Camera Movement")]
    [Tooltip ("Set whether the Cursor is hidden or shown.")]
    public bool lockCursor = false;
    public float mouseSensitivity = 10.0f;

    [Header ("Camera Controls")]
    [Tooltip ("Length of camera movement smoothing. Lower values = sharper stops. 0.1f offers a realistic feel.")]
    [Range (0.0f, 0.4f)]
    [SerializeField] private float cameraRotationSmoothTime = 0.1f;
    private float cameraPan; // Looking left and right
    private float cameraPanSmooth; // For smoothing the pan movement
    private float cameraPanSmoothVelocity; // Pan smoothing speed
    private float cameraTilt; // Looking up and down
    private float cameraTiltSmooth; // For smoothing the tilt movement
    private float cameraTiltSmoothVelocity; // Tilt smoothing speed
    [Tooltip ("Control the camera tilt range. X = Up. Y = Down. +-40 = A good range.")]
    [SerializeField] private Vector2 cameraTiltRange = new Vector2 (-40.0f, 40.0f); // Control how far player can look (up, down)

    [Header ("Sound Options")]
    public bool useSound;
    public AudioSource playFootsteps;
    public AudioClip[] walkingSounds;
    public AudioClip[] sprintingSounds;
    public float footstepVolume = 0.05f;
    private float stepTimer;

    // Movement Variables
    private Vector3 moveDirection;
    [Header ("Player Movement")]
    [Tooltip ("Walking speed. 5.0f feels good for shooter-like movement.")]
    [Range (2, 8)] public float walkSpeed = 5.0f;
    [Tooltip ("Sprinting speed. Usually 1.5x faster than walking speed for smooth movement change.")]
    [Range (4, 10)] public float sprintSpeed = 7.5f;
    [Tooltip ("Jump height. 10.0f feels good for arcade-like jump.")]
    private float currentSpeed; // For determining our speed in code
    [Tooltip ("Smooths player movement. Lower values = sharper stops. 0.1f feels cinematic.")]
    [Range (0.0f, 0.4f)]
    public float movementSmoothTime = 0.1f;
    [Tooltip ("Jump height. 7.5f feels good for arcade-like jumping (10.0f gravity). 10.0 for realistic jumping (20.0f gravity)")]
    public float jumpForce = 10.0f;
    public bool allowJumping = true;
    public bool slopes = false;
    private bool jumping;
    [Tooltip ("Amount of gravity. 10.0f feels good for arcade-like gravity. 20.0f for realistic gravity.")]
    public float gravityForce = 20.0f;
    private float fallingVelocity = 0.0f; // Keep track of falling speed
    private float yCollisionBounds = 0.0f; // Variable used in raycast to check if grounded
    private float lastGroundedTime = 0.0f; // Keep track of when last grounded
    private Vector3 velocity;
    private Vector3 currentVelocity;
    private bool sprinting;
    private bool moving;

    // Dissolving Floor Variables
    [HideInInspector] public bool dissolvingFloor;

    // Spherical Variables
    [HideInInspector] public bool sphericalMovement;
    [HideInInspector] public GameObject planetGameObject;
    [HideInInspector] public bool isModel;
    private Quaternion panRotation;
    private Quaternion playerToPlanetRotation;
    private GameObject playerChild; // Child object of the player

    // Wall-Walk Variables
    [HideInInspector] public bool wallWalk; // Enable wall walking
    [HideInInspector] public float gravityRotationSpeed = 4.0f; // How quickly should the player rotate
    [HideInInspector] public float wallWalkDetection = 1.5f; // How long should the raycast be
    [HideInInspector] public LayerMask groundLayers; // What layers are floor objects set as
    private Vector3 groundDirection; // What direction is the ground
    private bool wallWalkRotate;

    // Inner-Sphere Variables
    [HideInInspector] public bool insideSphere;
    [HideInInspector] public float insideSphereRadius = 5.0f;

    // Shooting Variables
    [HideInInspector] public bool enableShooting;
    [HideInInspector] public Rigidbody projectileRigidbody;
    [HideInInspector] public float projectileSpeed = 20;

    // Spherical world variables
    [HideInInspector] public bool sphericalWorld;
    [HideInInspector] public float sphereXAxis;
    [HideInInspector] public float sphereYAxis;

    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        playerChild = transform.GetChild(0).gameObject;

        // Assign controller variable to the Character Controller & relevant collider
        playerRigidbody = GetComponent<Rigidbody>();
        playerCollider = GetComponent<CapsuleCollider>();

        // Assign playerCamera as the main camera
        playerCamera = Camera.main;

        // If we want to lock the cursor then lock and hide it
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // Start is called before the first frame update
    void Start()
    {  
        // Establish playerCamera's variable values
        cameraPan = transform.eulerAngles.y;
        cameraPanSmooth = cameraPan;
        cameraTilt = playerCamera.transform.localEulerAngles.x;
        cameraTiltSmooth = cameraTilt;

        // Give currentSpeed variable a value
        currentSpeed = walkSpeed;
        
        // Set rigidbody values
        yCollisionBounds = playerCollider.bounds.extents.y;

        // Set direction of the ground
        groundDirection = transform.rotation.eulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        UpdateCameraMovement();
        UpdateGravity();
        UpdateDefaultMovement();
        FixedCameraMovement();

        if (useSound) UpdateSound();
        
        if (sphericalMovement && planetGameObject != null) UpdateSphericalRotation();

        if (insideSphere) UpdateInsideSphere();

        if (sphericalWorld) UpdateSphericalWorld();
        
        if (enableShooting && projectileRigidbody != null) UpdateShooting();

        if (Input.GetKey (KeyCode.W) || Input.GetKey (KeyCode.A) || Input.GetKey (KeyCode.S) || Input.GetKey (KeyCode.D))
        {
            if (CheckGrounded())
            {
                moving = true;
            }
            else
            {
                moving = false;
            }
        }
        else
        {
            moving = false;
        }
    }

    private void FixedUpdate()
    {
        
        if (wallWalk) UpdateWallWalk();

        playerRigidbody.AddForce (-transform.up * playerRigidbody.mass * gravityForce);


        // Apply movement to rigidbody based on calculations
        Vector3 localMove = transform.TransformDirection (velocity); // Final calculation
        playerRigidbody.AddForce (localMove - playerRigidbody.velocity, ForceMode.VelocityChange);
    }

    private void UpdateCameraMovement()
    {
        // Get mouse movement
        float mouseX = Input.GetAxisRaw ("Mouse X");
        float mouseY = Input.GetAxisRaw ("Mouse Y");

        // Stop camera from swinging down on game start
        float mouseMagnitude = Mathf.Sqrt (mouseX * mouseX + mouseY * mouseY);
        if (mouseMagnitude > 5) 
        {
            mouseX = 0;
            mouseY = 0;
        }

        cameraPan += mouseX * (mouseSensitivity * 100) * Time.deltaTime; // Move camera left & right
        cameraTilt -= mouseY * (mouseSensitivity * 100) * Time.deltaTime; // Move camera up & down

        // Clamp the camera pitch so that the there is a limit when looking up & down
        cameraTilt = Mathf.Clamp (cameraTilt, cameraTiltRange.x, cameraTiltRange.y);

        // Smooth camera movement
        cameraTiltSmooth = Mathf.SmoothDampAngle 
        (
            cameraTiltSmooth, 
            cameraTilt, 
            ref cameraTiltSmoothVelocity, 
            cameraRotationSmoothTime
        );
        cameraPanSmooth = Mathf.SmoothDampAngle 
        (
            cameraPanSmooth, 
            cameraPan, 
            ref cameraPanSmoothVelocity, 
            cameraRotationSmoothTime
        );

        // Get cameraPanSmooth float to work with rigidbody rotation by making it a Quaternion
        panRotation = Quaternion.Euler(0.0f, 1.0f * cameraPanSmooth, 0.0f);
    }

    private void FixedCameraMovement() 
    {
        // Horizontal camera movement. Uses the rigidbody to rotate.
        // playerRigidbody.rotation = panRotation;
        playerChild.transform.localRotation = panRotation;

        // Vertical camera movement. Uses Camera transform to rotate.
        playerCamera.transform.localEulerAngles = Vector3.right * cameraTiltSmooth;
    }

    private void UpdateGravity()
    {
        // Establish falling speed. Increase as the falling duration grows
        fallingVelocity -= gravityForce * Time.deltaTime;

        // Check for jump input and if true, check that the character isn't jumping or falling. Then jump
        if (Input.GetKeyDown (KeyCode.Space) && CheckGrounded() && allowJumping)
        {
            jumping = true;
            fallingVelocity = jumpForce;
        }
        else if (CheckGrounded() && fallingVelocity <= 0) // If there is collision below the player (ground)
        {
            jumping = false;
            lastGroundedTime = Time.time; // Set lastGroundedTime to the current time
            fallingVelocity = 0; // Stop fallingVelocity
        }
    }

    private void UpdateDefaultMovement()
    {
        // Create a new Vector2 variable that takes in our movement inputs
        Vector2 input = new Vector2 (Input.GetAxisRaw ("Horizontal"), Input.GetAxisRaw ("Vertical"));

        // Normalize the Vector2 input variable and make it a Vector3. Then multiply it with the set walk speed.
        Vector3 inputDirection = new Vector3 (input.x, 0, input.y).normalized;
        Vector3 inputMove = inputDirection * walkSpeed;

        // If sprint key is pressed then make our current speed equal to sprintSpeed 
        float speedOffset = 2; // Speed offset required to keep values tidy (Stop speed from being 0.5f, for example.)
        if (Input.GetKey (KeyCode.LeftShift) && CheckGrounded())
        {
            sprinting = true;
            currentSpeed = sprintSpeed / speedOffset;
        }
        else if (!jumping)
        {
            sprinting = false;
            currentSpeed = walkSpeed / speedOffset;
        }

        // Get and convert the direction of childObject for correct movement direction
        float facingDirection = playerChild.transform.localEulerAngles.y;
        Quaternion facingDirectionEuler = Quaternion.Euler (0.0f, facingDirection, 0.0f);

        // Create a new Vector3 that takes in childObject direction, input movement and current speed to then use in a movement smoothing calculation
        Vector3 targetVelocity = facingDirectionEuler * inputMove * currentSpeed; 
        velocity = Vector3.SmoothDamp 
        (
            velocity, 
            targetVelocity, 
            ref currentVelocity, 
            movementSmoothTime
        ); // ref currentVelocity because function needs to set a currentVelocity

        if (CheckCeilingCollision())
        {
            fallingVelocity = -1.0f;
        }

        // Set velocity to match the recorded movement from previous movement sections
        velocity = new Vector3 (velocity.x, fallingVelocity, velocity.z); // This is used in FixedUpdate() to move the player
    }

    // Boolean function that uses a raycast to see if there is ground within a superficial amount of the collider bounds.
    private bool CheckGrounded()
    {
        if (slopes) return Physics.Raycast (transform.position, -transform.up, yCollisionBounds + 0.5f);
        else if (!sphericalMovement || isModel)
        {
            // return Physics.Raycast (transform.position, -transform.up, yCollisionBounds + 0.1f);

            if (Physics.Raycast (new Vector3 (transform.position.x + 0.25f, transform.position.y, transform.position.z), -transform.up, yCollisionBounds + 0.05f) || 
            Physics.Raycast (new Vector3 (transform.position.x - 0.25f, transform.position.y, transform.position.z), -transform.up, yCollisionBounds + 0.05f) || Physics.Raycast (transform.position, -transform.up, yCollisionBounds + 0.025f))
            {
                return true;
            }
            else return false;
        }
        else return Physics.Raycast (transform.position, -transform.up, 0.1f);
    }

    private void UpdateSound()
    {
        if (moving)
        {
            stepTimer += Time.deltaTime * walkSpeed;

            if (sprinting && stepTimer > 0.5f)
            {
                playFootsteps.clip = sprintingSounds [Random.Range (0, sprintingSounds.Length)];
                playFootsteps.volume = footstepVolume * 1.5f;
                playFootsteps.PlayOneShot (playFootsteps.clip);
                stepTimer = 0;
            }
            else if (stepTimer > 1.0f)
            {
                playFootsteps.clip = walkingSounds [Random.Range (0, walkingSounds.Length)];
                // playFootsteps.volume = 0.05f;
                playFootsteps.volume = footstepVolume;
                playFootsteps.PlayOneShot (playFootsteps.clip);
                stepTimer = 0;
            }
        }
    }

    // Bool to check ceiling collision with capsule raycast for accurate detection
    private bool CheckCeilingCollision()
    {
        return Physics.Raycast (transform.position, transform.up, yCollisionBounds + 0.1f);
    }

    private void UpdateSphericalRotation()
    {
        // Get the target direction of the planetGameObject to the player
        Vector3 targetDirection = (playerRigidbody.position - planetGameObject.transform.position).normalized;

        // Create a smooth movement of adusting player rotation to match axis with the planetGameObject
        playerToPlanetRotation = Quaternion.Slerp 
        (
            transform.rotation, // Get player's current rotation
            Quaternion.FromToRotation (playerRigidbody.transform.up, targetDirection) * playerRigidbody.rotation, // Compare current position to planet pos
            cameraRotationSmoothTime * 2 // Speed of rotation
        );

        // Rotate the player
        playerRigidbody.MoveRotation (playerToPlanetRotation);
    }

    private void UpdateWallWalk()
    {
        Vector3 setGroundDirection = SurfaceAngle();

        setGroundDirection = new Vector3 (Mathf.Round (setGroundDirection.x), Mathf.Round (setGroundDirection.y), Mathf.Round (setGroundDirection.z));
        groundDirection = Vector3.Lerp (groundDirection, setGroundDirection, gravityRotationSpeed * Time.deltaTime);

        Quaternion wallRotation = Quaternion.FromToRotation (transform.up, groundDirection) * transform.rotation;

        transform.rotation = wallRotation;
    }

    // Check to see the angle of the object to climb
    // NOTE: See if a raycast can be drawn in a movement direction for more accurate ray hits
    Vector3 SurfaceAngle()
    {
        Vector3 hitDirection = Vector3.zero;
        Vector3 hitPosition = new Vector3 (transform.position.x, transform.position.y - 0.25f, transform.position.z);
        
		Vector3 localMove = transform.TransformDirection (velocity); // Final calculation

        // Front raycast
        RaycastHit rayFront;
        Physics.Raycast (hitPosition, localMove, out rayFront, wallWalkDetection, groundLayers);
        if (rayFront.transform != null)
        {
            hitDirection += rayFront.normal;
        }

        return hitDirection.normalized;
    }

    private void UpdateInsideSphere()
    {
        Vector4 playerPos = new Vector4 (transform.position.x, transform.position.y, transform.position.z, 0);
        Shader.SetGlobalVector ("_PlayerPos", playerPos);
        Shader.SetGlobalFloat ("_MaskRadius", insideSphereRadius);
    }

    private void UpdateSphericalWorld()
    {
        if (!insideSphere)
        {
            Vector4 playerPos = new Vector4 (transform.position.x, transform.position.y, transform.position.z, 0);
            Shader.SetGlobalVector ("_PlayerPos", playerPos);
        }

        Shader.SetGlobalFloat ("_SphereXAxis", sphereXAxis);
        Shader.SetGlobalFloat ("_SphereYAxis", sphereYAxis);
    }

    private void UpdateShooting()
    {
        if (Input.GetMouseButtonDown (0))
        {
            // Get Vector3 of desired spawn location of the projectile.
            Vector3 projectilePosition = new Vector3 (playerCamera.transform.position.x, playerCamera.transform.position.y - 0.15f, playerCamera.transform.position.z);

            // Get Quaternion to ensure the projectile travels in the direction the player is facing
            Quaternion projectileRotation = Quaternion.Euler (transform.rotation.x, playerChild.transform.localRotation.y, transform.rotation.z);

            // Instantiate the projectile with the above variables
            Rigidbody projectile = Instantiate (projectileRigidbody, projectilePosition, projectileRotation) as Rigidbody;

            // Disable gravity so we can apply our own
            projectile.useGravity = false;

            // Add a sphere collider to allow the projectile to be deleted upon impact
            SphereCollider sphereCollider = projectile.gameObject.AddComponent <SphereCollider>();
            sphereCollider.isTrigger = true;

            // Add forward momentum to launch the projectile
            projectile.AddForce (playerCamera.transform.forward * (projectileSpeed * 20));
        }

    }

    // Function that handles player teleportation within portals
    public override void Teleport (Transform inPortal, Transform outPortal, Vector3 teleportPosition, Quaternion teleportRotation) 
    {
        // Rotate the player game object to match the outPortal's X & Z rotations. Doesn't work if LateUpdate() lock is being called.
        if (outPortal.eulerAngles.x > 1 || outPortal.eulerAngles.z > 1 || outPortal.eulerAngles.x < -1 || outPortal.eulerAngles.z < -1)
        {
            Vector3 relativeRot = (outPortal.rotation.eulerAngles * -1) + transform.rotation.eulerAngles; // Get the opposite rotation of current rotation
            if (relativeRot.y < 1 && relativeRot.y > -1) relativeRot.y += 180;
            else if (relativeRot.y < -179 && relativeRot.y > -181) relativeRot.y += 180;
            else if (relativeRot.y < 181 && relativeRot.y > 179) relativeRot.y += 180;

            Vector3 cameraRot = (outPortal.rotation.eulerAngles * -1) + playerChild.transform.rotation.eulerAngles; // Get the opposite rotation of current rotation
            Quaternion teleRot = outPortal.rotation * Quaternion.Euler (cameraRot); // Establish rotation variable for correct way to face after teleportation
            
            float shortestDistance = Mathf.DeltaAngle (cameraPanSmooth, teleRot.y); // Calculate the shortest distance between player's camera rotation and desired teleportation rotation

            cameraPan += shortestDistance; // Establish correct rotation for left & right camera movement

            cameraPanSmooth += shortestDistance; // Correct Yaw Smooth with calculated shortest distance to allow for continuity

            playerRigidbody.transform.rotation = Quaternion.Euler (relativeRot);
            playerChild.transform.rotation = Quaternion.Euler (transform.up * cameraPanSmooth); // Set player rotation to correct rotation. It should match the implied direction before entering portals
        }
        else
        {
            Vector3 eulerRotation = teleportRotation.eulerAngles; // Create a Vector3 of quaternion for transform calculation

            float shortestDistance = Mathf.DeltaAngle (cameraPanSmooth, eulerRotation.y); // Calculate the shortest distance between player's camera rotation and desired teleportation rotation

            Vector3 correctRotation = new Vector3 (0, 0, 0);
            transform.rotation = Quaternion.Euler (correctRotation);

            cameraPan += shortestDistance; // Establish correct rotation for left & right camera movement

            cameraPanSmooth += shortestDistance; // Correct Yaw Smooth with calculated shortest distance to allow for continuity

            playerChild.transform.rotation = Quaternion.Euler (transform.up * cameraPanSmooth); // Set player rotation to correct rotation. It should match the implied direction before entering portals
        }

        velocity = outPortal.TransformVector (inPortal.InverseTransformVector (-velocity)); // Move player off the dotProduct (mid point) of the portal and match velocity of entering

        playerRigidbody.transform.position = teleportPosition; // Set player position to the calculated teleport location

        Physics.SyncTransforms(); // Sync physics to stop drifting
    }
}
