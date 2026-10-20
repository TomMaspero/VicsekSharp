namespace Vicsek.Core;

/// <summary>
/// Parameters of the Vicsek model. Distances are in world units; the world is a
/// periodic square of side <see cref="BoxSize"/>.
/// </summary>
public sealed class SimulationParameters
{
    /// <summary>Number of particles. Changing it requires a new <see cref="Simulation"/>.</summary>
    public int Count { get; set; } = 2000;

    /// <summary>Side of the periodic square. Changing it requires a new <see cref="Simulation"/>.</summary>
    public double BoxSize { get; set; } = 1000;

    /// <summary>Interaction radius: particles closer than this align with each other.</summary>
    public double Radius { get; set; } = 25;

    /// <summary>Distance travelled per step (v0 * dt).</summary>
    public double Speed { get; set; } = 1;

    /// <summary>
    /// Noise amplitude eta in radians. Each step a random angle uniform in
    /// [-eta/2, eta/2] is added to the heading.
    /// </summary>
    public double Noise { get; set; } = 0.5;

    public SimulationParameters Clone() => (SimulationParameters)MemberwiseClone();

    internal void Validate()
    {
        if (Count < 0) throw new ArgumentOutOfRangeException(nameof(Count));
        if (BoxSize <= 0) throw new ArgumentOutOfRangeException(nameof(BoxSize));
        if (Radius <= 0) throw new ArgumentOutOfRangeException(nameof(Radius));
        if (Radius > BoxSize / 2) throw new ArgumentOutOfRangeException(nameof(Radius), "Radius must be at most half the box size.");
        if (Speed < 0) throw new ArgumentOutOfRangeException(nameof(Speed));
        if (Noise < 0) throw new ArgumentOutOfRangeException(nameof(Noise));
    }
}
