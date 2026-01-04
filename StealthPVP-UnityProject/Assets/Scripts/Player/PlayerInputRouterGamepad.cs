using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gamepad-oriented input router. Uses stick axes for movement/aiming and button/key bindings for actions.
/// </summary>
public class PlayerInputRouterGamepad : PlayerInputRouter
{
    [Header("Gamepad Axes")]
    [SerializeField, Tooltip("Left stick horizontal axis name.")] private string moveHorizontalAxis = "Horizontal2";
    [SerializeField, Tooltip("Left stick vertical axis name.")] private string moveVerticalAxis = "Vertical2";
    [SerializeField, Tooltip("Right stick horizontal axis name.")] private string aimHorizontalAxis = "AimHorizontal2";
    [SerializeField, Tooltip("Right stick vertical axis name.")] private string aimVerticalAxis = "AimVertical2";
    [SerializeField, Tooltip("Fallback axis names to try for right stick horizontal if the primary name returns 0.")] private List<string> aimHorizontalFallbackAxes = new List<string> { "4th Axis", "Axis4", "RightStickHorizontal", "RightStickX" };
    [SerializeField, Tooltip("Fallback axis names to try for right stick vertical if the primary name returns 0.")] private List<string> aimVerticalFallbackAxes = new List<string> { "5th Axis", "Axis5", "RightStickVertical", "RightStickY" };
    [Header("Trigger")]
    [SerializeField, Tooltip("Analog trigger axis name used for primary/attack (e.g., \"RT\" or \"TriggerRight\"). Leave empty to ignore.")] private string primaryTriggerAxis = "";
    [SerializeField, Tooltip("Normalized trigger value required to count as pressed.")] private float triggerPressThreshold = 0.5f;
    [SerializeField, Tooltip("If true, 0 means fully pressed and 1 means released (invert axis).")] private bool invertTriggerAxis = false;
    [SerializeField, Tooltip("If true, the trigger will drive primary pressed/held/released.")] private bool useTriggerForPrimary = false;
    [Header("Secondary Trigger")]
    [SerializeField, Tooltip("Analog trigger axis name used for secondary/stun (e.g., \"RT\" or \"TriggerRight\"). Leave empty to ignore.")] private string secondaryTriggerAxis = "";
    [SerializeField, Tooltip("Normalized trigger value required to count as pressed for secondary.")] private float secondaryTriggerPressThreshold = 0.5f;
    [SerializeField, Tooltip("If true, 0 means fully pressed and 1 means released (invert axis).")] private bool invertSecondaryTriggerAxis = false;
    [SerializeField, Tooltip("If true, the trigger will drive secondary pressed/held/released.")] private bool useTriggerForSecondary = false;
    [Header("Buttons (set via LocalVersusGameManager)")]
    [SerializeField, Tooltip("Optional button name for primary/attack. Leave empty to use keycodes only.")] private string primaryButton = "";
    [SerializeField, Tooltip("Optional button name for secondary attack. Leave empty to use keycodes only.")] private string secondaryButton = "";
    [SerializeField, Tooltip("Optional button name for jump. Leave empty to use keycodes only.")] private string jumpButton = "";
    [SerializeField, Tooltip("Optional button name for dash. Leave empty to use keycodes only.")] private string dashButton = "";
    [SerializeField, Tooltip("Optional button name for run. Leave empty to use keycodes only.")] private string runButton = "";
    [SerializeField, Tooltip("Optional button name for interact. Leave empty to use keycodes only.")] private string interactButton = "";

    [Header("Debug")]
    [SerializeField, Tooltip("Prints when buttons/axes are detected; useful for finding the right joystick button ids.")] private bool debugInputs = false;
    [Header("Axis Inversion")]
    [SerializeField] private bool invertMoveX;
    [SerializeField] private bool invertMoveY;
    [SerializeField] private bool invertAimX;
    [SerializeField] private bool invertAimY;

