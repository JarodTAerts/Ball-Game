using Godot;

namespace BallFightGame;

/// <summary>
/// Tutorial controller. Drives a checkpoint-based progression system with
/// timed auto-advance messages after all checkpoints are collected.
///
/// Replaces Unity's TutorialController.cs + CheckPointController.cs.
/// Key improvements:
///   - Checkpoints use BodyEntered (one-shot) instead of OnTriggerStay
///     (which fired every frame the player stood inside, skipping steps)
///   - Data-driven message arrays instead of giant if/else chains
///   - Single script manages the whole flow
/// </summary>
public partial class TutorialController : Node
{
    [Signal] public delegate void TutorialMessageEventHandler(string message);

    [Export] public float AutoAdvanceInterval { get; set; } = 5f;

    private static readonly PackedScene HandgunPickupScene =
        GD.Load<PackedScene>(Scenes.WeaponPickup);
    private static readonly PackedScene EnemyScene =
        GD.Load<PackedScene>(Scenes.EnemyNormal);

    // Checkpoint messages (indexed by checkpoint number)
    private readonly string[] _checkpointMessages =
    {
        "Welcome to the tutorial. Let's start by rolling around using the 'WASD' keys and collecting the purple checkpoints.",
        "", // checkpoint 1 — no new message, just collect
        "You can jump by pressing Space. After jumping there is a short recovery time where you cannot jump.",
        "", // checkpoint 3
        "Pressing 'F' will turn off and on your flashlight if you need it.",
        "", // checkpoint 5 — triggers weapon spawn
    };

    // Post-checkpoint auto-advance messages
    private readonly string[] _combatMessages =
    {
        "An enemy has just spawned! Aim and shoot with the mouse to kill it.",
        "Notice the ammo counter in the lower left. The left number is the loaded ammo, the right is your total ammo.",
        "When you run out of loaded ammo you must press 'R' to reload. It will then take a second to reload.",
        "If your total ammo is running low you can collect boxes like the one in the center to get more.",
        "You can also press 'P' at any time during the game to pause.",
        "When you are finished messing around press 'X' to return to the menu and play.",
    };

    private int  _checkpointsCollected;
    private int  _combatMessageIndex;
    private bool _checkpointsComplete;
    private bool _combatPhaseStarted;
    private Timer _autoAdvanceTimer = null!;

    private GameManager _gm = null!;

    public override void _Ready()
    {
        _gm = GetNode<GameManager>("/root/GameManager");

        _autoAdvanceTimer = new Timer
        {
            WaitTime = AutoAdvanceInterval,
            OneShot = false,
        };
        _autoAdvanceTimer.Timeout += OnAutoAdvanceTick;
        AddChild(_autoAdvanceTimer);

        // Show the first message
        ShowCheckpointMessage(0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // X returns to menu from tutorial (matches Unity)
        if (@event.IsActionPressed(InputActions.ReturnToMenu))
            _gm.ReturnToMenu();
    }

    /// <summary>
    /// Called by Checkpoint nodes when the player enters them.
    /// </summary>
    public void OnCheckpointCollected()
    {
        _checkpointsCollected++;
        ShowCheckpointMessage(_checkpointsCollected);

        // After 6 checkpoints (indices 0–5), spawn weapon and enemy
        if (_checkpointsCollected >= 6 && !_combatPhaseStarted)
            StartCombatPhase();
    }

    private void ShowCheckpointMessage(int index)
    {
        if (index < _checkpointMessages.Length && _checkpointMessages[index].Length > 0)
            EmitSignal(SignalName.TutorialMessage, _checkpointMessages[index]);
    }

    private void StartCombatPhase()
    {
        _combatPhaseStarted = true;

        // Spawn a HandGun for the player to pick up
        var wm = GetNode<WeaponManager>("/root/WeaponManager");
        var handgun = GD.Load<WeaponData>("res://resources/weapons/handgun.tres");
        wm.SpawnDrop(handgun, 15, 60, Vector3.Zero);

        // Spawn an enemy to fight
        var enemy = EnemyScene.Instantiate<Node3D>();
        GetTree().CurrentScene.AddChild(enemy);
        enemy.GlobalPosition = new Vector3(20, 1, 20);

        // Start auto-advancing combat messages
        _combatMessageIndex = 0;
        EmitSignal(SignalName.TutorialMessage, _combatMessages[0]);
        _autoAdvanceTimer.Start();
    }

    private void OnAutoAdvanceTick()
    {
        _combatMessageIndex++;
        if (_combatMessageIndex < _combatMessages.Length)
        {
            EmitSignal(SignalName.TutorialMessage, _combatMessages[_combatMessageIndex]);
        }
        else
        {
            _autoAdvanceTimer.Stop();
        }
    }
}
