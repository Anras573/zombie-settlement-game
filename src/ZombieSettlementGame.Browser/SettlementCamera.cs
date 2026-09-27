using System;
using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Fits the camera to the settlement grid and maps screen clicks back onto it. Both directions of
/// the same view-projection: <see cref="ComputeZoom"/>/<see cref="UpdateZoom"/> build it,
/// <see cref="TryScreenToCell"/> inverts it.
/// </summary>
public static class SettlementCamera
{
    /// <summary>Empty world-unit margin kept visible around the grid on every side.</summary>
    private const float CameraMargin = 1f;

    /// <summary>
    /// Zoom that fits the whole <see cref="SettlementGrid.Width"/> x <see cref="SettlementGrid.Height"/>
    /// grid plus a <see cref="CameraMargin"/> margin inside the viewport on every side, whatever its
    /// <paramref name="aspectRatio"/> — the narrower of a height-fit and a width-fit zoom (see
    /// <see cref="Camera2D.ViewProjection"/> for how <c>Zoom</c> maps to visible half-extents).
    /// A fixed height-only fit (the original approach) crops the left/right edges off-screen on
    /// a portrait phone, where aspect ratio is well under 1.
    /// </summary>
    public static float ComputeZoom(float aspectRatio)
    {
        var zoomForHeight = 1f / (SettlementGrid.Height / 2f + CameraMargin);
        var zoomForWidth = aspectRatio / (SettlementGrid.Width / 2f + CameraMargin);
        return MathF.Min(zoomForHeight, zoomForWidth);
    }

    public static bool TryGetCamera(World world, out Camera2D camera)
    {
        camera = default;
        return world.TryGetEntity("camera", out var cameraEntity) && world.TryGetComponent(cameraEntity, out camera);
    }

    /// <summary>Re-fits the camera's <see cref="Camera2D.Zoom"/> to the current viewport shape;
    /// called once per tick since <paramref name="aspectRatio"/> can change (resize, rotation).</summary>
    public static void UpdateZoom(World world, float aspectRatio)
    {
        if (!TryGetCamera(world, out var camera))
            return;

        camera.Zoom = ComputeZoom(aspectRatio);
        world.AddComponent(world.GetEntity("camera"), camera);
    }

    /// <summary>
    /// Maps a mouse position, given in the same NDC coordinates <see cref="Camera2D.ViewProjection"/>
    /// produces, to the grid cell underneath it by inverting the camera's view-projection.
    /// Returns <c>false</c> when the point falls outside the buildable interior (the outermost
    /// ring is the boundary wall, not placeable ground) or the camera matrix isn't invertible.
    /// </summary>
    public static bool TryScreenToCell(
        Camera2D camera,
        float aspectRatio,
        Vector2 mouseNdc,
        out int column,
        out int row
    )
    {
        column = 0;
        row = 0;

        if (!Matrix4x4.Invert(camera.ViewProjection(aspectRatio), out var inverseViewProjection))
            return false;

        var worldPoint = Vector4.Transform(new Vector4(mouseNdc.X, mouseNdc.Y, 0f, 1f), inverseViewProjection);

        column = (int)MathF.Floor((worldPoint.X - SettlementGrid.Origin.X) / SettlementGrid.TileWorldSize);
        var rowFromBottom = (int)MathF.Floor((worldPoint.Y - SettlementGrid.Origin.Y) / SettlementGrid.TileWorldSize);
        row = SettlementGrid.Height - 1 - rowFromBottom;

        return column >= 1 && column <= SettlementGrid.Width - 2 && row >= 1 && row <= SettlementGrid.Height - 2;
    }
}