    private KeyCode primaryKeyCode = KeyCode.JoystickButton2;
    private KeyCode secondaryKeyCode = KeyCode.None;
    private KeyCode jumpKeyCode = KeyCode.JoystickButton0;
    private KeyCode dashKeyCode = KeyCode.JoystickButton1;
    private KeyCode runKeyCode = KeyCode.JoystickButton5;
    private KeyCode interactKeyCode = KeyCode.JoystickButton3;
    private KeyCode interactKeyCodeAlt = KeyCode.None;
    private bool _previousPrimaryHeld;
    private bool _previousSecondaryHeld;
    [Header("Aim")]
    [SerializeField, Tooltip("Meters ahead of the player to place the aim point when using the right stick.")] private float aimDistance = 8f;
    [SerializeField, Tooltip("Deadzone for aim stick.")] private float aimDeadZone = 0.15f;

    public override PlayerInputSnapshot PollInput()
    {
        if (!IsInputAllowed)
        {
            _previousPrimaryHeld = false;
            _previousSecondaryHeld = false;
            return default;
        }

        float triggerValue = SafeGetAxisRaw(primaryTriggerAxis);
        if (invertTriggerAxis)
        {
            triggerValue = 1f - triggerValue;
        }
        bool triggerHeld = useTriggerForPrimary && triggerValue >= triggerPressThreshold;

        float secondaryTriggerValue = SafeGetAxisRaw(secondaryTriggerAxis);
        if (invertSecondaryTriggerAxis)
        {
            secondaryTriggerValue = 1f - secondaryTriggerValue;
        }
        bool secondaryTriggerHeld = useTriggerForSecondary && secondaryTriggerValue >= secondaryTriggerPressThreshold;

        bool primaryButtonHeld = GetButton(primaryButton, primaryKeyCode, false);
        bool primaryHeld = primaryButtonHeld || triggerHeld;
        bool primaryPressed = primaryHeld && !_previousPrimaryHeld;
        bool primaryReleased = !primaryHeld && _previousPrimaryHeld;

        bool secondaryHeld = GetButton(secondaryButton, secondaryKeyCode, false) || secondaryTriggerHeld;
        bool secondaryPressed = secondaryHeld && !_previousSecondaryHeld;
        bool secondaryReleased = !secondaryHeld && _previousSecondaryHeld;

        Vector2 moveAxis = new Vector2(
            SafeGetAxisRaw(moveHorizontalAxis),
            SafeGetAxisRaw(moveVerticalAxis));
        if (invertMoveX)
        {
            moveAxis.x = -moveAxis.x;
        }
        if (invertMoveY)
        {
            moveAxis.y = -moveAxis.y;
        }

        PlayerInputSnapshot snapshot = new PlayerInputSnapshot
        {
            RunHeld = GetButton(runButton, runKeyCode, false),
            StopPressed = false,
            JumpPressed = GetButtonDown(jumpButton, jumpKeyCode, false),
            DashPressed = GetButtonDown(dashButton, dashKeyCode, false),
            InteractPressed = GetButtonDown(interactButton, interactKeyCode, false),
            InteractAltPressed = interactKeyCodeAlt != KeyCode.None && Input.GetKeyDown(interactKeyCodeAlt),
            PrimaryPressed = primaryPressed,
            PrimaryHeld = primaryHeld,
            PrimaryReleased = primaryReleased,
            SecondaryPressed = secondaryPressed,
            SecondaryHeld = secondaryHeld,
            SecondaryReleased = secondaryReleased,
            MoveAxis = moveAxis
        };

        Vector2 aimAxis = new Vector2(
            SafeGetAxisWithFallback(aimHorizontalAxis, aimHorizontalFallbackAxes),
            SafeGetAxisWithFallback(aimVerticalAxis, aimVerticalFallbackAxes));
        if (invertAimX)
        {
            aimAxis.x = -aimAxis.x;
        }
        if (invertAimY)
        {
            aimAxis.y = -aimAxis.y;
        }

        if (aimAxis.sqrMagnitude >= aimDeadZone * aimDeadZone || snapshot.PrimaryHeld || snapshot.PrimaryPressed || snapshot.PrimaryReleased
            || snapshot.SecondaryHeld || snapshot.SecondaryPressed || snapshot.SecondaryReleased)
        {
            if (TryBuildAimPoint(aimAxis, out Vector3 aimPoint))
            {
                snapshot.HasAimPoint = true;
                snapshot.AimPoint = aimPoint;
            }
        }

        _previousPrimaryHeld = primaryHeld;
        _previousSecondaryHeld = secondaryHeld;

        if (debugInputs)
        {
            DebugDetected(snapshot, aimAxis, triggerValue, secondaryTriggerValue);
            DebugKeycodes();
        }

        return snapshot;
    }

