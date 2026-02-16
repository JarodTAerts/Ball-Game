using Godot;

namespace BallFightGame;

/// <summary>
/// Per-level configuration. Attach to the root node of any level scene
/// to override global settings like player boundary.
/// </summary>
public partial class LevelConfig : Node
{
    [Export] public float PlayerBoundary { get; set; } = 50f;

    public override void _Ready()
    {
        var gm = GetNode<GameManager>("/root/GameManager");
        gm.PlayerBoundary = PlayerBoundary;
        GD.Print($"[LevelConfig] PlayerBoundary set to {PlayerBoundary}");
    }
}
