namespace Vicsek.Core;

/// <summary>A self-propelled particle: position in the periodic box and heading in radians.</summary>
public record struct Particle(double X, double Y, double Theta);