    private bool TryBuildAimPoint(Vector2 aimAxis, out Vector3 point)
    {
        point = default;

        Transform self = transform;
        if (!self)
        {
            return false;
        }

        Camera cam = ResolveCamera();
        Vector3 forward = cam ? cam.transform.forward : self.forward;
        Vector3 right = cam ? cam.transform.right : self.right;
        forward.y = 0f;
        right.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = self.forward;
            forward.y = 0f;
        }
        if (right.sqrMagnitude < 0.0001f)
        {
            right = self.right;
            right.y = 0f;
        }

        forward.Normalize();
        right.Normalize();

        Vector3 aimDirection = self.forward; // default to facing direction
        if (aimAxis.sqrMagnitude >= aimDeadZone * aimDeadZone)
        {
            Vector3 composite = right * aimAxis.x + forward * aimAxis.y;
            composite.y = 0f;
            if (composite.sqrMagnitude > 0.0001f)
            {
                aimDirection = composite.normalized;
            }
        }
        else
        {
            return false; // keep previous aim if stick is near center
        }

        float distance = Mathf.Max(0.1f, aimDistance);
        point = self.position + aimDirection * distance;
        return true;
    }

    private bool GetButton(string buttonName, KeyCode keyCode, bool allowMouseButtons)
    {
        bool button = !string.IsNullOrEmpty(buttonName) && SafeGetButton(buttonName);
        bool key = keyCode != KeyCode.None && Input.GetKey(keyCode);
        bool mouse = allowMouseButtons && (Input.GetMouseButton(0) || Input.GetMouseButton(1));
        return button || key || mouse;
    }

    private bool GetButtonDown(string buttonName, KeyCode keyCode, bool allowMouseButtons)
    {
        bool button = !string.IsNullOrEmpty(buttonName) && SafeGetButtonDown(buttonName);
        bool key = keyCode != KeyCode.None && Input.GetKeyDown(keyCode);
        bool mouse = allowMouseButtons && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1));
        return button || key || mouse;
    }

    private bool GetButtonUp(string buttonName, KeyCode keyCode, bool allowMouseButtons)
    {
        bool button = !string.IsNullOrEmpty(buttonName) && SafeGetButtonUp(buttonName);
        bool key = keyCode != KeyCode.None && Input.GetKeyUp(keyCode);
        bool mouse = allowMouseButtons && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1));
        return button || key || mouse;
    }

    public void SetMoveAxes(string horizontal, string vertical)
    {
        moveHorizontalAxis = horizontal;
        moveVerticalAxis = vertical;
    }

    public void SetAimAxes(string horizontal, string vertical)
    {
        aimHorizontalAxis = horizontal;
        aimVerticalAxis = vertical;
    }

    public void SetButtonNames(string primary, string jump, string dash, string run, string interact)
    {
        primaryButton = primary ?? string.Empty;
        jumpButton = jump ?? string.Empty;
        dashButton = dash ?? string.Empty;
        runButton = run ?? string.Empty;
        interactButton = interact ?? string.Empty;
    }

    public void SetButtonKeyCodes(KeyCode primary, KeyCode jump, KeyCode dash, KeyCode run, KeyCode interact, KeyCode interactAlt = KeyCode.None)
    {
        primaryKeyCode = primary;
        jumpKeyCode = jump;
        dashKeyCode = dash;
        runKeyCode = run;
        interactKeyCode = interact;
        interactKeyCodeAlt = interactAlt;
    }

    public void SetSecondaryKeyCode(KeyCode secondary)
    {
        secondaryKeyCode = secondary;
    }

    public void SetSecondaryButtonName(string name)
    {
        secondaryButton = name ?? string.Empty;
    }

    public void SetPrimaryTrigger(string axisName, bool use, float threshold = 0.5f, bool invert = false)
    {
        primaryTriggerAxis = axisName ?? string.Empty;
        useTriggerForPrimary = use;
        triggerPressThreshold = Mathf.Clamp01(threshold);
        invertTriggerAxis = invert;
    }

    public void SetSecondaryTrigger(string axisName, bool use, float threshold = 0.5f, bool invert = false)
    {
        secondaryTriggerAxis = axisName ?? string.Empty;
        useTriggerForSecondary = use;
        secondaryTriggerPressThreshold = Mathf.Clamp01(threshold);
        invertSecondaryTriggerAxis = invert;
    }

    public void SetAxisInversion(bool moveX, bool moveY, bool aimX, bool aimY)
    {
        invertMoveX = moveX;
        invertMoveY = moveY;
        invertAimX = aimX;
        invertAimY = aimY;
    }

    private float SafeGetAxisRaw(string axisName)
    {
        if (string.IsNullOrEmpty(axisName))
        {
            return 0f;
        }

        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch (System.Exception)
        {
            return 0f;
        }
    }

    private float SafeGetAxis(string axisName)
    {
        if (string.IsNullOrEmpty(axisName))
        {
            return 0f;
        }

        try
        {
            return Input.GetAxis(axisName);
        }
        catch (System.Exception)
        {
            return 0f;
        }
    }

    private float SafeGetAxisWithFallback(string primaryName, List<string> fallbacks)
    {
        float value = SafeGetAxis(primaryName);
        if (Mathf.Abs(value) > 0.001f)
        {
            return value;
        }

        if (fallbacks == null)
        {
            return value;
        }

        for (int i = 0; i < fallbacks.Count; i++)
        {
            string name = fallbacks[i];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            value = SafeGetAxis(name);
            if (Mathf.Abs(value) > 0.001f)
            {
                if (debugInputs)
                {
                    Debug.Log($"[PlayerInputRouterGamepad] Using fallback axis '{name}' value={value:0.00}", this);
                }
                return value;
            }
        }

        return 0f;
    }

    private bool SafeGetButton(string buttonName)
    {
        try
        {
            return Input.GetButton(buttonName);
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private bool SafeGetButtonDown(string buttonName)
    {
        try
        {
            return Input.GetButtonDown(buttonName);
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private bool SafeGetButtonUp(string buttonName)
    {
        try
        {
            return Input.GetButtonUp(buttonName);
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private void DebugDetected(PlayerInputSnapshot snapshot, Vector2 aimAxis, float triggerValue, float secondaryTriggerValue)
    {
        if (snapshot.PrimaryPressed || snapshot.JumpPressed || snapshot.DashPressed || snapshot.InteractPressed
            || snapshot.InteractAltPressed || snapshot.RunHeld)
        {
            Debug.Log($"[PlayerInputRouterGamepad] Button detected: Primary={snapshot.PrimaryPressed} Secondary={snapshot.SecondaryPressed} Jump={snapshot.JumpPressed} Dash={snapshot.DashPressed} Interact={snapshot.InteractPressed} InteractAlt={snapshot.InteractAltPressed} RunHeld={snapshot.RunHeld} trigger={triggerValue:0.00} secondaryTrigger={secondaryTriggerValue:0.00} keycodes: P={primaryKeyCode} S={secondaryKeyCode} J={jumpKeyCode} D={dashKeyCode} R={runKeyCode} I={interactKeyCode} IA={interactKeyCodeAlt}", this);
        }

        if (snapshot.MoveAxis.sqrMagnitude > 0.01f || aimAxis.sqrMagnitude > 0.01f)
        {
            Debug.Log($"[PlayerInputRouterGamepad] Axes move={snapshot.MoveAxis} aim={aimAxis}", this);
        }
    }

    private void DebugKeycodes()
    {
        // Helps discover which KeyCode maps to the pressed gamepad buttons on this platform.
        for (int i = 0; i <= 19; i++)
        {
            KeyCode code = KeyCode.JoystickButton0 + i;
            if (Input.GetKeyDown(code))
            {
                Debug.Log($"[PlayerInputRouterGamepad] Detected joystick key down: {code}", this);
            }
        }
    }
}
